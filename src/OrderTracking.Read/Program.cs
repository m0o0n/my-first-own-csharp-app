
using Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration["DB_URL"]!);
    options.DatabaseSchemaName = "write_orders";
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
