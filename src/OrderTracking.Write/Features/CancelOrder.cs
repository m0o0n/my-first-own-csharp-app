using Marten;
using OrderTracking.Contracts.Events;
using OrderTracking.Write.Domain;
using Wolverine;

namespace OrderTracking.Write.Features;

public static class CancelOrder
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/{orderId:guid}/cancel", Handle);
    }

    public static async Task<IResult> Handle(Guid orderId, IDocumentSession session, IMessageBus bus)
    {
        var order = await session.Events.FetchLatest<Order>(orderId);
        if (order == null)
        {
            return Results.NotFound();
        }

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Cancelled)
        {
            return Results.BadRequest("Order is not in a valid state for cancellation.");
        }

        var orderCancelled = new OrderCancelled(OrderId: orderId, CancelledAt: DateTimeOffset.UtcNow, Reason: "Cancelled by user request");

        session.Events.Append(orderId, orderCancelled);
        await session.SaveChangesAsync();
        await bus.PublishAsync(orderCancelled);

        return Results.Ok();
    }
}