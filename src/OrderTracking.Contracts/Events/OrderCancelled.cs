namespace OrderTracking.Contracts.Events;

public record OrderCancelled(
    Guid OrderId, 
    DateTimeOffset CancelledAt, 
    string Reason
);