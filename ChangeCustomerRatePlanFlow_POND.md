# CHANGE CUSTOMER RATE PLAN Device Flow for POND IoT Service Provider

## Overview
The CHANGE CUSTOMER RATE PLAN Device Flow is a bulk change operation that updates customer-level rate plans for devices in the POND IoT Service Provider system. This process enables service providers to modify customer billing plans, data allocations, and service configurations at the customer/tenant level rather than at the carrier infrastructure level. The operation validates customer rate plan compatibility, updates device assignments, manages effective date scheduling, and maintains comprehensive audit trails across different portal types (M2M, Mobility) specifically for POND IoT integration.

## Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerRatePlanChangeDetails() → Customer Rate Plan Validation → Rate Plan Repository Lookup → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessCustomerRatePlanChangeAsync() → GetDeviceChanges() → Effective Date Evaluation → Database Processing (Immediate/Scheduled) → Portal-Specific Logging → BulkChangeStatus.PROCESSED → Customer Rate Plan Change Complete

## Process Flow:

### Phase 1: User Request & Validation
- User selects devices for customer rate plan change from M2M UI
- User selects target customer rate plan and configuration
- User configures data allocation and pool settings
- User sets optional effective date for future implementation
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "CustomerRatePlanChange" (DeviceChangeType.CustomerRatePlanChange = 4)
  - Devices: List of ICCIDs
  - CustomerRatePlanUpdate: Customer rate plan configuration
  - EffectiveDate: Optional future effective date

### Phase 2: Controller Validation (M2MController.cs)
M2MController.ValidateBulkChange() is called with BuildCustomerRatePlanChangeDetails() method executing:

- Check each ICCID exists in database
- Validate customer rate plan exists in system
- Check service provider compatibility (POND integration)
- Validate device requirements for POND IoT Service Provider
- Create M2M_DeviceChange records with validation results

**Key Validation Methods:**

```csharp
// From M2MController.cs lines 1647-1697
private static IEnumerable<M2M_DeviceChange> BuildCustomerRatePlanChangeDetails(
    AltaWorxCentral_Entities awxDb, 
    HttpSessionStateBase session, 
    BulkChangeCreateModel bulkChange, 
    int serviceProviderId, 
    DeviceChangeType changeType
)
```

**Validation Checks:**

- Device Existence: Verify ICCID exists in device inventory
- Device Status: Check device is not archived (IsActive=true, IsDeleted=false)
- Customer Rate Plan Validation: Validate CustomerRatePlanId exists in system
- POND Integration-Specific Validation:
  - Service Provider Integration: Verify IntegrationType.Pond compatibility
  - Customer Pool Validation: Check CustomerPoolId if specified
  - Data Allocation Validation: Validate CustomerDataAllocationMB limits
- Permission Validation: Verify user has access to target service provider and customer rate plans

```csharp
// Customer Rate Plan validation specific to POND
var customerRatePlanRepository = new CustomerRatePlanRepository(awxDb);
var customerRatePlan = customerRatePlanRepository.GetByCustomerRatePlanId(
    bulkChange.CustomerRatePlanUpdate.CustomerRatePlanId);

if (integrationId.Equals((int)IntegrationType.Pond))
{
    // POND-specific customer rate plan validation
    ValidatePondCustomerRatePlanCompatibility(customerRatePlan, serviceProviderId);
}
```

### Phase 3: Queue Processing
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
  - BulkChangeId: Generated ID
  - ChangeRequestTypeId: DeviceChangeType.CustomerRatePlanChange (4)
  - ServiceProviderId: POND IoT Service Provider
  - TenantId: Current tenant
- User gets immediate response with BulkChangeId

### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
Lambda receives SQS message and ProcessBulkChangeAsync() routes to ProcessCustomerRatePlanChangeAsync()
Located at lines 2105-2167 in AltaworxDeviceBulkChange.cs

**Service Provider Routing:**

```csharp
// From AltaworxDeviceBulkChange.cs line 487
case ChangeRequestType.CustomerRatePlanChange:
    await ProcessCustomerRatePlanChangeAsync(context, logRepo, bulkChange, sqlRetryPolicy);
    return false;
```

