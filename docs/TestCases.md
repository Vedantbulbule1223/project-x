# Test Cases Document
## SwiftRoute Pulse Command Center

---

# PART 1: UNIT TEST CASES (Business Logic)

## UC-01: License Validity Check

**Component:** `AssignmentService.CreateAssignmentAsync()`
**Business Rule:** A driver with an expired license MUST NOT be assigned.

| Test ID | Scenario                             | Input                                              | Expected Result                             | Status |
|---------|--------------------------------------|----------------------------------------------------|---------------------------------------------|--------|
| UC-01-1 | Expired license — block assignment   | Driver D002 (expired 2023-01-15), any active vehicle | Throws InvalidOperationException: "expired license" | ✅ PASS |
| UC-01-2 | Valid license — allow to proceed     | Driver D001 (expires 2025-12-31), active vehicle   | Does not throw license error                | ✅ PASS |
| UC-01-3 | License expires today (edge case)    | Driver with expiry = DateTime.UtcNow.Date          | Blocked (same-day expiry = expired)         | ✅ PASS |
| UC-01-4 | License expires tomorrow             | Expiry = DateTime.UtcNow.AddDays(1)                | Allowed                                     | ✅ PASS |

---

## UC-02: Vehicle Status Lock

**Component:** `AssignmentService.CreateAssignmentAsync()`
**Business Rule:** Only `Active` vehicles can be assigned.

| Test ID | Scenario                             | Input                            | Expected Result                                    | Status |
|---------|--------------------------------------|----------------------------------|----------------------------------------------------|--------|
| UC-02-1 | Active vehicle — allow               | V101 (Active)                    | Proceeds past vehicle status check                 | ✅ PASS |
| UC-02-2 | MaintenanceRequired — block          | V102 (MaintenanceRequired)       | Throws: "Vehicle not available. Status: MaintenanceRequired" | ✅ PASS |
| UC-02-3 | InTransit — block                    | V103 (InTransit)                 | Throws: "Vehicle not available. Status: InTransit" | ✅ PASS |
| UC-02-4 | Maintenance — block                  | Any vehicle in Maintenance       | Throws with status in message                      | ✅ PASS |
| UC-02-5 | OutOfService — block                 | Any vehicle in OutOfService      | Throws with status in message                      | ✅ PASS |

---

## UC-03: Resource Exclusivity (Double-Booking Prevention)

**Component:** `AssignmentService.CreateAssignmentAsync()` + Concurrency Token
**Business Rule:** No driver or vehicle can have more than one active assignment.

| Test ID | Scenario                                   | Input                                              | Expected Result                                    | Status |
|---------|--------------------------------------------|----------------------------------------------------|---------------------------------------------------|--------|
| UC-03-1 | Driver already on active assignment        | D003 (Status=Assigned), attempt second assignment  | Throws: "Driver already on active assignment"      | ✅ PASS |
| UC-03-2 | Vehicle already on active assignment       | V103 (InTransit), attempt second assignment        | Throws: "Vehicle already assigned"                 | ✅ PASS |
| UC-03-3 | Concurrent requests for same driver        | Two parallel requests for D001                     | Only first succeeds; second throws concurrency error | ✅ PASS |
| UC-03-4 | Driver freed after assignment completes    | D001 completes assignment → try new assignment     | Allowed                                            | ✅ PASS |
| UC-03-5 | Driver freed after assignment cancelled    | D001 assignment cancelled → try new assignment     | Allowed                                            | ✅ PASS |

---

## UC-04: License Class Matching (Bonus Rule)

**Component:** `AssignmentService.CreateAssignmentAsync()`
**Business Rule:** Class B driver cannot drive a Class A vehicle.

| Test ID | Scenario                             | Input                                     | Expected Result                         | Status |
|---------|--------------------------------------|-------------------------------------------|-----------------------------------------|--------|
| UC-04-1 | ClassA driver → ClassA vehicle       | D001 (ClassA) + V101 (ClassA)             | Allowed                                 | ✅ PASS |
| UC-04-2 | ClassA driver → ClassB vehicle       | D001 (ClassA) + V104 (ClassB)             | Allowed (Class A covers Class B)        | ✅ PASS |
| UC-04-3 | ClassB driver → ClassA vehicle       | D005 (ClassB) + V101 (ClassA)             | Throws: "Class B cannot drive Class A"  | ✅ PASS |
| UC-04-4 | ClassB driver → ClassB vehicle       | D005 (ClassB) + V104 (ClassB)             | Allowed                                 | ✅ PASS |

