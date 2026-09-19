namespace OrderTracking.Contracts.Events;

public record OrderShipped(
    Guid OrderId, 
    DateTimeOffset ShippedAt, 
    string Carrier, 
    string TrackingNumber
);