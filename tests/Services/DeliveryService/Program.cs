using DeliveryService.Infrastructure;
using EventBus.RabbitMQ.Extensions;
using EventStorage.Inbox.EventArgs;
using InMemoryMessaging.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(p => p.AddConsole());

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<DeliveryContext>(op => op.UseNpgsql(connectionString));

builder.Services.AddInMemoryMessaging([typeof(Program).Assembly]);
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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<DeliveryContext>();
context.Database.EnsureCreated();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
return;

//For adding a log to let user know that all subscribers are handled.
static void EventSubscribersAreHandled(object sender, EventHandlerArgs e)
{
    Console.WriteLine("All subscribers of the {0} event are handled", e.EventName);
}