---

## UC-05: 10,000 km Maintenance Threshold

**Component:** `VehicleService.UpdateOdometerAsync()`
**Business Rule:** Auto-flag vehicle as MaintenanceRequired when km since service ≥ 10,000.

| Test ID | Scenario                                   | Input                                          | Expected Result                                    | Status |
|---------|--------------------------------------------|------------------------------------------------|----------------------------------------------------|--------|
| UC-05-1 | Update below threshold                     | V101: current=45000, new=49999, lastService=42000 | Status remains Active (diff=7999 km)             | ✅ PASS |
| UC-05-2 | Update crosses exactly 10,000 km           | V101: current=45000, new=52000, lastService=42000 | Status → MaintenanceRequired (diff=10000 km)     | ✅ PASS |
| UC-05-3 | Update crosses above threshold             | V101: current=45000, new=55000, lastService=42000 | Status → MaintenanceRequired + SignalR alert      | ✅ PASS |
| UC-05-4 | Odometer cannot go backwards               | V101: current=45000, new=44000               | Throws: "Odometer cannot be decreased"             | ✅ PASS |
| UC-05-5 | Vehicle already in MaintenanceRequired     | V102: already flagged, update odometer         | Status stays MaintenanceRequired (no double-trigger) | ✅ PASS |
| UC-05-6 | SignalR alert broadcast on threshold cross | Trigger threshold                              | `MaintenanceAlert` event sent to all clients       | ✅ PASS |

---

## UC-06: Maintenance Status Restoration

**Component:** `VehicleService.LogMaintenanceAsync()`
**Business Rule:** Vehicle returns to Active only via maintenance log creation.

| Test ID | Scenario                                   | Input                                              | Expected Result                                    | Status |
|---------|--------------------------------------------|----------------------------------------------------|---------------------------------------------------|--------|
| UC-06-1 | Log maintenance on MaintenanceRequired     | V102 + OdometerAtService=88000                     | V102 Status → Active; LastServiceOdometer=88000    | ✅ PASS |
| UC-06-2 | Log maintenance on Active vehicle          | V101 + log                                         | Status stays Active; log created                   | ✅ PASS |
| UC-06-3 | LastServiceOdometer updated correctly      | V102 + OdometerAtService=88000                     | V102.LastServiceOdometer = 88000                   | ✅ PASS |
| UC-06-4 | Manual status change without log           | PATCH /vehicles/V102/status → Active (no log)      | Allowed via admin; but log is required for proper flow | ✅ PASS |

---

## UC-07: JWT Authentication

**Component:** `AuthService.LoginAsync()`

| Test ID | Scenario                    | Input                              | Expected Result                  | Status |
|---------|-----------------------------|------------------------------------|----------------------------------|--------|
| UC-07-1 | Valid credentials           | admin / Admin@123                  | Returns JWT token + user info    | ✅ PASS |
| UC-07-2 | Wrong password              | admin / wrongpass                  | Throws UnauthorizedAccessException | ✅ PASS |
| UC-07-3 | Non-existent user           | unknown / pass                     | Throws UnauthorizedAccessException | ✅ PASS |
| UC-07-4 | Deactivated user            | User with IsActive=false            | Throws UnauthorizedAccessException | ✅ PASS |
| UC-07-5 | Token contains correct role | Login as dispatcher1               | JWT claim "role" = "Dispatcher"   | ✅ PASS |

---

## UC-08: Role-Based Access Control

**Component:** Controller `[Authorize(Roles = "...")]` attributes

| Test ID | Scenario                                   | User Role    | Endpoint                   | Expected Result    | Status |
|---------|--------------------------------------------|--------------|----------------------------|--------------------|--------|
| UC-08-1 | Dispatcher creates assignment              | Dispatcher   | POST /api/assignments      | 201 Created        | ✅ PASS |
| UC-08-2 | Technician tries to create assignment      | Technician   | POST /api/assignments      | 403 Forbidden      | ✅ PASS |
| UC-08-3 | Technician updates odometer                | Technician   | PATCH /vehicles/{vin}/odometer | 200 OK         | ✅ PASS |
| UC-08-4 | Dispatcher tries to delete driver          | Dispatcher   | DELETE /api/drivers/{id}   | 403 Forbidden      | ✅ PASS |
| UC-08-5 | Unauthenticated request                    | None (no JWT)| GET /api/dashboard/stats   | 401 Unauthorized   | ✅ PASS |

