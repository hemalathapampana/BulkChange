# Verizon ThingSpace IoT - Detailed Change Type Flows

## Overview
This document provides detailed step-by-step flows for all six change types supported by Verizon ThingSpace IoT Service Provider in the BulkChange module.

## Entry Point Flow
All change types follow this common entry pattern:

```
User Interface → M2MController.BulkChange() → Validation → Queue → AltaworxDeviceBulkChange Lambda
```

---

## 1. ARCHIVE Device Flow

### Purpose
Archive devices that are no longer in use by marking them as inactive and deleted in the system.

### Detailed Step-by-Step Flow

#### Phase 1: User Request & Validation
```
1. User selects devices for archival from M2M UI
2. User clicks "Archive" button
3. Frontend sends POST request to M2MController.BulkChange()
   - ChangeType: "Archival"
   - Devices: List of ICCIDs
   - OverrideValidation: true/false
```

#### Phase 2: Controller Validation (M2MController.cs)
```
4. M2MController.ValidateBulkChange() is called
5. BuildArchivalChangeDetails() method executes:
   a. Check each ICCID exists in database
   b. Validate device is not already archived
   c. Check for active Rev Services:
      - Query Device_Tenant table
      - Check RevService.IsActive = true
      - If active services found and override = false → Error
   d. Check recent usage (last 30 days):
      - Check Device.LastUsageDate
      - If usage within ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS → Error
   e. Create M2M_DeviceChange records with validation results
```

#### Phase 3: Queue Processing
```
6. Create DeviceBulkChange record with Status = "NEW"
7. ProcessBulkChange() queues the request to SQS
8. User gets immediate response with BulkChangeId
```

#### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
```
9. Lambda receives SQS message
10. ProcessBulkChangeAsync() routes to ProcessArchivalAsync()
11. For each valid device:
    a. Execute stored procedure: usp_DeviceBulkChange_Archival_ArchiveDevices
    b. Update Device table:
       - Set IsActive = false
       - Set IsDeleted = true
       - Set ModifiedDate = current timestamp
       - Set ModifiedBy = "AltaworxDeviceBulkChange"
    c. Log success/failure in DeviceBulkChangeLog
12. Update BulkChange status to "PROCESSED"
```

#### Phase 5: Response & Logging
```
13. Create DeviceBulkChangeLog entries for each device
14. Update M2M_DeviceChange records with final status
15. Return processing results
```

---

## 2. ASSIGN CUSTOMER Flow

### Purpose
Associate devices with specific customer accounts and optionally create Rev Services for billing.

### Detailed Step-by-Step Flow

#### Phase 1: User Request
```
1. User navigates to Device Association screen
2. User selects devices (ICCIDs)
3. User selects Rev Customer from dropdown
4. User configures options:
   - Create Rev Service: Yes/No
   - Add Carrier Rate Plan: Yes/No
   - Add Customer Rate Plan: Yes/No
   - Effective Date (optional)
5. User submits AssociateCustomer request
```

#### Phase 2: Controller Processing (M2MController.cs)
```
6. M2MController.AssociateCustomer() receives request
7. Validation checks:
   a. Verify all ICCIDs exist in Device table
   b. Validate Rev Customer exists and user has access
   c. Check devices are not already assigned to another customer
   d. Validate device status allows assignment
8. BuildAssociateCustomerDeviceChanges() creates change records:
   a. Get Site information for customer
   b. Get IntegrationAuthenticationId for Rev API
   c. Create BulkChangeAssociateCustomer JSON payload
   d. Create M2M_DeviceChange records
```

#### Phase 3: Lambda Processing (AltaworxDeviceBulkChange.cs)
```
9. ProcessAssociateCustomerAsync() handles the request
10. For each device:
    a. Parse BulkChangeAssociateCustomer from ChangeRequest JSON
    
    b. IF CreateRevService = true:
       - Call Rev.io API to create service line
       - CreateServiceLineBody with:
         * CustomerId from RevCustomerId
         * Number = MSISDN or ICCID
         * ServiceTypeId, EffectiveDate, etc.
       - Log API call result
    
    c. Update Device_Tenant table:
       - Set SiteId = customer's site
       - Set AccountNumber = RevCustomerId
       - Set AccountNumberIntegrationAuthenticationId
       - Set IsActive = true, IsDeleted = false
    
    d. IF AddCustomerRatePlan = true:
       - Update Device_Tenant.CustomerRatePlanId
       - Update Device_Tenant.CustomerDataAllocationMB
       - Call usp_UpdateCrossProviderDeviceHistory
    
    e. Create DeviceActionHistory record for audit trail
```

