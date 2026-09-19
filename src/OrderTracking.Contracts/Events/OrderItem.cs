namespace OrderTracking.Contracts.Events;

public record OrderItem(
    string ProductName, 
    int Quantity, 
    decimal Price
);