---

# PART 2: FUNCTIONAL TEST CASES (End-to-End)

## FC-01: Full Assignment Lifecycle

**User Journey:** Dispatcher creates, activates, and completes an assignment.

| Step | Action                                            | Expected Result                                        |
|------|---------------------------------------------------|--------------------------------------------------------|
| 1    | Login as dispatcher1                              | Dashboard loads, JWT stored                            |
| 2    | Navigate to Assignments → New Assignment           | Modal opens with driver/vehicle dropdowns              |
| 3    | Select D001 (Rajesh Kumar, ClassA, Valid)          | No warning shown                                       |
| 4    | Select V101 (BharatBenz, Active, ClassA)           | No warning shown                                       |
| 5    | Fill route, origin, destination, scheduled start   | Form accepts input                                     |
| 6    | Click "Create Assignment"                          | Success toast; assignment appears in list as "Pending" |
| 7    | Click "Activate" on the new assignment             | Status changes to "Active"; driver shows "Assigned"    |
| 8    | Click "Complete" on the active assignment          | Status → "Completed"; driver → "Available", vehicle → "Active" |

---

## FC-02: Expired License Block

**User Journey:** Dispatcher attempts to assign expired driver.

| Step | Action                                            | Expected Result                                        |
|------|---------------------------------------------------|--------------------------------------------------------|
| 1    | Login as dispatcher1                              | Dashboard loads                                        |
| 2    | New Assignment → Select D002 (Amit Sharma)        | License shows "EXPIRED" warning in dropdown            |
| 3    | Select any Active vehicle                         | -                                                      |
| 4    | Submit form                                       | Error message: "Driver 'Amit Sharma' has an expired license. Assignment blocked." |
| 5    | Assignment is NOT created                         | No record in database; driver still Available          |

---

## FC-03: Vehicle Status Lock

**User Journey:** Dispatcher attempts to assign a non-active vehicle.

| Step | Action                                              | Expected Result                                           |
|------|-----------------------------------------------------|-----------------------------------------------------------|
| 1    | Login as dispatcher1                                | -                                                         |
| 2    | New Assignment → Select valid driver                | -                                                         |
| 3    | Note: V102 does NOT appear in vehicle dropdown      | Only Active vehicles shown                                |
| 4    | Verify V102 status on Vehicles page                 | Shows "Needs Service" badge                               |

---

## FC-04: Real-Time Maintenance Alert

**User Journey:** Technician updates odometer past threshold; Dispatcher sees live alert.

| Step | Action                                              | Expected Result                                           |
|------|-----------------------------------------------------|-----------------------------------------------------------|
| 1    | Open two browser tabs: Tab A (Dispatcher), Tab B (Technician) | Both connected to SignalR                        |
| 2    | Tab B: Login as tech1                               | -                                                         |
| 3    | Tab B: Navigate to Vehicles → V101 → Odometer       | -                                                         |
| 4    | Tab B: Set odometer to 53,000 (42,000 last service + 11,000 = over threshold) | -                              |
| 5    | Tab A: Within 1 second                              | Toast notification: "V101 BharatBenz needs service!" appears |
| 6    | Tab A: Alert strip at top shows "1 vehicle requires maintenance" | -                                           |
| 7    | Tab B: Vehicle status on Vehicles page → "Needs Service" | Automatic status update confirmed                   |

---

## FC-05: Maintenance Restoration Flow

**User Journey:** Technician logs service on a Maintenance Required vehicle.

| Step | Action                                              | Expected Result                                           |
|------|-----------------------------------------------------|-----------------------------------------------------------|
| 1    | Login as tech1                                      | -                                                         |
| 2    | Navigate to Maintenance → see V102 in alert section | V102 shown as critical                                    |
| 3    | Click "Log Service →" on V102                       | Modal opens pre-filled with V102's VIN                    |
| 4    | Fill description, odometer=88000, cost              | -                                                         |
| 5    | Click "Log Service"                                 | Success: "Maintenance logged! Vehicle restored to Active." |
| 6    | V102 status on Vehicles page                        | Now shows "Active"                                        |
| 7    | V102.LastServiceOdometer                            | Updated to 88,000 km                                      |
| 8    | Dispatcher can now assign V102                      | V102 appears in active vehicles dropdown                  |

---

## FC-06: Double-Booking Prevention

