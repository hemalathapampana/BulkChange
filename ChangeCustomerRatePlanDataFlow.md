# Change Customer Rate Plan Data Flow - Verizon ThingSpace IoT

## Overview

This document outlines the complete data flow for **Change Customer Rate Plan** functionality in the Verizon ThingSpace IoT system. The process handles both individual device updates and bulk device processing while maintaining comprehensive audit trails and system integrations.

## System Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   User Interface│    │  M2MController  │    │ Database Layer  │
│   (Device Grid) │───▶│  Validation &   │───▶│ Device_Tenant   │
│                 │    │  Processing     │    │ Updates         │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │                       ▼                       ▼
         │              ┌─────────────────┐    ┌─────────────────┐
         │              │ AltaworxDevice  │    │ DeviceAction    │
         └─────────────▶│ BulkChange      │───▶│ History         │
                        │ (Lambda)        │    │ (Audit Trail)   │
                        └─────────────────┘    └─────────────────┘
                                 │                       │
                                 ▼                       ▼
                        ┌─────────────────┐    ┌─────────────────┐
                        │ Integration     │    │ Optimization    │
                        │ Updates         │    │ System Sync     │
                        └─────────────────┘    └─────────────────┘
```

## Detailed Step-by-Step Data Flow

### **Phase 1: Individual Device Update (M2MController.cs)**

#### User Interface Actions
- **Device Selection**: User clicks on device in M2M Inventory grid
- **Rate Plan Selection**: User selects "Create Customer Rate Plan" from device options
- **Parameter Input**:
  - Select new `CustomerRatePlanId` from dropdown list
  - Optionally set `CustomerRatePoolId` for shared data pools
  - Set effective date for the rate plan change
  - Configure `CustomerDataAllocationMB` if required

#### Data Collection
```csharp
// Input parameters collected from UI
public ActionResult UpdateM2MCustomerRatePlan(
    int deviceId, 
    decimal? customerDataAllocationMB, 
    int? customerRatePlanId, 
    int? customerRatePoolId, 
    DateTime? effectiveDate, 
    string customerRatePlanName, 
    string customerRatePoolName)
```

**Required Data Points:**
- Device ID (target device identifier)
- Customer Rate Plan ID (new rate plan to apply)
- Customer Rate Pool ID (optional, for shared pools)
- Customer Data Allocation MB (data limit allocation)
- Effective Date (implementation timestamp)
- Current user session information for audit trail

### **Phase 2: Direct Processing (No Lambda for individual updates)**

#### Validation Steps
```csharp
// M2MController.UpdateM2MCustomerRatePlan method validation
var deviceRepository = new DeviceRepository(altaWrxDb);
var device = deviceRepository.GetDeviceById(deviceId);
if (device == null)
    return new JsonResult { Data = new { Success = false, Message = "Device not found." } };

var deviceTenant = altaWrxDb.Device_Tenant.FirstOrDefault(
    x => x.DeviceId == deviceId && x.TenantId == permissionManager.Tenant.id);
```

**Validation Checklist:**
- **Device Existence Check**: Verify device exists in Device table using DeviceRepository
- **Tenant Authorization**: Confirm Device_Tenant record exists for user's tenant
- **Rate Plan Validation**: Validate CustomerRatePlanId exists and is accessible to tenant
- **Date Validation**: Check effective date is valid (not in past for scheduled changes)
- **Permission Check**: Verify user has M2M module access permissions

#### Update Device_Tenant Record
```csharp
// Direct database updates in M2MController
if (customerRatePlanId != null && customerRatePlanId > 0)
{
    deviceTenant.CustomerRatePlanId = customerRatePlanId;
    deviceTenant.CustomerDataAllocationMB = customerDataAllocationMB;
}
else if (customerRatePlanId != CommonConstants.NO_CHANGE)
{
    deviceTenant.CustomerRatePlanId = null;
    deviceTenant.CustomerDataAllocationMB = null;
}

if (customerRatePoolId != null && customerRatePoolId >= 0)
{
    deviceTenant.CustomerRatePoolId = customerRatePoolId;
}
else if (customerRatePoolId != CommonConstants.NO_CHANGE)
{
    deviceTenant.CustomerRatePoolId = null;
}

