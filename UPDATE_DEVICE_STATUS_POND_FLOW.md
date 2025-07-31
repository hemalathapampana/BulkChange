# UPDATE DEVICE STATUS Device Flow - POND IoT Service Provider

## Overview
The UPDATE DEVICE STATUS Device Flow is a bulk change operation that updates device status and service configurations for devices in the POND IoT Service Provider system. This process enables service providers to modify device operational states, enable/disable services, and manage device lifecycle transitions at the device level. The operation validates device status compatibility, updates device service configurations, manages effective date scheduling, and maintains comprehensive audit trails across different portal types (M2M, Mobility) specifically for POND IoT integration.

## Whole Flow:
User Interface → M2MController.BulkChange() → BuildStatusUpdateChangeDetails() → Device Status Validation → Device Status Repository Lookup → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessStatusUpdateAsync() → ProcessPondStatusUpdateAsync() → GetDeviceChanges() → POND API Service Call → Database Processing (Immediate) → Portal-Specific Logging → BulkChangeStatus.PROCESSED → Device Status Update Complete

## Process Flow:

### Phase 1: User Request & Validation
- User selects devices for status update from M2M UI
- User selects target device status (Active/Inactive)
- User configures service status settings
- User sets optional effective date for future implementation
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "StatusUpdate" (DeviceChangeType.StatusUpdate = 1)
  - Devices: List of ICCIDs
  - StatusUpdate: Device status configuration
  - EffectiveDate: Optional future effective date

### Phase 2: Controller Validation (M2MController.cs)
M2MController.ValidateBulkChange() is called with BuildStatusUpdateChangeDetails() method executing:

**Check each ICCID exists in database**
- Validate device status exists in system
- Check service provider compatibility (POND integration)
- Validate device requirements for POND IoT Service Provider
- Create M2M_DeviceChange records with validation results

**Key Validation Methods:**

```csharp
// From M2MController.cs lines 1698-1735
private static IEnumerable<M2M_DeviceChange> BuildStatusUpdateChangeDetails(
    AltaWorxCentral_Entities awxDb, 
    HttpSessionStateBase session, 
    PermissionManager permissionManager, 
    BulkChangeCreateModel bulkChange, 
    int serviceProviderId
)
```

**Validation Checks:**

- **Device Existence:** Verify ICCID exists in device inventory
- **Device Status:** Check device is not archived (IsActive=true, IsDeleted=false)
- **Device Status Validation:** Validate target status exists in system
- **POND Integration-Specific Validation:**
  - Service Provider Integration: Verify IntegrationType.Pond compatibility
  - Device Status Compatibility: Check target status is valid for POND
  - Service Status Validation: Validate service configuration settings
- **Permission Validation:** Verify user has access to target service provider and device status changes

```csharp
// Device Status validation specific to POND
var targetStatus = statusUpdate.TargetStatus.Trim();
var targetStatusId = awxDb.DeviceStatus.First(x =>
    x.IsActive && !x.IsDeleted
    && (x.Status == targetStatus || x.DisplayName == targetStatus)
    && x.IntegrationId == (int)IntegrationType.Pond).id;

if (integrationId.Equals((int)IntegrationType.Pond))
{
    // POND-specific device status validation
    ValidatePondDeviceStatusCompatibility(targetStatus, serviceProviderId);
}
```

### Phase 3: Queue Processing
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
  - BulkChangeId: Generated ID
  - ChangeRequestTypeId: DeviceChangeType.StatusUpdate (1)
  - ServiceProviderId: POND IoT Service Provider
  - TenantId: Current tenant
- User gets immediate response with BulkChangeId

### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
Lambda receives SQS message and ProcessBulkChangeAsync() routes to ProcessStatusUpdateAsync()
Located at lines 2527-2598 in AltaworxDeviceBulkChange.cs

**Service Provider Routing:**

```csharp
// From AltaworxDeviceBulkChange.cs line 478
case ChangeRequestType.StatusUpdate:
    var changes = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, PageSize).ToList();
    return await ProcessStatusUpdateAsync(context, logRepo, bulkChange, changes, retryNumber);
```

