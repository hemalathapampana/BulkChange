# POD19 Dataflow Diagrams for 6 Change Types

## Overview

This document provides comprehensive dataflow diagrams for the 6 primary change types supported by the POD19 integration within the device bulk change system. POD19 is one of the integration types that handles device management operations through the Jasper platform.

## POD19 Integration Context

POD19 (`IntegrationType.POD19`) is a Jasper-based integration that processes device changes through the Jasper API infrastructure. It shares processing patterns with other Jasper integrations like standard Jasper, TMobileJasper, and Rogers.

## The 6 Change Types

1. **Customer Rate Plan Change** (`DeviceChangeType.CustomerRatePlanChange`)
2. **Carrier Rate Plan Change** (`DeviceChangeType.CarrierRatePlanChange`)
3. **Status Update** (`DeviceChangeType.StatusUpdate`)
4. **Edit Username** (`DeviceChangeType.EditUsername`)
5. **Change ICCID or IMEI** (`DeviceChangeType.ChangeICCIDorIMEI`)
6. **Archival** (`DeviceChangeType.Archival`)

---

## 1. Customer Rate Plan Change Dataflow

### Purpose
Updates customer-facing billing plans and data allocation for POD19 devices.

### Dataflow Diagram
```
┌─────────────────────┐
│ Client Request      │
│ (Customer Rate Plan)│
│ POD19 Devices       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ ChangeType: 4       │
│ ServiceProvider:    │
│ POD19               │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate POD19      │
│ Service Provider    │
│ & Customer Plan     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Customer    │
│ Rate Plan Update:   │
│ • CustomerRatePlanId│
│ • DataAllocationMB  │
│ • CustomerPoolId    │
│ • EffectiveDate     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check Effective     │
│ Date vs Current     │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Immediate    │    │ Scheduled    │
│ Processing   │    │ Processing   │
│ (Now)        │    │ (Future)     │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Execute SP:  │    │ Add to Queue:│
│ CustomerRate │    │ CustomerRate │
│ PlanChange   │    │ PlanDevice   │
│ UpdateDevices│    │ Queue Table  │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Update POD19 │    │ Schedule for │
│ Device Rate  │    │ Future       │
│ Plans via DB │    │ Processing   │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success/ │    │ Log Queue    │
│ Error to     │    │ Status       │
│ AMOP/M2M     │    │              │
└──────────────┘    └──────────────┘
```

### Key Data Elements
- **Input**: `CustomerRatePlanId`, `CustomerDataAllocationMB`, `CustomerPoolId`, `EffectiveDate`
- **Processing**: Database stored procedures for immediate or queue-based future processing
- **Output**: Updated device rate plan assignments in AMOP database

---

## 2. Carrier Rate Plan Change Dataflow

### Purpose
Updates carrier-specific network connectivity plans for POD19 devices through Jasper API.

### Dataflow Diagram
```
┌─────────────────────┐
│ Client Request      │
│ (Carrier Rate Plan) │
│ POD19 Devices       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ ChangeType: 7       │
│ POD19 Integration   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Carrier     │
│ Rate Plan Update:   │
│ • CarrierRatePlan   │
│ • CommPlan          │
│ • PlanUuid          │
│ • RatePlanId        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate POD19      │
│ Carrier Plan        │
│ Compatibility       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get POD19 Jasper    │
│ Authentication      │
│ Credentials         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device:    │
│ Build Jasper API    │
│ Request             │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Execute Jasper API  │
│ Rate Plan Change    │
│ (POD19 Endpoint)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Parse Jasper        │
│ Response            │
│ (Success/Error)     │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Success:     │    │ Error:       │
│ Update DB    │    │ Log Error    │
│ with New     │    │ Mark Device  │
│ Rate Plan    │    │ as Failed    │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Error        │
│ to M2M/      │    │ Notification │
│ Mobility     │    │ & Logging    │
└──────────────┘    └──────────────┘
```

### Key Data Elements
- **Input**: `CarrierRatePlan`, `CommPlan`, `PlanUuid`, `RatePlanId`
- **Processing**: Jasper API calls for rate plan modification
- **Output**: Updated carrier rate plan assignments via Jasper

---

## 3. Status Update Dataflow

### Purpose
Updates device activation status (Active, Suspended, etc.) for POD19 devices through Jasper API.

### Dataflow Diagram
```
┌─────────────────────┐
│ Client Request      │
│ (Status Update)     │
│ POD19 Devices       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ ChangeType: Status  │
│ POD19 Integration   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Status      │
│ Update Request:     │
│ • TargetStatus      │
│ • Reason            │
│ • EffectiveDate     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate POD19      │
│ Status Transition   │
│ Rules               │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get POD19 Jasper    │
│ Authentication      │
│ & Service Details   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessNewService   │
│ ActivationStatus    │
│ Async               │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device:    │
│ Build Jasper Status │
│ Update Request      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Execute Jasper API  │
│ Status Change       │
│ (POD19 Endpoint)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Monitor Jasper      │
│ Async Response      │
│ Status              │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Success:     │    │ Pending/     │
│ Update Device│    │ Error:       │
│ Status in DB │    │ Log & Retry  │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Error        │
│ Mark as      │    │ Handling &   │
│ PROCESSED    │    │ Notification │
└──────────────┘    └──────────────┘
```

### Key Data Elements
- **Input**: `TargetStatus`, `ReasonCode`, `EffectiveDate`
- **Processing**: Jasper API status update with async monitoring
- **Output**: Updated device status in AMOP and carrier systems

---

## 4. Edit Username Dataflow

### Purpose
Updates device username/subscriber identifier for POD19 devices through Jasper API.

