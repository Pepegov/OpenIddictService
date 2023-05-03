﻿using MicroserviceOpenIddictTemplate.DAL.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using MicroserviceOpenIddictTemplate.Identity.Definitions.OpenIddict;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pepegov.MicroserviceFramerwork.AspNetCore.Definition;

namespace MicroserviceOpenIddictTemplate.Identity.Definitions.Identity;

public class AuthorizationDefinition : Definition
{
    public override void ConfigureServicesAsync(IServiceCollection services, WebApplicationBuilder builder)
    {
        var url = builder.Configuration.GetSection("IdentityServerUrl").GetValue<string>("Authority");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/connect/login";
            });

        //services.AddAuthorization();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthData.AuthenticationSchemes, policy =>
            {
                policy.RequireAuthenticatedUser();
                //policy.RequireClaim("scope", "api");
            });
        });
        services.AddSingleton<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, AppPermissionHandler>();
    }

    public override void ConfigureApplicationAsync(WebApplication app)
    {
        app.UseRouting();
        app.UseCors(AppData.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();

        // registering UserIdentity helper as singleton
        UserIdentity.Instance.Configure(app.Services.GetService<IHttpContextAccessor>()!);
    }

}