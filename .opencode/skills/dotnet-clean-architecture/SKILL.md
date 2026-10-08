---
name: dotnet-clean-architecture
description: Apply modern .NET best practices and Clean Architecture to ASP.NET Core applications with Domain, Application, Infrastructure, API, and Angular Frontend layers. Use when creating, modifying, reviewing, refactoring, testing, or designing features in this architecture.
compatibility: opencode
metadata:
  stack: dotnet-aspnetcore-efcore-postgresql-angular
  architecture: clean-architecture
  style: pragmatic-ddd-cqrs
---

# .NET Clean Architecture

Use this skill whenever the repository follows, or is intended to follow, this structure:

```text
Api/
Application/
Domain/
Infrastructure/
Frontend/        # Angular
```

The goal is **maintainable, testable, pragmatic software**, not maximum abstraction.

## 1. Core rules

The dependency direction is:

```text
Frontend ──HTTP──> Api ──> Application ──> Domain
                         │
                         └───────────────> Infrastructure
```

Infrastructure implements abstractions required by Application/Domain.

Strict rules:

- Domain MUST NOT reference Application, Infrastructure, Api, EF Core, ASP.NET Core, Angular, or infrastructure-specific libraries.
- Application MUST NOT reference Api or Infrastructure implementations.
- Api MUST NOT contain business rules.
- Infrastructure MUST NOT leak persistence models through the API.
- Controllers/endpoints MUST be thin.
- Application orchestrates use cases.
- Domain owns business invariants.
- Infrastructure owns technical concerns.
- Frontend communicates through HTTP contracts and never depends on server-side implementation details.
- Do not introduce an abstraction merely to satisfy a pattern.
- Prefer the simplest design that preserves the architectural boundary.

Before changing code, inspect the existing solution/project structure and follow established conventions unless they violate these rules.

## 2. First decide where the code belongs

Use this decision tree:

```text
Is this a business invariant?
  -> Domain

Does this coordinate a use case?
  -> Application

Does this expose HTTP?
  -> Api

Does this talk to PostgreSQL, EF Core, files, email, Kafka, external APIs, etc.?
  -> Infrastructure

Does this render UI, manage browser state, or call HTTP from the browser?
  -> Angular Frontend
```

Examples:

```text
"An order cannot be paid twice"
    -> Domain

"Load order, validate payment request, call payment gateway, persist result"
    -> Application

"POST /api/orders/{id}/pay"
    -> Api

"StripePaymentGateway"
    -> Infrastructure

"PaymentService using HttpClient"
    -> Infrastructure

"Button that starts payment"
    -> Angular
```

## 3. Domain

Prefer rich domain models over anemic entities when behavior is meaningful.

### Entities

Entities have identity and encapsulate invariants.

```csharp
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public OrderId Id { get; private set; }
    public OrderStatus Status { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public void AddItem(Product product, int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        if (Status != OrderStatus.Draft)
            throw new DomainException("Only draft orders can be modified.");

        _items.Add(OrderItem.Create(product.Id, quantity, product.Price));
    }

    public void Submit()
    {
        if (_items.Count == 0)
            throw new DomainException("An order cannot be submitted without items.");

        Status = OrderStatus.Submitted;
    }
}
```

Rules:

- Keep setters private/protected when mutation must be controlled.
- Constructors/factories should establish valid state.
- Avoid public collections that allow arbitrary mutation.
- Avoid domain methods that depend on HTTP, database, logging, configuration, or framework services.
- Prefer explicit methods such as `Submit()`, `Cancel()`, `ChangeAddress()` over public property mutation when invariants exist.
- Use `DateTimeOffset` for real-world timestamps unless a domain explicitly requires another representation.
- Prefer UTC internally.

### Value Objects

Use value objects when a concept is defined by its value rather than identity.

Examples:

- Money
- EmailAddress
- PhoneNumber
- Address
- ProductCode

Prefer immutable value objects.

Do not create value objects for every primitive automatically. Introduce them when they express a domain concept, validation, or repeated semantics.

### Aggregates

An aggregate is a consistency boundary.

Rules:

- Define a clear aggregate root.
- External code should modify an aggregate through its root.
- Do not load/update an entire aggregate when only a small projection is required.
- Keep aggregate boundaries small enough to avoid unnecessary transaction coupling.
- Do not make every entity an aggregate root.

### Domain Services

