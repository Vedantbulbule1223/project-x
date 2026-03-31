# Design Patterns & Justification Report
## SwiftRoute Pulse Command Center

---

## 1. Repository Pattern

**Where Used:** `IDriverRepository`, `IVehicleRepository`, `IAssignmentRepository`, `IMaintenanceLogRepository`

**What It Does:**
Abstracts data access behind an interface layer. All database queries live inside repository classes — controllers and services never call `DbContext` directly.

**Why Chosen for SwiftRoute:**
SwiftRoute's business rules are complex and tightly coupled to data integrity. By isolating database access, we can:
- **Unit-test business logic** without a real database (mock the repository).
- **Swap MySQL for another provider** (e.g., PostgreSQL) without touching any service or controller.
- Keep all EF-specific concerns (`.Include()`, `.AnyAsync()`, LINQ queries) out of the business layer.

**Code Example:**
```csharp
public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(string id);
    Task<bool> HasActiveAssignmentAsync(string driverId);
    ...
}
```

---

## 2. Unit of Work Pattern

**Where Used:** `IUnitOfWork` / `UnitOfWork`

**What It Does:**
Groups all repositories under a single transaction boundary. One call to `SaveChangesAsync()` commits all changes atomically.

**Why Chosen for SwiftRoute:**
Assignment creation is a multi-entity operation. When creating an assignment we must simultaneously:
1. Insert the `Assignment` record.
2. Update `Driver.Status → Assigned`.
3. Update `Vehicle.Status → InTransit`.

If any step fails, none should persist. The Unit of Work pattern wraps all three in one `DbContext` and one `SaveChangesAsync()`, giving us atomicity for free.

```csharp
await _uow.Assignments.CreateAsync(assignment);
await _uow.Drivers.UpdateAsync(driver);     // all on same DbContext
await _uow.Vehicles.UpdateAsync(vehicle);
await _uow.SaveChangesAsync();              // single atomic commit
```

---

## 3. Service Layer (Facade) Pattern

**Where Used:** `AssignmentService`, `VehicleService`, `AuthService`

**What It Does:**
Encapsulates all business logic inside dedicated service classes that sit between controllers and repositories. Controllers become thin — they only handle HTTP serialization and routing.

**Why Chosen for SwiftRoute:**
SwiftRoute has extensive, multi-step business rules for every write operation. Putting these rules in the service layer means:
- **Controllers stay clean** and focused on HTTP concerns.
- **Rules are tested in isolation** without spinning up HTTP infrastructure.
- The same service can be called from a background job, a test, or a controller.

The `AssignmentService.CreateAssignmentAsync()` method enforces 7 sequential business rules before writing to the DB — this complexity has no place in a controller.

---

## 4. Observer / Event Pattern (via SignalR)

**Where Used:** `PulseHub`, `IHubContext<PulseHub>` injected into services

**What It Does:**
When a state change occurs (odometer update crosses 10k, assignment created), services broadcast a message to all connected clients via SignalR WebSockets. Clients react without polling.

**Why Chosen for SwiftRoute:**
The "Communication Lag" problem is explicitly described in the spec: Dispatchers couldn't see Maintenance Team's shop updates for *hours*. The Observer pattern via SignalR solves this by:
- Pushing `MaintenanceAlert` to every connected dashboard the instant an odometer update triggers the 10k rule.
- Pushing `AssignmentCreated` / `VehicleUpdated` so every dispatcher's screen refreshes in real time.
- Eliminating the need for costly polling.

```csharp
// In VehicleService — after detecting threshold breach:
await _hub.Clients.All.SendAsync("MaintenanceAlert", alert);
```

---

## 5. Concurrency Token Pattern (Optimistic Locking)

**Where Used:** `Driver.RowVersion`, `Vehicle.RowVersion`, `Assignment.RowVersion`

**What It Does:**
EF Core uses a `[Timestamp]` / `.IsRowVersion()` byte-array column. When saving, EF generates a `WHERE RowVersion = @original` clause. If another dispatcher modified the row in between, the update affects 0 rows → EF throws `DbUpdateConcurrencyException`.

**Why Chosen for SwiftRoute:**
The "Ghost Assignment" problem — two dispatchers saving over each other — is the #1 described crisis. This pattern ensures:
- The **first save wins**, the second gets a clear error.
- No driver or vehicle can be double-booked even under concurrent load.
- The error is surfaced to the user as a friendly "Resource Already Assigned" message.

```csharp
catch (DbUpdateConcurrencyException)
{
    throw new InvalidOperationException(
        "Resource Already Assigned – another dispatcher created an assignment simultaneously.");
}
```

---

## 6. Dependency Injection (built-in .NET DI)

**Where Used:** Throughout — all services, repositories, hub context registered in `Program.cs`

**What It Does:**
All dependencies are registered with the DI container and injected via constructor injection.

**Why Chosen for SwiftRoute:**
- **Testability**: Any dependency can be replaced with a mock.
- **Lifecycle management**: `DbContext` is scoped per request, preventing connection leaks.
- **Loose coupling**: Services don't `new` their dependencies.

---

## 7. DTO (Data Transfer Object) Pattern

**Where Used:** All `*Dto`, `*Request`, `*Response` records in `DTOs/Dtos.cs`

**What It Does:**
Domain entities (`Driver`, `Vehicle`) never leave the API layer. Instead, dedicated DTOs are mapped and serialized.

**Why Chosen for SwiftRoute:**
- Prevents accidental exposure of sensitive fields (e.g., `RowVersion`, `PasswordHash`).
- Allows the API contract to evolve independently of the DB schema.
- Computed properties (e.g., `IsLicenseValid`, `KmSinceService`) can be included in the DTO without storing them in the DB.

---

## Pattern Summary Table

| Pattern              | Location                    | Problem Solved                        |
|----------------------|-----------------------------|---------------------------------------|
| Repository           | Repositories/               | Testable data access, DB abstraction  |
| Unit of Work         | UnitOfWork                  | Atomic multi-entity transactions      |
| Service Layer        | Services/                   | Business rule isolation               |
| Observer (SignalR)   | Hubs/PulseHub + Services    | Real-time updates, eliminates lag     |
| Concurrency Token    | Models (RowVersion)         | Ghost assignment / double-booking     |
| Dependency Injection | Program.cs                  | Loose coupling, testability           |
| DTO                  | DTOs/                       | Safe API contracts, encapsulation     |
