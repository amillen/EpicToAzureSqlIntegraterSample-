# Architecture Documentation

## Overview

The Epic Billing Analytics & Triage application follows a multi-tier architecture designed for healthcare billing data workflows. This document provides detailed technical guidance for understanding, extending, and deploying the solution.

## Architecture Patterns

### Layered Architecture

```
┌─────────────────────────────────────────────────┐
│           Presentation Layer (React)            │
│  - Material-UI Components                       │
│  - MSAL Authentication                          │
│  - TypeScript Type Safety                       │
└─────────────────────────────────────────────────┘
                     ↓ ↑ (HTTPS/JSON)
┌─────────────────────────────────────────────────┐
│        API Layer (.NET 8 Web API)               │
│  - RESTful Endpoints                            │
│  - JWT Bearer Authentication                    │
│  - Authorization Policies                       │
│  - CORS Configuration                           │
└─────────────────────────────────────────────────┘
                     ↓ ↑ (EF Core)
┌─────────────────────────────────────────────────┐
│       Data Access Layer (EF Core)               │
│  - DbContext                                    │
│  - Entity Models                                │
│  - LINQ Queries                                 │
└─────────────────────────────────────────────────┘
                     ↓ ↑ (SQL)
┌─────────────────────────────────────────────────┐
│          Database Layer (Azure SQL)             │
│  - Staging Schema (raw loads)                   │
│  - Core Schema (normalized tables)              │
│  - Stored Procedures (ETL logic)                │
│  - Analytics Views (reporting)                  │
└─────────────────────────────────────────────────┘
                     ↑ (Bulk Insert)
┌─────────────────────────────────────────────────┐
│      Ingestion Layer (.NET Console)             │
│  - CSV Parsing (CsvHelper)                      │
│  - SqlBulkCopy                                  │
│  - Stored Procedure Execution                   │
└─────────────────────────────────────────────────┘
```

## Component Details

### 1. Database Layer (Azure SQL)

#### Schema Design

**Staging Schema**
- Purpose: Holds raw CSV data before processing
- Tables: `EpicClaimCsv`, `EpicChargeCsv`
- Truncated after each ETL run
- No constraints for fast bulk inserts

**Core Schema (dbo)**
- Purpose: Normalized, relational data model
- Key tables:
  - `Patient`: Dimension table with patient demographics
  - `Encounter`: Hospital visit information
  - `Claim`: Billing claim header (fact table)
  - `ChargeLine`: Line-item charges (fact table)
  - `WorkQueueItem`: Triage workflow state
  - `TriageNote`: User annotations
  - `AuditLog`: Compliance tracking

**Indexing Strategy**
- Primary keys: Clustered indexes on surrogate keys
- Foreign keys: Non-clustered indexes
- Search fields: `EpicClaimId`, `MRNHash`, `DenialCode`
- Composite indexes for common query patterns

#### ETL Stored Procedures

**sp_ProcessEpicClaims**
- Upserts patients (MERGE on `EpicPatientId`)
- Upserts encounters (MERGE on `EpicEncounterId`)
- Upserts claims (MERGE on `EpicClaimId`)
- Atomic transaction with rollback on error

**sp_ProcessEpicCharges**
- Deletes existing charge lines for claims in staging
- Inserts new charge lines (full refresh per claim)
- Maintains referential integrity

**sp_RebuildWorkQueue**
- Evaluates business rules per claim:
  - Denials: Status = 'DENIED'/'PENDED' OR has denial codes
  - Underpaid: TotalPaid < TotalAllowed
  - Clean: Default
- Upserts `WorkQueueItem` records
- Sets priority (1=Denials, 2=Underpaid, 3=Clean)

#### Analytics Views

Designed for BI tool consumption (Tableau, Power BI):

