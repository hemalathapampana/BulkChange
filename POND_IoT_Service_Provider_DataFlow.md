# POND IoT Service Provider - Data Flow Documentation

## Overview

This document outlines the comprehensive data flow for the **POND IoT Service Provider** covering four primary change types across five distinct processing phases. The POND IoT platform provides device management capabilities through a robust, scalable architecture that handles bulk device operations efficiently.

### Supported Change Types
1. **Assign Customer** - Associate devices with specific customers/tenants
2. **Change Carrier Rate Plan** - Update carrier-level rate plans for connectivity
3. **Change Customer Rate Plan** - Update customer-facing billing rate plans  
4. **Update Device Status** - Modify device operational status (Active, Inactive, etc.)

### Processing Phases
1. **User Request & Validation** - Initial request processing and input validation
2. **Controller Validation (M2MController.cs)** - Business logic validation and bulk change creation
3. **Queue Processing** - SQS message handling and asynchronous processing
4. **Lambda Processing (AltaworxDeviceBulkChange.cs)** - Core business logic execution
5. **Response & Logging** - Result processing and audit trail creation

---

## Whole Flow Architecture

```
┌─────────────────────┐    ┌─────────────────────┐    ┌─────────────────────┐
│   User Request      │───▶│  Controller         │───▶│  SQS Queue          │
│   (Web/API)         │    │  Validation         │    │  Processing         │
│   Phase 1           │    │  Phase 2            │    │  Phase 3            │
└─────────────────────┘    └─────────────────────┘    └─────────────────────┘
                                      │                          │
                                      ▼                          ▼
┌─────────────────────┐    ┌─────────────────────┐    ┌─────────────────────┐
│   Response &        │◀───│  Lambda Processing  │◀───│  Queue Message      │
│   Logging           │    │  (AltaworxDevice    │    │  Dequeue            │
│   Phase 5           │    │  BulkChange.cs)     │    │                     │
└─────────────────────┘    │  Phase 4            │    └─────────────────────┘
                           └─────────────────────┘
```

---

## Process Flow Details

### Phase 1: User Request & Validation

#### 1.1 Request Entry Points
- **M2M Portal**: Web-based device management interface
- **API Endpoints**: Programmatic bulk change requests
- **Upload Interface**: CSV/Excel file uploads for bulk operations

#### 1.2 Initial Request Structure
```csharp
public class BulkChangeRequest
{
    public int? ServiceProviderId { get; set; }
    public int? ChangeType { get; set; }
    public bool? ProcessChanges { get; set; }
    public string[] Devices { get; set; }
    
    // Change Type Specific Properties
    public BulkChangeCustomerRatePlanUpdate CustomerRatePlanUpdate { get; set; }
    public CarrierRatePlanUpdate CarrierRatePlanUpdate { get; set; }
    public BulkChangeStatusUpdate StatusUpdate { get; set; }
    public BulkChangeAssociateCustomer CustomerAssignment { get; set; }
}
```

#### 1.3 Change Type Constants
```csharp
public static class ChangeRequestType
{
    public const string CustomerAssignment = "customerassignment";
    public const string CarrierRatePlanChange = "carrierrateplanchange"; 
    public const string CustomerRatePlanChange = "customerrateplanchange";
    public const string StatusUpdate = "statusupdate";
}
```

### Phase 2: Controller Validation (M2MController.cs)

#### 2.1 Authentication & Authorization
```csharp
// Permission validation
if (permissionManager.UserCannotAccess(Session, ModuleEnum.M2M))
    return RedirectToAction("Index", "Home");

// Tenant scope validation
var serviceProvider = permissionManager.GetUserServiceProvider();
```

#### 2.2 Device Validation
- **Device Existence**: Verify devices exist in tenant scope
- **Status Compatibility**: Check current status vs target status
- **Rate Plan Compatibility**: Validate rate plan assignments
- **Customer Assignment**: Verify customer/site relationships

#### 2.3 Business Rule Validation

##### For Assign Customer:
```csharp
private static IEnumerable<M2M_DeviceChange> BuildCustomerAssignmentDetails(...)
{
    // Validate RevCustomer exists
    var revCustomer = revCustomers.FirstOrDefault(x => x.RevCustomerId.Contains(changeRequest.RevCustomerId));
    if (revCustomer == null)
        throw new ValidationException("Rev Customer not found");
    
    // Validate site assignment
    var site = sites.FirstOrDefault(x => x.RevCustomerId == revCustomer.id);
    if (site == null)
        throw new ValidationException("Site not found for customer");
}
```