### Dataflow Diagram
```
┌─────────────────────┐
│ Client Request      │
│ (Edit Username)     │
│ POD19 Devices       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ ChangeType:         │
│ EditUsername        │
│ POD19 Integration   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Username    │
│ Update Request:     │
│ • NewUsername       │
│ • Validation Rules  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate Username   │
│ Format & Rules      │
│ for POD19           │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get POD19 Jasper    │
│ Authentication      │
│ & Device Service    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessEditUsername │
│ Async               │
│ (POD19 Specific)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device:    │
│ Build Jasper Edit   │
│ Username Request    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Execute Jasper API  │
│ Username Update     │
│ (POD19 Endpoint)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check Edit Success  │
│ via Jasper Audit    │
│ Trail               │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Success:     │    │ Error:       │
│ Update DB    │    │ Log Error    │
│ Username     │    │ Send Email   │
│ Reference    │    │ Notification │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Error        │
│ Mark as      │    │ Recovery &   │
│ PROCESSED    │    │ Notification │
└──────────────┘    └──────────────┘
```

### Key Data Elements
- **Input**: `NewUsername`, validation patterns
- **Processing**: Jasper API username update with audit trail verification
- **Output**: Updated username in Jasper and local database

---

## 5. Change ICCID or IMEI Dataflow

### Purpose
Updates device hardware identifiers (ICCID or IMEI) for POD19 devices. Note: POD19 uses Jasper processing, not ThingSpace.

### Dataflow Diagram
```
┌─────────────────────┐
│ Client Request      │
│ (Change ICCID/IMEI) │
│ POD19 Devices       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ ChangeType:         │
│ ChangeICCIDorIMEI   │
│ POD19 Integration   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Change      │
│ Equipment Request:  │
│ • NewICCID          │
│ • NewIMEI           │
│ • ChangeType        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate ICCID/IMEI │
│ Format & Uniqueness │
│ for POD19           │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Processing    │
│ Route:              │
│ NOT ThingSpace      │
│ (Jasper-based)      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessChange       │
│ Equipment Async     │
│ (POD19 Path)        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device:    │
│ Build Equipment     │
│ Change Request      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Execute Jasper API  │
│ Equipment Change    │
│ (POD19 Endpoint)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate Change     │
│ Success via         │
│ Database Query      │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Success:     │    │ Error:       │
│ Update DB    │    │ Log Error    │
│ with New     │    │ Rollback     │
│ ICCID/IMEI   │    │ Changes      │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Error        │
│ Mark as      │    │ Notification │
│ PROCESSED    │    │ & Recovery   │
└──────────────┘    └──────────────┘
```

### Key Data Elements
- **Input**: `NewICCID`, `NewIMEI`, change type indicator
- **Processing**: Jasper-based equipment change (not ThingSpace for POD19)
- **Output**: Updated hardware identifiers in Jasper and local systems

---

## 6. Archival Dataflow

### Purpose
Archives devices by moving them to inactive/archived status for POD19 devices.

### Dataflow Diagram
```
┌─────────────────────┐
│ Client Request      │
│ (Archive Devices)   │
│ POD19 Devices       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ ChangeType:         │
│ Archival            │
│ POD19 Integration   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Archival    │
│ Request:            │
│ • Archive Reason    │
│ • Effective Date    │
│ • Retention Policy  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate Archival   │
│ Permissions &       │
│ Business Rules      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Build Archival      │
│ Change Details      │
│ for POD19 Devices   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessArchival     │
│ Changes per         │
│ Device              │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device:    │
│ Deactivate Services │
│ Update Status       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Move to Archive     │
│ Tables/Status       │
│ (Database)          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Update POD19        │
│ Service Provider    │
│ Status (if needed)  │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Success:     │    │ Error:       │
│ Mark Devices │    │ Log Error    │
│ as ARCHIVED  │    │ Maintain     │
│              │    │ Active State │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Error        │
│ Complete     │    │ Notification │
│ Archive      │    │ & Recovery   │
└──────────────┘    └──────────────┘
```

### Key Data Elements
- **Input**: Archive reason, effective date, retention policy
- **Processing**: Status updates and database archival operations
- **Output**: Devices moved to archived status with retained audit history

---

## POD19 Integration Specifics

### Authentication Pattern
```
POD19 Jasper Authentication → Session Token → API Calls
```

### Common Processing Elements

1. **Service Provider Validation**
   - Verify POD19 integration type
   - Validate service provider configuration
   - Check authentication credentials

2. **Device Validation**
   - Validate device exists in POD19 system
   - Check device status compatibility
   - Verify tenant permissions

3. **API Communication**
   - Use Jasper API endpoints (shared with other Jasper integrations)
   - Handle async responses where applicable
   - Implement retry policies for failed requests

4. **Logging Pattern**
   - Log to both M2M and Mobility portal logs
   - Include request/response details
   - Track processing status (PENDING → PROCESSED/ERROR)

5. **Error Handling**
   - Capture API errors
   - Database transaction rollback
   - Email notifications for critical failures
   - Audit trail maintenance

### Database Tables Involved

- **Device Tables**: Device status and configuration
- **BulkChange Tables**: Change tracking and status
- **Log Tables**: Audit trail and error tracking
- **Queue Tables**: Future-scheduled changes
- **Authentication Tables**: POD19 service provider credentials

## Integration Flow Summary

All POD19 change types follow this general pattern:

1. **Request Validation** → **POD19 Authentication** → **Device Processing** → **API Communication** → **Response Handling** → **Database Updates** → **Logging**

The specific implementation varies by change type, but the core infrastructure and error handling patterns remain consistent across all 6 change types for POD19 integration.