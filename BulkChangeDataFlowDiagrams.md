# Bulk Change Data Flow Diagrams

This document contains dataflow diagrams for all 6 bulk change types supported by the system, following the same visual style and structure.

## 1. Customer Rate Plan Change (Type 4)

```
┌─────────────────────┐
│ Start Export Action │
│ Export(CustomerRatePlanChange) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check if User is    │
│ SuperAdmin or TenantAdmin │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ RuleDefinitionRepository │
│ GetListByType(CustomerRatePlanChange) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For each Rule       │
│ Create RuleExportModel │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ RuleExportModel Logic        │
│ RemainingDaysInCycle =       │
│ (EndDate - Today).Days       │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Switch(objectType): │
│ Set dataSetName     │
│ (CustomerRatePlan)  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessCustomerRatePlanChangeAsync │
│ Check EffectiveDate │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ Immediate Processing         │
│ EXEC usp_DeviceBulkChange_   │
│ CustomerRatePlanChange_      │
│ UpdateDevices               │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Generate Excel File │
│ ExcelUtilities.Export() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Send to Browser     │
│ FileContentResult   │
│ CustomerRatePlanExport.xlsx │
└─────────────────────┘
```

## 2. Carrier Rate Plan Change (Type 7)

```
┌─────────────────────┐
│ Start Export Action │
│ Export(CarrierRatePlanChange) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check if User is    │
│ SuperAdmin or TenantAdmin │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ RuleDefinitionRepository │
│ GetListByType(CarrierRatePlanChange) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For each Rule       │
│ Create RuleExportModel │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ RuleExportModel Logic        │
│ RemainingDaysInCycle =       │
│ (EndDate - Today).Days       │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Switch(objectType): │
│ Set dataSetName     │
│ (CarrierRatePlan / M2M) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BuildCustomerRatePlanChangeDetails │
│ Process CarrierRatePlanUpdate │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ Carrier-specific Processing   │
│ Jasper/ThingSpace/Telegence   │
│ API Integration              │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Generate Excel File │
│ ExcelUtilities.Export() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Send to Browser     │
│ FileContentResult   │
│ CarrierRatePlanExport.xlsx │
└─────────────────────┘
```

## 3. Status Update (Device Activation/Deactivation)

```
┌─────────────────────┐
│ Start Export Action │
│ Export(StatusUpdate) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check if User is    │
│ SuperAdmin or TenantAdmin │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ RuleDefinitionRepository │
│ GetListByType(StatusUpdate) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For each Rule       │
│ Create RuleExportModel │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ RuleExportModel Logic        │
│ RemainingDaysInCycle =       │
│ (EndDate - Today).Days       │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Switch(objectType): │
│ Set dataSetName     │
│ (CrossProvider / Mobility / M2M) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BuildStatusUpdateChangeDetails │
│ Process Status Changes │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ Carrier Integration          │
│ Jasper: BuildStatusUpdateRequestJasper │
│ ThingSpace: BuildStatusUpdateRequestThingSpace │
│ Telegence: ProcessNewServiceActivationStatusAsync │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Generate Excel File │
│ ExcelUtilities.Export() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Send to Browser     │
│ FileContentResult   │
│ StatusUpdateExport.xlsx │
└─────────────────────┘
```

## 4. Device Archival

```
┌─────────────────────┐
│ Start Export Action │
│ Export(Archival)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check if User is    │
│ SuperAdmin or TenantAdmin │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ RuleDefinitionRepository │
│ GetListByType(Archival) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For each Rule       │
│ Create RuleExportModel │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ RuleExportModel Logic        │
│ RemainingDaysInCycle =       │
│ (EndDate - Today).Days       │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Switch(objectType): │
│ Set dataSetName     │
│ (CrossProvider / Mobility / M2M) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BuildArchivalChangeDetails │
│ Check Recent Usage  │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ GetArchivalChanges           │
│ Filter by archivalRecentUsageCutoff │
│ Check ActiveRevServices      │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Generate Excel File │
│ ExcelUtilities.Export() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Send to Browser     │
│ FileContentResult   │
│ ArchivalExport.xlsx │
└─────────────────────┘
```