Use a domain service only when a domain operation:

- is business logic;
- does not naturally belong to one entity/value object;
- requires multiple domain concepts.

Do not use Domain Services as a dumping ground for business logic.

### Domain Events

Use domain events for meaningful business facts:

```text
OrderSubmitted
PaymentCompleted
UserRegistered
```

Domain events should describe something that happened, not an imperative command.

Keep event handlers out of entities.

Avoid using domain events for trivial internal method calls.

## 4. Application layer

Application defines and executes use cases.

Typical structure:

```text
Application/
├── Common/
│   ├── Behaviors/
│   ├── Errors/
│   ├── Interfaces/
│   └── Models/
├── Orders/
│   ├── Commands/
│   │   ├── CreateOrder/
│   │   ├── SubmitOrder/
│   │   └── CancelOrder/
│   └── Queries/
│       ├── GetOrder/
│       └── ListOrders/
└── DependencyInjection.cs
```

Prefer feature-oriented organization over huge technical folders.

### Commands and queries

Use CQRS pragmatically:

```text
Command = changes state
Query   = reads state
```

A command handler should generally:

1. validate the request;
2. load required domain state;
3. invoke domain behavior;
4. persist changes;
5. publish required events/integration messages;
6. return an appropriate result.

A query should optimize for reading and may use projections rather than domain entities.

Do not force CQRS into tiny CRUD operations if it creates needless complexity.

### DTOs

Never expose EF Core entities directly from the API.

Use request/response contracts.

Prefer explicit DTOs:

```csharp
public sealed record CreateOrderRequest(
    Guid CustomerId,
    IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record OrderResponse(
    Guid Id,
    string Status,
    decimal Total);
```

Do not blindly mirror every entity property into a DTO.

### Application interfaces

Interfaces belong to the layer that owns the dependency abstraction.

Examples:

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
}
```

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
```

Infrastructure implements them.

Do not create `IWhateverService` interfaces for every class merely because dependency inversion exists.

## 5. Result and error handling

Use a consistent strategy for expected failures.

Distinguish:

```text
Expected business failure
    -> Result / Error / validation response

Unexpected failure
    -> exception -> global exception handling -> ProblemDetails
```

Examples of expected failures:

- Order does not exist.
- Product is unavailable.
- User cannot perform this operation.
- Invalid state transition.

Do not use exceptions as normal control flow.

Do not catch `Exception` inside every handler.

Centralize unexpected exception handling.

Use RFC 7807-compatible `ProblemDetails` responses from the API.

Keep domain errors independent from HTTP status codes.

Bad:

```csharp
throw new BadRequestException(...);
```

inside Domain.

Better:

```text
Domain -> domain error
Api    -> maps application/domain error to HTTP response
```

## 6. Validation

Use FluentValidation or the repository's established validation mechanism.

Separate:

```text
Input validation
    -> Application

Business invariants
    -> Domain
```

Example:

```csharp
public sealed class CreateOrderValidator
    : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty();

        RuleFor(x => x.Items)
            .NotEmpty();
    }
}
```

Do not duplicate the same invariant in five layers.

Application validation improves user-facing feedback; Domain validation guarantees correctness regardless of caller.

## 7. Persistence / EF Core

Infrastructure owns EF Core.

Recommended organization:

```text
Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   │   ├── OrderConfiguration.cs
│   │   └── ...
│   ├── Migrations/
│   └── Repositories/
└── DependencyInjection.cs
```

Prefer `IEntityTypeConfiguration<TEntity>` for non-trivial mappings.

Keep persistence configuration out of Domain.

Do not scatter EF configuration across entities unless the project intentionally uses data annotations.

### DbContext

Use scoped `DbContext` in normal web requests.

Use `AsNoTracking()` for read-only queries.

Do not use `AsNoTracking()` blindly when tracked updates are required.

Avoid N+1 queries.

Project directly to DTOs for read-heavy queries when appropriate:

```csharp
var orders = await dbContext.Orders
    .AsNoTracking()
    .Where(...)
    .Select(x => new OrderResponse(...))
    .ToListAsync(cancellationToken);
```

Avoid loading entities only to map them into a DTO when a direct projection is sufficient.

### Repositories

Do not automatically create generic repositories such as:

```csharp
IRepository<T>
```

EF Core already provides a unit-of-work/change-tracking abstraction.

Use repositories when they provide meaningful domain/application semantics, aggregate boundaries, or isolate a persistence concern.