deviceTenant.ModifiedDate = DateTime.UtcNow;
deviceTenant.ModifiedBy = SessionHelper.GetAuditByName(Session);

altaWrxDb.Entry(deviceTenant).State = EntityState.Modified;
altaWrxDb.SaveChanges();
```

**Database Fields Updated:**
- `CustomerRatePlanId` = new rate plan value
- `CustomerDataAllocationMB` = new data allocation
- `CustomerRatePoolId` = new pool assignment (if specified)
- `ModifiedDate` = current timestamp
- `ModifiedBy` = current user identifier

#### Create Audit Trail
```csharp
// DeviceActionHistory creation for Customer Rate Plan change
if (previousCustomerRatePlan != customerRatePlanName && customerRatePlanId != CommonConstants.NO_CHANGE)
{
    var deviceActionHistory = new DeviceActionHistory()
    {
        ServiceProviderId = device.ServiceProviderId,
        M2MDeviceId = device.id,
        ICCID = device.ICCID,
        MSISDN = device.MSISDN,
        PreviousValue = previousCustomerRatePlan,
        CurrentValue = customerRatePlanName,
        ChangedField = CommonStrings.CustomerRatePlan,
        ChangeEventType = CommonStrings.UpdateCustomerRatePlan,
        DateOfChange = DateTime.UtcNow,
        ChangedBy = SessionHelper.GetAuditByName(Session),
        Username = device.Username,
        CustomerAccountName = deviceTenant.Site.Name,
        CustomerAccountNumber = deviceTenant.AccountNumber,
        TenantId = permissionManager.Tenant.id,
        IsActive = true,
        IsDeleted = false
    };
    deviceActionHistories.Add(deviceActionHistory);
}
```

**Audit Record Fields:**
- `ChangedField` = "CustomerRatePlan"
- `PreviousValue` = old rate plan name
- `CurrentValue` = new rate plan name
- `DateOfChange` = current time
- `ChangedBy` = current user
- Device and tenant context information

### **Phase 3: Bulk Processing (For multiple devices)**

#### Bulk Change Decision Logic
```csharp
// AltaworxDeviceBulkChange.ProcessCustomerRatePlanChangeRequestAsync
var changeRequest = JsonConvert.DeserializeObject<BulkChangeRequest>(change.ChangeRequest);
var customerRatePlanId = changeRequest?.CustomerRatePlanUpdate?.CustomerRatePlanId;
var effectiveDate = changeRequest?.CustomerRatePlanUpdate?.EffectiveDate;

