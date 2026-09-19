namespace OrderTracking.Contracts.Events;

public record OrderPlaced(
    Guid OrderId, 
    DateTimeOffset PlacedAt, 
    Guid CustomerId, 
    decimal TotalAmount,
    IReadOnlyList<OrderItem> Items
);