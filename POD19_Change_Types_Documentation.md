# POD19 Service Provider - Device Change Types Documentation

## 1. ARCHIVE Change Type

### Overview
The ARCHIVE Device Flow is a bulk change operation that permanently deactivates devices in the system by marking them as inactive and deleted. This process ensures devices that are no longer in use are properly removed from active management while maintaining audit trails and data integrity. Remove unused devices from active monitoring and billing cycles. The archival process validates device eligibility (checking for recent usage within 30 days), executes bulk database updates through stored procedures, and logs all changes across different portal types (M2M, Mobility) while maintaining comprehensive audit trails.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildArchivalChangeDetails() → Validation (30-day usage check) → GetArchivalChanges() → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessArchivalAsync() → GetDeviceChanges() → usp_DeviceBulkChange_Archival_ArchiveDevices → Database Update (IsActive=false, IsDeleted=true) → Portal-Specific Logging (M2M/Mobility)→ BulkChangeStatus.PROCESSED → Archive Complete

### Process Flow:

**Phase 1: User Request & Validation**
- User selects devices for archival from M2M UI
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "Archival"
  - Devices: List of ICCIDs
  - OverrideValidation: true/false

**Phase 2: Controller Validation (M2MController.cs)**
- M2MController.ValidateBulkChange() is called
- BuildArchivalChangeDetails() method executes:
  a. Check each ICCID exists in database
  b. Validate device is not already archived
  c. Check for active Rev Services:
     - Query Device_Tenant table
     - Check RevService.IsActive = true
     - If active services found and override = false → Error
  d. Check recent usage (last 30 days):
     - Check Device.LastUsageDate
     - If usage within ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS i.e., 30→ Error
  e. Create M2M_DeviceChange records with validation results

**Phase 3: Queue Processing**
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
- User gets immediate response with BulkChangeId

**Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)**
- Lambda receives SQS message
- ProcessBulkChangeAsync() routes to ProcessArchivalAsync()
- For each valid device:
  a. Execute stored procedure: usp_DeviceBulkChange_Archival_ArchiveDevices
  b. Update Device table:
     - Set IsActive = false
     - Set IsDeleted = true
     - Set ModifiedDate = current timestamp
     - Set ModifiedBy = "AltaworxDeviceBulkChange"
  c. Log success/failure in DeviceBulkChangeLog

**Phase 5: Response & Logging**
- Create DeviceBulkChangeLog entries for each device
- Update M2M_DeviceChange records with final status
- Return processing results

### Sample Request
```json
{
    "OverrideValidation": false,
    "Devices": [
        "89148000008245116615"
    ],
    "ServiceProviderId": 4,
    "ChangeType": 5
}
```

---

## 2. ASSIGN CUSTOMER Change Type

### Overview
The ASSIGN CUSTOMER Device Flow is a bulk change operation that associates devices with specific customer accounts in the system. This process enables proper customer billing allocation, service management, and access control. The assignment process validates customer eligibility, creates or updates revenue services, and maintains comprehensive audit trails across different portal types (M2M, Mobility).

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerAssignmentChangeDetails() → Customer Validation → RevService Creation/Update → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessAssociateCustomerAsync() → GetDeviceChanges() → Customer Assignment Logic → Database Update (Customer Assignment) → Portal-Specific Logging (M2M/Mobility) → BulkChangeStatus.PROCESSED → Assignment Complete

### Process Flow:

**Phase 1: User Request & Validation**
- User selects devices for customer assignment from M2M UI
- User selects target customer and service configuration
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "CustomerAssignment"
  - Devices: List of ICCIDs
  - RevCustomerId: Target customer ID
  - CreateRevService: true/false

**Phase 2: Controller Validation (M2MController.cs)**
- M2MController.ValidateBulkChange() is called
- BuildCustomerAssignmentChangeDetails() method executes:
  a. Check each ICCID exists in database
  b. Validate target customer exists and is active
  c. Check customer permissions and access rights
  d. Validate service provider compatibility
  e. Create M2M_DeviceChange records with validation results

**Phase 3: Queue Processing**
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
- User gets immediate response with BulkChangeId

**Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)**
- Lambda receives SQS message
- ProcessBulkChangeAsync() routes to ProcessAssociateCustomerAsync()
- For each valid device:
  a. Create or update RevService records
  b. Update Device-Customer associations
  c. Update billing and access control tables
  d. Log success/failure in DeviceBulkChangeLog