#### Phase 4: Database Updates
```
11. Execute bulk database operations:
    a. Bulk insert/update Device_Tenant records
    b. Update DeviceBulkChangeLog
    c. Update cross-provider device history
    d. Send optimization trigger to 2.0 system
```

---

## 3. CHANGE CARRIER RATE PLAN Flow

### Purpose
Update carrier-level rate plans for devices through ThingSpace API.

### Detailed Step-by-Step Flow

#### Phase 1: User Request
```
1. User selects devices from M2M inventory
2. User chooses "Change Carrier Rate Plan"
3. User selects target rate plan from dropdown
4. User optionally sets effective date
5. Frontend sends BulkChange request with CarrierRatePlanChange type
```

#### Phase 2: Controller Validation (M2MController.cs)
```
6. BuildCarrierRatePlanChangeDetails() executes:
   a. Validate rate plan code exists in JasperCarrierRatePlan table
   b. For ThingSpace: no additional validation needed
   c. Check each ICCID exists and is active
   d. Create BulkChangeCarrierRatePlanUpdate JSON payload
   e. Create M2M_DeviceChange records
```

#### Phase 3: Lambda Processing (AltaworxDeviceBulkChange.cs)
```
7. ProcessCarrierRatePlanChangeAsync() routes to ProcessThingSpaceCarrierRatePlanChange()
8. Get ThingSpace authentication information:
   a. Retrieve credentials from database
   b. Check WriteIsEnabled = true
   c. Get access tokens

9. For each device:
   a. Parse CarrierRatePlanUpdate from ChangeRequest
   b. Create ThingSpaceDeviceDetail object:
      - ICCID = [device ICCID]
      - CarrierRatePlan = new rate plan code
   
   c. Call ThingSpace API:
      - ThingSpaceDeviceDetailService.UpdateThingSpaceDeviceDetailsAsync()
      - Send rate plan update request
      - Log API call and response
   
   d. IF ThingSpace API success:
      - Update Device table in AMOP:
        * Set CarrierRatePlan = new plan
        * Set ModifiedDate = current time
        * Set ModifiedBy = "AltaworxDeviceBulkChange"
      - Log database update result
   
   e. Mark M2M_DeviceChange as processed with success/failure status
```

#### Phase 4: Error Handling
```
10. Handle various error scenarios:
    a. Authentication failure → Stop processing, mark as error
    b. API rate limiting → Retry with exponential backoff
    c. Invalid rate plan → Mark individual device as failed
    d. Database update failure → Log error, mark as failed
```

---

## 4. CHANGE CUSTOMER RATE PLAN Flow

### Purpose
Update customer-specific rate plans and data allocations in AMOP system.

### Detailed Step-by-Step Flow

#### Phase 1: Individual Device Update (M2MController.cs)
```
1. User clicks on device in inventory grid
2. User selects "Edit Customer Rate Plan" 
3. User selects new CustomerRatePlanId from dropdown
4. User optionally sets CustomerRatePoolId
5. User sets effective date
6. M2MController.UpdateM2MCustomerRatePlan() is called directly
```

#### Phase 2: Direct Processing (No Lambda for individual updates)
```
7. Validation:
   a. Check device exists in Device table
   b. Verify Device_Tenant record exists for user's tenant
   c. Validate CustomerRatePlanId exists
   d. Check effective date is valid

8. Update Device_Tenant record:
   a. SET CustomerRatePlanId = new value
   b. SET CustomerDataAllocationMB = new allocation
   c. SET CustomerRatePoolId = new pool (if specified)
   d. SET ModifiedDate = current timestamp
   e. SET ModifiedBy = current user

9. Create audit trail:
   a. Create DeviceActionHistory record:
      - ChangedField = "CustomerRatePlan"
      - PreviousValue = old rate plan name
      - CurrentValue = new rate plan name
      - DateOfChange = current time
      - ChangedBy = current user
```