**Customer Rate Plan Processing Logic:**

1. **Change Request Parsing**: Extract customer rate plan update details
2. **Effective Date Evaluation**: Determine immediate vs scheduled processing
3. **Database Operation Selection**: Route to appropriate processing method
4. **POND Integration Handling**: Apply POND-specific customer rate plan logic

### Phase 5: POND-Specific Customer Rate Plan Processing
Located at lines 2105-2167 in AltaworxDeviceBulkChange.cs

**Processing Flow:**

1. **Extract Customer Rate Plan Details:**
   ```csharp
   var changeRequest = JsonConvert.DeserializeObject<BulkChangeRequest>(change.ChangeRequest);
   var customerRatePlanId = changeRequest?.CustomerRatePlanUpdate?.CustomerRatePlanId;
   var customerRatePoolId = changeRequest?.CustomerRatePlanUpdate?.CustomerPoolId;
   var effectiveDate = changeRequest?.CustomerRatePlanUpdate?.EffectiveDate;
   var customerDataAllocationMB = changeRequest?.CustomerRatePlanUpdate?.CustomerDataAllocationMB;
   ```

2. **Effective Date Processing Logic:**
   ```csharp
   if (effectiveDate == null || effectiveDate?.ToUniversalTime() <= DateTime.UtcNow)
   {
       // Immediate Processing - Execute stored procedure immediately
       dbResult = await ProcessCustomerRatePlanChangeAsync(bulkChange.Id, 
           customerRatePlanId, effectiveDate, customerDataAllocationMB, 
           customerRatePoolId, connectionString, logger, syncPolicy);
   }
   else
   {
       // Scheduled Processing - Add to queue for future execution
       dbResult = await ProcessAddCustomerRatePlanChangeToQueueAsync(bulkChange, 
           customerRatePlanId, effectiveDate, customerDataAllocationMB, 
           customerRatePoolId, context);
   }
   ```

3. **POND Customer Rate Plan Management:**

   **Immediate Processing:**
   - Execute stored procedure: `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices`
   - Update device customer rate plan associations
   - Apply data allocation changes
   - Update customer pool assignments
   - Create audit trail entries

   **Scheduled Processing:**
   - Insert records into CustomerRatePlanDeviceQueue table
   - Store effective date for future processing
   - Maintain customer rate plan change requests
   - Schedule background processing

### Phase 6: Database Operations & POND Integration

**For POND IoT Service Provider:**

**Immediate Processing Flow:**
```csharp
// From AltaworxDeviceBulkChange.cs lines 2240-2290
private static async Task<DeviceChangeResult<string, string>> ProcessCustomerRatePlanChangeAsync(
    long bulkChangeId, int? customerRatePlanId, DateTime? effectiveDate, 
    decimal? customerDataAllocationMB, int? customerRatePoolId, 
    string connectionString, IKeysysLogger logger, ISyncPolicy syncPolicy)
```

**Database Operations:**
```sql
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = @bulkChangeId,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @customerDataAllocationMB = @customerDataAllocationMB,
    @effectiveDate = @effectiveDate,
    @needToMarkProcessed = @needToMarkProcessed
```

**Scheduled Processing Flow:**
```csharp
// From AltaworxDeviceBulkChange.cs lines 2168-2240
private static async Task<DeviceChangeResult<string, string>> ProcessAddCustomerRatePlanChangeToQueueAsync(
    BulkChange bulkChange, int? customerRatePlanId, DateTime? effectiveDate, 
    decimal? customerDataAllocationMB, int? customerRatePoolId, 
    KeySysLambdaContext context)
```

**Queue Management:**
- Create DataTable with device change records
- Bulk insert into CustomerRatePlanDeviceQueue table
- Store scheduled processing parameters
- Maintain effective date tracking

### Phase 7: Response & Logging

**Portal-Specific Logging:**

