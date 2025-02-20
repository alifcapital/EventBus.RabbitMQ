using EventBus.RabbitMQ.Extensions;
using Microsoft.EntityFrameworkCore;
using UsersService.Infrastructure;
using UsersService.Repositories;
using UsersService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<UserContext>(op => op.UseNpgsql(connectionString));

// Add services to the container.
builder.Services.AddRabbitMqEventBus(builder.Configuration,
    assemblies: [typeof(Program).Assembly],
    defaultOptions: options =>
    {
        options.HostName = "localhost";
        options.QueueArguments.Add("x-priority", 10);
    },
    eventStoreOptions: options =>
    {
        options.Inbox.IsEnabled = true;
        options.Inbox.TableName = "ReceivedEvents";
        options.Inbox.ConnectionString = connectionString;
        options.Outbox.IsEnabled = true;
        options.Outbox.TableName = "SentEvents";
        options.Outbox.ConnectionString = connectionString;
    }
);

// builder.Services.AddEventStore(builder.Configuration,
//     assemblies: [typeof(Program).Assembly]
//     , options =>
//     {
//         options.Inbox.IsEnabled = true;
//         options.Inbox.TableName = "ReceivedEvents";
//         options.Inbox.ConnectionString = "Connection string of the SQL database";
//         //Other settings of the Inbox
//         
//         options.Outbox.IsEnabled = true;
//         options.Outbox.TableName = "SentEvents";
//         options.Outbox.ConnectionString = "Connection string of the SQL database";
//         //Other settings of the Outbox
//     });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<IWebHookProvider, WebHookProvider>();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<UserContext>();
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