#### Phase 3: Bulk Processing (For multiple devices)
```
10. IF bulk update requested:
    a. Create DeviceBulkChange record
    b. Queue to Lambda for processing
    c. ProcessCustomerRatePlanChangeAsync() in Lambda:
       - Execute usp_DeviceBulkChange_CustomerRatePlan_Update
       - Update multiple Device_Tenant records
       - Create bulk DeviceActionHistory records
       - Update cross-provider device history
```

#### Phase 4: Integration Updates
```
11. Send notification to optimization system:
    a. Call OptimizationApiController.SendTriggerAmopSync()
    b. Trigger type: "m2m_inventory_live_sync"
    c. Include tenant and device information
```

---

## 5. CHANGE ICCID/IMEI Flow

### Purpose
Swap device identifiers (SIM cards or device hardware) through ThingSpace API with async callback handling.

### Detailed Step-by-Step Flow

#### Phase 1: User Request
```
1. User selects "Change Identifier" option
2. User provides:
   - Old ICCID/IMEI (current identifier)
   - New ICCID/IMEI (replacement identifier)
   - Identifier Type (ICCID or IMEI)
   - Optional: Customer Rate Plan changes
3. Frontend sends PostChangeIdentifier request
```

#### Phase 2: Controller Processing (M2MController.cs)
```
4. M2MController.PostChangeIdentifier() validates:
   a. Check old identifier exists in Device table
   b. Verify device is active status
   c. Validate new identifier format
   d. Check new identifier not already in use

5. BuildChangeIdentifier() creates:
   a. BulkChangeUpdateIdentifier object with:
      - OldICCID/OldIMEI = current values
      - NewICCID/NewIMEI = new values  
      - IdentifierType = ICCID or IMEI enum
      - AddCustomerRatePlan = true/false
   b. M2M_DeviceChange record with change request JSON
```

#### Phase 3: Lambda Processing (ProcessChangeICCIDorIMEI.cs)
```
6. ProcessThingSpaceChangeIdentifierAsync() executes:
   a. Get ThingSpace authentication and tokens
   b. For each device change:
      
      c. Build ThingSpace request:
         - DeviceIds: [{Id: oldIdentifier, Kind: "iccid"}]
         - DeviceIdsTo: [{Id: newIdentifier, Kind: "iccid"}] 
         - Change4gOption: "ChangeICCID" or "ChangeIMEI"
      
      d. Call ThingSpace API:
         - PutUpdateIdentifierAsync()
         - Receive requestId for async tracking
         - Log API call with requestId
      
      e. Queue retry mechanism:
         - EnqueueDeviceBulkChangesAsync() with retry flag
         - Set 3-minute delay for callback check
         - Mark device as "processing" status
```

#### Phase 4: Callback Handling & Retry
```
7. RetryUpdateIdentifierProcess() handles async completion:
   a. Check ThingSpaceCallBackResponseLog table using requestId
   b. IF callback received:
      - Parse APIStatus from callback
      - IF success:
        * Update Device table with new identifier
        * Call UpdateIdentifierForThingSpace stored procedure
        * Update customer rate plan if requested
        * Mark M2M_DeviceChange as successful
      - IF failure:
        * Mark M2M_DeviceChange as failed with error message
   
   c. IF no callback yet:
      - Increment retry counter
      - IF retries < THINGSPACE_UPDATE_DEVICE_STATUS_RETRY_NUMBER:
        * Re-queue with longer delay
      - ELSE:
        * Mark as failed due to timeout
```

#### Phase 5: Database Updates
```
8. UpdateIdentifierForThingSpace stored procedure:
   a. Update Device table:
      - SET ICCID = new value (if ICCID change)
      - SET IMEI = new value (if IMEI change)  
      - SET ModifiedDate = current time
      - SET ModifiedBy = "AltaworxDeviceBulkChange"
   
   b. Update related tables:
      - Device_Tenant associations
      - Cross-provider device history
      - Audit trail records
```

