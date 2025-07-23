# Process Flow Documentation

## Overview

This document outlines the process flows for the Altaworx Device Bulk Change system, detailing the step-by-step workflows for various device management operations across M2M and Mobility portals.

## Core Process Types

### 1. Customer Rate Plan Change Process

#### Process Overview
Updates customer-specific rate plans for billing and data allocation purposes.

```mermaid
flowchart TD
    A[Bulk Change Request] --> B{Validate Request}
    B -->|Valid| C[Extract Customer Rate Plan Data]
    B -->|Invalid| D[Return Error Response]
    C --> E{Check Effective Date}
    E -->|Immediate| F[Process Immediately]
    E -->|Future| G[Add to Schedule Queue]
    F --> H[Execute Database Procedure]
    G --> I[Store in Queue Table]
    H --> J[Update Device Records]
    I --> K[Schedule Future Processing]
    J --> L[Log Results]
    K --> L
    L --> M[Complete Process]
```

#### Detailed Steps

**Step 1: Request Validation**
```csharp
// Input validation
if (request.CustomerRatePlanUpdate == null) 
    return ValidationError("Customer rate plan update required");

if (request.Devices == null || request.Devices.Length == 0)
    return ValidationError("Device list cannot be empty");
```

**Step 2: Effective Date Processing**
```csharp
var effectiveDate = request.CustomerRatePlanUpdate.EffectiveDate;
var isImmediate = effectiveDate == null || 
                  effectiveDate?.ToUniversalTime() <= DateTime.UtcNow;

if (isImmediate) {
    await ProcessCustomerRatePlanChangeAsync(...);
} else {
    await ProcessAddCustomerRatePlanChangeToQueueAsync(...);
}
```

**Step 3: Database Operations**
```sql
-- Immediate processing
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId,
    @customerRatePlanId,
    @customerRatePoolId,
    @customerDataAllocationMB,
    @effectiveDate,
    @needToMarkProcessed

-- Scheduled processing
INSERT INTO CustomerRatePlanDeviceQueue 
    (DeviceId, CustomerRatePlanId, CustomerRatePoolId, 
     CustomerDataAllocationMB, EffectiveDate, PortalType, TenantId)
```

### 2. Device Status Change Process

#### Process Overview
Changes device operational status across service providers.

```mermaid
flowchart TD
    A[Status Change Request] --> B[Validate Device Identifiers]
    B --> C[Lookup Current Status]
    C --> D{Status Transition Valid?}
    D -->|Yes| E[Determine Service Provider]
    D -->|No| F[Return Business Rule Error]
    E --> G{Service Provider Type}
    G -->|Jasper| H[Call Jasper API]
    G -->|ThingSpace| I[Call ThingSpace API]
    G -->|Telegence| J[Call Telegence API]
    G -->|Other| K[Call Generic API]
    H --> L[Process API Response]
    I --> L
    J --> L
    K --> L
    L --> M{API Success?}
    M -->|Yes| N[Update Database Status]
    M -->|No| O[Log Error & Retry]
    N --> P[Send Notifications]
    O --> Q{Retry Attempts < Max?}
    Q -->|Yes| R[Add to Retry Queue]
    Q -->|No| S[Mark as Failed]
    P --> T[Complete Process]
    R --> T
    S --> T
```

#### Status Transition Rules

**Valid Transitions:**
- `Inventory` → `Active`
- `Active` → `Suspended`
- `Suspended` → `Active`
- `Active` → `Deactive`
- `Deactive` → `Retired`

**Business Rules:**
```csharp
public bool IsValidStatusTransition(string currentStatus, string newStatus)
{
    var allowedTransitions = new Dictionary<string, string[]>
    {
        ["Inventory"] = new[] { "Active", "Test Ready" },
        ["Active"] = new[] { "Suspended", "Deactive" },
        ["Suspended"] = new[] { "Active", "Deactive" },
        ["Test Ready"] = new[] { "Active", "Deactive" },
        ["Deactive"] = new[] { "Retired" }
    };
    
    return allowedTransitions.ContainsKey(currentStatus) && 
           allowedTransitions[currentStatus].Contains(newStatus);
}
```