##### For Carrier Rate Plan:
```csharp
if (changeType.Equals(DeviceChangeType.CarrierRatePlanChange))
{
    var carrierRatePlanCode = bulkChange.CarrierRatePlanUpdate.CarrierRatePlan;
    var carrierRatePlan = carrierRatePlanRepository.GetByCode(carrierRatePlanCode);
    
    bulkChange.CarrierRatePlanUpdate.PlanUuid = carrierRatePlan.PlanUuid;
    bulkChange.CarrierRatePlanUpdate.RatePlanId = carrierRatePlan.JasperRatePlanId.Value;
}
```

##### For Customer Rate Plan:
```csharp
// Validate customer rate plan exists and is accessible
var customerRatePlan = customerRatePlanRepository.GetById(customerRatePlanId);
if (customerRatePlan == null || customerRatePlan.TenantId != permissionManager.Tenant.id)
    throw new ValidationException("Invalid customer rate plan");
```

##### For Device Status:
```csharp
// Validate status transition is allowed
var allowedStatuses = GetAllowedStatusTransitions(currentStatus, integrationType);
if (!allowedStatuses.Contains(targetStatus))
    throw new ValidationException($"Status transition from {currentStatus} to {targetStatus} not allowed");
```

#### 2.4 Bulk Change Creation
```csharp
var bulkChange = new DeviceBulkChange
{
    ServiceProviderId = serviceProviderId,
    ChangeRequestTypeId = (int)changeType,
    Status = BulkChangeStatus.PENDING,
    CreatedBy = SessionHelper.GetAuditByName(Session),
    CreatedDate = DateTime.UtcNow,
    PortalTypeId = (int)PortalTypes.M2M,
    TenantId = permissionManager.Tenant.id,
    SiteId = GetSiteIdForBulkChange(deviceChanges)
};
```

### Phase 3: Queue Processing

#### 3.1 SQS Message Structure
```csharp
public class SqsValues
{
    public long BulkChangeId { get; set; }
    public long M2MDeviceChangeId { get; set; }
    public bool IsRetryNewActivateThingSpaceDevice { get; set; }
    public bool IsFromAutomatedUpdateDeviceStatusLambda { get; set; }
    public bool IsRetryUpdateIdentifier { get; set; }
    public int RetryNumber { get; set; }
    public string RequestId { get; set; }
}
```

#### 3.2 Queue Enqueuing Process
```csharp
internal static async Task<object> ProcessBulkChange(...)
{
    if (bulkChange.M2M_DeviceChange.Any(change => !change.IsProcessed))
    {
        var queueName = ValueFromCustomObjects(customObjectDbList, CommonConstants.CUSTOM_OBJECT_BULK_CHANGE_QUEUE_KEY);
        var sqsHelper = new SqsHelper(awsAccessKey, awsSecretAccessKey);
        var errorMessage = await sqsHelper.EnqueueBulkChangeAsync(queueName, id);
        
        bulkChange.Status = BulkChangeStatus.PROCESSING;
    }
}
```

#### 3.3 Message Attributes
- `BULK_CHANGE_ID`: Primary identifier for the bulk change
- `M2M_DEVICE_CHANGE_ID`: Individual device change identifier
- `RETRY_NUMBER`: Current retry attempt count
- `IS_RETRY_*`: Retry-specific flags for different operations
- `REQUEST_ID`: External API request tracking identifier

#### 3.4 Queue Processing Patterns
- **Immediate Processing**: For changes with no effective date or past effective date
- **Scheduled Processing**: For future-dated changes (queued to device-specific queue tables)
- **Retry Processing**: For failed operations with exponential backoff
- **Batch Processing**: Multiple device changes processed in paginated batches

### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)

#### 4.1 Lambda Entry Point
```csharp
public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
{
    var keysysContext = new KeySysLambdaContext(context);
    
    if (sqsEvent.Records.Count == 1)
    {
        return await ProcessEventRecordAsync(keysysContext, sqsEvent.Records[0]);
    }
}
```

#### 4.2 Change Type Processing Switch
```csharp
switch (bulkChange.ChangeRequestType.ToLowerInvariant())
{
    case ChangeRequestType.StatusUpdate:
        return await ProcessStatusUpdateAsync(context, logRepo, bulkChange, changes, retryNumber);
    case ChangeRequestType.CustomerAssignment:
        return await ProcessAssociateCustomerAsync(context, logRepo, bulkChange, changes);
    case ChangeRequestType.CarrierRatePlanChange:
        return await ProcessCarrierRatePlanChangeAsync(context, logRepo, bulkChange, sqlRetryPolicy);
    case ChangeRequestType.CustomerRatePlanChange:
        await ProcessCustomerRatePlanChangeAsync(context, logRepo, bulkChange, sqlRetryPolicy);
        return false;
}
```

