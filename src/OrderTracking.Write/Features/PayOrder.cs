using Marten;
using OrderTracking.Contracts.Events;
using OrderTracking.Write.Domain;

namespace OrderTracking.Write.Features;

public record PayOrderRequest(decimal Amount);

public static class PayOrder
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/{orderId:guid}/pay", Handle);
    }


    public static async Task<IResult> Handle(Guid orderId, PayOrderRequest payload, IDocumentSession session)
    {
        var order = await session.Events.FetchLatest<Order>(orderId);
        if (order == null)
        {
            return Results.NotFound();
        }

        if(order.TotalAmount > payload.Amount)
        {
            return Results.BadRequest("Payment amount is less than total order amount.");
        }

        if (order.Status != OrderStatus.Placed)
        {
            return Results.BadRequest("Order is not in a valid state for payment.");
        }

        var orderPaid = new OrderPaid(OrderId: orderId, PaidAt: DateTimeOffset.UtcNow, PaymentReference: Guid.NewGuid().ToString(), PaidAmount: payload.Amount);

        session.Events.Append(orderId, orderPaid);

        await session.SaveChangesAsync();

        return Results.Ok();
    }
}