- **vw_WorkQueueOverview**: Aggregated queue metrics
- **vw_ClaimFinancials**: Star-schema fact view
- **vw_ChargeLineDetails**: Denormalized charge analysis
- **vw_DenialAnalysis**: Grouped denial statistics
- **vw_PayerPerformance**: Payer-level KPIs

Views use CTEs and window functions for performance.

### 2. API Layer (.NET 8 Web API)

#### Technology Stack
- **Framework**: ASP.NET Core 8.0
- **ORM**: Entity Framework Core 8.0 with SQL Server provider
- **Authentication**: Microsoft.Identity.Web (JWT Bearer)
- **Serialization**: System.Text.Json

#### Controllers

**WorkQueueController** (`/api/workqueue`)
- `GET /api/workqueue`: Paginated list with filters
  - Query params: queue, status, assignedTo, search, from, to, page, pageSize
  - Returns: Items + pagination headers (X-Total-Count, X-Page, X-Page-Size)
- `GET /api/workqueue/{id}`: Full detail with claim, notes
- `POST /api/workqueue/{id}/assign`: Assigns to current user (from JWT)
- `POST /api/workqueue/{id}/status`: Updates status
- `POST /api/workqueue/{id}/notes`: Adds triage note

**ClaimsController** (`/api/claims`)
- `PATCH /api/claims/{claimId}`: Updates editable fields (status, payer)

**AnalyticsController** (`/api/analytics`)
- `GET /api/analytics/workqueue-overview`: Reads from `vw_WorkQueueOverview`
- `GET /api/analytics/claim-financials`: Reads from `vw_ClaimFinancials` with pagination

#### Authentication & Authorization

**Dev Bypass Mode** (`DEV_BYPASS_AUTH=true`)
- Skips JWT validation
- Uses hardcoded user: `dev-analyst@example.com`
- For local development only

**Production Mode** (Azure Entra ID)
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(config.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BillingReader", policy => 
        policy.RequireRole("BillingReader", "BillingAnalyst", "BillingAdmin"));
    options.AddPolicy("BillingAnalyst", policy => 
        policy.RequireRole("BillingAnalyst", "BillingAdmin"));
    options.AddPolicy("BillingAdmin", policy => 
        policy.RequireRole("BillingAdmin"));
});
```

Apply policies via `[Authorize(Policy="BillingAnalyst")]` attributes.

#### Audit Service

Singleton service injected into controllers:

```csharp
public interface IAuditService
{
    Task LogAsync(string entityName, string entityId, string action, 
                  string actorUpn, object? before, object? after);
}
```

Logs:
- Entity name and ID
- Action type (Assign, StatusChange, Update, Create)
- Actor UPN (from JWT or dev mode)
- Before/After JSON snapshots

Persists to `AuditLog` table asynchronously.

#### CORS Configuration

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(config["FrontendUrl"] ?? "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size");
    });
});
```

### 3. Frontend (React + TypeScript)

#### Technology Stack
- **Build Tool**: Vite 5.x
- **Framework**: React 18
- **Language**: TypeScript 5.x
- **UI Library**: Material-UI (MUI) v6
- **Routing**: React Router v6
- **Auth**: MSAL React (for production Entra ID)

#### Page Components

**WorkQueueList** (`/workqueue`)
- Displays paginated table of work queue items
- Filters: Queue, Status, Assigned To, Search
- Client-side routing to detail page on row click
- MUI Table with Pagination component

**WorkQueueDetailPage** (`/workqueue/:id`)
- Three sections: Work Queue Info, Claim Info, Charge Lines, Notes
- Actions: Assign, Update Status, Add Note
- Real-time updates via API calls
- Conditional rendering based on data availability

**AdminPage** (`/admin`)
- Dashboard cards: Total Items, Total At Risk
- Work Queue Overview table from analytics view
- Dev mode indicator

#### API Service Layer

Centralized API client (`services/api.ts`):

