using EventBus.RabbitMQ.Extensions;
using EventBus.RabbitMQ.Subscribers.Models;
using EventStorage.Inbox.EventArgs;
using EventStorage.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddRabbitMqEventBus(builder.Configuration,
    assemblies: [typeof(Program).Assembly],
    eventStoreOptions: options =>
    {
        options.Inbox.ConnectionString = connectionString;
        options.Outbox.ConnectionString = connectionString;
    },
    eventSubscribersHandled: EventSubscriberHandled
);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

//For adding log to let you know that all subscribers are handled.
static void EventSubscriberHandled(object sender, EventHandlerArgs e)
{
    Console.WriteLine("All subscribers of the {0} event are handled", e.EventName);
}