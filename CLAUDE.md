# CLAUDE.md — AlifCapital.EventBus.RabbitMQ

## Project Identity

| Field | Value |
|---|---|
| **Package ID** | `AlifCapital.EventBus.RabbitMQ` |
| **Assembly** | `AlifCapital.EventBus.RabbitMQ` |
| **Version** | `10.0.23` |
| **Framework** | `.NET 10.0` |
| **Repository** | `github.com/alifcapital/EventBus.RabbitMQ` (private) |
| **Company** | Alif Capital |

## Purpose

A .NET library that provides **RabbitMQ transport** for publishing and subscribing to events, built on top of `AlifCapital.EventStorage`. It extends the EventStorage Inbox/Outbox patterns to work with RabbitMQ as the message broker, and is specifically designed to support **multiple virtual hosts** simultaneously.

---

## Dependency on EventStorage

This library directly depends on `AlifCapital.EventStorage` (`10.1.1`). It is **not a standalone library** — it delegates all persistence, retry, and idempotency logic to EventStorage.

### EventStorage interfaces consumed by this library

| EventStorage Type | Used In |
|---|---|
| `IOutboxEvent` | `IPublishEvent` inherits from it |
| `IInboxEvent` | `ISubscribeEvent` inherits from it |
| `IHasHeaders` | `IBaseEvent` inherits from it — all events have headers |
| `IMessageBrokerEventPublisher` | `MessageBrokerEventPublisher` implements it to bridge Outbox → RabbitMQ |
| `IMessageBrokerEventHandler<T>` | `IEventSubscriber<T>` wraps it |
| `IInboxEventManager` | `EventConsumerService` injects it to store received events |
| `IOutboxEventManager` | Injected by consuming services for Outbox-based publishing; `OutboxEventPublisherManager` forwards the `CancellationToken` to `StoreAsync` |
| `IInboxEventsService` / `IOutboxEventsService` | Registered by `AddEventStore`; not used by the library itself, exposed to consumers for viewing/managing events (see "Managing Inbox / Outbox events") |
| `InboxAndOutboxOptions` | Passed via `eventStoreOptions` in `AddRabbitMqEventBus` |
| `EventProviderType.MessageBroker` | Used when storing to Inbox/Outbox |
| `EventHandlerArgs` / `InboxEventArgs` | Event hooks wired through both registrations |
| `AddEventStore(...)` | Called internally by `AddRabbitMqEventBus` — **do not call both** |

> ⚠️ Any change to the above types in `EventStorage` must be validated against this library before release.

---

## Key Dependencies

| Package | Version | Role |
|---|---|---|
| `AlifCapital.EventStorage` | `10.1.1` | Inbox/Outbox persistence and retry |
| `RabbitMQ.Client` | `7.2.1` | AMQP transport layer |
| `Polly` | `8.6.6` | Resilient connection retry on startup |
| `Microsoft.Extensions.*` | `10.0.7` | DI, hosting, configuration |

---

## Domain Model & Interface Hierarchy

```
EventStorage                          EventBus.RabbitMQ
──────────────────────────────────    ──────────────────────────────────────
IEvent                                IBaseEvent : IEvent, IHasHeaders
IHasHeaders                             └── CreatedAt, Headers
IOutboxEvent : IEvent             ◄── IPublishEvent : IBaseEvent, IOutboxEvent
IInboxEvent : IEvent              ◄── ISubscribeEvent : IBaseEvent, IInboxEvent
IMessageBrokerEventHandler<T>     ◄── IEventSubscriber<T> : IMessageBrokerEventHandler<T>
```

### Concrete base records (for consumer use)

```csharp
// For publishing — inherits IPublishEvent
public record UserDeleted : PublishEvent
{
    public required Guid UserId { get; init; }
    public required string UserName { get; init; }
}

// For subscribing — inherits ISubscribeEvent
public record UserCreated : SubscribeEvent
{
    public required Guid UserId { get; init; }
    public required string UserName { get; init; }
}

// Handler
public class UserCreatedHandler : IEventSubscriber<UserCreated>
{
    public async Task HandleAsync(UserCreated @event) { /* ... */ }
}
```

All events automatically carry `EventId` (Guid), `CreatedAt` (DateTime), and `Headers` (Dictionary<string, string>).

---

## Architecture Overview