```typescript
class ApiService {
  private async fetch(url: string, options?: RequestInit) {
    const response = await fetch(`${API_URL}${url}`, {
      ...options,
      headers: { 'Content-Type': 'application/json', ...options?.headers },
    });
    if (!response.ok) throw new Error(`API error: ${response.status}`);
    return response;
  }

  async getWorkQueue(params?: {...}): Promise<{items, totalCount}> { ... }
  async getWorkQueueItem(id: number): Promise<WorkQueueDetail> { ... }
  // ... other methods
}

export const apiService = new ApiService();
```

Used in components via hooks:

```typescript
const [items, setItems] = useState<WorkQueueListItem[]>([]);
useEffect(() => {
  apiService.getWorkQueue({ page: 1 }).then(result => setItems(result.items));
}, []);
```

#### State Management

Currently uses React hooks (useState, useEffect) for simplicity. For production, consider:
- **Context API**: For global auth state
- **React Query**: For server state caching
- **Zustand/Redux**: For complex client state

### 4. Ingestion Layer (.NET Console App)

#### CSV Processing Flow

```
1. Read CSV files from directory
   ↓
2. Parse with CsvHelper into strongly-typed records
   ↓
3. Convert to DataTable
   ↓
4. SqlBulkCopy to staging tables
   ↓
5. Execute sp_ProcessEpicClaims
   ↓
6. Execute sp_ProcessEpicCharges
   ↓
7. Execute sp_RebuildWorkQueue
```

#### Key Features

- **Strongly-typed CSV models**: `ClaimCsvRecord`, `ChargeCsvRecord`
- **Bulk insert**: Uses `SqlBulkCopy` for performance (~10k rows/sec)
- **Error handling**: Transaction rollback on SP failures
- **Parameterized**: Accepts data directory as command-line argument
- **Logging**: Console output for progress tracking

#### Production Alternative: Azure Data Factory

Replace console app with ADF:

```json
{
  "pipeline": {
    "activities": [
      {
        "name": "CopyClaimsCsv",
        "type": "Copy",
        "source": { "type": "DelimitedTextSource", "storeSettings": {...} },
        "sink": { "type": "AzureSqlSink", "tableOption": "staging.EpicClaimCsv" }
      },
      {
        "name": "ProcessClaims",
        "type": "SqlServerStoredProcedure",
        "storedProcedureName": "dbo.sp_ProcessEpicClaims"
      }
    ]
  }
}
```

Benefits: Scheduling, monitoring, managed identity auth.

## Deployment Architecture

### Azure Deployment Topology

```
┌─────────────────────────────────────────────────────────┐
│                      Azure Subscription                  │
│                                                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Azure Static Web Apps                           │   │
│  │  - React Frontend                                │   │
│  │  - CDN Distribution                              │   │
│  │  - Custom Domain                                 │   │
│  └──────────────────────────────────────────────────┘   │
│                         ↓ HTTPS                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Azure App Service (Linux)                       │   │
│  │  - .NET 8 Web API                                │   │
│  │  - Managed Identity                              │   │
│  │  - VNet Integration                              │   │
│  └──────────────────────────────────────────────────┘   │
│                         ↓ Private Endpoint               │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Azure SQL Database                              │   │
│  │  - Business Critical Tier                        │   │
│  │  - Private Endpoint                              │   │
│  │  - TDE Enabled                                   │   │
│  │  - Audit Logging                                 │   │
│  └──────────────────────────────────────────────────┘   │
│                         ↑ Managed Identity               │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Azure Data Factory                              │   │
│  │  - Scheduled Pipelines                           │   │
│  │  - Blob Storage Source                           │   │
│  │  - Managed Identity to SQL                       │   │
│  └──────────────────────────────────────────────────┘   │
│                         ↑                                │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Azure Blob Storage                              │   │
│  │  - CSV File Drops                                │   │
│  │  - Lifecycle Policies                            │   │
│  └──────────────────────────────────────────────────┘   │
│                                                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Azure Key Vault                                 │   │
│  │  - Connection Strings                            │   │
│  │  - API Secrets                                   │   │
│  │  - Entra ID Client Secrets                       │   │
│  └──────────────────────────────────────────────────┘   │
│                                                           │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Azure Entra ID (Azure AD)                       │   │
│  │  - App Registrations (API + SPA)                 │   │
│  │  - Security Groups (Roles)                       │   │
│  │  - Conditional Access Policies                   │   │
│  └──────────────────────────────────────────────────┘   │
│                                                           │
└─────────────────────────────────────────────────────────┘
```