#### 4.3 Change Type Specific Processing

##### 4.3.1 Assign Customer Processing
```csharp
private async Task ProcessAssociateCustomerAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, List<BulkChangeDetailRecord> changes)
{
    var changeRequest = GetBulkChangeRequest(context, bulkChange.Id, bulkChange.PortalTypeId);
    var request = JsonConvert.DeserializeObject<BulkChangeAssociateCustomer>(changeRequest);
    
    if (string.IsNullOrEmpty(request?.RevCustomerId))
    {
        // Process non-Rev customer assignment
        await bulkChangeRepository.UpdateAMOPCustomer(context, logRepo, changes, bulkChange);
    }
    else
    {
        // Process Rev customer association with service creation
        foreach (var change in changes)
        {
            var dbResult = await ProcessRevCustomerAssignment(context, change, request);
            LogAssignmentResult(logRepo, bulkChange, change, dbResult);
        }
    }
}
```

**Database Operations:**
- Execute `usp_DeviceBulkChange_Assign_Non_Rev_Customer` for AMOP assignments
- Create Rev services via stored procedures for Rev customer assignments
- Update device-tenant relationships and site assignments

##### 4.3.2 Change Carrier Rate Plan Processing
```csharp
private async Task<bool> ProcessCarrierRatePlanChangeAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, ISyncPolicy sqlRetryPolicy)
{
    var changes = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, PageSize);
    
    foreach (var change in changes)
    {
        var carrierRatePlan = JsonConvert.DeserializeObject<BulkChangeCarrierRatePlanUpdate>(change.ChangeRequest);
        
        switch (bulkChange.IntegrationId)
        {
            case (int)IntegrationType.Jasper:
                await ProcessJasperCarrierRatePlanChange(context, logRepo, bulkChange, change, carrierRatePlan);
                break;
            case (int)IntegrationType.ThingSpace:
                await ProcessThingSpaceCarrierRatePlanChange(context, logRepo, bulkChange, change, carrierRatePlan);
                break;
            case (int)IntegrationType.Pond:
                await ProcessPondCarrierRatePlanChange(context, logRepo, bulkChange, change, carrierRatePlan);
                break;
            case (int)IntegrationType.Telegence:
                await ProcessTelegenceCarrierRatePlanChange(context, logRepo, bulkChange, change, carrierRatePlan);
                break;
        }
    }
}
```

**Integration-Specific Processing:**
- **Jasper**: API calls to update device rate plan
- **ThingSpace**: Status updates with rate plan changes
- **POND**: Package termination and new package creation
- **Telegence**: Subscriber update requests

##### 4.3.3 Change Customer Rate Plan Processing
```csharp
private async Task ProcessCustomerRatePlanChangeAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, ISyncPolicy sqlRetryPolicy)
{
    var change = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, 1).FirstOrDefault();
    var changeRequest = JsonConvert.DeserializeObject<BulkChangeCustomerRatePlanUpdate>(change.ChangeRequest);
    
    var customerRatePlanId = changeRequest.CustomerRatePlanUpdate?.CustomerRatePlanId;
    var customerDataAllocationMB = changeRequest.CustomerRatePlanUpdate?.CustomerDataAllocationMB;
    var customerRatePoolId = changeRequest.CustomerRatePlanUpdate?.CustomerPoolId;
    var effectiveDate = changeRequest.CustomerRatePlanUpdate?.EffectiveDate;
    
    DeviceChangeResult<string, string> dbResult;
    
    if (effectiveDate == null || effectiveDate?.ToUniversalTime() <= DateTime.UtcNow)
    {
        // Immediate processing
        dbResult = await ProcessCustomerRatePlanChangeAsync(bulkChange.Id, customerRatePlanId, effectiveDate, 
            customerDataAllocationMB, customerRatePoolId, context.CentralDbConnectionString, context.logger, sqlRetryPolicy);
    }
    else
    {
        // Schedule for future processing
        dbResult = await ProcessAddCustomerRatePlanChangeToQueueAsync(bulkChange, customerRatePlanId, 
            effectiveDate, customerDataAllocationMB, customerRatePoolId, context);
    }
}
```

