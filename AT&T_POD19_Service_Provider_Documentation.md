# AT&T POD19 Service Provider Documentation

## Overview

This document provides comprehensive documentation for the AT&T POD19 service provider implementation, covering process flows, dataflow diagrams, request/response patterns, and API endpoints for the supported change types.

## Supported Change Types

The AT&T POD19 service provider supports the following change types:

1. **Archive** - Device archival operations
2. **Assign Customer** - Customer assignment to devices
3. **Change Carrier Rateplan** - Carrier-level rate plan modifications
4. **Change Customer Rateplan** - Customer-level rate plan modifications  
5. **Edit Username/Cost Center** - Username and cost center updates

## Service Provider Integration

### Integration Type
- **Integration ID**: POD19
- **Service Provider**: AT&T
- **Portal Type**: M2M

### Key Integration Points
```csharp
case IntegrationType.POD19:
    // POD19 specific processing logic
```

## Process Flow Diagrams

### 1. Main Bulk Change Processing Flow

```
┌─────────────────────┐
│ Client Request      │
│ (M2M Controller)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate Request    │
│ & Change Type       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Route by Change     │
│ Request Type        │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ Archive      │ │ Assign       │ │ Change       │ │ Change       │ │ Edit         │
│ Devices      │ │ Customer     │ │ Carrier      │ │ Customer     │ │ Username/    │
│              │ │              │ │ Rateplan     │ │ Rateplan     │ │ Cost Center  │
└──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └──────┬───────┘
       │                │                │                │                │
       ▼                ▼                ▼                ▼                ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                        Execute Change Processing                                  │
└──────────────────────────────────────────────────────────────────────────────────┘
       │
       ▼
┌─────────────────────┐
│ Log Results &       │
│ Update Status       │
└─────────────────────┘
```

### 2. Customer Rate Plan Change Dataflow

```
┌─────────────────────┐
│ Client Request      │
│ (Customer Rate Plan)│
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ Validation          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Customer    │
│ Rate Plan Update    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check Effective     │
│ Date                │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Immediate    │    │ Scheduled    │
│ Processing   │    │ Processing   │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Execute SP   │    │ Add to Queue │
│ Update       │    │ Table        │
│ Devices      │    │              │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success/ │    │ Log Queue    │
│ Error        │    │ Status       │
└──────────────┘    └──────────────┘
```

## API Endpoints and URLs

### M2M Controller Endpoints

#### Primary API Routes
- **Base URL**: `/M2M/`
- **Bulk Changes**: `/M2M/BulkChanges`
- **Device Search**: `/M2M/SearchDevices`
- **Bulk Change Validation**: `/M2M/ValidateBulkChange` (POST)
- **Bulk Change Processing**: `/M2M/BulkChange` (POST)

#### External Service URLs
```csharp
// Jasper API Endpoints
DeviceStatusUpdatePath = "JASPER_DEVICE_RATE_PLAN_UPDATE_PATH"
JasperDeviceUsernameUpdatePath = "JASPER_DEVICE_USERNAME_UPDATE_PATH"
JasperDeviceAuditTrailPath = "JASPER_DEVICE_AUDIT_TRAIL_PATH"

// Telegence API Endpoints  
TelegenceDeviceStatusUpdateURL = "TELEGENCE_DEVICE_STATUS_UPDATE_URL"
TelegenceSubscriberUpdateURL = "TELEGENCE_SUBSCRIBER_UPDATE_URL"

// ThingSpace API Endpoints
ThingSpaceGetStatusRequestURL = "THINGSPACE_GET_STATUS_REQUEST_URL"
ThingSpaceChangeIdentifierPath = "THINGSPACE_CHANGE_IDENTIFIER_URL"

// Proxy URLs
ProxyUrl = "PROXY_URL"
- GET: /api/Proxy/Get
- POST: /api/Proxy/Post  
- PATCH: /api/Proxy/Patch

// Teal API
TealAssignRatePlanPath = "TEAL_ASSIGN_RATE_PLAN_URL"
```

## Change Type Implementation Details

### 1. Archive Devices

#### Request Structure
```csharp
public class ArchivalRequest
{
    public string[] Devices { get; set; }
    public int ServiceProviderId { get; set; }
    public bool ProcessChanges { get; set; }
}
```

