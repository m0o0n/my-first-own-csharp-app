namespace OrderTracking.Write.Domain;

using OrderTracking.Contracts.Events;

public class Order
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public IReadOnlyList<OrderItem> Items { get; private set; } = new List<OrderItem>();
    public decimal PaidAmount { get; private set; }

    public void Apply(OrderPlaced e)
    {
        Id = e.OrderId;
        Status = OrderStatus.Placed;
        CustomerId = e.CustomerId;
        TotalAmount = e.TotalAmount;
        Items = e.Items;
    }
    public void Apply(OrderPaid e)
    {
        Id = e.OrderId;
        Status = OrderStatus.Paid;
        PaidAmount = e.PaidAmount;
    }
    public void Apply(OrderShipped e) {
        Id = e.OrderId;
        Status = OrderStatus.Shipped;

    }
    public void Apply(OrderCancelled e) {
        Id = e.OrderId;
        Status = OrderStatus.Cancelled;
     }
}