**Device Status Processing Logic:**

- Change Request Parsing: Extract device status update details
- Integration Type Evaluation: Route to POND-specific processing
- Database Operation Selection: Route to appropriate processing method
- POND Integration Handling: Apply POND-specific device status logic

### Phase 5: POND-Specific Device Status Processing
Located at lines 2770-2881 in AltaworxDeviceBulkChange.cs

**Processing Flow:**

**Extract Device Status Details:**

```csharp
var changeRequest = JsonConvert.DeserializeObject<StatusUpdateRequest<dynamic>>(change.ChangeRequest);
var updateStatus = changeRequest.UpdateStatus;
var postUpdateStatusId = changeRequest.PostUpdateStatusId;
var isIgnoreCurrentStatus = changeRequest.IsIgnoreCurrentStatus;
```

**POND Authentication & Service Setup:**

```csharp
var pondRepository = new PondRepository(context.CentralDbConnectionString, context.logger);
var pondAuthentication = pondRepository.GetPondAuthentication(ParameterizedLog(context), base64Service, bulkChange.ServiceProviderId);
var pondApiService = new PondApiService(pondAuthentication, _httpRequestFactory, context.IsProduction);
```

**Write Permission Validation:**

```csharp
if (!pondAuthentication.WriteIsEnabled)
{
    string message = string.Format(LogCommonStrings.WRITE_IS_DISABLED_FOR_SERVICE_PROVIDER_ID, bulkChange.ServiceProviderId);
    // Log error and mark as processed with error
    MarkProcessed(context, bulkChange.Id, change.Id, false, changeRequest.PostUpdateStatusId, message);
    return false;
}
```

**POND Device Status Management:**

**Device Status Processing Logic:**

```csharp
PondUpdateServiceStatusRequest updateStatusRequest;
if (changeRequest.UpdateStatus == DeviceStatusConstant.POND_ACTIVE)
{
    // Enable all service status
    updateStatusRequest = new PondUpdateServiceStatusRequest();
}
else
{
    // Disable all service status
    updateStatusRequest = new PondUpdateServiceStatusRequest(false);
}
```

**API Call Execution:**

```csharp
// Handle update service statuses
var updateServiceStatusResult = await pondApiService.UpdateServiceStatus(
    httpClientFactory.GetClient(), 
    iccid, 
    updateStatusRequest, 
    context.logger);
```

### Phase 6: Database Operations & POND Integration
For POND IoT Service Provider:

**Immediate Processing Flow:**

```csharp
// From AltaworxDeviceBulkChange.cs lines 5587-5600
private static void MarkProcessed(KeySysLambdaContext context, long bulkChangeId, long changeDetailId, 
    bool apiResult, int newDeviceStatusId, string statusDetails, bool isProcessed = true)
```

**Database Operations:**

```sql
EXEC usp_DeviceBulkChange_StatusUpdate_UpdateDeviceRecords
    @bulkChangeId = @bulkChangeId,
    @changeDetailId = @changeDetailId,
    @newDeviceStatusId = @newDeviceStatusId,
    @statusDetails = @statusDetails,
    @isProcessed = @isProcessed
```

**POND API Integration:**

- Service Status Updates: Enable/disable device services via POND API
- Device Status Synchronization: Update local database with POND API results
- Error Handling: Process API errors and update status accordingly
- Audit Trail Creation: Log all API calls and responses

### Phase 7: Response & Logging
**Portal-Specific Logging:**

**M2M Portal Integration:**

```csharp
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    M2MDeviceChangeId = change.Id,
    LogEntryDescription = "Update Device Status: Update POND API",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = updateServiceStatusResult.ActionText + Environment.NewLine + updateServiceStatusResult.RequestObject,
    ResponseText = updateServiceStatusResult.ResponseObject,
    HasErrors = updateServiceStatusResult.HasErrors,
    ResponseStatus = updateServiceStatusResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
    ProcessedDate = DateTime.UtcNow
});
```

**Mobility Portal Integration:**