Good:

```csharp
IOrderRepository
```

Potentially unnecessary:

```csharp
IGenericRepository<Order>
```

### Transactions

Use transactions when one use case requires atomic persistence across multiple changes.

Do not wrap every single database operation in an explicit transaction if the underlying operation is already atomic.

Keep transaction orchestration in Application/Infrastructure boundaries, not Domain.

## 8. PostgreSQL

Use PostgreSQL-specific features deliberately.

Recommended:

- correct indexes based on real query patterns;
- explicit migrations;
- appropriate constraints;
- unique indexes for invariants that must also hold at database level;
- UTC timestamps;
- database-generated values only when appropriate;
- optimistic concurrency when concurrent updates matter.

A business invariant that can be violated by concurrent requests should generally have a database-level protection where feasible.

Example:

```text
Domain rule:
    username must be unique

Application:
    checks existing username

Database:
    UNIQUE constraint/index
```

The application check improves UX; the database constraint guarantees integrity.

## 9. API layer

Controllers/endpoints should be thin.

A controller should mostly:

```text
HTTP request
    -> bind
    -> authorize
    -> invoke application use case
    -> map result
    -> HTTP response
```

Avoid:

```csharp
[HttpPost]
public async Task<IActionResult> Create(...)
{
    // 150 lines of business logic
}
```

Prefer conventional REST semantics.

Example:

```text
POST   /api/orders
GET    /api/orders/{id}
GET    /api/orders
PUT    /api/orders/{id}
DELETE /api/orders/{id}
POST   /api/orders/{id}/submit
```

Do not invent RPC-like endpoints when a resource-oriented design is clearer.

### API contracts

Treat API request/response models as public contracts.

Avoid exposing internal implementation details.

Use explicit status codes.

Typical mapping:

```text
200 OK
201 Created
204 No Content
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Entity
500 Internal Server Error
```

Use `409 Conflict` when the request is syntactically valid but conflicts with current resource state.

## 10. Authentication and authorization

Authentication answers:

```text
Who are you?
```

Authorization answers:

```text
What are you allowed to do?
```

Prefer policy-based authorization for non-trivial authorization rules.

Do not put authorization logic only in Angular. Angular guards improve UX; the API must enforce authorization.

Never trust client-provided:

- user IDs;
- roles;
- permissions;
- prices;
- ownership;
- security decisions.

Obtain security-sensitive identity data from the authenticated server-side principal.

## 11. Dependency Injection

Register dependencies at composition roots.

Typical locations:

```text
Api/Program.cs
Infrastructure/DependencyInjection.cs
Application/DependencyInjection.cs
```

Keep registrations discoverable.

Use appropriate lifetimes:

```text
Singleton  -> stateless/thread-safe application-wide services
Scoped     -> request-scoped services, DbContext
Transient  -> lightweight stateless services when appropriate
```

Do not inject scoped services into singletons.

Avoid service locator patterns.

Avoid resolving services manually from `IServiceProvider` except in legitimate framework/composition scenarios.

## 12. Async and cancellation

All I/O should normally be asynchronous.

Use:

```csharp
Task
ValueTask
CancellationToken
```

Pass cancellation tokens through the call chain:

```text
API
 -> Application
 -> Infrastructure
 -> EF Core / HttpClient
```

Prefer:

```csharp
Task<Order?> GetAsync(
    OrderId id,
    CancellationToken cancellationToken)
```

Avoid:

```csharp
async Task Foo()
{
    ...
}
```

when an operation performs cancellable I/O but ignores the token.

Do not use `.Result` or `.Wait()` in application code.

Avoid `async void` except UI event handlers where required by the framework.

## 13. External services

Use typed/named `HttpClient` through `IHttpClientFactory`.

Infrastructure owns external API clients.

Example:

```text
Application
    IEmailSender
        ^
        |
Infrastructure
    SmtpEmailSender
```

Application should not know whether the implementation uses SMTP, SendGrid, SES, etc.

For unreliable external dependencies consider:

- timeout;
- cancellation;
- retry only when safe;
- exponential backoff;
- circuit breaker;
- idempotency.

Do not blindly retry non-idempotent operations.

## 14. Messaging and Kafka

When Kafka is used:

```text
Domain Event
    !=
Integration Event
    !=
Command
```

A command asks for something to happen.

A domain event says something happened inside the domain.