**Phase 5: Response & Logging**
- Create DeviceBulkChangeLog entries for each device
- Update M2M_DeviceChange records with final status
- Return processing results

### Sample Request
```json
{
    "OverrideValidation": false,
    "Devices": [
        "89148000008245116615"
    ],
    "ServiceProviderId": 4,
    "ChangeType": 1,
    "RevService": {
        "RevCustomerId": "12345",
        "CreateRevService": true
    }
}
```

---

## 3. CHANGE CARRIER RATEPLAN Change Type

### Overview
The CHANGE CARRIER RATEPLAN Device Flow is a bulk change operation that updates the carrier rate plan for devices in the system. This process enables modification of carrier-level pricing and service configurations while maintaining service continuity. The rate plan change process validates plan eligibility, checks device compatibility, and executes carrier-specific API calls to update billing configurations.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerRatePlanChangeDetails() → Carrier Rate Plan Validation → EID Validation (Teal) → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessCarrierRatePlanChangeAsync() → GetDeviceChanges() → Carrier API Calls → Database Update (Rate Plan Changes) → Portal-Specific Logging (M2M/Mobility) → BulkChangeStatus.PROCESSED → Rate Plan Change Complete

### Process Flow:

**Phase 1: User Request & Validation**
- User selects devices for carrier rate plan change from M2M UI
- User selects target carrier rate plan
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "CarrierRatePlanChange"
  - Devices: List of ICCIDs
  - CarrierRatePlan: Target rate plan code

**Phase 2: Controller Validation (M2MController.cs)**
- M2MController.ValidateBulkChange() is called
- BuildCustomerRatePlanChangeDetails() method executes:
  a. Check each ICCID exists in database
  b. Validate carrier rate plan exists and is active
  c. Check device compatibility with rate plan
  d. For Teal integration: Validate EID exists on device
  e. Retrieve rate plan metadata (PlanUuid for Teal, RatePlanId for Pond)
  f. Create M2M_DeviceChange records with validation results

**Phase 3: Queue Processing**
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
- User gets immediate response with BulkChangeId

**Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)**
- Lambda receives SQS message
- ProcessBulkChangeAsync() routes to ProcessCarrierRatePlanChangeAsync()
- For each valid device:
  a. Execute carrier-specific API calls to change rate plan
  b. Update device rate plan associations in database
  c. Update billing configurations
  d. Log success/failure in DeviceBulkChangeLog

**Phase 5: Response & Logging**
- Create DeviceBulkChangeLog entries for each device
- Update M2M_DeviceChange records with final status
- Return processing results

### Sample Request
```json
{
    "OverrideValidation": false,
    "Devices": [
        "89148000008245116615"
    ],
    "ServiceProviderId": 4,
    "ChangeType": 7,
    "CarrierRatePlanUpdate": {
        "CarrierRatePlan": "STANDARD_PLAN_001"
    }
}
```

---

## 4. CHANGE CUSTOMER RATEPLAN Change Type

### Overview
The CHANGE CUSTOMER RATEPLAN Device Flow is a bulk change operation that updates the customer-specific rate plan and billing configurations for devices. This process enables modification of customer-level pricing, data allocations, and billing pools while maintaining service continuity. The process validates customer rate plan eligibility, updates billing configurations, and maintains comprehensive audit trails.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerRatePlanChangeDetails() → Customer Rate Plan Validation → Rate Pool Validation → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessCustomerRatePlanChangeAsync() → GetDeviceChanges() → usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices → Database Update (Customer Rate Plan) → Portal-Specific Logging (M2M/Mobility) → BulkChangeStatus.PROCESSED → Rate Plan Change Complete

### Process Flow:

**Phase 1: User Request & Validation**
- User selects devices for customer rate plan change from M2M UI
- User selects target customer rate plan and rate pool
- User configures data allocation settings
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "CustomerRatePlanChange"
  - Devices: List of ICCIDs
  - CustomerRatePlan: Target rate plan details

**Phase 2: Controller Validation (M2MController.cs)**
- M2MController.ValidateBulkChange() is called
- BuildCustomerRatePlanChangeDetails() method executes:
  a. Check each ICCID exists in database
  b. Validate customer rate plan exists and is active
  c. Validate customer rate pool compatibility
  d. Check device eligibility for rate plan change
  e. Validate data allocation limits
  f. Create M2M_DeviceChange records with validation results