### Publishing Flow

```
Direct (fire-and-forget):
  IEventPublisherManager.PublishAsync(event)
          │
          ▼
  EventPublisherCollector.GetPublisherSettings(event)
          │  resolves: exchange, routing key, virtual host, naming policy
          ▼
  RabbitMqConnectionManager.GetOrCreateConnection(virtualHostSettings)
          │
          ▼
  IChannel.BasicPublishAsync(exchange, routingKey, properties, body)
          │  AMQP properties: MessageId = EventId, Type = EventTypeName
          │  Headers: PropertyNamingPolicy, TraceParentId, custom headers


Via Outbox (guaranteed delivery):
  IOutboxEventManager.StoreAsync(event, EventProviderType.MessageBroker, cancellationToken)
          │  persists to outbox DB table
          ▼
  [EventStorage background processor — after SecondsToDelayProcessEvents]
          │
          ▼
  MessageBrokerEventPublisher.PublishAsync(IOutboxEvent)
          │  casts to IPublishEvent
          ▼
  IEventPublisherManager.PublishAsync(event) → RabbitMQ
```

### Subscribing / Receiving Flow

```
RabbitMQ AMQP delivery → EventConsumerService.Consumer_ReceivingEvent()
          │
          │  reads: eventType = BasicProperties.Type ?? RoutingKey
          │  reads: eventId   = BasicProperties.MessageId
          │  reads: headers   = BasicProperties.Headers (UTF-8 decoded)
          │
          ├─ UseInbox = false ──────────────────────────────────────────►
          │                      deserialize → IEventSubscriber<T>.HandleAsync()
          │                      → EventSubscriberCollector.OnExecutingSubscribedEvent()
          │                      → BasicAckAsync()
          │
          └─ UseInbox = true ──────────────────────────────────────────►
                        IInboxEventManager.Store(eventId, eventTypeName,
                            EventProviderType.MessageBroker,
                            payload, headers, routingKey, namingPolicyType)
                            │
                            ▼
                        [EventStorage background processor]
                            │
                            ▼
                        IEventSubscriber<T>.HandleAsync()
                        → BasicAckAsync()
```

> **Note:** `BasicAckAsync` is called immediately after storing to Inbox (not after handling), meaning the message is acknowledged once safely persisted — preventing loss even if the handler crashes.

---

## Connection Management

`RabbitMqConnectionManager` maintains a pool of connections keyed by `{HostName}:{HostPort}:{VirtualHost}`. Connections are reused across publishers and subscribers of the same virtual host.

### `RabbitMqConnection` resilience
- Initial connect: **Polly** exponential backoff, retries on `SocketException` / `BrokerUnreachableException`
- Retry count configured by `RetryConnectionCount` (default: `3`)
- Auto-reconnects on `ConnectionShutdown`, `CallbackException`, `ConnectionBlocked`
- Consumer channel is re-created on `CallbackException` via `CreateChannelAndSubscribeReceiverAsync`
- `SemaphoreSlim _connectionGate` prevents concurrent reconnects

### TLS / SSL
Set `UseTls: true` per virtual host or globally. Requires cert files on disk:

```json
"UseTls": true,
"SslProtocolVersion": "Tls12",
"ClientCertPath": "certs/client.pem",
"ClientKeyPath": "certs/client.key"
```

> In Kubernetes environments, use `Tls12` — the default `Tls` may fail.

---

## Startup Sequence

`StartEventBusServices` (hosted service) runs at application start:

1. `EventPublisherCollector.CreateExchangeForPublishersAsync()` — declares exchanges for all registered publishers
2. `EventSubscriberCollector.CreateConsumerForEachQueueAndStartReceivingEventsAsync()` — creates channels, declares queues, binds routing keys, starts consuming
3. `PrintLoadedPublishersInformation()` — logs all loaded publisher configs (visible at `Debug` level)
4. `PrintLoadedSubscribersInformation()` — logs all loaded subscriber configs (visible at `Debug` level)

`EventBusNotifier` (hosted service) logs warnings if:
- RabbitMQ is disabled (`IsEnabled: false`)
- `UseInbox: true` but `Inbox.IsEnabled: false` in EventStorage — events will be handled without inbox

---

## Registration

`AddRabbitMqEventBus` is the single entry point. It internally calls `AddEventStore` — **never call both** in the same application.

