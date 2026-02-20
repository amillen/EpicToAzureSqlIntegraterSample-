# Security Documentation

This document outlines security best practices, patterns, and recommendations for the Epic Billing Analytics & Triage application, with a focus on healthcare data protection and Azure deployment.

> ⚠️ **IMPORTANT**: This is a demo application. For production PHI use, conduct a full security audit and HIPAA compliance review.

## Table of Contents

1. [Authentication & Authorization](#authentication--authorization)
2. [Data Protection](#data-protection)
3. [Network Security](#network-security)
4. [Azure Services Security](#azure-services-security)
5. [Compliance Considerations](#compliance-considerations)
6. [Row-Level Security](#row-level-security)
7. [Audit & Monitoring](#audit--monitoring)
8. [Deployment Checklist](#deployment-checklist)

---

## Authentication & Authorization

### Azure Entra ID (Azure AD) Integration

#### API Configuration

**App Registration**: Create in Azure Portal
- Name: `epic-billing-api`
- Supported account types: Single tenant
- API Permissions: None (API exposes scopes)
- Expose an API:
  - Application ID URI: `api://epic-billing-api`
  - Scopes: `access_as_user`

**appsettings.json**:
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "{your-tenant-id}",
    "ClientId": "{api-client-id}",
    "Audience": "api://{api-client-id}"
  },
  "DEV_BYPASS_AUTH": false
}
```

**Code** (already implemented in Program.cs):
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
```

#### Frontend Configuration

**App Registration**: Separate SPA registration
- Name: `epic-billing-spa`
- Platform: Single-page application
- Redirect URIs: `https://yourapp.azurestaticapps.net/`
- Permissions: `api://epic-billing-api/access_as_user`

**MSAL Configuration** (for production):
```typescript
import { PublicClientApplication } from '@azure/msal-browser';

const msalConfig = {
  auth: {
    clientId: import.meta.env.VITE_AZURE_CLIENT_ID,
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_AZURE_TENANT_ID}`,
    redirectUri: import.meta.env.VITE_AZURE_REDIRECT_URI,
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
};

const msalInstance = new PublicClientApplication(msalConfig);

// Acquire token for API calls
const tokenRequest = {
  scopes: ['api://epic-billing-api/access_as_user'],
};

const response = await msalInstance.acquireTokenSilent(tokenRequest);
// Use response.accessToken in Authorization header
```

### Role-Based Access Control (RBAC)

#### Azure AD Security Groups

Create groups and assign users:

| Group Name | Description | Permissions |
|------------|-------------|-------------|
| `BillingReaders` | View-only access | Read work queue, view claims |
| `BillingAnalysts` | Triage analysts | Read + Write (assign, status, notes) |
| `BillingAdmins` | System administrators | All operations + admin dashboard |

#### Role Claims in Token

Configure App Roles in app registration manifest:
```json
{
  "appRoles": [
    {
      "allowedMemberTypes": ["User"],
      "description": "Billing Analyst role",
      "displayName": "BillingAnalyst",
      "id": "{unique-guid}",
      "isEnabled": true,
      "value": "BillingAnalyst"
    }
  ]
}
```

Assign groups to roles in Enterprise Applications.

#### API Authorization

Apply policies to controllers:
```csharp
[Authorize(Policy = "BillingAnalyst")]
public class WorkQueueController : ControllerBase
{
    // Only BillingAnalyst and BillingAdmin can access
}

[Authorize(Policy = "BillingAdmin")]
public class AdminController : ControllerBase
{
    // Only BillingAdmin can access
}
```

---

## Data Protection

### Data at Rest

#### Azure SQL Transparent Data Encryption (TDE)

**Enabled by default** on Azure SQL. Encrypts:
- Database files (.mdf, .ndf)
- Transaction log files (.ldf)
- Backups

Verify:
```sql
SELECT database_id, encryption_state, percent_complete
FROM sys.dm_database_encryption_keys;
-- encryption_state = 3 means encrypted
```

#### Column-Level Encryption (Optional)

For highly sensitive fields (e.g., SSN if stored):
```sql
-- Create master key
CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'Strong!Password123';

-- Create certificate
CREATE CERTIFICATE BillingCert
WITH SUBJECT = 'Billing PHI Protection';

-- Create symmetric key
CREATE SYMMETRIC KEY BillingKey
WITH ALGORITHM = AES_256
ENCRYPTION BY CERTIFICATE BillingCert;

-- Encrypt column
ALTER TABLE Patient ADD SSN_Encrypted VARBINARY(128);

-- Usage
OPEN SYMMETRIC KEY BillingKey DECRYPTION BY CERTIFICATE BillingCert;
UPDATE Patient SET SSN_Encrypted = EncryptByKey(Key_GUID('BillingKey'), @SSN);
CLOSE SYMMETRIC KEY BillingKey;
```

### Data in Transit

#### TLS/SSL

**API**: Enforce HTTPS in production
```csharp
// In Program.cs
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

**Azure SQL**: Connection string with `Encrypt=True`
```
Server=yourserver.database.windows.net;Database=EpicBilling;User Id=user;Password=pass;Encrypt=True;TrustServerCertificate=False;
```

**Frontend**: Deploy to HTTPS-only static site (Azure Static Web Apps enforces this).

### Data Masking

#### Dynamic Data Masking (DDM)

For non-production environments:
```sql
ALTER TABLE Patient
ALTER COLUMN MRNHash ADD MASKED WITH (FUNCTION = 'partial(0,"XXXX",0)');

-- Grant unmask permission to specific roles
GRANT UNMASK TO BillingAdmins;
```

Users without UNMASK see: `XXXX` instead of actual value.

---

## Network Security

### Private Endpoints

**Azure SQL Private Endpoint**: Removes public internet exposure

```bicep
resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'sql-private-endpoint'
  location: location
  properties: {
    subnet: { id: vnetSubnetId }
    privateLinkServiceConnections: [{
      name: 'sql-connection'
      properties: {
        privateLinkServiceId: sqlServer.id
        groupIds: ['sqlServer']
      }
    }]
  }
}

// Disable public network access on SQL Server
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  properties: {
    publicNetworkAccess: 'Disabled'
  }
}
```

**Benefits**:
- SQL traffic stays within Azure VNet
- No exposure to public internet
- Compliant with HIPAA/HITRUST requirements

### Virtual Network Integration

**App Service VNet Integration**:
```bicep
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  properties: {
    virtualNetworkSubnetId: appServiceSubnetId
  }
}
```

Allows App Service to access private endpoint resources.

### Firewall Rules

**If using public endpoint** (not recommended for prod):
```sql
-- Azure SQL Firewall
-- Allow only Azure services
EXECUTE sp_set_database_firewall_rule 
  @name = N'AllowAzureServices', 
  @start_ip_address = '0.0.0.0', 
  @end_ip_address = '0.0.0.0';

-- Or specific IP ranges
EXECUTE sp_set_database_firewall_rule 
  @name = N'OfficeNetwork', 
  @start_ip_address = '203.0.113.0', 
  @end_ip_address = '203.0.113.255';
```

**App Service IP Restrictions**:
```json
{
  "ipSecurityRestrictions": [
    {
      "ipAddress": "10.0.0.0/24",
      "action": "Allow",
      "priority": 100,
      "name": "AllowVNet"
    }
  ]
}
```

---

## Azure Services Security

### Managed Identity

Use **system-assigned managed identity** to eliminate stored credentials.

#### App Service to SQL

**Enable Managed Identity** on App Service:
```bash
az webapp identity assign --resource-group rg --name epic-billing-api
```

**Grant SQL Access**:
```sql
-- Create user for managed identity
CREATE USER [epic-billing-api] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [epic-billing-api];
ALTER ROLE db_datawriter ADD MEMBER [epic-billing-api];
ALTER ROLE db_ddladmin ADD MEMBER [epic-billing-api]; -- if migrations needed
```

**Connection String** (no password):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=yourserver.database.windows.net;Database=EpicBilling;Authentication=Active Directory Managed Identity;"
  }
}
```

#### Azure Data Factory to SQL

ADF can also use managed identity:
```json
{
  "linkedService": {
    "type": "AzureSqlDatabase",
    "typeProperties": {
      "connectionString": "Server=yourserver.database.windows.net;Database=EpicBilling;",
      "authenticationType": "SystemAssignedManagedIdentity"
    }
  }
}
```

Grant same SQL permissions as above.

### Key Vault Integration

**Store secrets in Key Vault**:
```bash
az keyvault secret set --vault-name billing-keyvault --name SqlConnectionString --value "Server=..."
```

**Access from App Service**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "@Microsoft.KeyVault(VaultName=billing-keyvault;SecretName=SqlConnectionString)"
  }
}
```

**Enable App Service to read Key Vault**:
```bash
# Get App Service managed identity
PRINCIPAL_ID=$(az webapp identity show --resource-group rg --name epic-billing-api --query principalId -o tsv)

# Grant Key Vault access
az keyvault set-policy --name billing-keyvault --object-id $PRINCIPAL_ID --secret-permissions get list
```

---

## Compliance Considerations

### HIPAA Alignment

This application includes patterns for HIPAA compliance:

1. **Encryption**: TDE for data at rest, TLS for data in transit
2. **Access Controls**: RBAC with Azure AD
3. **Audit Logs**: All writes logged to `AuditLog` table
4. **Data Masking**: MRN is hashed in sample data
5. **Network Isolation**: Private endpoints recommended

**Additional requirements**:
- Business Associate Agreement (BAA) with Microsoft
- Azure SQL in Business Critical tier
- Enable Advanced Threat Protection
- Configure retention policies for audit logs
- Implement breach notification procedures

### Audit Logging

#### Application Audit Log

Already implemented in `AuditLog` table:
- **Who**: ActorUpn (from JWT claim)
- **What**: Action (Assign, StatusChange, Update, Create)
- **When**: CreatedUtc (timestamp)
- **Where**: EntityName + EntityId
- **Details**: BeforeJson, AfterJson (full state snapshots)

Query examples:
```sql
-- User activity
SELECT * FROM AuditLog WHERE ActorUpn = 'jane.doe@example.com' ORDER BY CreatedUtc DESC;

-- Changes to specific claim
SELECT * FROM AuditLog WHERE EntityName = 'Claim' AND EntityId = '123' ORDER BY CreatedUtc;

-- All status changes today
SELECT * FROM AuditLog WHERE Action = 'StatusChange' AND CreatedUtc >= CAST(GETUTCDATE() AS DATE);
```

#### Azure SQL Auditing

Enable server-level auditing:
```bash
az sql server audit-policy update \
  --resource-group rg \
  --server yourserver \
  --state Enabled \
  --blob-storage-target-state Enabled \
  --storage-account mystorage
```

Captures:
- All DDL (CREATE, ALTER, DROP)
- All DML (SELECT, INSERT, UPDATE, DELETE)
- Failed login attempts
- Privilege escalations

### Data Retention

**GDPR/HIPAA**: Define retention policies

```sql
-- Example: Delete old audit logs after 7 years
DELETE FROM AuditLog WHERE CreatedUtc < DATEADD(YEAR, -7, GETUTCDATE());

-- Or use temporal tables for automatic archival
ALTER TABLE Patient ADD PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo);
ALTER TABLE Patient SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.PatientHistory));
```

---

## Row-Level Security

Implement **Row-Level Security (RLS)** to filter data by user attributes.

### Example: Filter by Assigned User

**Create predicate function**:
```sql
CREATE SCHEMA Security;
GO

CREATE FUNCTION Security.fn_WorkQueuePredicate(@AssignedToUpn NVARCHAR(255))
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessGranted
    WHERE 
        -- Allow if assigned to current user
        @AssignedToUpn = USER_NAME()
        OR 
        -- Allow if user is admin
        IS_MEMBER('BillingAdmins') = 1
        OR
        -- Allow if unassigned and user is analyst
        (@AssignedToUpn IS NULL AND IS_MEMBER('BillingAnalysts') = 1);
GO
```

**Apply security policy**:
```sql
CREATE SECURITY POLICY WorkQueuePolicy
ADD FILTER PREDICATE Security.fn_WorkQueuePredicate(AssignedToUpn)
ON dbo.WorkQueueItem
WITH (STATE = ON);
```

**Usage**:
- Users see only items assigned to them or unassigned
- Admins see all items
- Transparent to application code (EF Core queries automatically filtered)

### Example: Filter by Facility/Department

If patients belong to facilities:

```sql
CREATE FUNCTION Security.fn_FacilityPredicate(@FacilityId INT)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN
    SELECT 1 AS AccessGranted
    WHERE 
        @FacilityId IN (
            SELECT FacilityId FROM Security.UserFacilityAccess
            WHERE UserUpn = USER_NAME()
        );
GO

CREATE SECURITY POLICY FacilityPolicy
ADD FILTER PREDICATE Security.fn_FacilityPredicate(FacilityId)
ON dbo.Patient
WITH (STATE = ON);
```

---

## Audit & Monitoring

### Application Insights

**Track custom events**:
```csharp
public class AuditService : IAuditService
{
    private readonly TelemetryClient _telemetry;

    public async Task LogAsync(...)
    {
        // Log to database
        await _context.SaveChangesAsync();

        // Also send to Application Insights
        _telemetry.TrackEvent("AuditLog", new Dictionary<string, string>
        {
            { "EntityName", entityName },
            { "Action", action },
            { "ActorUpn", actorUpn }
        });
    }
}
```

**Alerts**: Create alerts on anomalies
- Failed login attempts > threshold
- High-privilege actions (DELETE, DROP)
- Unusual access patterns (time, IP)

### Azure Monitor

**SQL Metrics**:
- DTU/CPU percentage
- Connection failures
- Deadlocks
- Long-running queries

**App Service Metrics**:
- Response time
- HTTP 5xx errors
- Memory usage

**Set up action groups** for email/SMS notifications.

### Log Analytics

Query across all logs:
```kusto
// Failed API requests in last 24 hours
requests
| where timestamp > ago(24h)
| where success == false
| summarize count() by resultCode, url
```

---

## Deployment Checklist

### Pre-Deployment

- [ ] Remove all hardcoded secrets from code
- [ ] Set `DEV_BYPASS_AUTH=false`
- [ ] Configure Azure AD app registrations
- [ ] Create Azure SQL with Business Critical tier
- [ ] Enable TDE and Advanced Threat Protection on SQL
- [ ] Create Key Vault and store secrets
- [ ] Set up managed identities for App Service and ADF
- [ ] Configure private endpoints for SQL
- [ ] Enable VNet integration for App Service
- [ ] Set up Application Insights
- [ ] Configure SQL Server audit logging
- [ ] Review and harden CORS policy
- [ ] Implement rate limiting (use Azure Front Door or APIM)
- [ ] Set up WAF (Web Application Firewall) if using Front Door

### Post-Deployment

- [ ] Test authentication flow end-to-end
- [ ] Verify RBAC roles are working
- [ ] Confirm audit logs are being written
- [ ] Run security scan (e.g., Azure Security Center)
- [ ] Perform penetration testing
- [ ] Set up monitoring dashboards
- [ ] Configure backup retention policies
- [ ] Test disaster recovery procedures
- [ ] Document runbooks for incident response
- [ ] Conduct security awareness training for users

### Ongoing

- [ ] Monthly review of audit logs
- [ ] Quarterly access reviews (remove stale users)
- [ ] Patch management (update .NET SDK, npm packages)
- [ ] Vulnerability scanning (GitHub Dependabot, Snyk)
- [ ] Incident response drills
- [ ] Update this security documentation

---

## Security Contacts

- **Security Team**: security@example.com
- **Azure Support**: [Azure Portal](https://portal.azure.com/#create/Microsoft.Support)
- **Compliance Officer**: compliance@example.com

---

## References

- [Azure SQL Security Best Practices](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-best-practice)
- [Microsoft Identity Platform](https://learn.microsoft.com/en-us/azure/active-directory/develop/)
- [Azure Well-Architected Framework - Security](https://learn.microsoft.com/en-us/azure/architecture/framework/security/)
- [HIPAA on Azure](https://learn.microsoft.com/en-us/azure/compliance/offerings/offering-hipaa-us)
- [Row-Level Security](https://learn.microsoft.com/en-us/sql/relational-databases/security/row-level-security)

---

**Last Updated**: 2026-02-19

**Review Cycle**: Quarterly
