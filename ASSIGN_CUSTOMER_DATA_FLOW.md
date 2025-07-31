# POND IoT Service Provider - Assign Customer Data Flow

## Overview
The ASSIGN CUSTOMER Device Flow is a bulk change operation that associates devices with specific customer accounts in the system. This process enables proper customer billing allocation, service management, and access control. The assignment process validates customer eligibility, creates or updates revenue services, and maintains comprehensive audit trails across different portal types (M2M, Mobility).

## Whole Flow:
```
User Interface → M2MController.BulkChange() → BuildCustomerAssignmentChangeDetails() → Customer Validation → RevService Creation/Update → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessAssociateCustomerAsync() → GetDeviceChanges() → Customer Assignment Logic → Database Update (Customer Assignment) → Portal-Specific Logging (M2M/Mobility) → BulkChangeStatus.PROCESSED → Assignment Complete
```

## Process Flow:

### Phase 1: User Request & Validation
- **User selects devices for customer assignment from M2M UI**
- **User selects target customer and service configuration**
- **User clicks "Continue" button**
- **Frontend sends POST request to M2MController.BulkChange()**
  - ChangeType: "CustomerAssignment"
  - Devices: List of ICCIDs
  - RevCustomerId: Target customer ID
  - CreateRevService: true/false

### Phase 2: Controller Validation (M2MController.cs)
- **M2MController.ValidateBulkChange() is called**
- **BuildCustomerAssignmentChangeDetails() method executes:**
  - Check each ICCID exists in database
  - Validate target customer exists and is active
  - Check customer permissions and access rights
  - Validate service provider compatibility
  - Create M2M_DeviceChange records with validation results

#### Key Validation Methods:
```csharp
// From M2MController.cs lines 2084-2150
BuildAssociateCustomerDeviceChanges(
    awxDb, Session, permissionManager, model, 
    devices, deviceTenants, sites, deviceStatus, 
    revCustomers, useCarrierActivation
)
```

#### Validation Checks:
- **Device Existence**: Verify ICCID exists in vwM2MDeviceInventory
- **Device Status**: Check device is not archived (IsActive=true, IsDeleted=false)
- **Customer Validation**: Validate RevCustomer exists and is active
- **Service Conflicts**: Check for existing active Rev Services
- **Permission Validation**: Verify user has access to target customer

### Phase 3: Queue Processing
- **Create DeviceBulkChange record with Status = "NEW"**
- **ProcessBulkChange() queues the request to SQS**
  - BulkChangeId: Generated ID
  - ChangeRequestTypeId: DeviceChangeType.CustomerAssignment
  - ServiceProviderId: Target service provider
  - TenantId: Current tenant
- **User gets immediate response with BulkChangeId**

### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
- **Lambda receives SQS message**
- **ProcessBulkChangeAsync() routes to ProcessAssociateCustomerAsync()**
  - Located at lines 1466-1615 in AltaworxDeviceBulkChange.cs
- **For each valid device:**
  - Create or update RevService records
  - Update Device-Customer associations
  - Update billing and access control tables
  - Log success/failure in DeviceBulkChangeLog

#### Lambda Processing Details:
```csharp
// From AltaworxDeviceBulkChange.cs line 489
case ChangeRequestType.CustomerAssignment:
    var changeRequest = GetBulkChangeRequest(context, bulkChangeId, bulkChange.PortalTypeId);
    var request = JsonConvert.DeserializeObject<BulkChangeAssociateCustomer>(changeRequest);
    var associateCustomerChanges = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, pageSize).ToList();
    
    if (string.IsNullOrEmpty(request?.RevCustomerId))
    {
        await bulkChangeRepository.UpdateAMOPCustomer(context, logRepo, associateCustomerChanges, bulkChange);
    }
    else
    {
        await ProcessAssociateCustomerAsync(context, logRepo, bulkChange, associateCustomerChanges);
    }
```

#### Customer Assignment Logic:
1. **Rev Service Creation** (if CreateRevService = true):
   - Call Rev.io API to create service line
   - Associate service with customer account
   - Set up billing and usage tracking

2. **Device-Customer Association**:
   - Update Device_Tenant table
   - Set SiteId, AccountNumber, IntegrationAuthenticationId
   - Apply customer rate plans if specified

3. **Rate Plan Assignment**:
   - Update CustomerRatePlanId if provided
   - Update CustomerRatePoolId if provided
   - Queue future effective date changes if necessary

### Phase 5: Response & Logging
- **Create DeviceBulkChangeLog entries for each device**
  - M2M_DeviceChangeId: Reference to device change
  - ResponseStatus: PROCESSED/ERROR
  - LogEntryDescription: Details of operation
  - ProcessedDate: Timestamp
- **Update M2M_DeviceChange records with final status**
- **Return processing results**

## Key Database Operations

### Tables Updated:
- **DeviceBulkChange**: Main bulk change record
- **M2M_DeviceChange**: Individual device change records
- **DeviceBulkChangeLog**: Audit trail entries
- **Device_Tenant**: Device-customer associations
- **RevService**: Revenue service records (if creating new services)
- **RevServiceProduct**: Service product associations

### Integration Points:
- **Rev.io API**: For service creation and management
- **SQS Queue**: For asynchronous processing
- **Cross-Provider History**: For audit trails
- **Webhook Notifications**: For status updates

## Stored Procedure
```sql
usp_CustomerChangeType_AssignChangeType
```
**Purpose**: Handles the backend logic for customer assignment
**Location**: Database stored procedures
**Function**: Processes device-customer associations and updates billing records

## Error Handling & Validation

### Common Validation Errors:
- **"Invalid ICCID"**: Device not found in system
- **"M2M Device is archived"**: Device marked as deleted
- **"Active service line. New Service line not created"**: Existing active service conflicts
- **"Active service line. Cannot change customers"**: Service change restrictions
- **"Rev Customer not found"**: Invalid customer ID
- **"Writes disabled for this service provider"**: Provider restrictions

### Retry Logic:
- **SQL Transient Errors**: 3 retry attempts
- **HTTP API Calls**: Configurable retry with exponential backoff
- **Queue Processing**: Message requeue on temporary failures

## Monitoring & Logging

### Log Categories:
- **Pre-flight Checks**: Validation failures
- **API Calls**: Rev.io service creation
- **Database Updates**: AMOP system changes
- **Rate Plan Updates**: Customer rate plan assignments
- **Error Tracking**: Failed operations and reasons

### Status Tracking:
- **NEW**: Initial queue state
- **PROCESSING**: Lambda is processing
- **PROCESSED**: Successfully completed
- **ERROR**: Failed with errors

## Configuration Parameters

### Environment Variables:
- **DEVICE_BULK_CHANGE_QUEUE_URL**: SQS queue for processing
- **MAX_PARALLEL_REQUESTS**: Concurrent processing limit
- **PAGE_SIZE**: Batch size for device processing
- **RETRY_MAX_COUNT**: Maximum retry attempts

### Feature Flags:
- **CreateRevService**: Enable/disable service creation
- **AddCarrierRatePlan**: Enable rate plan updates
- **UseCarrierActivation**: Use carrier activation dates
- **WriteIsEnabled**: Provider write permissions

This comprehensive data flow ensures reliable device-customer associations while maintaining data integrity and providing complete audit trails for billing and compliance purposes.