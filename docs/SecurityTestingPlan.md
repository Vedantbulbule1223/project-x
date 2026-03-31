# Security Testing Plan
## SwiftRoute Pulse Command Center

---

## 1. Authentication Security (JWT)

### ST-01: Token Validation
| Test | Method | Expected |
|------|--------|----------|
| No token provided | GET /api/drivers (no header) | 401 Unauthorized |
| Expired token | Token with exp in past | 401 Unauthorized |
| Tampered signature | Modify token payload, keep signature | 401 Unauthorized |
| Wrong signing key | Token signed with different key | 401 Unauthorized |
| Valid token | Correct token | 200 OK |

### ST-02: Token Expiry
- Tokens expire after **8 hours** (configured in `Program.cs`)
- `ClockSkew = TimeSpan.Zero` — no grace period
- Frontend: on 401, localStorage is cleared and user redirected to /login

---

## 2. Role-Based Access Control (RBAC)

| Endpoint | Admin | Manager | Dispatcher | Technician |
|----------|-------|---------|------------|------------|
| GET /api/drivers | ✅ | ✅ | ✅ | ✅ |
| POST /api/drivers | ✅ | ✅ | ✅ | ❌ |
| DELETE /api/drivers | ✅ | ❌ | ❌ | ❌ |
| POST /api/assignments | ✅ | ✅ | ✅ | ❌ |
| PATCH /vehicles/odometer | ✅ | ✅ | ❌ | ✅ |
| POST /vehicles/maintenance | ✅ | ✅ | ❌ | ✅ |

**Testing Method:**
- Login as each role, attempt each endpoint
- Verify 403 Forbidden for unauthorized role attempts
- Confirm server-side enforcement (not just UI hiding)

---

## 3. SQL Injection Prevention

**Approach:** Entity Framework Core uses **parameterized queries** for all database operations.

```csharp
// EF Core translates this LINQ to parameterized SQL:
await _ctx.Users.FirstOrDefaultAsync(u => u.Username == req.Username)
// SQL: SELECT * FROM Users WHERE Username = @p0  ← parameterized, safe
```

**Test Cases:**
| Input | Where | Expected |
|-------|-------|----------|
| `' OR 1=1 --` | Username field | Returns "Invalid credentials", no DB error |
| `"; DROP TABLE Drivers; --` | Any string input | Treated as literal string, no execution |
| `<script>alert(1)</script>` | Name fields | Stored as escaped text, not executed |

---

## 4. XSS (Cross-Site Scripting) Prevention

**Server side:** API returns `Content-Type: application/json` — no HTML rendering.

**Client side:** Vue.js `{{ variable }}` syntax auto-escapes HTML entities:
```vue
<!-- Input: <script>alert(1)</script> -->
{{ driver.name }}
<!-- Rendered: &lt;script&gt;alert(1)&lt;/script&gt; ← safe -->
```

**v-html is NOT used** anywhere in the frontend codebase, eliminating XSS risk.

---

## 5. Password Security

- Passwords hashed using **BCrypt** with work factor 12 (default)
- `BCrypt.Net.BCrypt.HashPassword(password)` — salted automatically
- Raw passwords are **never stored or logged**
- Verification: `BCrypt.Net.BCrypt.Verify(input, hash)`

---

## 6. CORS Policy

Only the frontend origin is whitelisted:
```csharp
.WithOrigins("http://localhost:5173", "http://localhost:3000")
.AllowAnyMethod()
.AllowAnyHeader()
.AllowCredentials()
```

In production, replace with the actual deployed frontend domain.

---

## 7. Concurrency / Race Condition Security

**Problem addressed:** Ghost assignment (two dispatchers booking same driver simultaneously).

**Solution:** EF Core Row Versioning
```csharp
// If two dispatchers save simultaneously:
// Dispatcher 1: WHERE RowVersion = 0x001 → succeeds → RowVersion = 0x002
// Dispatcher 2: WHERE RowVersion = 0x001 → 0 rows affected → DbUpdateConcurrencyException
catch (DbUpdateConcurrencyException)
{
    throw new InvalidOperationException("Resource Already Assigned");
}
```

---

## 8. Production Hardening Checklist

| Item | Status | Notes |
|------|--------|-------|
| HTTPS enforced | ⬜ Pending | Add `app.UseHttpsRedirection()` in prod |
| JWT key in secrets | ⬜ Pending | Move to Azure Key Vault / env vars |
| DB connection encrypted | ⬜ Pending | Add `SslMode=Required` to connection string |
| Rate limiting | ⬜ Pending | Add `AddRateLimiter` middleware |
| Helmet headers | ⬜ Pending | Add security headers middleware |
| Audit logging | ⬜ Pending | Log all write operations with userId |
| Input validation | ✅ Done | Model validation via DTOs |
| Parameterized queries | ✅ Done | EF Core throughout |
| Password hashing | ✅ Done | BCrypt with salt |
| RBAC enforcement | ✅ Done | Server-side attribute-based |
