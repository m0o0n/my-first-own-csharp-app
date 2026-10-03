using Marten;
using OrderTracking.Contracts.Events;
using OrderTracking.Write.Domain;
using Wolverine;

namespace OrderTracking.Write.Features;

public static class ShipOrder
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/{orderId:guid}/ship", Handle);
    }

    public static async Task<IResult> Handle(Guid orderId, IDocumentSession session, IMessageBus bus)
    {
        var order = await session.Events.FetchLatest<Order>(orderId);
        if (order == null)
        {
            return Results.NotFound();
        }

        if (order.Status != OrderStatus.Paid)
        {
            return Results.BadRequest("Order is not in a valid state for shipping.");
        }

        var orderShipped = new OrderShipped(OrderId: orderId, ShippedAt: DateTimeOffset.UtcNow, Carrier: "Example Carrier", TrackingNumber: Guid.NewGuid().ToString());

        session.Events.Append(orderId, orderShipped);
        await session.SaveChangesAsync();
        await bus.PublishAsync(orderShipped);
        
        return Results.Ok();
    }
}