## 5. Edit Username

```
┌─────────────────────┐
│ Start Export Action │
│ Export(EditUsername) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check if User is    │
│ SuperAdmin or TenantAdmin │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ RuleDefinitionRepository │
│ GetListByType(EditUsername) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For each Rule       │
│ Create RuleExportModel │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ RuleExportModel Logic        │
│ RemainingDaysInCycle =       │
│ (EndDate - Today).Days       │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Switch(objectType): │
│ Set dataSetName     │
│ (CrossProvider / Mobility / M2M) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BuildUsernameChangeDetails │
│ Process Username Updates │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ Carrier Integration          │
│ Jasper: ProcessEditUsernameJasperAsync │
│ Telegence: ProcessEditUsernameTelegenceAsync │
│ AMOP: UpdateUsernameDeviceForAMOP │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Generate Excel File │
│ ExcelUtilities.Export() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Send to Browser     │
│ FileContentResult   │
│ EditUsernameExport.xlsx │
└─────────────────────┘
```

## 6. Change ICCID or IMEI

```
┌─────────────────────┐
│ Start Export Action │
│ Export(ChangeICCIDorIMEI) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check if User is    │
│ SuperAdmin or TenantAdmin │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ RuleDefinitionRepository │
│ GetListByType(ChangeICCIDorIMEI) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For each Rule       │
│ Create RuleExportModel │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ RuleExportModel Logic        │
│ RemainingDaysInCycle =       │
│ (EndDate - Today).Days       │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Switch(objectType): │
│ Set dataSetName     │
│ (CrossProvider / Mobility / M2M) │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessChangeEquipmentAsync │
│ Determine Change Type │
└──────────┬──────────┘
           │
           ▼
┌──────────────────────────────┐
│ Change Type Logic            │
│ if (newICCID != null): ICCID │
│ else: IMEI                   │
│ Set Change4gOption           │
└──────────┬───────────────────┘
           │
           ▼
┌──────────────────────────────┐
│ Carrier Integration          │
│ ThingSpace: ProcessThingSpaceChangeEquipmentAsync │
│ Telegence: ProcessTelegenceChangeEquipmentAsync │
│ Update Customer Rate Plan    │
└──────────┬───────────────────┘
           │
           ▼
┌─────────────────────┐
│ Generate Excel File │
│ ExcelUtilities.Export() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Send to Browser     │
│ FileContentResult   │
│ ChangeICCIDorIMEIExport.xlsx │
└─────────────────────┘
```

## Change Type Mapping

| Change Type ID | Change Type Name | Description |
|----------------|------------------|-------------|
| 4 | CustomerRatePlanChange | Customer-facing rate plan and data allocation changes |
| 7 | CarrierRatePlanChange | Carrier-specific network connectivity plan changes |
| - | StatusUpdate | Device activation, deactivation, suspension operations |
| - | Archival | Device archival based on usage and service status |
| - | EditUsername | Device username/identifier modifications |
| - | ChangeICCIDorIMEI | Device equipment identifier changes (SIM/Device swap) |

## Common Processing Elements

### User Authorization Check
All change types require SuperAdmin or TenantAdmin permissions before proceeding with the export.

### Rule Processing
1. **RuleDefinitionRepository**: Retrieves rules by change type
2. **RuleExportModel**: Creates export models with remaining days calculation
3. **DataSet Naming**: Sets appropriate dataset names based on object type

### Carrier Integration Points
- **Jasper**: Traditional M2M carrier integration
- **ThingSpace**: Verizon's IoT platform integration
- **Telegence**: Alternative carrier platform
- **AMOP**: Internal platform operations

### Export Generation
All flows conclude with:
1. Excel file generation using ExcelUtilities.Export()
2. FileContentResult delivery to browser
3. Standardized naming convention: `{ChangeType}Export.xlsx`

### Error Handling
Each flow includes:
- Authorization validation
- Rule definition validation
- Carrier API error handling
- Database transaction rollback scenarios
- Comprehensive logging throughout the process