```csharp
builder.Services.AddRabbitMqEventBus(
    builder.Configuration,
    assemblies: [typeof(Program).Assembly],

    // Overrides DefaultSettings from config
    defaultOptions: options =>
    {
        options.HostName = "localhost";
        options.QueueArguments.Add("x-priority", 10);
    },

    // Adds/overrides VirtualHostSettings from config
    virtualHostSettingsOptions: settings =>
    {
        settings.Add("payments", new RabbitMqHostSettings
        {
            VirtualHost = "payments",
            ExchangeName = "payments_exchange",
            HostName = "localhost",
            UserName = "admin",
            Password = "admin123"
        });
    },

    // Adds/overrides Publishers from config
    eventPublisherManagerOptions: pub =>
    {
        pub.AddPublisher<UserDeleted>(op => op.RoutingKey = "users.deleted");
    },

    // Adds/overrides Subscribers from config
    eventSubscriberManagerOptions: sub =>
    {
        sub.AddSubscriber<PaymentCreated, PaymentCreatedHandler>(op =>
            op.VirtualHostKey = "payments");
    },

    // Configures EventStorage Inbox/Outbox
    eventStoreOptions: options =>
    {
        options.Inbox.IsEnabled = true;
        options.Inbox.ConnectionString = connectionString;
        options.Outbox.IsEnabled = true;
        options.Outbox.ConnectionString = connectionString;
    },

    // Hooks — all optional
    executingSubscribedEvent: (sender, args) => { /* before subscriber handles */ },
    executingReceivedEvent:   (sender, args) => { /* before inbox processes */ },
    eventSubscribersHandled:  (sender, args) => { /* after all subscribers handled */ }
);
```

### DI registrations summary

| Service | Lifetime | Notes |
|---|---|---|
| `IEventPublisherManager` | Scoped | Primary publishing API |
| `IEventPublisherCollector` | Singleton | Publisher registry + channel factory |
| `IEventSubscriberCollector` | Singleton | Subscriber registry + consumer manager |
| `IRabbitMqConnectionManager` | Singleton | Connection pool |
| `RabbitMqOptions` | Singleton | Default settings |
| `StartEventBusServices` | Hosted | Startup wiring |
| `EventBusNotifier` | Hosted | Config warnings |

---

## Configuration Reference (`appsettings.json`)

```json
"RabbitMQSettings": {
  "DefaultSettings": {
    "IsEnabled": true,
    "HostName": "localhost",
    "HostPort": 5672,
    "VirtualHost": "/",
    "UserName": "guest",
    "Password": "guest",
    "ExchangeName": "DefaultExchange",
    "ExchangeType": "topic",
    "QueueName": "",
    "RoutingKey": "",
    "RetryConnectionCount": 3,
    "UseInbox": false,
    "UseOutbox": false,
    "EventNamingPolicy": "PascalCase",
    "PropertyNamingPolicy": "PascalCase",
    "UseTls": false,
    "SslProtocolVersion": "Tls",
    "ClientCertPath": null,
    "ClientKeyPath": null,
    "QueueArguments": {},
    "ExchangeArguments": {}
  },
  "Publishers": {
    "UserDeleted": {
      "VirtualHostKey": "payments",
      "RoutingKey": "users.deleted",
      "EventTypeName": "UserDeletedEvent",
      "PropertyNamingPolicy": "SnakeCaseLower"
    }
  },
  "Subscribers": {
    "PaymentCreated": {
      "VirtualHostKey": "payments",
      "QueueName": "payments_queue_UserService",
      "RoutingKey": "payments.created"
    }
  },
  "VirtualHostSettings": {
    "payments": {
      "VirtualHost": "payments",
      "ExchangeName": "payments_exchange",
      "HostName": "rabbitmq-host",
      "UserName": "admin",
      "Password": "secret",
      "EventNamingPolicy": "KebabCaseLower",
      "QueueArguments": { "x-queue-type": "quorum" }
    }
  }
}
```

### Settings resolution priority (highest to lowest)
1. Per-event publisher/subscriber options (code via `eventPublisherManagerOptions` / `eventSubscriberManagerOptions`)
2. Per-event config in `Publishers` / `Subscribers` sections
3. Virtual host config in `VirtualHostSettings[VirtualHostKey]`
4. `DefaultSettings`

