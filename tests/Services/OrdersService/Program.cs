using EventBus.RabbitMQ.Extensions;
using EventStorage.Inbox.EventArgs;
using Microsoft.EntityFrameworkCore;
using OrdersService.Infrastructure;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<OrderContext>(op => op.UseNpgsql(connectionString));
builder.Services.AddRabbitMqEventBus(builder.Configuration,
    assemblies: [typeof(Program).Assembly],
    eventStoreOptions: options =>
    {
        options.Inbox.ConnectionString = connectionString;
        options.Outbox.ConnectionString = connectionString;
    },
    eventSubscribersHandled: EventSubscribersAreHandled
);

builder.Services.AddControllers();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<OrderContext>();
context.Database.EnsureCreated();

app.MapDefaultEndpoints();
app.UseAuthorization();

app.MapControllers();

app.Run();
return;

//For adding a log to let user know that all subscribers are handled.
static void EventSubscribersAreHandled(object sender, EventHandlerArgs e)
{
    Console.WriteLine("All subscribers of the {0} event are handled", e.EventName);
}