### 3. Equipment Change Process (ICCID/IMEI)

#### Process Overview
Handles device identifier changes for SIM swaps and device replacements.

```mermaid
flowchart TD
    A[Equipment Change Request] --> B[Validate Identifiers]
    B --> C{Change Type}
    C -->|ICCID Change| D[Process SIM Swap]
    C -->|IMEI Change| E[Process Device Swap]
    C -->|Both| F[Process Complete Replacement]
    D --> G[Validate New ICCID]
    E --> H[Validate New IMEI]
    F --> I[Validate Both Identifiers]
    G --> J[Check ICCID Availability]
    H --> K[Check IMEI Availability]
    I --> L[Check Both Availability]
    J --> M{Available?}
    K --> M
    L --> M
    M -->|Yes| N[Update Device Record]
    M -->|No| O[Return Conflict Error]
    N --> P[Call Service Provider API]
    P --> Q{API Success?}
    Q -->|Yes| R[Commit Changes]
    Q -->|No| S[Rollback Changes]
    R --> T[Update Audit Trail]
    S --> U[Log Error]
    T --> V[Send Notifications]
    U --> V
    V --> W[Complete Process]
```

#### Validation Rules

**ICCID Validation:**
```csharp
public bool IsValidICCID(string iccid)
{
    // ICCID should be 19-20 digits
    if (string.IsNullOrEmpty(iccid) || iccid.Length < 19 || iccid.Length > 20)
        return false;
    
    // Should contain only digits
    return iccid.All(char.IsDigit);
}
```

**IMEI Validation:**
```csharp
public bool IsValidIMEI(string imei)
{
    // IMEI should be 15 digits
    if (string.IsNullOrEmpty(imei) || imei.Length != 15)
        return false;
    
    // Luhn algorithm validation
    return ValidateLuhnChecksum(imei);
}
```

### 4. New Service Activation Process

#### Process Overview
Activates new devices and establishes service connectivity.

```mermaid
flowchart TD
    A[Activation Request] --> B[Validate Device Data]
    B --> C[Check Service Provider]
    C --> D{Provider Available?}
    D -->|Yes| E[Prepare Activation Payload]
    D -->|No| F[Return Provider Error]
    E --> G[Call Activation API]
    G --> H{Activation Successful?}
    H -->|Yes| I[Update Device Status to Active]
    H -->|No| J{Is Async Activation?}
    J -->|Yes| K[Create Monitoring Job]
    J -->|No| L[Immediate Retry]
    I --> M[Configure Service Parameters]
    K --> N[Schedule Status Check]
    L --> O{Retry Count < Max?}
    M --> P[Send Welcome Notification]
    N --> Q[Check Activation Status]
    O -->|Yes| G
    O -->|No| R[Mark as Failed]
    P --> S[Complete Activation]
    Q --> T{Now Active?}
    T -->|Yes| I
    T -->|No| U{Check Count < Max?}
    U -->|Yes| N
    U -->|No| R
    R --> V[Send Failure Notification]
    S --> W[Success]
    V --> W
```

#### Activation Parameters

**ThingSpace Activation:**
```csharp
public class ThingSpaceActivationRequest
{
    public string Iccid { get; set; }
    public string ServicePlan { get; set; }
    public string ZipCode { get; set; }
    public string GroupName { get; set; }
    public string[] CarrierName { get; set; }
    public string AccountName { get; set; }
}
```

**Jasper Activation:**
```csharp
public class JasperActivationRequest
{
    public string Iccid { get; set; }
    public string RatePlan { get; set; }
    public string CommunicationPlan { get; set; }
    public DateTime EffectiveDate { get; set; }
}
```

### 5. Username Edit Process

#### Process Overview
Updates device username/identifiers with validation and conflict resolution.