#### Process Flow
1. **Validation**: Check device eligibility for archival
2. **Recent Usage Check**: Verify no usage in last 30 days
3. **Database Operation**: Execute `usp_DeviceBulkChange_Archival_ArchiveDevices`
4. **Logging**: Record archival status

#### Key Validations
- Device must not have usage in last 30 days (`ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS = 30`)
- Device must not be already archived
- Active Rev Services check

### 2. Assign Customer

#### Request Structure
```csharp
public class BulkChangeAssociateCustomer
{
    public string RevCustomerId { get; set; }
    public bool CreateRevService { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public BulkChangeAssociateRevService RevService { get; set; }
}
```

#### Process Flow
1. **Customer Validation**: Verify customer exists
2. **Service Creation**: Create Rev Service if required
3. **Database Update**: Execute `usp_DeviceBulkChange_Assign_Non_Rev_Customer`
4. **Status Update**: Mark devices as assigned

#### Database Operations
```sql
EXEC usp_DeviceBulkChange_Assign_Non_Rev_Customer
    @DeviceUpdatesTable = @deviceUpdates,
    @BulkChangeId = @bulkChangeId
```

### 3. Change Carrier Rateplan

#### Request Structure
```csharp
public class BulkChangeCarrierRatePlanUpdate
{
    public string CarrierRatePlan { get; set; }
    public string CommPlan { get; set; }
    public string PlanUuid { get; set; }
    public int? RatePlanId { get; set; }
}
```

#### POD19 Specific Processing
```csharp
case (int)IntegrationType.POD19:
    // POD19 carrier rate plan change logic
    var carrierRatePlan = JsonConvert.DeserializeObject<BulkChangeCarrierRatePlanUpdate>(change.ChangeRequest);
    result = await ProcessPOD19CarrierRatePlanChange(carrierRatePlan);
    break;
```

#### Process Flow
1. **Rate Plan Validation**: Verify carrier rate plan exists
2. **Integration Check**: Route to POD19 specific handler
3. **API Call**: Update rate plan via carrier API
4. **Database Update**: Sync changes to local database

### 4. Change Customer Rateplan

#### Request Structure
```csharp
public class BulkChangeCustomerRatePlanUpdate
{
    public int? CustomerRatePlanId { get; set; }
    public decimal? CustomerDataAllocationMB { get; set; }
    public int? CustomerPoolId { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
```

#### Process Flow
1. **Effective Date Check**: Immediate vs Scheduled processing
2. **Immediate Processing**: Execute stored procedure directly
3. **Scheduled Processing**: Add to queue table
4. **Validation**: Customer rate plan ID and data allocation

#### Database Operations
```sql
-- Immediate Processing
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = @bulkChangeId,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @customerDataAllocationMB = @customerDataAllocationMB,
    @effectiveDate = @effectiveDate,
    @needToMarkProcessed = @needToMarkProcessed

-- Individual Device Processing  
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber
    @bulkChangeId = @bulkChangeId,
    @subscriberNumber = @subscriberNumber,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @effectiveDate = @effectiveDate,
    @customerDataAllocationMB = @customerDataAllocationMB
```

### 5. Edit Username/Cost Center

#### Request Structure
```csharp
public class BulkChangeEditUsername
{
    public string Username { get; set; }
    public string CostCenter { get; set; }
    public List<string> Devices { get; set; }
}
```

#### POD19 Specific Processing
```csharp
case (int)IntegrationType.POD19:
    // POD19 username update processing
    var isEditSuccess = await jasperDeviceService.IsEditUsernamePOD19Success(
        JasperDeviceAuditTrailPath, 
        change.ICCID, 
        Common.CommonString.ERROR_MESSAGE, 
        Common.CommonString.USERNAME_STRING
    );
    break;
```

#### Process Flow
1. **Authentication**: Get service provider credentials
2. **API Call**: Update username via Jasper API
3. **Validation**: Check audit trail for success
4. **Rev.IO Update**: Update custom fields if Rev service exists
5. **Database Sync**: Update local device records

## Request/Response Patterns

### Standard Request Format
```json
{
    "ServiceProviderId": 123,
    "ChangeType": 4,
    "ProcessChanges": true,
    "Devices": ["ICCID1", "ICCID2", "ICCID3"],
    "CustomerRatePlanUpdate": {
        "CustomerRatePlanId": 456,
        "CustomerDataAllocationMB": 1024,
        "CustomerPoolId": 789,
        "EffectiveDate": "2024-01-15T00:00:00Z"
    }
}
```

