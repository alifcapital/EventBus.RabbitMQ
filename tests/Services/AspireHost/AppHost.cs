var builder = DistributedApplication.CreateBuilder(args);
var userService = builder.AddProject("users-service", "../UsersService/UsersService.csproj");
builder.AddProject("orders-service", "../OrdersService/OrdersService.csproj")
    .WithReference(userService)
    .WaitFor(userService);

builder.Build().Run();