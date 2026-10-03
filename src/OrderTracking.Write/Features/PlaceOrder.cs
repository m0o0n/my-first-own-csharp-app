namespace OrderTracking.Write.Features;

using Marten;
using OrderTracking.Contracts.Events;
using OrderTracking.Write.Domain;
using Wolverine;

public record PlaceOrderRequest(
    Guid CustomerId,
    IReadOnlyList<OrderItem> Items
);

public static class PlaceOrder
{

    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", Handle);
    }
    public static async Task<IResult> Handle(PlaceOrderRequest payload, IDocumentSession session, IMessageBus bus)
    {

        var orderId = Guid.NewGuid();
        var totalAmount = payload.Items.Sum(i => i.Price * i.Quantity);
        var date = DateTimeOffset.UtcNow;

        var orderPlaced = new OrderPlaced(
            OrderId: orderId,
            PlacedAt: date,
            CustomerId: payload.CustomerId,
            TotalAmount: totalAmount,
            Items: payload.Items
        );

        session.Events.StartStream<Order>(orderId, orderPlaced);
        await session.SaveChangesAsync();
        await bus.PublishAsync(orderPlaced);

        return Results.Created($"/orders/{orderId}", new { orderId });
    }
}