Unset properties in a virtual host **inherit** from `DefaultSettings` automatically. To explicitly unset a property (prevent inheritance), set it to empty string.

> `IsEnabled`, `UseInbox`, and `UseOutbox` are **not inheritable** by virtual host settings — they only apply in `DefaultSettings`.

### Auto-computed defaults (when not configured)
- **RoutingKey** → `{ExchangeName}.{EventName converted by EventNamingPolicy}`
- **QueueName** → virtual host `QueueName` if set, otherwise `ExchangeName`
- **EventTypeName** → class name converted by `EventNamingPolicy` (unless `EventTypeName` is explicitly set — then naming policy is ignored)

### Naming policies
Available for both `EventNamingPolicy` and `PropertyNamingPolicy`:
`PascalCase` · `CamelCase` · `SnakeCaseLower` · `SnakeCaseUpper` · `KebabCaseLower` · `KebabCaseUpper`

---

## Inbox / Outbox Integration

### Using Outbox for publishing

There are two ways to use the Outbox pattern for publishing events.

**Option 1 — Global `UseOutbox` flag (automatic)**

Enable in config or via code:
```json
"RabbitMQSettings": { "DefaultSettings": { "UseOutbox": true } }
"InboxAndOutbox":   { "Outbox": { "IsEnabled": true, "ConnectionString": "..." } }
```

Both flags must be `true`. When enabled, `IEventPublisherManager.PublishAsync` and `Collect` automatically route through the Outbox — no code changes needed at the call site. The registered implementation switches from `EventPublisherManager` to `OutboxEventPublisherManager`.

If `UseOutbox: true` but `Outbox.IsEnabled: false`, the application throws `EventBusException` at startup.

**Option 2 — Explicit `IOutboxEventManager` (manual)**

```csharp
// Event must implement IPublishEvent (which inherits IOutboxEvent)
public record UserCreated : IPublishEvent { ... }

// Register a publisher so EventStorage knows how to dispatch it
public class UserCreatedPublisher(IEventPublisherManager eventPublisher)
    : IMessageBrokerEventPublisher<UserCreated>
{
    public async Task PublishAsync(UserCreated e)
        => await eventPublisher.PublishAsync(e, CancellationToken.None);
}

// Store via Outbox — persists first, then dispatches via publisher above
await outboxEventManager.StoreAsync(userCreated, EventProviderType.MessageBroker);
```

> If no typed publisher is registered, the built-in `MessageBrokerEventPublisher` handles all `IPublishEvent` types generically.

### Using Inbox for receiving

Enable in config:
```json
"RabbitMQSettings": { "DefaultSettings": { "UseInbox": true } }
"InboxAndOutbox":   { "Inbox": { "IsEnabled": true, "ConnectionString": "..." } }
```

Both flags must be `true`. If `UseInbox: true` but `Inbox.IsEnabled: false`, the application throws `EventBusException` at startup.

### Managing Inbox / Outbox events (EventStorage `10.1.x`)

`AddEventStore` (called by `AddRabbitMqEventBus`) registers two scoped services with the same `IEventsManagementService` API:
`IInboxEventsService` and `IOutboxEventsService`. They are for an admin UI — the library adds **no endpoints and no authorization**.

| Method | Allowed status → result |
|---|---|
| `GetEventsAsync(EventsFilter, ct)` / `GetEventByIdAsync(id, ct)` / `GetProviderTypes()` | Read-only |
| `ExecuteAsync(id, request, ct)` | `Pending`/`Failed` (or `Processed` with `Force = true`) → `Processed` / `Failed` |
| `RescheduleAsync(id, tryAfterAt, request, ct)` | `Pending`/`Failed`/`Rejected` → `Pending` |
| `RejectAsync(id, request, ct)` | `Pending`/`Failed` → `Rejected` |
| `MarkAsProcessedAsync(id, request, ct)` | `Pending`/`Failed`/`Rejected` → `Processed` (without running it) |