**Database Operations:**
- Execute `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices` for immediate changes
- Insert into `CustomerRatePlanDeviceQueue` table for scheduled changes
- Execute `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber` for individual devices

##### 4.3.4 Update Device Status Processing
```csharp
private async Task<bool> ProcessStatusUpdateAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, List<BulkChangeDetailRecord> changes, int retryNumber)
{
    foreach (var change in changes)
    {
        var changeRequest = JsonConvert.DeserializeObject<BulkChangeStatusUpdateRequest<dynamic>>(change.ChangeRequest);
        
        switch (bulkChange.IntegrationId)
        {
            case (int)IntegrationType.Jasper:
                var apiResult = await UpdateJasperDeviceStatusAsync(context, jasperAuthentication, change.ICCID, changeRequest.UpdateStatus);
                await ProcessJasperStatusUpdateResult(context, logRepo, bulkChange, change, apiResult);
                break;
                
            case (int)IntegrationType.ThingSpace:
                var thingSpaceResult = await UpdateThingSpaceDeviceStatusAsync(context, logRepo, thingSpaceAuthentication, 
                    accessToken, sessionToken, bulkChange, change, changeRequest);
                await ProcessThingSpaceStatusUpdateResult(context, logRepo, bulkChange, change, thingSpaceResult);
                break;
                
            case (int)IntegrationType.Pond:
                var pondResult = await UpdatePondDeviceStatusAsync(context, logRepo, pondAuthentication, 
                    bulkChange, change, changeRequest);
                await ProcessPondStatusUpdateResult(context, logRepo, bulkChange, change, pondResult);
                break;
                
            case (int)IntegrationType.Telegence:
                var telegenceResult = await UpdateTelegenceDeviceStatusAsync(context.logger, logRepo, bulkChange, 
                    change, changeRequest);
                await ProcessTelegenceStatusUpdateResult(context, logRepo, bulkChange, change, telegenceResult);
                break;
        }
    }
}
```

**Status Update Flows:**
- **Activation**: Device inventory → pending activation → active
- **Deactivation**: Active → deactivated (with communication plan checks)
- **Suspension**: Active → suspended (temporary)
- **Restoration**: Suspended → active

#### 4.4 Error Handling & Retry Logic
```csharp
// SQL retry policy for transient failures
var sqlRetryPolicy = Policy
    .Handle<SqlException>(ex => IsTransientError(ex))
    .WaitAndRetryAsync(SQL_TRANSIENT_RETRY_MAX_COUNT, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// HTTP retry policy for API calls
var httpRetryPolicy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(HTTP_RETRY_MAX_COUNT, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
```

#### 4.5 Integration-Specific Processing

##### 4.5.1 Jasper Integration
- **Authentication**: Session-based API authentication
- **Rate Plan Updates**: Direct API calls to Jasper platform
- **Status Updates**: Device activation/deactivation through Jasper API
- **Error Handling**: Jasper-specific error codes and retry logic

##### 4.5.2 ThingSpace Integration  
- **Authentication**: OAuth2 token-based authentication
- **Device Management**: Bulk device operations via ThingSpace API
- **Status Tracking**: Asynchronous status updates with callback handling
- **Rate Plan Management**: Integrated with device status changes

##### 4.5.3 POND Integration
- **Package Management**: Terminate existing packages, create new ones
- **Device Status**: POND-specific status management
- **API Integration**: RESTful API calls with POND authentication
- **Carrier Rate Plans**: Package-based rate plan management

##### 4.5.4 Telegence Integration
- **Subscriber Management**: Subscriber-based device operations
- **Batch Processing**: Multiple devices per API request (up to 200)
- **Status Updates**: Telegence-specific status transitions
- **IP Provisioning**: Additional IP provisioning for new activations

### Phase 5: Response & Logging

#### 5.1 Logging Architecture
```csharp
public class DeviceBulkChangeLogRepository
{
    public void AddM2MLogEntry(CreateM2MDeviceBulkChangeLog logEntry)
    public void AddMobilityLogEntry(CreateMobilityDeviceBulkChangeLog logEntry)
    public void AddLNPLogEntry(CreateLNPDeviceBulkChangeLog logEntry)
}
```

#### 5.2 Portal-Specific Logging

##### 5.2.1 M2M Portal Logging
```csharp
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    M2MDeviceChangeId = change.Id,
    LogEntryDescription = GetLogDescription(operationType),
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
    ResponseText = dbResult.ResponseObject,
    HasErrors = dbResult.HasErrors,
    ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED
});
```

