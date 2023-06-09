using MassTransit;
using MediatR;
using MicroserviceOpenIddictTemplate.Identity.Endpoints.Account.Queries;

namespace MicroserviceOpenIddictTemplate.Identity.Endpoints.Account.Consumers;

public class RegisterAccountConsumer : IConsumer<RegisterAccountRequest>
{
    private readonly IMediator _mediator;

    public RegisterAccountConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task Consume(ConsumeContext<RegisterAccountRequest> context)
    {
        var result = await _mediator.Send(context.Message, context.CancellationToken);
        await context.RespondAsync(result);
    }
}