- Actions return `EventActionResult` (`Success` · `NotFound` · `AlreadyProcessing` · `InvalidState` · `Failed`) instead of throwing; they throw only when the Inbox/Outbox is disabled or on real errors.
- `EventActionRequest.PerformedBy` / `Comment` are stored in `updated_by` / `status_comment`.
- Executing an outbox event re-runs `MessageBrokerEventPublisher` → the event is re-published to RabbitMQ; executing an inbox event re-runs the `IEventSubscriber<T>` handlers.
- Tables now use a string `status` column (`Pending` · `Failed` · `Processed` · `Rejected`) instead of `processed_at`; old tables are migrated on startup under an exclusive lock (`InboxAndOutbox.SecondsToWaitForMigrationLock`, default `30`).
- Test endpoints: `tests/Services/UsersService/Controllers/BaseEventsController.cs` → `api/inbox-events`, `api/outbox-events` (`GET`, `GET {id}`, `GET provider-types`, `POST {id}/execute|force-execute|reschedule?tryAfterAt=|reject|mark-as-processed` with body `{ "performedBy", "comment" }`).

---

## Event Headers

Headers are propagated via `IHasHeaders` on all events. Custom headers can be attached before publishing:

```csharp
userUpdated.Headers = new Dictionary<string, string>
{
    { "TraceId", HttpContext.TraceIdentifier },
    { "TenantId", tenantId }
};
await _eventPublisherManager.PublishAsync(userUpdated, ct);
```

Read on the subscriber side:
```csharp
public async Task HandleAsync(UserUpdated @event)
{
    @event.Headers?.TryGetValue("TraceId", out var traceId);
}
```

The library automatically adds to headers: `TraceParentId` (for distributed tracing) and `event.naming-policy-type` (for deserialization validation).

---

## Observability

### OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddEventBusInstrumentation(
            shouldAttachEventPayload: true,   // attaches serialized event body to trace
            shouldAttachEventHeaders: true    // attaches event headers to trace
        )
        .AddEventStorageInstrumentation()    // add if also using Inbox/Outbox tracing
    );
```

**Instrumentation source name:** `EventBus.RabbitMQ`
**Span tag:** `messaging.system = "RabbitMQ"`
**Trace tags:** `event.received-id`, `event.received-type`, `event.received-routing-key`
**Trace context:** `TraceParentId` propagated in AMQP message headers for cross-service correlation

### Logging

Set log level to `Debug` to see:
- All loaded publisher and subscriber configurations on startup
- Each published and received event with its ID and type
- Connection open/close events per virtual host

```json
{ "Logging": { "LogLevel": { "Default": "Debug" } } }
```

### Aspire integration

The test infrastructure uses .NET Aspire (`tests/Services/AspireHost`). When running locally with Aspire, all `OpenTelemetry` traces and logs are visible in the Aspire dashboard. Filter by `Messaging` type to see publishing and receiving traces.

---

## Project Structure

```
EventBus.RabbitMQ.sln
├── src/
│   ├── EventBus.RabbitMQ.csproj
│   ├── Configurations/
│   │   ├── RabbitMqSettings.cs           # Top-level config model (DefaultSettings, Publishers, Subscribers, VirtualHostSettings)
│   │   ├── RabbitMqOptions.cs            # Extends RabbitMqHostSettings + IsEnabled + UseInbox
│   │   ├── RabbitMqHostSettings.cs       # Connection + exchange/queue + naming + TLS settings
│   │   └── RabbitMqOptionsConstant.cs    # Default values
│   ├── Connections/
│   │   ├── RabbitMqConnection.cs         # IConnection lifecycle, Polly retry, TLS, auto-reconnect
│   │   └── RabbitMqConnectionManager.cs  # Connection pool keyed by host:port:vhost
│   ├── Publishers/
│   │   ├── Managers/
│   │   │   ├── IEventPublisherManager.cs       # Public API: PublishAsync, Collect, CleanCollectedEvents
│   │   │   ├── EventPublisherManager.cs        # Scoped — serializes and sends to RabbitMQ, publishes collected on Dispose
│   │   │   ├── OutboxEventPublisherManager.cs  # Scoped — implements PublishAsync/Collect/CleanCollectedEvents to use IOutboxEventManager (UseOutbox=true)
│   │   │   ├── IEventPublisherCollector.cs
│   │   │   └── EventPublisherCollector.cs      # Singleton — registry of publishers + channel factory
│   │   ├── Messaging/
│   │   │   └── MessageBrokerEventPublisher.cs # IMessageBrokerEventPublisher bridge: Outbox → RabbitMQ
│   │   ├── Models/
│   │   │   └── IPublishEvent.cs           # : IBaseEvent, IOutboxEvent
│   │   └── Options/
│   │       └── EventPublisherOptions.cs   # Per-event routing key, virtual host key, event type name
│   ├── Subscribers/
│   │   ├── Consumers/
│   │   │   └── EventConsumerService.cs    # AMQP consumer: receive → Inbox or direct handler dispatch
│   │   ├── Managers/
│   │   │   ├── IEventSubscriberCollector.cs
│   │   │   └── EventSubscriberCollector.cs # Singleton — registry + consumer lifecycle
│   │   ├── Models/
│   │   │   ├── ISubscribeEvent.cs         # : IBaseEvent, IInboxEvent
│   │   │   └── IEventSubscriber<T>.cs     # : IMessageBrokerEventHandler<T>
│   │   └── Options/
│   │       └── EventSubscriberOptions.cs  # Per-event queue name, routing key, virtual host key
│   ├── BackgroundServices/
│   │   ├── StartEventBusServices.cs       # Startup: declare exchanges, create consumers
│   │   └── EventBusNotifier.cs            # Startup: log config warnings
│   ├── Extensions/
│   │   └── RabbitMqExtensions.cs          # AddRabbitMqEventBus — main registration entry point
│   ├── Instrumentation/
│   │   ├── EventBusInvestigationTagNames.cs
│   │   ├── InstrumentationBuilderExtensions.cs  # AddEventBusInstrumentation
│   │   └── Trace/
│   │       └── EventBusTraceInstrumentation.cs  # ActivitySource "EventBus.RabbitMQ"
│   └── Models/
│       └── IBaseEvent.cs                  # : IEvent, IHasHeaders + CreatedAt
└── tests/
    ├── EventBus.RabbitMQ.Tests/           # NUnit unit + integration tests
    └── Services/
        ├── AspireHost/                    # .NET Aspire orchestration for integration tests
        ├── ServiceDefaults/               # Shared OTel, health checks, service discovery
        ├── UsersService/                  # Integration test microservice (publishes + subscribes)
        │   └── Controllers/               # UserController + Inbox/OutboxEventsController (BaseEventsController) for the management API
        └── OrdersService/                 # Integration test microservice (subscribes)
