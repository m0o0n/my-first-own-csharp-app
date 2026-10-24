using Marten;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration["DB_URL"]!);
    options.DatabaseSchemaName = "read_orders";
});



builder.Host.UseWolverine(opts =>
{
    opts.CodeGeneration.AlwaysUseServiceLocationFor<IDocumentSession>();

    opts.UseRabbitMq(new Uri("amqp://guest:guest@localhost:5672"))
        .AutoProvision()
        .DeclareExchange("orders", ex => ex.BindQueue("orders-read"));

    opts.ListenToRabbitQueue("orders-read");
});


var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