**M2M Portal Integration:**
```csharp
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    M2MDeviceChangeId = change.Id,
    LogEntryDescription = "Change Customer Rate Plan: Update AMOP",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
    ResponseText = dbResult.ResponseObject,
    HasErrors = dbResult.HasErrors,
    ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
    ProcessedDate = DateTime.UtcNow
});
```

**Mobility Portal Integration:**
```csharp
logRepo.AddMobilityLogEntry(new CreateMobilityDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    MobilityDeviceChangeId = change.Id,
    LogEntryDescription = "Change Customer Rate Plan: Update AMOP",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
    ResponseText = dbResult.ResponseObject,
    HasErrors = dbResult.HasErrors,
    ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
    ProcessedDate = DateTime.UtcNow
});
```

## Key Database Operations

**Tables Updated:**
- DeviceBulkChange: Main bulk change record
- M2M_DeviceChange: Individual device change records
- DeviceBulkChangeLog: Audit trail entries
- Device_Tenant: Device customer rate plan associations
- CustomerRatePlanDeviceQueue: Scheduled change queue (for future effective dates)
- CustomerRatePlan: Customer rate plan reference data
- CustomerPool: Customer pool assignments

**POND-Specific Operations:**
- Customer rate plan assignment updates
- Data allocation limit modifications
- Customer pool membership changes
- Billing integration updates
- Service configuration adjustments

## Data Models

**Customer Rate Plan Update Structure:**
```csharp
public class BulkChangeCustomerRatePlanUpdate
{
    public int? CustomerRatePlanId { get; set; }
    public decimal? CustomerDataAllocationMB { get; set; }
    public int? CustomerPoolId { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
```

**Bulk Change Request Structure:**
```csharp
public class BulkChangeRequest
{
    public int? ServiceProviderId { get; set; }
    public int? ChangeType { get; set; } // CustomerRatePlanChange = 4
    public bool? ProcessChanges { get; set; }
    public string[] Devices { get; set; }
    public BulkChangeCustomerRatePlanUpdate CustomerRatePlanUpdate { get; set; }
}
```

## Key Differences from Carrier Rate Plan Changes

**Customer Rate Plan (This Flow):**
- **Purpose**: Customer-facing billing plans and data allocation
- **Scope**: Tenant-specific, customer-controlled
- **Processing**: Database-driven with scheduled execution capability
- **Properties**: CustomerRatePlanId, CustomerDataAllocationMB, CustomerPoolId, EffectiveDate

**Carrier Rate Plan (Different Flow):**
- **Purpose**: Carrier-specific network connectivity plans
- **Scope**: Service provider managed
- **Processing**: API-driven carrier integrations
- **Properties**: CarrierRatePlan, CommPlan, PlanUuid, RatePlanId

## Error Handling & Validation

**Validation Errors:**
- Invalid customer rate plan ID
- Invalid data allocation values
- Missing required parameters
- Invalid effective date
- Customer pool assignment conflicts
- POND integration compatibility issues

**Processing Errors:**
- Database connection failures
- Stored procedure execution errors
- Transaction rollback scenarios
- Concurrent modification conflicts
- Queue processing failures

**Error Response Structure:**
```csharp
public class DeviceChangeResult<string, string>
{
    public string ActionText { get; set; }
    public bool HasErrors { get; set; }
    public string RequestObject { get; set; }
    public string ResponseObject { get; set; }
}
```

## Performance Considerations

**Immediate Processing:**
- Synchronous database operations
- Transaction-based consistency
- Real-time customer rate plan updates
- Immediate audit trail creation

**Scheduled Processing:**
- Queue-based future execution
- Batch processing capabilities
- Efficient resource utilization
- Scalable scheduling system

## Integration Points

**POND IoT Service Provider Integration:**
- Customer rate plan management
- Data allocation tracking
- Billing system integration
- Service configuration updates
- Portal-specific logging and audit trails

**Database Integration:**
- Customer rate plan tables
- Device assignment tables
- Queue management tables
- Audit and logging tables

**Portal Integration:**
- M2M Portal: Device management interface
- Mobility Portal: Mobile device management
- Customer Portal: Rate plan configuration and monitoring