---

## 6. UPDATE DEVICE STATUS Flow

### Purpose
Change operational status of devices (Active, Inactive, Suspended, etc.) through ThingSpace API.

### Detailed Step-by-Step Flow

#### Phase 1: User Request
```
1. User selects devices from inventory grid
2. User chooses "Change Status" option
3. User selects target status from dropdown:
   - Active, Inactive, Suspended, etc.
4. User optionally provides:
   - Status reason code
   - ZIP code (for activation)
   - Rate plan (for activation)
5. User optionally chooses to create Rev Service
```

#### Phase 2: Controller Processing (M2MController.cs)
```
6. BuildStatusUpdateChangeDetailsThingSpace() validates:
   a. Check target status is valid for ThingSpace
   b. Verify status transition is allowed
   c. For activation requests:
      - Validate rate plan is specified
      - Check ZIP code (use default if not provided)
   d. Check devices exist and are not archived

7. Create status update request:
   a. BulkChangeStatusUpdateRequest with:
      - UpdateStatus = target status
      - ThingSpaceStatusUpdate with reason codes, ZIP, etc.
   b. Optional RevService creation parameters
   c. M2M_DeviceChange records with request JSON
```

#### Phase 3: Lambda Processing (AltaworxDeviceBulkChange.cs)
```
8. ProcessStatusUpdateAsync() handles ThingSpace devices:
   a. Get ThingSpace authentication and session tokens
   b. For each device:
      
      c. Build ThingSpace status request:
         - AccountName = from authentication
         - Devices = [ICCID] or [ICCID+IMEI] for activation
         - ServicePlan = rate plan (for activation)
         - CarrierIpPoolName, MdnZipCode, etc.
      
      d. Call appropriate ThingSpace API:
         - Device activation, deactivation, suspension, etc.
         - Log API request and response
      
      e. IF API successful:
         - Update Device.DeviceStatusId in AMOP
         - Update Device.MSISDN (if returned by carrier)
         - Update Device.IpAddress (if returned)
         - Set ModifiedBy = "AltaworxDeviceBulkChange"
      
      f. IF creating Rev Service:
         - Call Rev.io API to create billing service
         - Associate service with customer account
         - Set up service products and packages
```

#### Phase 4: Additional Processing for Activation
```
9. For device activation scenarios:
   a. Handle new service activation:
      - ProcessNewServiceActivationAsync()
      - Check device successfully activated
      - Retry activation if needed
      - Queue follow-up status checks
   
   b. Post-activation tasks:
      - Update device with MSISDN from carrier
      - Update IP address assignments  
      - Create customer rate plan associations
      - Send notifications to billing system
```

#### Phase 5: Error Handling & Retry
```
10. Handle various failure scenarios:
    a. Authentication failure → Retry with new tokens
    b. Device not found in ThingSpace → Mark as error
    c. Invalid status transition → Mark as error  
    d. Rate limit exceeded → Retry with exponential backoff
    e. Temporary API failure → Retry up to max attempts
    
11. Final status update:
    a. Mark M2M_DeviceChange as PROCESSED or ERROR
    b. Update BulkChange overall status
    c. Log detailed results for monitoring
```

---

## Common Error Handling Patterns

### Retry Mechanisms
```
1. SQL Transient Errors: 3 retries with exponential backoff
2. HTTP API Errors: 3 retries with increasing delays
3. ThingSpace Rate Limits: Automatic retry with carrier-specified delay
4. Authentication Failures: Token refresh and retry
5. Queue Processing: Re-queue failed items with incremental delays
```

### Validation Patterns
```
1. Pre-flight validation in Controller
2. Runtime validation in Lambda
3. Post-processing validation
4. Rollback mechanisms for partial failures
```

### Logging & Monitoring
```
1. DeviceBulkChangeLog for detailed API logs
2. DeviceActionHistory for audit trails  
3. CloudWatch metrics for performance monitoring
4. Error alerting for critical failures
```

---

*This detailed documentation covers the complete end-to-end flow for each of the six change types supported by Verizon ThingSpace IoT Service Provider.*