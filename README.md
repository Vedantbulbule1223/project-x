# SwiftRoute Pulse Command Center

A full-stack fleet management system built with **.NET 8 (C#)** backend and **Vue.js 3** frontend,
featuring real-time SignalR updates, JWT authentication, and strict business rule enforcement.

---

## Tech Stack

| Layer      | Technology                                      |
|------------|-------------------------------------------------|
| Backend    | ASP.NET Core 8, C#, Entity Framework Core 8     |
| Database   | MySQL 8 via Pomelo EF Core provider             |
| Real-time  | ASP.NET Core SignalR                            |
| Auth       | JWT Bearer Tokens + Role-Based Access Control   |
| Frontend   | Vue 3 (Composition API), Pinia, Vue Router 4    |
| HTTP       | Axios                                           |
| Real-time  | @microsoft/signalr client                       |

---

## Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- MySQL 8 running locally

### 1. Backend Setup

```bash
cd backend/SwiftRoute.API

# Update connection string in appsettings.json
# "Server=localhost;Port=3306;Database=SwiftRouteDb;User=root;Password=YourPassword;"

# Install EF tools (if not installed)
dotnet tool install --global dotnet-ef

# Run migrations and seed data
dotnet ef migrations add InitialCreate
dotnet ef database update

# Start API server
dotnet run
# API runs at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### 2. Frontend Setup

```bash
cd frontend

npm install
npm run dev
# Frontend runs at http://localhost:5173
```

---

## Default Login Credentials

| Role        | Username     | Password      |
|-------------|--------------|---------------|
| Admin       | admin        | Admin@123     |
| Dispatcher  | dispatcher1  | Dispatch@123  |
| Dispatcher  | dispatcher2  | Dispatch@123  |
| Manager     | manager1     | Manager@123   |
| Technician  | tech1        | Tech@123      |

---

## Business Rules Enforced

### Assignment Rules
1. **License Validity** – Hard blocks assignment if driver license is expired
2. **Vehicle Status Lock** – Only `Active` vehicles can be assigned
3. **Resource Exclusivity** – No driver/vehicle can be double-booked
4. **Class Matching** – Class B drivers cannot drive Class A vehicles

### Maintenance Rules
5. **10k Threshold** – Auto-flags vehicle as `MaintenanceRequired` when `CurrentOdometer - LastServiceOdometer >= 10,000 km`
6. **Status Restoration** – Vehicle only returns to `Active` after a Maintenance Log entry is created

### Concurrency
7. **Row Versioning** – EF Core `RowVersion` concurrency tokens prevent double-booking race conditions

---

## API Endpoints

| Method | Endpoint                        | Description               | Roles              |
|--------|---------------------------------|---------------------------|--------------------|
| POST   | /api/auth/login                 | Login → JWT token         | Public             |
| GET    | /api/dashboard/stats            | Live dashboard stats      | All                |
| GET    | /api/dashboard/alerts           | Maintenance alerts        | All                |
| GET    | /api/drivers                    | List all drivers          | All                |
| POST   | /api/drivers                    | Add driver                | Admin/Manager/Disp |
| PUT    | /api/drivers/{id}               | Update driver             | Admin/Manager      |
| DELETE | /api/drivers/{id}               | Delete driver             | Admin              |
| GET    | /api/vehicles                   | List all vehicles         | All                |
| POST   | /api/vehicles                   | Add vehicle               | Admin/Manager      |
| PATCH  | /api/vehicles/{vin}/odometer    | Update odometer           | Admin/Manager/Tech |
| PATCH  | /api/vehicles/{vin}/status      | Change vehicle status     | Admin/Manager      |
| GET    | /api/vehicles/{vin}/logs        | Get maintenance logs      | All                |
| POST   | /api/vehicles/maintenance       | Log maintenance service   | Admin/Manager/Tech |
| GET    | /api/assignments                | List all assignments      | All                |
| POST   | /api/assignments                | Create assignment         | Admin/Manager/Disp |
| PATCH  | /api/assignments/{id}/status    | Update assignment status  | Admin/Manager/Disp |

---

## SignalR Events (Real-time)

| Event               | Trigger                                      | Payload       |
|---------------------|----------------------------------------------|---------------|
| `AssignmentCreated` | New assignment created                       | Assignment ID |
| `AssignmentUpdated` | Assignment status changed                    | Assignment ID |
| `VehicleUpdated`    | Vehicle status/odometer changed              | VIN           |
| `MaintenanceAlert`  | Vehicle crosses 10k km service threshold     | Alert object  |

---

## Project Structure

```
swiftroute/
├── backend/
│   └── SwiftRoute.API/
│       ├── Controllers/    # API endpoints
│       ├── Data/           # DbContext + Seeder
│       ├── DTOs/           # Request/Response models
│       ├── Hubs/           # SignalR PulseHub
│       ├── Models/         # Domain entities
│       ├── Repositories/   # Repository + Unit of Work
│       ├── Services/       # Business logic layer
│       └── Program.cs      # App configuration
└── frontend/
    └── src/
        ├── views/          # Page components
        ├── stores/         # Pinia state stores
        ├── services/       # API + SignalR services
        ├── router/         # Vue Router
        └── style.css       # Global design system
```