**Phase 3: Queue Processing**
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
- User gets immediate response with BulkChangeId

**Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)**
- Lambda receives SQS message
- ProcessBulkChangeAsync() routes to ProcessCustomerRatePlanChangeAsync()
- For each valid device:
  a. Execute stored procedure: usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
  b. Update device customer rate plan associations
  c. Update customer rate pool assignments
  d. Update data allocation configurations
  e. Log success/failure in DeviceBulkChangeLog

**Phase 5: Response & Logging**
- Create DeviceBulkChangeLog entries for each device
- Update M2M_DeviceChange records with final status
- Return processing results

### Sample Request
```json
{
    "OverrideValidation": false,
    "Devices": [
        "89148000008245116615"
    ],
    "ServiceProviderId": 4,
    "ChangeType": 4,
    "CustomerRatePlanUpdate": {
        "CustomerRatePlanId": 123,
        "CustomerRatePoolId": 456,
        "DataAllocationMB": 1024
    }
}
```

---

## 5. EDIT USERNAME/COST CENTER Change Type

### Overview
The EDIT USERNAME/COST CENTER Device Flow is a bulk change operation that updates device username and cost center information for billing and organizational purposes. This process enables modification of device identification and cost allocation while maintaining service continuity. The process validates username formats, updates carrier systems, and maintains comprehensive audit trails across different integration types.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildUsernameChangeDetails() → Username Format Validation → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessEditUsernameAsync() → GetDeviceChanges() → Integration-Specific Processing (Jasper/POD19/Telegence) → Carrier API Calls → Database Update (Username/Cost Center) → Portal-Specific Logging (M2M/Mobility) → BulkChangeStatus.PROCESSED → Username Update Complete

### Process Flow:

**Phase 1: User Request & Validation**
- User selects devices for username/cost center update from M2M UI
- User enters new username and cost center information
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "EditUsername"
  - Devices: List of ICCIDs
  - Username: New username and cost center details

**Phase 2: Controller Validation (M2MController.cs)**
- M2MController.ValidateBulkChange() is called
- BuildUsernameChangeDetails() method executes:
  a. Check each ICCID exists in database
  b. Validate username format requirements
  c. Check cost center format and permissions
  d. Validate device eligibility for username changes
  e. Create M2M_DeviceChange records with validation results

**Phase 3: Queue Processing**
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
- User gets immediate response with BulkChangeId

**Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)**
- Lambda receives SQS message
- ProcessBulkChangeAsync() routes to ProcessEditUsernameAsync()
- Integration-specific processing:
  - **POD19**: ProcessEditUsernameJasperAsync() → Jasper API calls → Username audit trail validation
  - **Telegence**: ProcessEditUsernameTelegenceAsync() → Telegence API calls
  - **Other Jasper integrations**: Standard Jasper username update flow
- For each valid device:
  a. Execute carrier-specific username update API calls
  b. Update device username and cost center in database
  c. Validate update success through audit trail checks
  d. Log success/failure in DeviceBulkChangeLog

**Phase 5: Response & Logging**
- Create DeviceBulkChangeLog entries for each device
- Update M2M_DeviceChange records with final status
- Return processing results

### Sample Request
```json
{
    "OverrideValidation": false,
    "Devices": [
        "89148000008245116615"
    ],
    "ServiceProviderId": 4,
    "ChangeType": 6,
    "Username": {
        "NewUsername": "DEVICE_001",
        "CostCenter": "DEPT_SALES_001"
    }
}
```

---

## 6. STATUS UPDATE Change Type

### Overview
The STATUS UPDATE Device Flow is a bulk change operation that modifies device status (Active, Inactive, Suspended, etc.) across carrier networks. This process enables device lifecycle management while maintaining service continuity and proper billing states. The process validates status transitions, executes carrier-specific API calls, and maintains comprehensive audit trails across different integration types (Jasper, ThingSpace, POD19).

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildStatusUpdateChangeDetails() → Status Transition Validation → Integration-Specific Processing → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessStatusUpdateAsync() → GetDeviceChanges() → Carrier API Calls → Database Update (Device Status) → Portal-Specific Logging (M2M/Mobility) → BulkChangeStatus.PROCESSED → Status Update Complete

### Process Flow:

**Phase 1: User Request & Validation**
- User selects devices for status update from M2M UI
- User selects target status (Active, Inactive, Suspended, etc.)
- User configures integration-specific parameters
- User clicks "Continue" button
- Frontend sends POST request to M2MController.BulkChange()
  - ChangeType: "StatusUpdate"
  - Devices: List of ICCIDs/IMEIs
  - StatusUpdate: Target status and configuration

**Phase 2: Controller Validation (M2MController.cs)**
- M2MController.ValidateBulkChange() is called
- BuildStatusUpdateChangeDetails() method executes based on integration:
  - **POD19/Jasper**: BuildStatusUpdateChangeDetailsJasper()
  - **ThingSpace**: BuildStatusUpdateChangeDetailsThingSpace()
  a. Check each ICCID/IMEI exists in database
  b. Validate target status exists for integration
  c. Check status transition eligibility
  d. For ThingSpace: Validate rate plan requirements for activation
  e. Create M2M_DeviceChange records with validation results

**Phase 3: Queue Processing**
- Create DeviceBulkChange record with Status = "NEW"
- ProcessBulkChange() queues the request to SQS
- User gets immediate response with BulkChangeId

**Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)**
- Lambda receives SQS message
- ProcessBulkChangeAsync() routes to ProcessStatusUpdateAsync()
- Integration-specific processing based on carrier:
  - **POD19**: Jasper API integration for status updates
  - **ThingSpace**: ThingSpace API integration with rate plan management
  - **Other Jasper**: Standard Jasper status update flow
- For each valid device:
  a. Execute carrier-specific status update API calls
  b. Update device status in database
  c. Update billing and service states
  d. Log success/failure in DeviceBulkChangeLog

**Phase 5: Response & Logging**
- Create DeviceBulkChangeLog entries for each device
- Update M2M_DeviceChange records with final status
- Return processing results

### Sample Request
```json
{
    "OverrideValidation": false,
    "Devices": [
        "89148000008245116615"
    ],
    "ServiceProviderId": 4,
    "ChangeType": 3,
    "StatusUpdate": {
        "TargetStatus": "Active",
        "JasperStatusUpdate": {
            "ReasonCode": "CUSTOMER_REQUEST"
        }
    }
}
```

---

## Database Tables

| Table Name | Purpose | Key Fields |
|------------|---------|------------|
| DeviceBulkChange | Main bulk change tracking and management table | Id (PK), TenantId, Status, ChangeRequestTypeId, ChangeRequestType, ServiceProviderId, ServiceProvider, IntegrationId, Integration, PortalTypeId, CreatedBy, ProcessedDate |
| M2M_DeviceChange | Individual device change tracking for M2M devices | Id (PK), BulkChangeId (FK), DeviceId, ICCID, MSISDN, Status, ChangeRequest, IsProcessed, ProcessedDate, IsActive, IsDeleted |
| Mobility_DeviceChange | Individual device change tracking for Mobility devices | Id (PK), BulkChangeId (FK), ICCID, Status, ChangeRequest, StatusDetails, IsProcessed, ProcessedDate, IsActive, IsDeleted |
| Device | Main device information and status table | Id (PK), ICCID, IMEI, MSISDN, DeviceStatusId, ServiceProviderId, TenantId, IsActive, IsDeleted, ModifiedBy, ModifiedDate, IpAddress |
| DeviceBulkChangeLog | Audit trail for bulk change operations | Id (PK), BulkChangeId (FK), ICCID, Status, RequestText, ResponseText, ProcessedDate, ProcessedBy |

## Change Type Enumeration

```csharp
public enum DeviceChangeType
{
    CustomerAssignment = 1,
    StatusUpdate = 3,
    CustomerRatePlanChange = 4,
    Archival = 5,
    EditUsername = 6,
    CarrierRatePlanChange = 7,
    ChangeICCIDorIMEI = 8
}
```

## Integration Support for POD19

POD19 service provider supports the following change types with specific integration logic:

1. **ARCHIVE** - Full support with 30-day usage validation
2. **ASSIGN CUSTOMER** - Full support with RevService management
3. **CHANGE CARRIER RATEPLAN** - Full support with Jasper API integration
4. **CHANGE CUSTOMER RATEPLAN** - Full support with stored procedure execution
5. **EDIT USERNAME/COST CENTER** - Full support with POD19-specific username validation
6. **STATUS UPDATE** - Full support through Jasper API integration

Each change type maintains comprehensive audit trails and supports both M2M and Mobility portal types for the POD19 service provider.