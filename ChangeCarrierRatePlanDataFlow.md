# POND IoT Service Provider - Change Carrier Rate Plan Data Flow

## Overview
The CHANGE CARRIER RATE PLAN Device Flow is a bulk change operation that updates the carrier-level rate plans for devices in the system. This process enables service providers to modify device communication plans, data allocations, and billing rates directly with the carrier infrastructure. The operation validates rate plan compatibility, creates or updates carrier packages, terminates existing plans, and maintains comprehensive audit trails across different portal types (M2M, Mobility) and carrier integrations (POND, Jasper, ThingSpace, Teal, eBonding, Telegence).

## Whole Flow:
User Interface → M2MController.BulkChange() → BuildCustomerRatePlanChangeDetails() → Carrier Rate Plan Validation → Rate Plan Repository Lookup → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessCarrierRatePlanChangeAsync() → GetDeviceChanges() → Service Provider Routing → Carrier-Specific Processing → Package Management (Add/Terminate) → Database Update → Portal-Specific Logging → BulkChangeStatus.PROCESSED → Rate Plan Change Complete

## Process Flow:

### Phase 1: User Request & Validation
1. User selects devices for carrier rate plan change from M2M UI
2. User selects target carrier rate plan and configuration
3. User clicks "Continue" button
4. Frontend sends POST request to M2MController.BulkChange()
   - ChangeType: "CarrierRatePlanChange" (DeviceChangeType.CarrierRatePlanChange = 7)
   - Devices: List of ICCIDs
   - CarrierRatePlanUpdate: Rate plan configuration
   - EffectiveDate: Optional future effective date

### Phase 2: Controller Validation (M2MController.cs)
M2MController.ValidateBulkChange() is called
BuildCustomerRatePlanChangeDetails() method executes:
- Check each ICCID exists in database
- Validate carrier rate plan exists in system
- Check service provider compatibility
- Validate device requirements (EID for Teal integration)
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
- **Device Existence:** Verify ICCID exists in device inventory
- **Device Status:** Check device is not archived (IsActive=true, IsDeleted=false)
- **Rate Plan Validation:** Validate CarrierRatePlan exists using JasperCarrierRatePlanRepository
- **Integration-Specific Validation:**
  - **Teal Integration:** Device must have valid EID
  - **POND Integration:** Set RatePlanId from JasperRatePlanId
  - **Other Integrations:** Set PlanUuid if required
- **Permission Validation:** Verify user has access to target service provider

```csharp
// From M2MController.cs lines 1654-1673
if (changeType.Equals(DeviceChangeType.CarrierRatePlanChange))
{
    var carrierRatePlanRepository = new JasperCarrierRatePlanRepository(awxDb);
    var carrierRatePlanCode = bulkChange.CarrierRatePlanUpdate.CarrierRatePlan;
    var carrierRatePlan = carrierRatePlanRepository.GetByCarrierRatePlanCode(carrierRatePlanCode);
    
    if (integrationId.Equals((int)IntegrationType.Teal))
    {
        bulkChange.CarrierRatePlanUpdate.PlanUuid = carrierRatePlan.PlanUuid;
    }
    else if (integrationId.Equals((int)IntegrationType.Pond))
    {
        bulkChange.CarrierRatePlanUpdate.RatePlanId = carrierRatePlan.JasperRatePlanId.Value;
    }
}
```

### Phase 3: Queue Processing
1. Create DeviceBulkChange record with Status = "NEW"
2. ProcessBulkChange() queues the request to SQS
   - BulkChangeId: Generated ID
   - ChangeRequestTypeId: DeviceChangeType.CarrierRatePlanChange (7)
   - ServiceProviderId: Target service provider
   - TenantId: Current tenant
3. User gets immediate response with BulkChangeId

### Phase 4: Lambda Processing (AltaworxDeviceBulkChange.cs)
Lambda receives SQS message
ProcessBulkChangeAsync() routes to ProcessCarrierRatePlanChangeAsync()
Located at lines 648-677 in AltaworxDeviceBulkChange.cs

**Service Provider Routing:**
```csharp
// From AltaworxDeviceBulkChange.cs line 508
case ChangeRequestType.CarrierRatePlanChange:
    return await ProcessCarrierRatePlanChangeAsync(context, logRepo, bulkChange, sqlRetryPolicy);
```

**Integration-Specific Processing:**
- **Telegence:** ProcessTelegenceCarrierRatePlanChange()
- **eBonding:** ProcessEBondingCarrierRatePlanChange()
- **Jasper/TMobileJasper/POD19/Rogers:** ProcessJasperCarrierRatePlanChange()
- **ThingSpace:** ProcessThingSpaceCarrierRatePlanChange()
- **Teal:** ProcessTealCarrierRatePlanChange()
- **POND:** ProcessPondCarrierRatePlanChange()

### Phase 5: POND-Specific Processing (ProcessPondCarrierRatePlanChange)
Located at lines 1022-1130 in AltaworxDeviceBulkChange.cs

**Authentication & Setup:**
1. Retrieve POND authentication credentials
2. Validate service provider is enabled for write operations
3. Initialize POND API service with appropriate environment (Sandbox/Production)

