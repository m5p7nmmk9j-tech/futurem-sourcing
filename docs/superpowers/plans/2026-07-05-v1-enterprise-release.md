# FUTUREM Enterprise V1.0 Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a reproducible, tested, Docker-deployable FUTUREM Enterprise v1.0.0 release and publish it to GitHub in small verified commits.

**Architecture:** Preserve the existing .NET 9 API, Vue 3 web application, MySQL 8, Redis 7, and Docker Compose topology. Repair contract drift at existing controller/entity boundaries, add release-focused automated checks around the current HTTP API, and make CI the clean-environment release gate.

**Tech Stack:** .NET 9 / ASP.NET Core / EF Core / Pomelo MySQL, Vue 3 / TypeScript / Vite, MySQL 8, Redis 7, Docker Compose, GitHub Actions.

---

### Task 1: Repair API compilation

**Files:**
- Modify: `api/Futurem.Sourcing.Api/Controllers/SummaryOrdersController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/BusinessDashboardController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/ExcelCenterController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/GlobalSearchController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/PrintCenterController.cs`

- [ ] **Step 1: Reproduce the compiler failures**

Run:

```bash
dotnet build api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj -c Release --no-restore
```

Expected: failure with the recorded nullable and missing-property errors.

- [ ] **Step 2: Align controllers to the canonical entity contracts**

Use existing canonical names and nullable semantics:

```csharp
var customerId = request.CustomerId
    ?? pos.FirstOrDefault(x => x.CustomerId.HasValue)?.CustomerId;
if (!customerId.HasValue) return BadRequest("CustomerId required");
// SummaryOrder.CustomerId = customerId.Value

var date = x.RecordDate ?? x.CreatedAt;
// date remains nullable because BaseEntity.CreatedAt is nullable

// Contact -> ContactName
// BlNo -> BillOfLadingNo
// TargetId?.ToString() -> TargetId.ToString()
// PaymentDate.ToString(format) -> PaymentDate?.ToString(format) ?? ""
```

For PO totals and product carton quantity, derive export/print values from the existing `DocumentLines` model rather than adding duplicate persisted entity fields.

- [ ] **Step 3: Rebuild until compilation is clean**

Run the Task 1 build command. Expected: zero errors; investigate and eliminate actionable warnings in touched code.

- [ ] **Step 4: Commit the API compile repair**

```bash
git add api/Futurem.Sourcing.Api/Controllers
git commit -m "fix(api): align controllers with entity contracts"
git push origin main
```

### Task 2: Make Web builds deterministic

**Files:**
- Create: `web/package-lock.json`
- Modify: `.github/workflows/ci.yml`
- Modify: `scripts/check.sh`
- Modify: `scripts/check.bat`

- [ ] **Step 1: Generate and validate the npm lockfile**

Run:

```bash
cd web
npm install
npm ci
npm run build
```

Expected: TypeScript and Vite build succeed from the lockfile.

- [ ] **Step 2: Switch clean checks from `npm install` to `npm ci`**

Use `npm ci` in CI and local check scripts so the exact dependency graph is tested.

- [ ] **Step 3: Re-run API and Web clean builds**

Expected: both build successfully from declared inputs.

- [ ] **Step 4: Commit deterministic Web builds**

```bash
git add web/package-lock.json .github/workflows/ci.yml scripts/check.sh scripts/check.bat
git commit -m "build(web): lock dependencies for reproducible builds"
git push origin main
```

### Task 3: Validate MySQL schema and migrations

**Files:**
- Modify as evidence requires: `database/init.sql`
- Modify as evidence requires: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify as evidence requires: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `scripts/verify-mysql.sh`

- [ ] **Step 1: Start a clean MySQL container and apply `database/init.sql`**

Run the script against an empty named volume. Expected: schema creation exits zero.

- [ ] **Step 2: Verify idempotency**

Apply `database/init.sql` a second time. Expected: exit zero without destructive data changes.

- [ ] **Step 3: Start the API against the initialized database**

Expected: startup database checks complete and `/api/database/status` reports a usable schema.

- [ ] **Step 4: Commit only evidence-driven schema or verification changes**

```bash
git add database api/Futurem.Sourcing.Api/Data api/Futurem.Sourcing.Api/Services scripts/verify-mysql.sh
git commit -m "test(db): verify clean and repeatable mysql initialization"
git push origin main
```

### Task 4: Verify Docker Compose runtime

