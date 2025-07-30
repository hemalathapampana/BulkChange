# POD19 Edit Username Workflow Diagram

## Overview

This document outlines the workflow for **POD19 Edit Username** change type processing. POD19 is a specific integration type that requires additional audit verification after username updates to ensure the changes were successfully applied in the carrier system.

## POD19 Edit Username Processing Flow

```
┌─────────────────────┐
│ SQS Event Received  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Function Handler    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Parse SQS Message   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract BulkChangeId│
│ & Parameters        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Message Type?       │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│ ProcessEditUsername │
│ Async               │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get Device Changes  │
│ (Edit Username)     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Integration Type?   │
└──────┬──────────────┘
       │
       ▼ (POD19)
┌─────────────────────┐
│ ProcessEditUsername │
│ JasperAsync         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check Write Enabled │
│ for Service Provider│
└──────┬──────────────┘
       │
       ▼ Write Enabled
┌─────────────────────┐
│ Get Jasper Auth     │
│ Information         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Username    │
│ Update Parameters   │
│ (ContactName,       │
│  CostCenter1-3)     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Contact Name        │
│ Provided?           │
└──────┬──────────────┘
       │
       ▼ Yes
┌─────────────────────┐
│ Call Jasper API     │
│ UpdateUsername      │
│ JasperDeviceAsync   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Update Successful?  │
└──────┬──────────────┘
       │
       ▼ Yes
┌─────────────────────┐
│ **POD19 SPECIFIC**  │
│ Call Jasper Audit   │
│ Trail API to Verify │
│ Username Updated    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ IsEditUsername      │
│ POD19Success?       │
└──────┬──────────────┘
       │
       ▼ Yes          ▼ No
┌──────────────┐    ┌──────────────┐
│ Success:     │    │ Error:       │
│ Username     │    │ Audit Failed │
│ Updated &    │    │ Mark as      │
│ Verified     │    │ HasErrors    │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌─────────────────────┐
│ Process Cost Center │
│ Updates (if any)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Cost Centers        │
│ Provided?           │
└──────┬──────────────┘
       │
       ▼ Yes
┌─────────────────────┐
│ Get Rev Service     │
│ Information         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Lookup Rev Service  │
│ by ICCID            │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get Cost Center     │
│ Field Indexes       │
│ (1, 2, 3)           │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Build Fields        │
│ Update Dictionary   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Call Rev API        │
│ UpdateService       │
│ Fields              │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Rev Update          │
│ Successful?         │
└──────┬──────────────┘
       │
       ▼ Yes          ▼ No
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Log Error    │
│ Response     │    │ Response     │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌─────────────────────┐
│ Add Bulk Change     │
│ Log Entry           │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Mark M2M Device     │
│ Change as Processed │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Continue with Next  │
│ Device Change       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ All Changes         │
│ Processed?          │
└──────┬──────────────┘
       │
       ▼ Yes          ▼ No
┌──────────────┐    ┌──────────────┐
│ Mark Bulk    │    │ Continue     │
│ Change as    │    │ Processing   │
│ PROCESSED    │    │ Loop         │
└──────────────┘    └──────────────┘
                           │
                           ▲
                           │
                    ┌──────┘
                    │
                    ▼
┌─────────────────────┐
│ End                 │
└─────────────────────┘
```

## Key POD19 Differences

### 1. **Audit Verification Step**
Unlike other Jasper integration types, POD19 includes a mandatory audit verification:
- After successful username update via Jasper API
- Calls `IsEditUsernamePOD19Success()` method
- Checks Jasper audit trail to verify the change was actually applied
- If audit fails, marks the operation as failed even if initial API call succeeded

### 2. **Two-Phase Validation**
```
Phase 1: Standard Jasper API Call
    ↓
Phase 2: POD19 Audit Trail Verification (UNIQUE TO POD19)
    ↓
Final Success/Failure Determination
```

### 3. **Enhanced Error Handling**
- Standard Jasper API error handling
- Additional POD19-specific audit failure handling
- Detailed logging of both API response and audit verification results

## Processing Components

### Input Parameters
- **ContactName**: New username to set
- **CostCenter1-3**: Cost center values (optional)
- **ICCID**: Device identifier

### Integration-Specific Logic
```csharp
// POD19 Specific Audit Check
if (bulkChange.IntegrationId == (int)IntegrationType.POD19)
{
    var isEditSuccess = await jasperDeviceService.IsEditUsernamePOD19Success(
        JasperDeviceAuditTrailPath, 
        change.ICCID, 
        Common.CommonString.ERROR_MESSAGE, 
        Common.CommonString.USERNAME_STRING
    );
    
    if (!isEditSuccess)
    {
        updateResult.HasErrors = true;
        updateResult.ResponseObject = "Update username failed - audit verification failed";
    }
}
```

### Error Scenarios
1. **Service Provider Write Disabled**
2. **Jasper API Authentication Failure**
3. **Username Update API Failure**
4. **POD19 Audit Verification Failure** (Unique to POD19)
5. **Rev Service Cost Center Update Failure**

## Security and Compliance

### POD19-Specific Requirements
- Enhanced audit trail verification
- Additional logging for compliance
- Mandatory verification of all username changes
- Stricter error handling and rollback procedures