**For each valid device:**
1. **Get Existing Packages:** Retrieve active POND packages for device
2. **Add New Package:** Create new carrier rate plan package via POND API
3. **Activate New Package:** Update package status to ACTIVE
4. **Terminate Old Packages:** Deactivate existing packages if new package successful
5. **Database Update:** Save package information to local database
6. **Device Rate Plan Update:** Update M2M device record with new rate plan
7. **Audit Logging:** Create comprehensive log entries

**POND Package Management Logic:**
```csharp
// From AltaworxDeviceBulkChange.cs lines 1074-1123
var existingPackageIds = pondRepository.GetExistingPackages(
    ParameterizedLog(context), change.ICCID, serviceProviderId, 
    PondHelper.PackageStatus.ACTIVE
);

// Add new package based on Package Template Id
var carrierRatePlan = JsonConvert.DeserializeObject<BulkChangeCarrierRatePlanUpdate>(change.ChangeRequest);
var pondAddPackageResponse = await AddNewPondPackage(
    logRepo, bulkChange, pondAuthentication, pondApiService, 
    baseUri, change, carrierRatePlan
);

// Activate new package and terminate existing ones
if (!updateStatusResult.HasErrors)
{
    pondPackage.Status = PondHelper.PackageStatus.ACTIVE;
    
    if (existingPackageIds != null && existingPackageIds.Count > 0)
    {
        updateStatusResult = await TerminateExistingPackages(
            context, existingPackageIds, pondApiService, 
            pondAuthentication, baseUri, processedBy, 
            pondRepository, bulkChange.Id, change.Id, logRepo
        );
    }
}
```

### Phase 6: Database Operations & Response
**For Each Carrier Integration:**

**POND Integration:**
- Create/Update POND package records
- Terminate existing active packages
- Update device carrier rate plan associations
- Create comprehensive audit trail

**eBonding Integration:**
- Execute stored procedure: `usp_DeviceBulkChange_eBonding_CarrierRatePlanChange`
- Update carrier-specific device configurations

**Other Integrations (Jasper, ThingSpace, Teal, Telegence):**
- Call carrier-specific APIs
- Update device rate plan configurations
- Handle carrier-specific validation and error conditions

### Phase 7: Response & Logging
1. Create DeviceBulkChangeLog entries for each device
   - M2M_DeviceChangeId: Reference to device change
   - ResponseStatus: PROCESSED/ERROR
   - LogEntryDescription: Details of operation and any errors
   - ProcessedDate: Timestamp
2. Update M2M_DeviceChange records with final status
3. Return processing results

## Key Database Operations

**Tables Updated:**
- **DeviceBulkChange:** Main bulk change record
- **M2M_DeviceChange:** Individual device change records  
- **DeviceBulkChangeLog:** Audit trail entries
- **Device_Tenant:** Device carrier rate plan associations
- **POND_Package:** POND-specific package records (for POND integration)
- **JasperCarrierRatePlan:** Rate plan reference data

**Integration Points:**
- **POND API:** For package creation, activation, and termination
- **Jasper API:** For Jasper-based carrier operations
- **ThingSpace API:** For Verizon ThingSpace operations
- **Teal API:** For Teal carrier operations
- **eBonding:** For eBonding carrier operations
- **Telegence API:** For Telegence carrier operations
- **SQS Queue:** For asynchronous processing
- **Webhook Notifications:** For status updates

## Data Models

**CarrierRatePlanUpdate:**
```csharp
public class CarrierRatePlanUpdate
{
    public string CarrierRatePlan { get; set; }
    public string CommPlan { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string PlanUuid { get; set; }
    public long RatePlanId { get; set; }
}
```

**BulkChangeCarrierRatePlanUpdate:**
```csharp
public class BulkChangeCarrierRatePlanUpdate
{
    public CarrierRatePlanUpdate CarrierRatePlanUpdate { get; set; }
}
```

## Stored Procedures

**usp_DeviceBulkChange_eBonding_CarrierRatePlanChange**
- **Purpose:** Handles the backend logic for eBonding carrier rate plan changes
- **Location:** Database stored procedures
- **Function:** Processes device carrier rate plan updates for eBonding integration
- **Parameters:** ICCID, new carrier rate plan code, effective date, user information

## Error Handling & Validation

**Common Validation Errors:**
- Invalid ICCID (device does not exist)
- Carrier rate plan does not exist in system
- Device missing required EID (Teal integration)
- Service provider authentication failure
- Carrier API communication errors
- Active Rev Services conflicts

**POND-Specific Error Handling:**
- Authentication credential validation
- Service provider write permissions
- Package creation failures
- Package activation/termination errors
- Existing package conflicts

**Error Response Format:**
```json
{
    "isValid": false,
    "validationMessage": "One or more devices have validation errors",
    "errors": ["Invalid ICCID: 1234567890", "Rate plan 'PLAN123' does not exist"]
}
```

## Security & Authorization

**Access Control:**
- User must have CREATE permission for M2M module
- Service provider access validation
- Tenant-level data isolation
- Rate plan visibility restrictions based on user permissions

**Audit Trail:**
- Complete logging of all rate plan changes
- User identification and timestamp tracking
- Before/after state capture
- Integration-specific operation logging