An integration event communicates a business fact to another bounded context/service.

Do not publish raw EF entities to Kafka.

Use explicit event contracts.

If reliable DB + message publication is required, consider the Outbox Pattern:

```text
Application transaction
    ├── domain state changes
    └── outbox message

background publisher
    └── Kafka
```

The outbox is preferable to:

```text
DB commit
    then
Kafka publish
```

when losing a message between those two operations is unacceptable.

Consumers should be idempotent.

## 15. API ↔ Angular

Treat the API as the contract boundary.

Recommended Angular organization:

```text
Frontend/
└── src/app/
    ├── core/
    │   ├── auth/
    │   ├── http/
    │   └── config/
    ├── shared/
    └── features/
        └── orders/
            ├── pages/
            ├── components/
            ├── data-access/
            └── models/
```

Prefer feature-based organization.

Angular should not reproduce server-side business rules that affect correctness.

Good client-side responsibilities:

- form validation for UX;
- loading/error state;
- route guards;
- presentation;
- local UI state;
- calling API services.

Server responsibilities:

- authorization;
- business invariants;
- pricing;
- ownership;
- persistence;
- security.

For reactive state, use Angular signals where they simplify local/UI state. Do not introduce signals, RxJS streams, stores, or global state management solely because they are available.

## 16. Mapping

Mapping is useful at architectural boundaries.

Typical mappings:

```text
HTTP Request DTO
    -> Application Command

Domain Entity
    -> Response DTO

Persistence projection
    -> Query DTO
```

Use explicit mapping for important contracts.

AutoMapper is optional. Do not introduce it automatically.

For simple mappings, explicit code is often clearer.

## 17. Testing

Test behavior, not implementation details.

Test pyramid:

```text
       E2E
      /   \
 Integration
    /       \
   Unit Tests
```

### Domain tests

Should be fast and framework-independent.

Test:

- invariants;
- state transitions;
- value objects;
- domain services;
- domain events.

### Application tests

Test:

- use-case behavior;
- validation;
- authorization decisions where appropriate;
- interaction with abstractions.

Mock only boundaries that provide real value.

Do not mock every class.

### Integration tests

Use real infrastructure where practical.

For PostgreSQL integration tests, prefer Testcontainers when the project supports it.

Test:

- EF mappings;
- migrations;
- database constraints;
- repository behavior;
- API endpoints;
- authentication/authorization integration.

### E2E

Use sparingly for critical user journeys.

Do not make the entire test suite E2E.

## 18. Logging and observability

Use structured logging.

Prefer:

```csharp
logger.LogInformation(
    "Order {OrderId} submitted by {UserId}",
    orderId,
    userId);
```

over interpolated strings.

Do not log:

- passwords;
- access tokens;
- refresh tokens;
- secrets;
- sensitive personal data unless explicitly required and protected.

Use correlation/request IDs where useful.

For distributed systems consider OpenTelemetry for:

- traces;
- metrics;
- logs.

## 19. Configuration

Use strongly typed Options:

```csharp
public sealed class PaymentOptions
{
    public required string BaseUrl { get; init; }
}
```

Bind configuration explicitly.

Do not scatter:

```csharp
configuration["Payment:BaseUrl"]
```

through application code.

Never hard-code secrets.

Use environment variables, secret stores, or the project's established secret-management mechanism.

## 20. Dependency and package discipline

Before adding a NuGet package:

1. Check whether the framework already provides the functionality.
2. Check whether an existing project dependency can solve it.
3. Prefer mature, maintained packages.
4. Avoid packages that create architectural coupling without significant benefit.
5. Add the smallest necessary dependency.

Do not introduce MediatR, AutoMapper, FluentValidation, MassTransit, Serilog, or another library merely because it is popular. Use it when it solves a concrete problem in the project.

## 21. Patterns: decision rules

### Repository Pattern

Use when:

- aggregate persistence needs an explicit abstraction;
- persistence complexity deserves isolation;
- tests benefit from a meaningful boundary.

Do not use merely because "Clean Architecture requires repositories."

### Unit of Work

EF Core `DbContext` already behaves as a unit of work.

Do not create another abstraction unless it provides meaningful semantics.

### Specification Pattern

Use when complex query predicates need reuse/composition and the project benefits from it.

Do not create specifications for trivial `Where` clauses.

### Factory Pattern

Use when creation involves:

- multiple construction paths;
- complex invariant establishment;
- selecting an implementation;
- encapsulating creation policy.

Do not create factories for constructors that already express the creation clearly.

### Strategy Pattern

Use when an algorithm varies behind a stable abstraction:

```text
IPricingStrategy
    ├── RegularPricing
    ├── DiscountPricing
    └── PromotionalPricing
```

Do not create a strategy abstraction for one implementation with no meaningful variation.

### Decorator

Good for cross-cutting behavior:

```text
Handler
  ↑
LoggingDecorator
ValidationDecorator
CachingDecorator
```

Use framework pipeline behaviors where appropriate.

### Adapter

Use at external boundaries:

```text
Application
   |
   | IPaymentGateway
   v
Infrastructure
   PaymentProviderAdapter
```

### CQRS

Use when reads and writes have different needs or complexity.

Do not force CQRS into a tiny CRUD application if it adds ceremony without value.

## 22. MediatR / pipeline behaviors

If MediatR is already part of the project, use it consistently.

A useful pipeline may contain:

```text
Request
  ↓
Logging
  ↓
Validation
  ↓
Authorization (if applicable)
  ↓
Transaction (when applicable)
  ↓
Handler
```

Do not create pipeline behaviors that duplicate ASP.NET middleware responsibilities.

If MediatR is not present, do not add it automatically just because Commands/Queries are being used.

A direct Application Service or handler can be perfectly valid.

## 23. Caching

Cache only after identifying an actual performance or load requirement.

Consider:

```text
MemoryCache
Distributed cache / Redis
HTTP caching
Database/index optimization
```

First optimize query shape and indexing.

Cache invalidation must be explicit.

Never cache authorization-sensitive data without considering tenant/user scope.

## 24. Multi-tenancy

If the application is multi-tenant, tenant identity must be server-controlled.

Never trust:

```http
X-Tenant-Id: arbitrary-value
```

without validating that the authenticated principal can access that tenant.

Make tenant filtering difficult to forget.

Consider database constraints, query filters, repository boundaries, and authorization policies according to the project's requirements.

## 25. Security baseline

Always consider:

- input validation;
- output encoding;
- authorization;
- authentication;
- CSRF where applicable;
- CORS;
- SQL injection;
- SSRF;
- path traversal;
- insecure deserialization;
- secret exposure;
- excessive error details;
- mass assignment;
- rate limiting;
- file upload validation.

Never concatenate SQL with user input.

Never return stack traces in production responses.

Never trust client-calculated totals, permissions, roles, or ownership.

## 26. Performance baseline

Before optimizing:

1. Identify the actual bottleneck.
2. Measure.
3. Optimize.
4. Measure again.

Common EF Core problems:

- N+1 queries;
- over-fetching;
- missing indexes;
- loading large graphs;
- tracking unnecessarily;
- inefficient pagination.

For large datasets prefer keyset/seek pagination when offset pagination becomes expensive.

Do not introduce caching or microservices to solve a problem that an index or query projection would solve.

## 27. Naming

Prefer conventional .NET naming:

```text
PascalCase:
    Classes
    Methods
    Properties
    Records

camelCase:
    parameters
    local variables

I prefix:
    interfaces
```

Use meaningful names:

```text
IOrderRepository
CreateOrderCommand
CreateOrderCommandHandler
GetOrderQuery
GetOrderQueryHandler
OrderConfiguration
```

Avoid generic names:

```text
Helper
Manager
Processor
Utility
CommonService
DataService
```

unless the responsibility is genuinely clear.

## 28. Project structure

A good starting structure:

```text
src/
├── Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Contracts/
│   └── Program.cs
│
├── Application/
│   ├── Common/
│   ├── Features/
│   └── DependencyInjection.cs
│
├── Domain/
│   ├── Common/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   ├── Exceptions/
│   └── Services/
│
├── Infrastructure/
│   ├── Persistence/
│   ├── Identity/
│   ├── Messaging/
│   ├── ExternalServices/
│   └── DependencyInjection.cs
│
└── Frontend/
    └── Angular application

tests/
├── Domain.Tests/
├── Application.Tests/
├── Infrastructure.Tests/
├── Api.IntegrationTests/
└── E2E/
```

Feature-based organization inside Application and Angular is preferred as the codebase grows.

Do not create every folder until it is needed.

## 29. Implementation workflow

When asked to implement a feature:

### Step 1 — Inspect

Inspect:

- solution/project files;
- target framework;
- existing architecture;
- existing conventions;
- package versions;
- existing equivalent features;
- tests;
- DI registration;
- API conventions.

Do not assume a greenfield project.

### Step 2 — Identify the use case

Write down:

```text
Actor:
Goal:
Input:
Expected result:
Business invariants:
Authorization:
Persistence:
External dependencies:
Events/messages:
```

For a small task, keep this mental rather than creating unnecessary documentation.

### Step 3 — Place responsibilities

Explicitly determine:

```text
Domain:
Application:
Infrastructure:
Api:
Angular:
Tests:
```

### Step 4 — Implement bottom-up only when useful

A typical flow:

```text
Domain
  ↓
Application
  ↓
Infrastructure
  ↓
API
  ↓
Angular
  ↓
Tests / integration
```

Do not rigidly follow this order if the repository's workflow is different.

### Step 5 — Verify architecture

Check:

```text
Domain has no framework/persistence dependency.
Application does not depend on Infrastructure.
API does not contain business rules.
Infrastructure is registered through DI.
DTOs are not EF entities.
CancellationToken is propagated.
Expected failures are handled consistently.
Tests cover the important behavior.
```

### Step 6 — Verify build and tests

Run the narrowest useful verification first, then broader verification.

Examples:

```bash
dotnet build
dotnet test
dotnet ef migrations add ...
dotnet ef database update
```

Only run destructive database commands when explicitly appropriate.

## 30. Refactoring rules

When modifying existing code:

- preserve behavior unless the task explicitly changes it;
- avoid unrelated rewrites;
- do not introduce a new pattern merely to make code "cleaner";
- prefer incremental refactoring;
- keep commits/diffs understandable;
- remove dead code only when safe;
- update tests with behavior changes.

If existing architecture conflicts with this skill, prioritize the actual repository conventions unless they create a clear architectural or security defect.

## 31. Anti-patterns

Never introduce these without an explicit reason:

```text
God Service
God Controller
Generic Repository<T> everywhere
Generic UnitOfWork everywhere
Anemic Domain Model for complex business rules
Service Locator
Static mutable state
Database access from Domain
HTTP calls from Domain
EF entities returned directly from API
Business logic in Angular only
Business logic in controllers
Exception-driven normal control flow
One interface per class without a reason
One project per entity
Microservices without an actual boundary/reason
CQRS for every CRUD operation
Event-driven architecture for trivial workflows
Premature caching
Premature abstractions
```

## 32. Final quality gate

Before declaring a change complete, verify:

### Architecture
- [ ] Dependency direction is respected.
- [ ] Domain remains framework-independent.
- [ ] Application does not depend on Infrastructure.
- [ ] API remains thin.
- [ ] Infrastructure contains technical implementations.

### Domain
- [ ] Invariants are protected.
- [ ] Entities own meaningful behavior.
- [ ] Value objects are used where they clarify the domain.
- [ ] Aggregate boundaries are respected.

### Application
- [ ] Use case is explicit.
- [ ] DTOs/contracts are appropriate.
- [ ] Validation is in the correct layer.
- [ ] CancellationToken is propagated.
- [ ] Expected errors are represented consistently.

### Persistence
- [ ] EF configuration is appropriate.
- [ ] Queries avoid N+1.
- [ ] Read-only queries use no-tracking where appropriate.
- [ ] Database constraints protect important invariants.
- [ ] Transactions exist only where needed.

### API
- [ ] Correct HTTP semantics.
- [ ] Authorization is enforced server-side.
- [ ] ProblemDetails/error handling is consistent.
- [ ] Internal entities are not exposed.

### Angular
- [ ] API contracts are explicit.
- [ ] UI state is separated from server state where appropriate.
- [ ] Security-sensitive rules are not delegated to the client.
- [ ] Feature organization is maintained.

### Testing
- [ ] Domain behavior is tested.
- [ ] Important use cases are tested.
- [ ] Persistence/API integration is tested where valuable.
- [ ] Tests verify behavior rather than implementation details.

### Maintainability
- [ ] No unnecessary package/pattern was introduced.
- [ ] No unrelated refactor was performed.
- [ ] Names communicate intent.
- [ ] The implementation is simpler or clearer than the alternative.

When uncertain between two valid designs, prefer the one with fewer abstractions and clearer ownership.
