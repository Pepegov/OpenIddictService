﻿using System.Security.Claims;
using AutoMapper;
using MicroserviceOpenIddictTemplate.DAL.Database;
using MicroserviceOpenIddictTemplate.DAL.Models.Identity;
using MicroserviceOpenIddictTemplate.Identity.Definitions.Identity;
using MicroserviceOpenIddictTemplate.Identity.Endpoints.Account.ViewModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Pepegov.MicroserviceFramerwork.Patterns.UnitOfWork;

namespace MicroserviceOpenIddictTemplate.Identity.Application.Services;

public class AccountService : IAccountService
{
    private readonly IUnitOfWork<ApplicationDbContext> _unitOfWork;
    private readonly ILogger<AccountService> _logger;
    private readonly ApplicationUserClaimsPrincipalFactory _claimsFactory;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IMapper _mapper;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager, 
        IUnitOfWork<ApplicationDbContext> unitOfWork,
        ILogger<AccountService> logger,
        ApplicationUserClaimsPrincipalFactory claimsFactory,
        IHttpContextAccessor httpContext,
        IMapper mapper)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _claimsFactory = claimsFactory;
        _httpContext = httpContext;
        _mapper = mapper;
    }
    
    public Guid GetCurrentUserId()
    {
        var identity = _httpContext.HttpContext?.User.Identity;
        var identitySub = (identity is ClaimsIdentity claimsIdentity ? claimsIdentity.FindFirst("sub") : (Claim)null)
                          ?? throw new InvalidOperationException("sub claim is missing");
        
        Guid result;
        Guid.TryParse(identitySub.Value, out result);
        return result;
    }

    public async Task<UserAccountViewModel> RegisterAsync(RegisterViewModel model, CancellationToken cancellationToken)
    {
        var viewModel = new UserAccountViewModel();
        var user = _mapper.Map<ApplicationUser>(model);
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        var result = await _userManager.CreateAsync(user, model.Password);
        const string role = UserRoles.Client;

        if (result.Succeeded)
        {
            if (await _roleManager.FindByNameAsync(role) == null)
            {
                throw new ArgumentNullException($"role \"{role}\" not found");
            }

            await _userManager.AddToRoleAsync(user, role);
            //await AddClaimsToUser(user, role);

            var principal = await _claimsFactory.CreateAsync(user);
            var resultViewModel = _mapper.Map<UserAccountViewModel>(principal.Identity);
            resultViewModel.Roles = new List<string> { role };
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation($"User registration: email:{model.Email} | {_unitOfWork.LastSaveChangesResult.Exception} ");
            return await Task.FromResult(resultViewModel);
        }
        var errors = result.Errors.Select(x => $"{x.Code}: {x.Description}");
        await transaction.RollbackAsync(cancellationToken);
        _logger.LogInformation($"User dont register: email:{model.Email} | errors: {string.Join(", ", errors)} | {_unitOfWork.LastSaveChangesResult.Exception} ");
        throw new Exception($"User dont register: email:{model.Email} | errors: {string.Join(", ", errors)} | {_unitOfWork.LastSaveChangesResult.Exception} ");
    }

    public async Task<ClaimsPrincipal> GetPrincipalByIdAsync(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }
        var userManager = _userManager;
        var user = await userManager.FindByIdAsync(identifier);
        if (user == null)
        {
            throw new Exception("user not found");
        }

        var defaultClaims = await _claimsFactory.CreateAsync(user);
        return defaultClaims;
    }

    public Task<ClaimsPrincipal> GetPrincipalForUserAsync(ApplicationUser user) => _claimsFactory.CreateAsync(user);

    public Task<ApplicationUser> GetByIdAsync(Guid id)
    {
        var userManager = _userManager;
        return userManager.FindByIdAsync(id.ToString());
    }

    public async Task<ApplicationUser> GetCurrentUserAsync()
    {
        var userManager = _userManager;
        var userId = GetCurrentUserId().ToString();
        var user = await userManager.FindByIdAsync(userId);
        return user;
    }

    public async Task<IEnumerable<ApplicationUser>> GetUsersByEmailsAsync(IEnumerable<string> emails)
    {
        var userManager = _userManager;
        var result = new List<ApplicationUser>();
        foreach (var email in emails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user != null && !result.Contains(user))
            {
                result.Add(user);
            }
        }
        return await Task.FromResult(result);
    }

    public async Task<IEnumerable<ApplicationUser>> GetUsersInRoleAsync(string roleName)
    {
        var userManager = _userManager;
        return await userManager.GetUsersInRoleAsync(roleName);
    }
    
    private async Task AddClaimsToUser(ApplicationUser user, string role)
    {
        await _userManager.AddClaimAsync(user, new Claim(OpenIddictConstants.Claims.Name, user.UserName));
        await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, user.FirstName));
        await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Surname, user.LastName));
        await _userManager.AddClaimAsync(user, new Claim(OpenIddictConstants.Claims.Email, user.Email));
        await _userManager.AddClaimAsync(user, new Claim(OpenIddictConstants.Claims.Role, role));
    }
}