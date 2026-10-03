using Marten;
using OrderTracking.Write.Features;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration["DB_URL"]!);
    options.DatabaseSchemaName = "write_orders";
});

builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(new Uri("amqp://guest:guest@localhost:5672"))
        .AutoProvision();

    opts.PublishAllMessages().ToRabbitExchange("orders");
});

var app = builder.Build();


app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"Application started on Port: {app.Environment.ApplicationName}");
});

app.MapGet("/", () => "Hello World!");

PlaceOrder.MapEndpoints(app);
PayOrder.MapEndpoints(app);
ShipOrder.MapEndpoints(app);
CancelOrder.MapEndpoints(app);

app.Run();