##### 5.2.2 Mobility Portal Logging
```csharp
logRepo.AddMobilityLogEntry(new CreateMobilityDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    MobilityDeviceChangeId = change.Id,
    LogEntryDescription = GetLogDescription(operationType),
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = serializedRequest,
    ResponseText = serializedResponse,
    HasErrors = hasErrors,
    ResponseStatus = hasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED
});
```

#### 5.3 Operation-Specific Log Descriptions

| Change Type | Log Description Template |
|-------------|-------------------------|
| Assign Customer | "Assign Customer: {Operation}" |
| Carrier Rate Plan | "Change Carrier Rate Plan: {Integration} {Operation}" |
| Customer Rate Plan | "Change Customer Rate Plan: {Operation}" |
| Device Status | "Update Device Status: {Integration} {Operation}" |

#### 5.4 Response Data Structure
```csharp
public class DeviceChangeResult<TRequest, TResponse>
{
    public string ActionText { get; set; }
    public bool HasErrors { get; set; }
    public string RequestObject { get; set; }
    public string ResponseObject { get; set; }
    public TRequest Request { get; set; }
    public TResponse Response { get; set; }
}
```

#### 5.5 Audit Trail Components
- **Request Tracking**: Complete request payloads
- **Response Tracking**: Full API responses and database results
- **Error Tracking**: Detailed error messages and stack traces
- **Performance Tracking**: Processing times and retry counts
- **User Tracking**: User context and session information

#### 5.6 Status Update Workflow
```csharp
private async Task UpdateBulkChangeStatus(KeySysLambdaContext context, long bulkChangeId)
{
    var remainingChanges = GetUnprocessedChanges(context, bulkChangeId);
    
    if (!remainingChanges.Any())
    {
        // All changes processed - determine final status
        var hasErrors = HasAnyErrors(context, bulkChangeId);
        var finalStatus = hasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED;
        
        await UpdateBulkChangeStatus(context, bulkChangeId, finalStatus);
    }
    else
    {
        // Keep processing status for remaining changes
        await UpdateBulkChangeStatus(context, bulkChangeId, BulkChangeStatus.PROCESSING);
    }
}
```

---

## Change Type Specific Flows

### 1. Assign Customer Flow

```
User Request (Customer Assignment)
           │
           ▼
┌─────────────────────┐
│ Validate Customer   │
│ - RevCustomer ID    │
│ - Site Assignment   │
│ - Tenant Access     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Create Bulk Change  │
│ - ChangeType: 10    │
│ - Portal: M2M       │
│ - Status: PENDING   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Queue Processing    │
│ - SQS Message      │
│ - Batch Size: 100   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Lambda Processing   │
│ - Rev Service Create│
│ - Device Assignment │
│ - Site Association  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Database Updates    │
│ - Device_Tenant     │
│ - RevService        │
│ - Site Assignment   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Audit Logging       │
│ - Success/Error     │
│ - Response Details  │
└─────────────────────┘
```

### 2. Change Carrier Rate Plan Flow

```
User Request (Carrier Rate Plan)
           │
           ▼
┌─────────────────────┐
│ Validate Rate Plan  │
│ - Plan Exists       │
│ - Integration Match │
│ - Device Compatible │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Integration Router  │
│ ┌─────────────────┐ │
│ │ Jasper         │ │
│ │ ThingSpace     │ │
│ │ POND           │ │
│ │ Telegence      │ │
│ └─────────────────┘ │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ API Processing      │
│ - Authentication   │
│ - Rate Plan Update │
│ - Error Handling   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Database Sync       │
│ - Local Updates    │
│ - Status Tracking  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Result Processing   │
│ - Success/Failure  │
│ - Retry Logic      │
└─────────────────────┘
```

### 3. Change Customer Rate Plan Flow

```
User Request (Customer Rate Plan)
           │
           ▼
┌─────────────────────┐
│ Effective Date      │
│ Check               │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Immediate    │    │ Scheduled    │
│ Processing   │    │ Processing   │
│ (Now/Past)   │    │ (Future)     │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Execute SP   │    │ Queue Insert │
│ Update       │    │ Device_      │
│ Devices      │    │ CustomerRate │
│              │    │ PlanQueue    │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Database     │    │ Scheduled    │
│ Update       │    │ Processing   │
│ Complete     │    │ (Future Run) │
└──────┬───────┘    └──────────────┘
       │
       ▼
┌──────────────┐
│ Audit Log    │
│ Success      │
└──────────────┘
```

### 4. Update Device Status Flow