```csharp
logRepo.AddMobilityLogEntry(new CreateMobilityDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    MobilityDeviceChangeId = change.Id,
    LogEntryDescription = "Update Device Status: Update POND API",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = updateServiceStatusResult.ActionText + Environment.NewLine + updateServiceStatusResult.RequestObject,
    ResponseText = updateServiceStatusResult.ResponseObject,
    HasErrors = updateServiceStatusResult.HasErrors,
    ResponseStatus = updateServiceStatusResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
    ProcessedDate = DateTime.UtcNow
});
```

**RevService Processing:**

```csharp
// Process rev service if status update successful
if (!updateServiceStatusResult.HasErrors)
{
    await ProcessRevServiceCreation<ThingSpaceStatusUpdateRequest>(context, logRepo, httpRetryPolicy, sqlRetryPolicy,
        revApiClient, bulkChange, new List<BulkChangeDetailRecord>() { change }, integrationAuthenticationId);
}
```

## Key Database Operations

**Tables Updated:**

- **DeviceBulkChange:** Main bulk change record
- **M2M_DeviceChange:** Individual device change records
- **DeviceBulkChangeLog:** Audit trail entries
- **Device:** Device status updates
- **DeviceStatus:** Device status reference data

**POND-Specific Operations:**

- Device status assignment updates
- Service status configuration changes
- API integration logging
- Error handling and retry logic
- Service activation/deactivation

## Data Models

**Device Status Update Structure:**

```csharp
public class StatusUpdateRequest<T>
{
    public string UpdateStatus { get; set; }
    public bool IsIgnoreCurrentStatus { get; set; }
    public int PostUpdateStatusId { get; set; }
    public string AccountNumber { get; set; }
    public T Request { get; set; }
    public BulkChangeAssociateCustomer RevService { get; set; }
    public RevServiceProductCreateModel RevServiceProductCreateModel { get; set; }
    public int IntegrationAuthenticationId { get; set; }
}
```

**Bulk Change Request Structure:**

```csharp
public class BulkChangeRequest
{
    public int? ServiceProviderId { get; set; }
    public int? ChangeType { get; set; } // StatusUpdate = 1
    public bool? ProcessChanges { get; set; }
    public string[] Devices { get; set; }
    public BulkChangeStatusUpdate StatusUpdate { get; set; }
}
```

**POND Service Status Request:**

```csharp
public class PondUpdateServiceStatusRequest
{
    public bool EnableService { get; set; } = true;
    
    public PondUpdateServiceStatusRequest() { }
    
    public PondUpdateServiceStatusRequest(bool enableService)
    {
        EnableService = enableService;
    }
}
```

## POND-Specific Constants

**Device Status Constants:**

```csharp
public static class DeviceStatusConstant
{
    public const string POND_ACTIVE = "Active";
    public const string POND_INACTIVE = "Inactive";
    public const string POND_SUSPENDED = "Suspended";
    public const string POND_TERMINATED = "Terminated";
}
```

**Integration Processing:**

- **IntegrationType.Pond = 8**
- **DeviceChangeType.StatusUpdate = 1**
- **ChangeRequestType.StatusUpdate = "statusupdate"**

## Error Handling & Validation

**POND-Specific Validations:**

1. **Authentication Validation:** Verify POND API credentials are configured
2. **Write Permission Check:** Ensure WriteIsEnabled flag is true
3. **Device Existence:** Validate device exists in POND system
4. **Status Compatibility:** Check target status is supported by POND
5. **Service Configuration:** Validate service status settings

**Error Recovery:**

- API call retries with exponential backoff
- Error logging with detailed error messages
- Status rollback on partial failures
- Audit trail maintenance for failed operations

## Processing Characteristics

**Immediate Processing:** All device status updates are processed immediately
**No Scheduled Processing:** Unlike customer rate plan changes, device status updates do not support future effective dates
**Real-time API Integration:** Direct integration with POND API for service status updates
**Comprehensive Logging:** Full audit trail of all API calls and database changes
**Error Handling:** Robust error handling with detailed error reporting and recovery mechanisms