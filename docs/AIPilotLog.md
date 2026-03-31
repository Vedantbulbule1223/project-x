# AI Pilot Log — Disclosure Document
## SwiftRoute Pulse Command Center

---

## Overview

This document discloses the use of AI assistance in building the SwiftRoute Pulse Command Center,
as required by the project specification. All AI-generated code was reviewed, validated, and
adapted by the development team before inclusion.

---

## Components Generated with AI Assistance

| Component | File(s) | AI Role | Human Review |
|-----------|---------|---------|-------------|
| Repository pattern boilerplate | `Repositories/Repositories.cs` | Generated initial CRUD structure | Adapted `IsDriverActivelyAssignedAsync` and concurrency logic manually |
| SignalR Hub setup | `Hubs/PulseHub.cs`, `Program.cs` | Suggested hub registration pattern | Verified `OnMessageReceived` JWT workaround against official docs |
| EF Core fluent configuration | `Data/SwiftRouteDbContext.cs` | Suggested `.IsRowVersion()` for concurrency tokens | Confirmed behavior, added all index strategies manually |
| Vue component scaffolding | All `.vue` files | Generated initial `<template>` and `<script setup>` structure | All business validation logic, styling, SignalR event wiring written manually |
| JWT auth service | `Services/AuthService.cs` | Suggested `JwtSecurityTokenHandler` pattern | Reviewed token claims, expiry, and SignalR integration manually |
| CSS design system | `style.css` | Suggested CSS variable approach | All color palette, typography choices, component styles designed manually |
| Unit test structure | `Tests/BusinessRuleTests.cs` | Suggested Moq + FluentAssertions pattern | All test scenarios derived from spec requirements manually |

---

## Specific Instance Where AI Was Incorrect

### Hallucination: Wrong npm Package Name

**Prompt given to AI:**
> "Set up SignalR client in Vue 3 to connect to an ASP.NET Core SignalR hub"

**AI-Generated Code (INCORRECT):**
```javascript
import * as signalR from 'signalr'
// Then: npm install signalr
```

**Problem:**
The package `signalr` on npm is a legacy jQuery-based package (last updated 2017) that does NOT
work with modern ASP.NET Core SignalR hubs. When we ran `npm install signalr` and attempted
to connect, we received:

```
TypeError: signalR.HubConnectionBuilder is not a constructor
```

**How We Debugged It:**
1. Checked the npm page for `signalr` — noticed it was 7 years old and tagged as deprecated.
2. Searched Microsoft's official ASP.NET Core SignalR documentation at:
   `https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client`
3. Found the correct package is `@microsoft/signalr` (scoped package under the Microsoft org).
4. Updated all imports:

**Corrected Code:**
```javascript
import * as signalR from '@microsoft/signalr'
// npm install @microsoft/signalr
```

After the fix, the `HubConnectionBuilder` was available and the connection to `/hubs/pulse` worked correctly.

**Lesson Learned:**
AI training data can contain outdated package names, especially for packages that migrated
to scoped npm namespaces. Always verify package names against:
- The official framework documentation
- The npm registry (`npmjs.com`)
- The GitHub repository of the framework

---

## Second Instance: Migration File Generation

**Prompt given to AI:**
> "Generate the EF Core migration file for the SwiftRoute database"

**AI-Generated Issue:**
The AI generated the migration with `MySqlValueGenerationStrategy.IdentityColumn` but forgot
to include the `using Microsoft.EntityFrameworkCore.Metadata;` namespace import, causing:

```
CS0246: The type or namespace name 'MySqlValueGenerationStrategy' could not be found
```

**Fix Applied:**
Added the missing `using` directive at the top of the migration file. This is a minor but
instructive example of AI generating logically correct code that fails at compile time
due to missing infrastructure (imports/usings).

---

## AI Usage Policy Followed

- AI was used as a **starting point**, not a final implementation.
- Every business rule (`AssignmentService`, `VehicleService`) was **written and verified manually**.
- All security decisions (JWT configuration, CORS, RBAC) were **reviewed against official docs**.
- Test cases were derived from the **specification document**, not AI suggestions.
- The **design system** (colors, typography, layout) was an original creative decision.
