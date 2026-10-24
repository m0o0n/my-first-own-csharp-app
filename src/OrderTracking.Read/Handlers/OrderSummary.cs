using Marten;
using OrderTracking.Contracts.Events;
using OrderTracking.Read.Domain;
using Wolverine.Attributes;

namespace OrderTracking.Read.Handlers;

[WolverineHandler]
public class OrderSummaryProjection
{
    public async Task Handle(OrderPlaced e, IDocumentSession session)
    {
        var orderSummary = new OrderSummary
        {
            Id = e.OrderId,
            Status = OrderStatus.Placed,
            TotalAmount = e.TotalAmount,
            PaidAmount = 0,
            PlacedAt = e.PlacedAt,
            CustomerId = e.CustomerId
        };

        session.Store(orderSummary);
        await session.SaveChangesAsync();
    }

    public async Task Handle(OrderPaid e, IDocumentSession session)
    {
        var orderSummary = await session.LoadAsync<OrderSummary>(e.OrderId);
        if (orderSummary == null)
        {
            throw new InvalidOperationException($"Order summary not found for OrderId: {e.OrderId}");
        }

        if (orderSummary.Status is OrderStatus.Shipped or OrderStatus.Cancelled)
        {
            return;
        }

        orderSummary.Status = OrderStatus.Paid;
        orderSummary.PaidAmount = e.PaidAmount;

        session.Store(orderSummary);
        await session.SaveChangesAsync();
    }

    public async Task Handle(OrderShipped e, IDocumentSession session)
    {
        var orderSummary = await session.LoadAsync<OrderSummary>(e.OrderId);
        if (orderSummary == null)
        {
            throw new InvalidOperationException($"Order summary not found for OrderId: {e.OrderId}");
        }

        if (orderSummary.Status is OrderStatus.Cancelled)
        {
            return;
        }

        orderSummary.Status = OrderStatus.Shipped;
        orderSummary.ShippedAt = e.ShippedAt;

        session.Store(orderSummary);
        await session.SaveChangesAsync();
    }

    public async Task Handle(OrderCancelled e, IDocumentSession session)
    {
        var orderSummary = await session.LoadAsync<OrderSummary>(e.OrderId);
        if (orderSummary == null)
        {
            throw new InvalidOperationException($"Order summary not found for OrderId: {e.OrderId}");
        }

        if (orderSummary.Status is OrderStatus.Shipped)
        {
            return;
        }

        orderSummary.Status = OrderStatus.Cancelled;
        orderSummary.CancelledAt = e.CancelledAt;

        session.Store(orderSummary);
        await session.SaveChangesAsync();
    }
}