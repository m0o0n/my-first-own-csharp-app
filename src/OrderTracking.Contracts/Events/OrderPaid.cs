namespace OrderTracking.Contracts.Events;

public record OrderPaid(
    Guid OrderId, 
    DateTimeOffset PaidAt,
    string PaymentReference,
    decimal PaidAmount
);