### Infrastructure as Code

Use Bicep or Terraform to provision:

**Example Bicep Snippet**:
```bicep
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administrators: {
      azureADOnlyAuthentication: true
      principalType: 'Group'
      login: 'BillingAdmins'
      sid: billingAdminsGroupId
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'BC_Gen5_4'
    tier: 'BusinessCritical'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: '${sqlServerName}-pe'
  location: location
  properties: {
    subnet: { id: subnetId }
    privateLinkServiceConnections: [{
      name: 'sql-plsc'
      properties: {
        privateLinkServiceId: sqlServer.id
        groupIds: ['sqlServer']
      }
    }]
  }
}
```

## Performance Considerations

### Database Optimization

1. **Indexing**: All foreign keys indexed, search fields covered
2. **Query Plans**: Use `SET STATISTICS IO` to monitor reads
3. **Stored Procedures**: Compiled plans cached
4. **Partitioning**: Consider table partitioning for ChargeLine if >10M rows
5. **Columnstore**: Optional for analytics views if read-heavy

### API Optimization

1. **Response Caching**: Add `[ResponseCache]` attributes
2. **Pagination**: Always use for lists (default 25-100 items)
3. **Projection**: Use DTOs to avoid over-fetching
4. **Connection Pooling**: EF Core handles automatically
5. **Async/Await**: All DB calls are async

### Frontend Optimization

1. **Code Splitting**: Vite handles automatically with dynamic imports
2. **Lazy Loading**: React.lazy() for route components
3. **Memoization**: Use React.memo() for expensive renders
4. **Debouncing**: Debounce search inputs (not yet implemented)
5. **CDN**: Deploy to Azure Static Web Apps for global distribution

## Monitoring & Observability

### Application Insights Integration

```csharp
// In API Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

Track:
- Request duration
- Dependency calls (SQL)
- Exceptions
- Custom events (audit actions)

### Database Monitoring

Enable:
- **Query Performance Insight**: Azure SQL built-in
- **SQL Audit**: Track all DDL/DML
- **Azure Monitor**: Alerts on DTU/CPU thresholds

### Logging

- **API**: ILogger with structured logging
- **Frontend**: Console.error for dev, Application Insights for prod
- **Ingestor**: Console output, consider Application Insights SDK

## Extensibility Points

1. **Custom Queue Rules**: Modify `sp_RebuildWorkQueue`
2. **Additional Analytics Views**: Add to `03_views.sql`
3. **New Entities**: Add models, migrate DB, update API
4. **Advanced Search**: Implement full-text search on SQL
5. **Notifications**: Add SignalR for real-time updates
6. **Exports**: Add Excel/PDF export endpoints

## Testing Strategy

### Unit Tests
- API Controllers: Mock DbContext with InMemory provider
- Services: Mock dependencies (IAuditService)
- Frontend: Jest + React Testing Library

### Integration Tests
- API: TestServer with real SQL (test DB)
- E2E: Playwright for frontend flows

### Performance Tests
- Load testing: Apache JMeter or k6
- Target: 100 concurrent users, <500ms p95 latency

## References

- [EF Core Best Practices](https://learn.microsoft.com/en-us/ef/core/performance/)
- [Azure SQL Security](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-overview)
- [React Performance](https://react.dev/learn/render-and-commit#optimizing-performance)
- [API Design Guidelines](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design)
