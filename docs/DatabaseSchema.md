# Database Schema Document
## SwiftRoute Pulse — MySQL 8

---

## Table: Drivers

| Column         | Type           | Constraints                        | Description                     |
|----------------|----------------|------------------------------------|---------------------------------|
| Id             | VARCHAR(36)    | PK                                 | Driver ID (e.g., D001)          |
| Name           | VARCHAR(100)   | NOT NULL                           | Full name                       |
| LicenseClass   | VARCHAR(20)    | NOT NULL                           | 'ClassA' or 'ClassB'            |
| LicenseExpiry  | DATETIME       | NOT NULL                           | License expiry date             |
| Status         | VARCHAR(20)    | NOT NULL, DEFAULT 'Available'      | Available/Assigned/OffDuty/Susp |
| Phone          | VARCHAR(20)    | NULLABLE                           | Contact number                  |
| Email          | VARCHAR(100)   | NULLABLE                           | Email address                   |
| CreatedAt      | DATETIME       | NOT NULL, DEFAULT UTC_NOW          | Record creation timestamp       |
| UpdatedAt      | DATETIME       | NOT NULL                           | Last modification timestamp     |
| RowVersion     | TIMESTAMP(6)   | NOT NULL, auto-updated             | Optimistic concurrency token    |

**Indexes:**
- `IX_Drivers_Status` on `Status` — speeds up filtering by availability

---

## Table: Vehicles

| Column               | Type           | Constraints                   | Description                        |
|----------------------|----------------|-------------------------------|------------------------------------|
| VIN                  | VARCHAR(50)    | PK                            | Vehicle Identification Number      |
| Model                | VARCHAR(100)   | NOT NULL                      | e.g., BharatBenz 3523R             |
| LicenseClass         | VARCHAR(20)    | NOT NULL                      | 'ClassA' or 'ClassB'               |
| CurrentOdometer      | DOUBLE         | NOT NULL, DEFAULT 0           | Current odometer reading (km)      |
| LastServiceOdometer  | DOUBLE         | NOT NULL, DEFAULT 0           | Odometer at last service (km)      |
| Status               | VARCHAR(30)    | NOT NULL, DEFAULT 'Active'    | Active/Maintenance/OutOfService etc|
| Notes                | VARCHAR(500)   | NULLABLE                      | Free-text notes                    |
| CreatedAt            | DATETIME       | NOT NULL                      | Record creation timestamp          |
| UpdatedAt            | DATETIME       | NOT NULL                      | Last modification timestamp        |
| RowVersion           | TIMESTAMP(6)   | NOT NULL, auto-updated        | Optimistic concurrency token       |

**Indexes:**
- `IX_Vehicles_Status` on `Status` — speeds up availability filters
- **Computed rule:** `KmSinceService = CurrentOdometer - LastServiceOdometer >= 10000` → auto-set Status = 'MaintenanceRequired'

---

## Table: Users

| Column       | Type         | Constraints              | Description                        |
|--------------|--------------|--------------------------|------------------------------------|
| Id           | VARCHAR(36)  | PK                       | GUID user ID                       |
| Username     | VARCHAR(50)  | NOT NULL, UNIQUE         | Login username                     |
| PasswordHash | VARCHAR(255) | NOT NULL                 | BCrypt-hashed password             |
| FullName     | VARCHAR(100) | NOT NULL                 | Display name                       |
| Email        | VARCHAR(100) | NOT NULL                 | Email address                      |
| Role         | VARCHAR(20)  | NOT NULL                 | Admin/Manager/Dispatcher/Technician|
| IsActive     | TINYINT(1)   | NOT NULL, DEFAULT 1      | Soft-delete flag                   |
| CreatedAt    | DATETIME     | NOT NULL                 | Record creation timestamp          |

**Indexes:**
- `IX_Users_Username` UNIQUE on `Username`

---

## Table: Assignments