**Files:**
- Modify as evidence requires: `docker-compose.yml`
- Modify as evidence requires: `api/Dockerfile`
- Modify as evidence requires: `web/Dockerfile`
- Modify as evidence requires: `web/nginx.conf`
- Create: `scripts/smoke-test.sh`

- [ ] **Step 1: Validate and build Compose**

```bash
docker compose config
docker compose build --pull
docker compose up -d
docker compose ps
```

Expected: MySQL, Redis, API, and Web remain running.

- [ ] **Step 2: Poll readiness and inspect unhealthy logs**

The smoke script must poll HTTP/database readiness with a bounded timeout and print `docker compose logs` on failure.

- [ ] **Step 3: Verify Web-to-API routing and API health endpoints**

Expected: Web returns HTTP 200 and API endpoints respond through the documented ports.

- [ ] **Step 4: Commit runtime fixes and smoke checks**

```bash
git add docker-compose.yml api/Dockerfile web/Dockerfile web/nginx.conf scripts/smoke-test.sh
git commit -m "test(ops): add docker compose release smoke checks"
git push origin main
```

### Task 5: Add full business-flow integration coverage

**Files:**
- Create: `tests/integration/business-flow.sh`
- Modify as evidence requires: the existing controllers and services for RFQ, CO, PO, Receiving, QC, SO, Container, Shipment, Finance, and Payment
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Write the failing API workflow test**

The test must seed/login, create required master data, and exercise:

```text
RFQ -> CO -> PO -> Receiving -> QC -> SO -> Container -> Shipment -> Finance -> Payment
```

At each step, parse the returned identifier and assert the expected status, linkage, and financial balance. Run it before fixes; expected: fail at the first unsupported or broken transition.

- [ ] **Step 2: Repair one failing transition at a time**

For each transition: retain the failing assertion, make the minimal backward-compatible controller/service fix, and rerun from a clean database.

- [ ] **Step 3: Add the Compose integration job to GitHub Actions**

Start the stack, wait for readiness, run `tests/integration/business-flow.sh`, and always upload/print Compose logs on failure.

- [ ] **Step 4: Commit each independently verified workflow repair**

Use small messages such as:

```bash
git commit -m "fix(receiving): preserve po linkage during receipt"
git commit -m "fix(finance): reconcile payment balances"
git commit -m "test(e2e): cover enterprise sourcing workflow"
```

Push each verified commit to `origin/main`.

### Task 6: Complete release documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/03-BusinessFlow.md`
- Modify: `docs/04-Development.md`
- Modify: `docs/05-Changelog.md`
- Create: `docs/06-Deployment.md`
- Create: `docs/07-Release-Checklist.md`

- [ ] **Step 1: Record exact clean-build, deployment, migration, backup, rollback, and smoke-test commands**

- [ ] **Step 2: Document default credentials as development-only and require production overrides**

- [ ] **Step 3: Record the verified workflow and known non-blocking limitations**

- [ ] **Step 4: Commit documentation**

```bash
git add README.md docs
git commit -m "docs: finalize v1 enterprise release guide"
git push origin main
```

### Task 7: Gate and publish v1.0.0

**Files:**
- Modify as evidence requires: `.github/workflows/ci.yml`

- [ ] **Step 1: Run the complete local release gate**

```bash
./scripts/check.sh
./scripts/verify-mysql.sh
./scripts/smoke-test.sh
tests/integration/business-flow.sh
```

Expected: all exit zero from a clean environment.

- [ ] **Step 2: Verify the latest `main` GitHub Actions run**

Expected: API build, Web build, Docker build, MySQL verification, and business-flow integration all pass.

- [ ] **Step 3: Confirm the tree is clean and tag the exact verified commit**

```bash
git status --short
git tag -a v1.0.0 -m "FUTUREM Enterprise v1.0.0"
git push origin v1.0.0
```

- [ ] **Step 4: Create the GitHub Release**

Create a non-draft, non-prerelease `v1.0.0` release with highlights, deployment requirements, upgrade notes, verification evidence, and checksums for any attached artifacts.

---

## Plan self-review

- Covers API, Web, CI, Docker, MySQL, full business workflow, runtime repair, documentation, tagging, and GitHub Release.
- Preserves the existing architecture and public entity/controller contracts wherever possible.
- Uses clean-environment checks and small commits as explicit gates.
- Contains no deferred implementation placeholders; conditional file changes are evidence-driven to avoid speculative refactoring.