if (effectiveDate == null || effectiveDate?.ToUniversalTime() <= DateTime.UtcNow)
{
    // Immediate processing path
    dbResult = await ProcessCustomerRatePlanChangeAsync(bulkChange.Id, customerRatePlanId,
        effectiveDate, customerDataAllocationMB, customerRatePoolId, 
        context.CentralDbConnectionString, context.logger, syncPolicy);
}
else
{
    // Scheduled processing path
    dbResult = await ProcessAddCustomerRatePlanChangeToQueueAsync(bulkChange, customerRatePlanId,
        effectiveDate, customerDataAllocationMB, customerRatePoolId, context);
}
```

#### Immediate Bulk Processing
```csharp
// ProcessCustomerRatePlanChangeAsync method
cmd.CommandText = SQLConstant.StoredProcedureName.DEVICE_BULK_CHANGE_CUSTOMER_RATE_PLAN_CHANGE_UPDATE_DEVICE;
cmd.Parameters.AddWithValue(CommonSQLParameterNames.EFFECTIVE_DATE, effectiveDate ?? (object)DBNull.Value);
cmd.Parameters.AddWithValue(CommonSQLParameterNames.BULK_CHANGE_ID, bulkChangeId);
cmd.Parameters.AddWithValue(CommonSQLParameterNames.CUSTOMER_RATE_PLAN_ID, customerRatePlanId ?? (object)DBNull.Value);
cmd.Parameters.AddWithValue(CommonSQLParameterNames.CUSTOMER_RATE_POOL_ID, customerRatePoolId ?? (object)DBNull.Value);
cmd.Parameters.AddWithValue(CommonSQLParameterNames.CUSTOMER_DATA_ALLOCATION_MB, customerDataAllocationMB ?? (object)DBNull.Value);
cmd.Parameters.AddWithValue(CommonSQLParameterNames.NEED_TO_MARK_PROCESSED, needToMarkProcess);
```

**Immediate Processing Steps:**
- Create DeviceBulkChange record in database
- Execute stored procedure: `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices`
- Update multiple Device_Tenant records in single transaction
- Create bulk DeviceActionHistory records for audit trail
- Update cross-provider device history

#### Scheduled Bulk Processing
```csharp
// ProcessAddCustomerRatePlanChangeToQueueAsync method
DataTable table = new DataTable();
table.Columns.Add("Id");
table.Columns.Add("DeviceId");
table.Columns.Add("CustomerRatePlanId");
table.Columns.Add("CustomerRatePoolId");
table.Columns.Add("CustomerDataAllocationMB");
table.Columns.Add("EffectiveDate");
table.Columns.Add("PortalType");
table.Columns.Add("TenantId");
// ... populate queue table
```

**Scheduled Processing Steps:**
- Create entries in CustomerRatePlanDeviceQueue table
- Lambda function processes queue at scheduled time
- Execute `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber` for each device
- Generate bulk audit records upon completion

#### Logging Operations
```csharp
// M2M Portal logging
if (bulkChange.PortalTypeId == PortalTypeM2M)
{
    logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
    {
        BulkChangeId = bulkChange.Id,
        ErrorText = dbResult.HasErrors ? dbResult.ResponseObject : null,
        HasErrors = dbResult.HasErrors,
        LogEntryDescription = "Change Customer Rate Plan: Update AMOP",
        M2MDeviceChangeId = change.Id,
        ProcessBy = "AltaworxDeviceBulkChange",
        ProcessedDate = DateTime.UtcNow,
        ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
        RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
        ResponseText = dbResult.ResponseObject
    });
}
```

### **Phase 4: Integration Updates**

#### Cross-Provider History Update
```csharp
// Update cross-provider device history
altaWrxDb.usp_UpdateCrossProviderDeviceHistory(
    deviceTenant.DeviceId.ToString(), 
    string.Empty, 
    (int)PortalTypeEnum.M2M, 
    deviceTenant.TenantId, 
    device.ServiceProviderId, 
    effectiveDate
);
```

**Cross-Provider Integration:**
- Update device history across multiple service providers
- Maintain consistency in multi-carrier environments
- Track rate plan changes for billing reconciliation

#### Optimization System Notification
```csharp
// Send notification to optimization system 2.0
int? tenantId = permissionManager.Tenant.id;
OptimizationApiController optimizationApiController = new OptimizationApiController();
optimizationApiController.SendTriggerAmopSync("m2m_inventory_live_sync", tenantId, null);
```

**Integration Trigger Details:**
- **Trigger Type**: "m2m_inventory_live_sync"
- **Payload**: Tenant ID and device information
- **Purpose**: Synchronize customer rate plan changes with optimization systems
- **Target**: Real-time inventory synchronization between AMOP 1.0 and 2.0

#### User Action Logging
```csharp
UserActionRepository.LogAction(
    SessionHelper.LoggedInUser(Session), 
    "M2M", 
    $"Updated Customer Rate Plan to {customerRatePlanId} for {deviceId}"
);
```

## Data Models

### BulkChangeCustomerRatePlanUpdate
```csharp
public class BulkChangeCustomerRatePlanUpdate
{
    public int? CustomerRatePlanId { get; set; }
    public decimal? CustomerDataAllocationMB { get; set; }
    public int? CustomerPoolId { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
```

### Device_Tenant (Key Database Table)
```csharp
// Key fields updated during rate plan change
public class Device_Tenant
{
    public int DeviceId { get; set; }
    public int TenantId { get; set; }
    public int? CustomerRatePlanId { get; set; }
    public decimal? CustomerDataAllocationMB { get; set; }
    public int? CustomerRatePoolId { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string ModifiedBy { get; set; }
    // ... other fields
}
```

### DeviceActionHistory (Audit Trail)
```csharp
public class DeviceActionHistory
{
    public string ChangedField { get; set; }          // "CustomerRatePlan"
    public string PreviousValue { get; set; }         // Old rate plan name
    public string CurrentValue { get; set; }          // New rate plan name
    public string ChangeEventType { get; set; }       // "UpdateCustomerRatePlan"
    public DateTime DateOfChange { get; set; }        // Timestamp
    public string ChangedBy { get; set; }            // User identifier
    public int TenantId { get; set; }                // Tenant context
    // ... device context fields
}
```

## Database Operations

### Stored Procedures Used

#### Individual Device Update
```sql
-- For single device processing
EXEC dbo.usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber
    @bulkChangeId = @bulkChangeId,
    @subscriberNumber = @subscriberNumber,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @effectiveDate = @effectiveDate,
    @customerDataAllocationMB = @customerDataAllocationMB
```

#### Bulk Device Update
```sql
-- For multiple device processing
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = @bulkChangeId,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @customerDataAllocationMB = @customerDataAllocationMB,
    @effectiveDate = @effectiveDate,
    @needToMarkProcessed = @needToMarkProcessed
```

#### Cross-Provider History Update
```sql
-- For maintaining cross-provider consistency
EXEC usp_UpdateCrossProviderDeviceHistory
    @deviceIds = @deviceIdList,
    @changeType = '',
    @portalType = @portalTypeM2M,
    @tenantId = @tenantId,
    @serviceProviderId = @serviceProviderId,
    @effectiveDate = @effectiveDate
```

## Error Handling

### Validation Errors
- **Invalid Device**: Device not found or not accessible to tenant
- **Invalid Rate Plan**: CustomerRatePlanId does not exist or not authorized
- **Invalid Date**: Effective date in the past or invalid format
- **Permission Denied**: User lacks M2M module access
- **Missing Parameters**: Required fields not provided

### Processing Errors
- **Database Connection**: Connection string invalid or database unavailable
- **Transaction Failure**: Rollback on partial update failures
- **Stored Procedure Error**: SQL execution errors or constraint violations
- **Concurrent Modification**: Optimistic concurrency conflicts

### Error Response Structure
```csharp
public class DeviceChangeResult<string, string>
{
    public string ActionText { get; set; }        // Action attempted
    public bool HasErrors { get; set; }           // Error flag
    public string RequestObject { get; set; }     // Input parameters
    public string ResponseObject { get; set; }    // Result or error message
}
```

## Security and Compliance

### Authorization Controls
- **Tenant Isolation**: Users can only modify devices in their tenant
- **Role-Based Access**: M2M module permissions required
- **Rate Plan Visibility**: Only authorized rate plans visible to tenant
- **Audit Trail**: Complete change tracking for compliance

### Data Protection
- **Encrypted Connections**: Database connections use encryption
- **Sanitized Logging**: No sensitive data in log files
- **Input Validation**: All parameters validated before processing
- **SQL Injection Prevention**: Parameterized queries used throughout

## Performance Considerations

### Optimization Strategies
- **Batch Processing**: Bulk updates for multiple devices
- **Connection Pooling**: Efficient database connection management
- **Async Operations**: Non-blocking database operations
- **Transaction Batching**: Minimize database round trips

### Monitoring and Metrics
- **Processing Time**: Track operation duration
- **Error Rates**: Monitor failure percentages
- **Throughput**: Devices processed per minute
- **Queue Depth**: Scheduled change backlog

## Integration Points

### Internal Systems
- **AMOP 1.0**: Primary portal interface
- **AMOP 2.0**: Optimization system integration
- **Rev Customer Service**: Customer management
- **Billing Systems**: Rate plan billing integration

### External APIs
- **Verizon ThingSpace**: Carrier integration
- **Optimization Services**: Real-time sync triggers
- **Notification Systems**: Alert and email services

## Future Enhancements

### Planned Improvements
1. **Real-time Validation**: Immediate rate plan availability checking
2. **Advanced Scheduling**: Recurring rate plan changes
3. **Bulk Import**: CSV/Excel file processing for mass updates
4. **API Endpoints**: RESTful API for external integrations
5. **Analytics Dashboard**: Rate plan change analytics and reporting

### Scalability Roadmap
1. **Microservice Architecture**: Service decomposition for scalability
2. **Event-Driven Processing**: Event sourcing implementation
3. **Caching Layer**: Rate plan data caching for performance
4. **Load Balancing**: Processing load distribution across instances