| Column           | Type         | Constraints                          | Description                         |
|------------------|--------------|--------------------------------------|-------------------------------------|
| Id               | INT          | PK, AUTO_INCREMENT                   | Assignment ID                       |
| DriverId         | VARCHAR(36)  | FK → Drivers.Id, NOT NULL            | Assigned driver                     |
| VehicleVIN       | VARCHAR(50)  | FK → Vehicles.VIN, NOT NULL          | Assigned vehicle                    |
| Route            | VARCHAR(200) | NOT NULL                             | Route name                          |
| Origin           | VARCHAR(100) | NOT NULL                             | Origin location                     |
| Destination      | VARCHAR(100) | NOT NULL                             | Destination location                |
| ScheduledStart   | DATETIME     | NOT NULL                             | Planned start time                  |
| ScheduledEnd     | DATETIME     | NULLABLE                             | Planned end time                    |
| ActualStart      | DATETIME     | NULLABLE                             | Actual departure time               |
| ActualEnd        | DATETIME     | NULLABLE                             | Actual completion time              |
| Status           | VARCHAR(20)  | NOT NULL, DEFAULT 'Pending'          | Pending/Active/Completed/Cancelled  |
| CreatedByUserId  | VARCHAR(36)  | FK → Users.Id, NOT NULL              | Dispatcher who created it           |
| CreatedAt        | DATETIME     | NOT NULL                             | Creation timestamp                  |
| UpdatedAt        | DATETIME     | NOT NULL                             | Last update timestamp               |
| RowVersion       | TIMESTAMP(6) | NOT NULL, auto-updated               | Optimistic concurrency token        |

**Indexes:**
- `IX_Assignments_DriverId_Status` — for checking active driver assignments (double-booking check)
- `IX_Assignments_VehicleVIN_Status` — for checking active vehicle assignments (double-booking check)

---

## Table: MaintenanceLogs

| Column             | Type           | Constraints                    | Description                       |
|--------------------|----------------|--------------------------------|-----------------------------------|
| Id                 | INT            | PK, AUTO_INCREMENT             | Log ID                            |
| VehicleVIN         | VARCHAR(50)    | FK → Vehicles.VIN, CASCADE     | Serviced vehicle                  |
| Description        | VARCHAR(500)   | NOT NULL                       | Work performed                    |
| OdometerAtService  | DOUBLE         | NOT NULL                       | Odometer when serviced            |
| PerformedByUserId  | VARCHAR(36)    | FK → Users.Id, RESTRICT        | Technician who performed service  |
| ServiceDate        | DATETIME       | NOT NULL, DEFAULT UTC_NOW      | Date of service                   |
| Cost               | DECIMAL(10,2)  | NOT NULL, DEFAULT 0            | Service cost in INR               |
| Parts              | VARCHAR(300)   | NULLABLE                       | Parts replaced                    |
| CreatedAt          | DATETIME       | NOT NULL                       | Record creation timestamp         |

---

## Entity Relationships

```
Users ──────────────────────────────────────┐
  │                                         │
  │ (CreatedBy) 1──* Assignments *──1 Drivers
  │                     │
  │ (PerformedBy) 1──*  │
  │                     │
  └── MaintenanceLogs *──1 Vehicles
```

**Relationship Summary:**
- One Driver → Many Assignments (a driver's history)
- One Vehicle → Many Assignments (a vehicle's trip history)
- One Vehicle → Many MaintenanceLogs (a vehicle's service history)
- One User → Many Assignments (dispatcher audit trail)
- One User → Many MaintenanceLogs (technician audit trail)

---

## Indexing Strategy

| Index Name                         | Table        | Columns               | Reason                                      |
|------------------------------------|--------------|-----------------------|---------------------------------------------|
| IX_Drivers_Status                  | Drivers      | Status                | Fast filter for available drivers           |
| IX_Vehicles_Status                 | Vehicles     | Status                | Fast filter for active vehicles             |
| IX_Assignments_DriverId_Status     | Assignments  | DriverId, Status      | Double-booking prevention query             |
| IX_Assignments_VehicleVIN_Status   | Assignments  | VehicleVIN, Status    | Double-booking prevention query             |
| IX_Users_Username (UNIQUE)         | Users        | Username              | Fast login lookup, uniqueness enforcement   |
