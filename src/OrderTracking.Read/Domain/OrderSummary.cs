

using JasperFx;

namespace OrderTracking.Read.Domain;

public class OrderSummary
{
    [Identity]
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public Guid CustomerId { get; set; }
}