### Standard Response Format
```json
{
    "BulkChangeId": 12345,
    "Status": "PROCESSED",
    "ProcessedDevices": 3,
    "FailedDevices": 0,
    "Errors": [],
    "CreatedDate": "2024-01-01T10:00:00Z"
}
```

### Error Response Format
```json
{
    "HasErrors": true,
    "ActionText": "ProcessCustomerRatePlanChange",
    "RequestObject": "CustomerRatePlanId: 456, EffectiveDate: 2024-01-15",
    "ResponseObject": "Error: Invalid customer rate plan ID",
    "ErrorDetails": [
        {
            "ICCID": "1234567890",
            "Error": "Device not found"
        }
    ]
}
```

## Database Schema

### Key Tables
- `DeviceBulkChange` - Main bulk change records
- `M2M_DeviceChange` - Individual device changes
- `Device_CustomerRatePlanOrRatePool_Queue` - Scheduled rate plan changes
- `M2M_DeviceBulkChangeLog` - Audit logs
- `Mobility_DeviceBulkChangeLog` - Mobility portal logs

### Change Request Types
```csharp
public enum ChangeRequestType
{
    StatusUpdate = 1,
    ActivateNewService = 2,
    Archival = 3,
    CustomerRatePlanChange = 4,
    CustomerAssignment = 5,
    CarrierRatePlanChange = 7,
    CreateRevService = 8,
    ChangeICCIDAndIMEI = 9,
    EditUsernameCostCenter = 10
}
```

## Error Handling

### Common Error Scenarios
1. **Invalid Customer Rate Plan ID**
2. **Device Not Found**
3. **Device Already Archived**
4. **Recent Usage Validation Failed**
5. **API Authentication Failures**
6. **Database Connection Issues**

### Error Response Structure
```csharp
public class DeviceChangeResult<TRequest, TResponse>
{
    public string ActionText { get; set; }
    public bool HasErrors { get; set; }
    public TRequest RequestObject { get; set; }
    public TResponse ResponseObject { get; set; }
}
```

## Security and Authentication

### Service Provider Authentication
- Integration-specific credentials stored securely
- Environment variable configuration
- Token-based authentication for external APIs

### Authorization
- Tenant-level access control
- Role-based permissions
- Device visibility restrictions

## Performance Considerations

### Batch Processing
- Page size: 100 devices per batch (configurable)
- Parallel processing for API calls
- Asynchronous operations for large bulk changes

### Queue Management
- SQS queues for processing coordination
- Retry mechanisms for failed operations
- Dead letter queues for error handling

## Monitoring and Logging

### Log Types
- **M2M Portal Logs**: Device management operations
- **Mobility Portal Logs**: Mobile device operations
- **System Logs**: Application-level logging
- **Audit Logs**: Change tracking and compliance

### Key Metrics
- Processing success/failure rates
- API response times
- Queue depths and processing delays
- Error distribution by type

## Configuration

### Environment Variables
```bash
# API Endpoints
JASPER_DEVICE_RATE_PLAN_UPDATE_PATH
JASPER_DEVICE_USERNAME_UPDATE_PATH
JASPER_DEVICE_AUDIT_TRAIL_PATH
TELEGENCE_DEVICE_STATUS_UPDATE_URL
TELEGENCE_SUBSCRIBER_UPDATE_URL
THINGSPACE_GET_STATUS_REQUEST_URL
THINGSPACE_CHANGE_IDENTIFIER_URL
TEAL_ASSIGN_RATE_PLAN_URL

# Queue URLs
DEVICE_BULK_CHANGE_QUEUE_URL
EBONDING_DEVICE_STATUS_CHANGE_QUEUE_URL

# Proxy Configuration
PROXY_URL

# Processing Configuration
MAX_PARALLEL_REQUESTS=10
THINGSPACE_UPDATE_DEVICE_STATUS_RETRY_NUMBER=3
```

### Database Connections
- Central database connection string
- Entity Framework configuration
- SQL retry policies for transient failures

This documentation provides a comprehensive view of the AT&T POD19 service provider implementation, covering all requested aspects of process flows, dataflows, request/response patterns, and technical implementation details.