```mermaid
flowchart TD
    A[Username Edit Request] --> B[Validate New Username]
    B --> C{Username Format Valid?}
    C -->|Yes| D[Check Username Availability]
    C -->|No| E[Return Format Error]
    D --> F{Username Available?}
    F -->|Yes| G[Update Device Record]
    F -->|No| H[Return Conflict Error]
    G --> I[Call Service Provider API]
    I --> J{API Success?}
    J -->|Yes| K[Send Email Notification]
    J -->|No| L[Rollback Changes]
    K --> M[Update Audit Log]
    L --> N[Log Error]
    M --> O[Complete Process]
    N --> O
```

#### Username Validation

**Format Rules:**
```csharp
public bool IsValidUsername(string username)
{
    // Length between 3-50 characters
    if (string.IsNullOrEmpty(username) || 
        username.Length < 3 || username.Length > 50)
        return false;
    
    // Alphanumeric and specific special characters only
    var validPattern = @"^[a-zA-Z0-9._-]+$";
    return Regex.IsMatch(username, validPattern);
}
```

**Uniqueness Check:**
```sql
SELECT COUNT(*) FROM Devices 
WHERE Username = @newUsername 
  AND TenantId = @tenantId 
  AND Id != @currentDeviceId
```

### 6. Bulk Processing Workflow

#### Process Overview
Manages large-scale device operations with batching and progress tracking.

```mermaid
flowchart TD
    A[Bulk Request] --> B[Parse Device List]
    B --> C[Validate All Devices]
    C --> D{All Valid?}
    D -->|Yes| E[Create Bulk Change Record]
    D -->|No| F[Return Validation Errors]
    E --> G[Create Device Change Records]
    G --> H[Determine Processing Strategy]
    H --> I{Immediate or Queued?}
    I -->|Immediate| J[Process All Devices]
    I -->|Queued| K[Add to Processing Queue]
    J --> L[Batch Process Devices]
    K --> M[Schedule Processing]
    L --> N{All Processed?}
    M --> O[Queue Status: Pending]
    N -->|Yes| P[Update Bulk Status: Completed]
    N -->|No| Q[Update Bulk Status: Partially Completed]
    O --> R[Monitor Queue]
    P --> S[Send Completion Notification]
    Q --> S
    R --> T[Process Queued Items]
    S --> U[Complete]
    T --> N
```

#### Batch Processing Logic

**Device Batching:**
```csharp
public async Task ProcessDeviceBatch(IEnumerable<Device> devices, int batchSize = 100)
{
    var deviceBatches = devices.Chunk(batchSize);
    
    foreach (var batch in deviceBatches)
    {
        var tasks = batch.Select(device => ProcessSingleDevice(device));
        await Task.WhenAll(tasks);
        
        // Progress reporting
        await UpdateBatchProgress(completedCount, totalCount);
    }
}
```

### 7. Error Handling and Retry Process

#### Process Overview
Comprehensive error handling with categorized retry strategies.

```mermaid
flowchart TD
    A[Operation Failure] --> B[Categorize Error]
    B --> C{Error Type}
    C -->|Transient| D[Apply Retry Policy]
    C -->|Validation| E[Return Immediate Error]
    C -->|Business Rule| F[Return Business Error]
    C -->|Integration| G[Check Retry Count]
    D --> H{Retry Successful?}
    G --> I{Count < Max?}
    H -->|Yes| J[Continue Processing]
    H -->|No| K[Apply Backoff]
    I -->|Yes| L[Queue for Retry]
    I -->|No| M[Mark as Failed]
    K --> N[Schedule Next Retry]
    L --> N
    E --> O[Log Error]
    F --> O
    M --> O
    J --> P[Success]
    N --> Q[Retry Processing]
    O --> R[Notify Administrators]
    Q --> A
    P --> S[Complete]
    R --> S
```

#### Retry Policies

**SQL Transient Errors:**
```csharp
public static readonly RetryPolicy SqlRetryPolicy = Policy
    .Handle<SqlException>(ex => IsTransientError(ex))
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
            LogRetryAttempt(retryCount, timespan)
    );
```

