# POND IoT Service Provider - Device Change Operations

## Overview
The POND IoT Service Provider is a device management system that supports four distinct bulk change operations for IoT device management. This service provider integrates with the Pond API to manage device lifecycle, customer assignments, rate plan changes, and service status updates. Each operation maintains comprehensive audit trails and supports both M2M and Mobility portal types.

## Service Provider Integration
- **Integration Type**: `IntegrationType.Pond`
- **API Authentication**: Uses `PondAuthentication` with distributor-based authentication
- **Environment Support**: Sandbox and Production environments
- **Base API Endpoints**: 
  - Sandbox: `pondAuthentication.SandboxURL`
  - Production: `pondAuthentication.ProductionURL`

---

## 1. ASSIGN CUSTOMER Change Type

### Overview
The ASSIGN CUSTOMER operation for POND IoT assigns devices to specific customer accounts, enabling proper billing allocation and service management. This process validates customer eligibility and creates or updates revenue services through the Pond API.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerAssignmentChangeDetails() → Customer Validation → RevService Creation/Update → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessAssociateCustomerAsync() → GetDeviceChanges() → Customer Assignment Logic → Database Update (Customer Assignment) → Portal-Specific Logging (M2M/Mobility) → BulkChangeStatus.PROCESSED → Assignment Complete

### Process Flow:

#### Phase 1: User Request & Validation
- User selects POND IoT devices for customer assignment from M2M UI
- User selects target customer and service configuration  
- User clicks "Continue" button
- Frontend sends POST request to `M2MController.BulkChange()`
  - `ChangeType`: "CustomerAssignment"
  - `Devices`: List of ICCIDs
  - `RevCustomerId`: Target customer ID
  - `CreateRevService`: true/false

#### Phase 2: Controller Validation (M2MController.cs)
- `M2MController.ValidateBulkChange()` is called
- `BuildCustomerAssignmentChangeDetails()` method executes:
  a. Check each ICCID exists in database
  b. Validate target customer exists and is active
  c. Check customer permissions and access rights
  d. Validate POND service provider compatibility
  e. Create M2M_DeviceChange records with validation results

#### Phase 3: Queue Processing
- Create DeviceBulkChange record with Status = "NEW"
- `ProcessBulkChange()` queues the request to SQS
- User gets immediate response with BulkChangeId

#### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
- Lambda receives SQS message
- `ProcessBulkChangeAsync()` routes to `ProcessAssociateCustomerAsync()`
- For each valid device:
  a. Create or update RevService records
  b. Update Device-Customer associations
  c. Update billing and access control tables
  d. Log success/failure in DeviceBulkChangeLog

#### Phase 5: Response & Logging
- Create DeviceBulkChangeLog entries for each device
- Update M2M_DeviceChange records with final status
- Return processing results

### API Endpoints:
- Customer Validation: Internal database validation
- RevService Creation: RevIO API integration
- Device Assignment: Database stored procedures

### Stored Procedures:
- `usp_DeviceBulkChange_Assign_Non_Rev_Customer`
- `usp_DeviceBulkChange_RevService_UpdateM2MChange`
- `usp_DeviceBulkChange_RevService_UpdateMobilityChange`

---

## 2. CHANGE CARRIER RATEPLAN Change Type

### Overview
The CHANGE CARRIER RATEPLAN operation for POND IoT updates device carrier rate plans through the Pond API. This process terminates existing packages and creates new packages with updated rate plan configurations.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerRatePlanChangeDetails() → Rate Plan Validation → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessCarrierRatePlanChangeAsync() → ProcessPondCarrierRatePlanChange() → Pond API Authentication → UpdatePondCarrierRatePlanForDevices() → Terminate Existing Packages → Add New Package → Update Package Status → Database Update → BulkChangeStatus.PROCESSED → Rate Plan Change Complete

### Process Flow:

#### Phase 1: User Request & Validation
- User selects POND IoT devices for carrier rate plan change from M2M UI
- User selects target carrier rate plan
- User clicks "Continue" button
- Frontend sends POST request to `M2MController.BulkChange()`
  - `ChangeType`: "CarrierRatePlanChange"
  - `Devices`: List of ICCIDs
  - `CarrierRatePlan`: Target rate plan code
  - `EffectiveDate`: When change takes effect

#### Phase 2: Controller Validation (M2MController.cs)
- `M2MController.ValidateBulkChange()` is called
- `BuildCustomerRatePlanChangeDetails()` method executes:
  a. Validate each ICCID exists and is active
  b. Validate target carrier rate plan exists
  c. Check POND service provider compatibility
  d. Validate rate plan permissions
  e. Create M2M_DeviceChange records

#### Phase 3: Queue Processing
- Create DeviceBulkChange record with Status = "NEW"
- Queue the request to SQS
- User gets immediate response with BulkChangeId

#### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
- Lambda receives SQS message via `ProcessBulkChangeAsync()`
- Routes to `ProcessCarrierRatePlanChangeAsync()`
- For POND integration, calls `ProcessPondCarrierRatePlanChange()`
- `PondRepository.GetPondAuthentication()` retrieves API credentials
- `PondApiService` initialized with authentication

#### Phase 5: POND API Processing
- `UpdatePondCarrierRatePlanForDevices()` processes each device:
  a. `GetExistingPackages()` retrieves active packages for ICCID
  b. `AddNewPondPackage()` creates new package with rate plan
  c. `UpdateStatusForNewPondPackageOnApi()` activates new package
  d. `TerminateExistingPackages()` deactivates old packages
  e. `SaveNewPondDeviceCarrierRatePlanToDatabase()` updates database

### API Endpoints:
- Add Package: `{baseUri}/{distributorId}/v1/sim/{iccid}/package`
- Update Package Status: `{baseUri}/{distributorId}/v1/package/{packageId}/status`

### Stored Procedures:
- `PondRepository.AddDeviceCarrierRatePlan()`
- `PondRepository.UpdateDeviceCarrierRatePlanStatus()`
- `PondRepository.GetExistingPackages()`

---

## 3. CHANGE CUSTOMER RATEPLAN Change Type

### Overview
The CHANGE CUSTOMER RATEPLAN operation for POND IoT updates customer-specific rate plans for devices. This process modifies billing and service configurations at the customer level while maintaining device-carrier relationships.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerRatePlanChangeDetails() → Customer Rate Plan Validation → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessCustomerRatePlanChangeAsync() → Database Update (Customer Rate Plans) → Portal-Specific Logging → BulkChangeStatus.PROCESSED → Customer Rate Plan Change Complete

### Process Flow:

#### Phase 1: User Request & Validation
- User selects POND IoT devices for customer rate plan change from M2M UI
- User selects target customer rate plan or rate pool
- User sets effective date
- User clicks "Continue" button
- Frontend sends POST request to `M2MController.BulkChange()`
  - `ChangeType`: "CustomerRatePlanChange"
  - `Devices`: List of ICCIDs
  - `CustomerRatePlanId`: Target customer rate plan ID
  - `CustomerRatePoolId`: Target rate pool ID (optional)
  - `EffectiveDate`: When change takes effect

#### Phase 2: Controller Validation (M2MController.cs)
- `M2MController.ValidateBulkChange()` is called
- `BuildCustomerRatePlanChangeDetails()` method executes:
  a. Validate each ICCID exists and is assigned to customer
  b. Validate target customer rate plan exists and is accessible
  c. Check rate pool compatibility if specified
  d. Validate effective date
  e. Create M2M_DeviceChange records

#### Phase 3: Queue Processing
- Create DeviceBulkChange record with Status = "NEW"
- Queue the request to SQS
- User gets immediate response with BulkChangeId

#### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
- Lambda receives SQS message via `ProcessBulkChangeAsync()`
- Routes to `ProcessCustomerRatePlanChangeAsync()`
- For each device:
  a. Validate customer rate plan assignment
  b. Update device customer rate plan associations
  c. Apply effective date for billing changes
  d. Log success/failure for each device

### API Endpoints:
- Customer Rate Plan Validation: Internal database queries
- Rate Plan Updates: Database stored procedures

### Stored Procedures:
- `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices`
- `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber`

---

## 4. UPDATE DEVICE STATUS Change Type

### Overview
The UPDATE DEVICE STATUS operation for POND IoT manages device activation, deactivation, and service status changes through the Pond API. This process updates both device status and associated service statuses.

### Whole Flow:
User Interface → M2MController.BulkChange() → BuildStatusUpdateChangeDetails() → Device Status Validation → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessStatusUpdateAsync() → ProcessPondStatusUpdateAsync() → Pond API Authentication → Update Service Status → RevService Processing → Database Update → Portal-Specific Logging → BulkChangeStatus.PROCESSED → Status Update Complete

### Process Flow:

#### Phase 1: User Request & Validation
- User selects POND IoT devices for status update from M2M UI
- User selects target device status (Active/Inactive/Suspended)
- User clicks "Continue" button
- Frontend sends POST request to `M2MController.BulkChange()`
  - `ChangeType`: "StatusUpdate"
  - `Devices`: List of ICCIDs
  - `UpdateStatus`: Target device status
  - `PostUpdateStatusId`: Final status ID

#### Phase 2: Controller Validation (M2MController.cs)
- `M2MController.ValidateBulkChange()` is called
- `BuildStatusUpdateChangeDetails()` method executes:
  a. Validate each ICCID exists and is manageable
  b. Validate target status is allowed for device
  c. Check current device status compatibility
  d. Validate user permissions for status change
  e. Create M2M_DeviceChange records

#### Phase 3: Queue Processing
- Create DeviceBulkChange record with Status = "NEW"
- Queue the request to SQS
- User gets immediate response with BulkChangeId

#### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
- Lambda receives SQS message via `ProcessBulkChangeAsync()`
- Routes to `ProcessStatusUpdateAsync()`
- For POND integration, calls `ProcessPondStatusUpdateAsync()`
- `PondRepository.GetPondAuthentication()` retrieves API credentials
- `PondApiService` initialized for status updates

#### Phase 5: POND API Processing
- `ProcessPondStatusUpdateAsync()` processes each device:
  a. Deserialize `StatusUpdateRequest` from change request
  b. Create `PondUpdateServiceStatusRequest` based on target status
  c. Call `pondApiService.UpdateServiceStatus()` to update device
  d. Process RevService creation if status update successful
  e. Mark device as processed with final status

### API Endpoints:
- Update Service Status: POND API service status endpoint
- RevService Integration: RevIO API for service updates

### Stored Procedures:
- `usp_DeviceBulkChange_StatusUpdate_UpdateDeviceRecords`
- `usp_DeviceBulkChange_UpdateMobilityDeviceChange`

---

## Common Components

### Authentication & Security
- **Authentication Class**: `PondAuthentication`
- **Repository**: `PondRepository`
- **API Service**: `PondApiService`
- **Security**: Distributor-based authentication with sandbox/production environments

### Error Handling & Logging
- **Logging Repository**: `DeviceBulkChangeLogRepository`
- **Log Entries**: `CreateM2MDeviceBulkChangeLog`
- **Status Tracking**: `BulkChangeStatus` (NEW, PROCESSING, PROCESSED, ERROR)
- **Retry Policies**: SQL and HTTP retry policies for resilience

### Database Integration
- **Connection**: Central database connection string
- **Repositories**: Device, Customer, Rate Plan repositories
- **Audit Trail**: Complete operation logging for compliance

### Queue Management
- **Message Queue**: Amazon SQS
- **Processing**: Asynchronous Lambda processing
- **Retry Logic**: Built-in retry mechanisms for failed operations