```

---

## Design Rules & Constraints

| Rule | Detail |
|---|---|
| One publisher per event type | Duplicate registration throws `EventBusException` at startup |
| Multiple subscribers per event type | Allowed — all are invoked on receive, even for duplicate type names across namespaces |
| `AddRabbitMqEventBus` calls `AddEventStore` internally | Never call both — second registration is skipped silently |
| `UseInbox: true` requires `Inbox.IsEnabled: true` | `EventBusException` thrown at startup if Inbox is not enabled in EventStorage config |
| `UseOutbox: true` requires `Outbox.IsEnabled: true` | `EventBusException` thrown at startup if Outbox is not enabled in EventStorage config |
| `UseOutbox: true` changes `IEventPublisherManager` impl | Registers `OutboxEventPublisherManager` instead of `EventPublisherManager`; `PublishAsync`/`Collect` route through `IOutboxEventManager` |
| `EventTypeName` overrides `EventNamingPolicy` | If `EventTypeName` is set, naming policy is ignored for that event |
| Subscriber event type resolved from `BasicProperties.Type` | Falls back to `RoutingKey` if type header is absent |
| Naming policy mismatch throws | If received `event.naming-policy-type` header differs from configured, an `EventBusException` is thrown |
| `BasicAck` on inbox path is immediate | ACK sent after `IInboxEventManager.Store()`, not after handler execution |
| `IEventPublisherManager` is `IDisposable` | Collected events are published on `Dispose` (end of scope) |

---

## CI/CD

| Workflow | Trigger |
|---|---|
| `build.yml` | Push / PR |
| `run-tests.yml` | Reusable — NUnit tests with PostgreSQL + RabbitMQ service containers |
| `service-tests.yml` | PR to `main` |
| `push-nuget-package.yml` | Manual / release |
| `release.yml` | Release tag |
| `service-versioning.yml` | Reusable — auto-increments patch version in `.csproj`, commits to `main` |

Version format: `MAJOR.MINOR.PATCH` — patch auto-rolls over to next minor at 100.