**User Journey:** Two dispatchers try to book the same driver simultaneously.

| Step | Action                                              | Expected Result                                           |
|------|-----------------------------------------------------|-----------------------------------------------------------|
| 1    | Dispatcher 1 creates assignment for D001 + V101    | Success — assignment created                              |
| 2    | Dispatcher 2 (logged in as dispatcher2) tries D001 | Error: "Driver 'Rajesh Kumar' is already on an active assignment. Resource Already Assigned." |
| 3    | Dispatcher 2 tries V101                            | Error: "Vehicle already assigned. Resource Already Assigned." |

---

# PART 3: SECURITY TESTING PLAN

## ST-01: Authentication
- All `/api/*` endpoints (except `/api/auth/login`) return **401** without Bearer token.
- Expired JWT tokens are rejected.
- Tokens signed with wrong key are rejected.

## ST-02: Authorization (RBAC)
- Role claims in JWT are validated server-side on every request.
- Client-side UI hiding is cosmetic only — all enforcement is API-level.

## ST-03: SQL Injection
- EF Core uses parameterized queries exclusively — no raw SQL interpolation.
- Test: Submit `' OR 1=1 --` as username → Returns "Invalid credentials", no DB error.

## ST-04: XSS Prevention
- API returns `Content-Type: application/json` — no HTML rendering.
- Vue.js uses `{{ }}` template syntax which auto-escapes HTML entities.

## ST-05: Input Validation
- All request DTOs are validated (required fields, numeric ranges).
- Odometer cannot decrease — validated in service layer.

---

# PART 4: LOAD TESTING PLAN

## Objective
Validate system handles 500+ concurrent dispatcher sessions and status updates.

## Tool: k6 (open-source load testing)

## Test Scenario 1: Concurrent Assignment Reads
- **Users:** 200 virtual users
- **Action:** GET /api/assignments every 2 seconds
- **Target:** P95 response time < 300ms; 0 errors

## Test Scenario 2: Concurrent Odometer Updates
- **Users:** 50 virtual users updating different vehicles simultaneously
- **Action:** PATCH /api/vehicles/{vin}/odometer
- **Target:** All succeed; no data corruption; P95 < 500ms

## Test Scenario 3: Double-Booking Race Condition
- **Users:** 10 virtual users simultaneously assigning same driver
- **Action:** POST /api/assignments with same driverId
- **Target:** Exactly 1 succeeds; 9 receive "Resource Already Assigned" error

## Test Scenario 4: SignalR Broadcast Scale
- **Users:** 500 connected SignalR clients
- **Action:** 1 odometer update triggers MaintenanceAlert to all
- **Target:** All 500 clients receive alert within 2 seconds

## Sample k6 Script (Scenario 1)
```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = { vus: 200, duration: '60s' };

export default function () {
  const res = http.get('http://localhost:5000/api/assignments', {
    headers: { Authorization: `Bearer ${__ENV.TOKEN}` }
  });
  check(res, { 'status 200': (r) => r.status === 200 });
  sleep(2);
}
```

---

# PART 5: AI PILOT LOG (Disclosure)

## Components Generated with AI Assistance
1. **Repository Pattern boilerplate** — AI generated the standard CRUD repository structure; manually reviewed and adapted for SwiftRoute-specific methods like `IsDriverActivelyAssignedAsync`.
2. **SignalR hub setup** — AI suggested the `OnMessageReceived` JWT workaround for SignalR; verified against official ASP.NET Core docs.
3. **Vue component scaffolding** — AI generated initial template structure; all business logic, validation display logic, and styling were manually designed.
4. **EF Core fluent configuration** — AI suggested `IsRowVersion()` for concurrency tokens; confirmed against EF Core docs and tested.

## Instance Where AI Was Incorrect (Hallucination Example)
**Issue:** When asked to generate the SignalR client setup in Vue.js, the AI suggested:
```javascript
import * as signalR from 'signalr' // INCORRECT package name
```
The correct npm package is `@microsoft/signalr`. The incorrect package doesn't exist on npm and throws a module-not-found error at runtime.

**How Debugged:** Ran `npm install signalr` → "not found". Cross-referenced with Microsoft's official SignalR documentation at `learn.microsoft.com`, which clearly specifies `@microsoft/signalr` as the correct package. Updated all imports accordingly.

**Lesson:** Always verify package names against official documentation or npm registry — AI training data can contain outdated or incorrect package names.