**HTTP Request Errors:**
```csharp
public static readonly RetryPolicy HttpRetryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(5),
        onRetry: LogHttpRetry
    );
```

### 8. Notification and Reporting Process

#### Process Overview
Automated notifications and status reporting for various stakeholders.

```mermaid
flowchart TD
    A[Process Event] --> B{Notification Trigger}
    B -->|Success| C[Generate Success Notification]
    B -->|Error| D[Generate Error Notification]
    B -->|Progress| E[Generate Progress Update]
    C --> F[Determine Recipients]
    D --> G[Determine Error Recipients]
    E --> H[Determine Progress Recipients]
    F --> I[Format Success Message]
    G --> J[Format Error Message]
    H --> K[Format Progress Message]
    I --> L[Send Email]
    J --> M[Send Alert Email]
    K --> N[Send Status Update]
    L --> O[Log Notification]
    M --> P[Log Error Alert]
    N --> Q[Log Progress Update]
    O --> R[Update Delivery Status]
    P --> R
    Q --> R
    R --> S[Complete Notification]
```

#### Notification Templates

**Success Notification:**
```html
<h2>Bulk Change Completed Successfully</h2>
<p>Bulk Change ID: {BulkChangeId}</p>
<p>Total Devices: {TotalDevices}</p>
<p>Successfully Processed: {SuccessCount}</p>
<p>Completion Time: {CompletionTime}</p>
```

**Error Notification:**
```html
<h2>Bulk Change Failed</h2>
<p>Bulk Change ID: {BulkChangeId}</p>
<p>Error: {ErrorMessage}</p>
<p>Failed Devices: {FailedDevices}</p>
<p>Support Contact: {SupportEmail}</p>
```

### 9. Monitoring and Health Check Process

#### Process Overview
Continuous monitoring of system health and performance metrics.

```mermaid
flowchart TD
    A[Health Check Trigger] --> B[Check Database Connectivity]
    B --> C[Check Queue Status]
    C --> D[Check External API Health]
    D --> E[Check Processing Metrics]
    E --> F{All Systems Healthy?}
    F -->|Yes| G[Update Health Status: OK]
    F -->|No| H[Identify Issues]
    G --> I[Log Health Check]
    H --> J[Generate Alerts]
    J --> K[Escalate Critical Issues]
    K --> L[Update Health Status: Degraded/Down]
    I --> M[Schedule Next Check]
    L --> M
    M --> N[Complete Health Check]
```

#### Health Metrics

**System Metrics:**
- Database connection time
- Queue depth and processing rate
- API response times
- Error rates by component
- Memory and CPU utilization

**Business Metrics:**
- Processing throughput
- Success/failure rates
- Average processing time
- Customer satisfaction scores

### 10. Data Archival Process

#### Process Overview
Automated archival of completed and historical data.

```mermaid
flowchart TD
    A[Archival Job Start] --> B[Identify Records for Archival]
    B --> C{Records Found?}
    C -->|Yes| D[Validate Data Integrity]
    C -->|No| E[Log: No Records to Archive]
    D --> F[Create Archive Backup]
    F --> G[Verify Backup Integrity]
    G --> H{Backup Valid?}
    H -->|Yes| I[Move Data to Archive]
    H -->|No| J[Retry Backup Process]
    I --> K[Update Archive Index]
    J --> L{Retry Count < Max?}
    K --> M[Verify Archive Completion]
    L -->|Yes| F
    L -->|No| N[Alert: Archive Failed]
    M --> O{Archive Successful?}
    O -->|Yes| P[Remove Source Data]
    O -->|No| Q[Rollback Archive]
    P --> R[Update Retention Logs]
    Q --> S[Alert: Data Inconsistency]
    R --> T[Complete Archival]
    E --> T
    N --> T
    S --> T
```

---

*This document provides comprehensive process flows for the Altaworx Device Bulk Change system. Each process includes detailed steps, decision points, error handling, and integration touchpoints.*