```
User Request (Status Update)
           │
           ▼
┌─────────────────────┐
│ Status Validation   │
│ - Current Status    │
│ - Target Status     │
│ - Transition Rules  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Integration Routing │
│ Based on Service    │
│ Provider Type       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Pre-Processing      │
│ - Communication     │
│   Plan Check        │
│ - Rate Plan Lookup  │
│ - Site Assignment   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ API Call Execution  │
│ - Device Status API │
│ - Async Processing  │
│ - Callback Handling │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Post-Processing     │
│ - Database Update   │
│ - Rev Customer      │
│   Association       │
│ - Usage Tracking    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Status Confirmation │
│ - Success/Failure   │
│ - Retry if Needed   │
└─────────────────────┘
```

---

## Integration-Specific Considerations

### Jasper Integration
- **Session Management**: Maintains session state across requests
- **Rate Limiting**: Respects API rate limits with exponential backoff
- **Communication Plans**: Validates communication plan requirements for deactivation
- **Device Status Mapping**: Maps internal status to Jasper-specific status codes

### ThingSpace Integration
- **OAuth2 Authentication**: Token-based authentication with refresh capability
- **Asynchronous Processing**: Status updates processed asynchronously with callbacks
- **Bulk Operations**: Supports bulk device operations for efficiency
- **MDN Assignment**: Automatic MDN assignment for activated devices

### POND Integration
- **Package Management**: Package-based rate plan system
- **Termination Logic**: Terminates existing packages before creating new ones
- **Status Synchronization**: Maintains status sync between POND and AMOP
- **Device Groups**: Supports device grouping for bulk operations

### Telegence Integration
- **Subscriber Model**: Device management through subscriber abstraction
- **Batch Processing**: Processes up to 200 devices per API request
- **IP Provisioning**: Additional IP provisioning workflow for new activations
- **Activation Status**: Polling-based activation status checking

---

## Error Handling & Recovery

### Retry Mechanisms
- **SQL Retry**: Transient database error retry with exponential backoff
- **HTTP Retry**: Network failure retry for API calls
- **Business Logic Retry**: Operation-specific retry for business failures

### Error Classification
- **Validation Errors**: Input validation failures (no retry)
- **Transient Errors**: Temporary failures (automatic retry)
- **Business Errors**: Business rule violations (manual intervention)
- **System Errors**: Infrastructure failures (escalation required)

### Recovery Procedures
- **Manual Retry**: Failed changes can be manually retried from the portal
- **Bulk Reprocessing**: Entire bulk changes can be resubmitted
- **Partial Recovery**: Individual device changes can be corrected and reprocessed
- **Data Correction**: Database corrections for inconsistent states

---

## Performance & Scalability

### Processing Metrics
- **Batch Size**: 100 devices per processing batch (configurable)
- **Concurrent Processing**: Multiple Lambda instances for parallel processing
- **Queue Throughput**: SQS supports high-throughput message processing
- **Database Connection**: Connection pooling for optimal database performance

### Monitoring & Alerting
- **CloudWatch Metrics**: Lambda execution metrics and error rates
- **Custom Metrics**: Business-specific metrics and KPIs
- **Error Alerting**: Real-time alerting for critical failures
- **Performance Monitoring**: Processing time and throughput monitoring

### Scalability Considerations
- **Auto-scaling**: Lambda auto-scales based on queue depth
- **Database Scaling**: Database connection limits and query optimization
- **API Rate Limits**: Respect for external API rate limitations
- **Queue Depth Management**: SQS queue depth monitoring and management

---

## Security & Compliance

### Authentication & Authorization
- **User Authentication**: Session-based authentication for portal access
- **API Authentication**: Token-based authentication for programmatic access
- **Tenant Isolation**: Strict tenant boundary enforcement
- **Role-Based Access**: Granular permissions based on user roles

### Data Protection
- **Encryption**: Data encryption in transit and at rest
- **Audit Logging**: Comprehensive audit trails for all operations
- **Data Retention**: Configurable data retention policies
- **Privacy Compliance**: GDPR and other privacy regulation compliance

### Integration Security
- **API Security**: Secure API communication with external systems
- **Credential Management**: Secure storage and rotation of API credentials
- **Network Security**: VPC and security group configurations
- **Access Logging**: Detailed logging of all external API calls

This comprehensive documentation provides a complete overview of the POND IoT Service Provider data flow across all four change types and five processing phases. Each phase is detailed with specific code examples, database operations, and integration considerations to ensure thorough understanding of the system architecture and operation.