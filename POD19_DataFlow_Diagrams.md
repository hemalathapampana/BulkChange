# POD19 Technical Data Flow Diagrams

## Overview

This document provides detailed data flow diagrams for each change type supported in the POD19 technical dataflow system. POD19 is an integration type that follows the Jasper processing pattern for various bulk change operations.

## Supported Change Types

POD19 supports the following change types:
1. **Customer Rate Plan Change** (Type 4)
2. **Carrier Rate Plan Change** (Type 7)
3. **Status Update**
4. **Edit Username**
5. **Archival**
6. **Change ICCID/IMEI**

---

## 1. Customer Rate Plan Change Data Flow

### Overview
Customer Rate Plan Changes manage billing plans and data allocation at the customer/tenant level.

### Data Flow Diagram

```
┌─────────────────────┐
│ Client Request      │
│ CustomerRatePlan    │
│ Update              │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ Validation          │
│ - CustomerRatePlanId│
│ - DataAllocationMB  │
│ - CustomerPoolId    │
│ - EffectiveDate     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Processing    │
│ (Uses Jasper Flow)  │
└──────────┬──────────┘
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
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Execute SP   │    │ Add to Queue │
│ usp_Device   │    │ Customer     │
│ BulkChange_  │    │ RatePlan     │
│ Customer     │    │ DeviceQueue  │
│ RatePlan     │    │ Table        │
│ Change_      │    │              │
│ UpdateDevices│    │              │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Update Device│    │ Queue for    │
│ Rate Plans   │    │ Future       │
│              │    │ Processing   │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success/ │    │ Log Queue    │
│ Error to     │    │ Status       │
│ M2M/Mobility │    │              │
│ Portal       │    │              │
└──────────────┘    └──────────────┘
```

### Key Components
- **Input**: CustomerRatePlanUpdate with ID, allocation, pool, and effective date
- **Processing**: POD19 uses Jasper processing pattern
- **Storage**: Immediate DB update or queue for future processing
- **Logging**: Portal-specific logging (M2M/Mobility)

---

## 2. Carrier Rate Plan Change Data Flow

### Overview
Carrier Rate Plan Changes manage network connectivity plans at the carrier/service provider level.

### Data Flow Diagram

```
┌─────────────────────┐
│ Client Request      │
│ CarrierRatePlan     │
│ Update              │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ Validation          │
│ - CarrierRatePlan   │
│ - CommPlan          │
│ - PlanUuid          │
│ - RatePlanId        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Integration   │
│ Check               │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessJasper       │
│ CarrierRatePlan     │
│ Change()            │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Jasper              │
│ Authentication      │
│ Check               │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Auth Success │    │ Auth Failed  │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Write        │    │ Log Error    │
│ Permission   │    │ Return False │
│ Check        │    │              │
└──────┬───────┘    └──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Write        │    │ Write        │
│ Enabled      │    │ Disabled     │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Process      │    │ Log Warning  │
│ Rate Plan    │    │ Return False │
│ Changes      │    │              │
└──────┬───────┘    └──────────────┘
       │
       ▼
┌──────────────┐
│ Update       │
│ Devices with │
│ New Carrier  │
│ Rate Plan    │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Log Results  │
│ to Portal    │
└──────────────┘
```

### Key Components
- **Input**: CarrierRatePlanUpdate with carrier-specific plan details
- **Authentication**: Jasper authentication validation
- **Processing**: POD19 follows Jasper carrier rate plan processing
- **Security**: Write permission validation
- **Output**: Device updates with new carrier rate plan

---

## 3. Status Update Data Flow

### Overview
Status Update changes manage device activation states and service status.

### Data Flow Diagram

```
┌─────────────────────┐
│ Client Request      │
│ StatusUpdate        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ Validation          │
│ - TargetStatus      │
│ - Device List       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Integration   │
│ Type Check          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessJasper       │
│ StatusUpdateAsync() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Authentication &    │
│ Retry Policy Setup  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Rev API Client      │
│ Integration         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Device Processing   │
│ Loop                │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐
│ For Each     │
│ Device       │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Update Device│
│ Status via   │
│ Jasper API   │
└──────┬───────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ API Success  │    │ API Failure  │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Update Local │    │ Retry Logic  │
│ Device Status│    │ Application  │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Log Error    │
│ to Portal    │    │ to Portal    │
└──────────────┘    └──────────────┘
```

### Key Components
- **Input**: StatusUpdate with target status and device identifiers
- **Processing**: POD19 uses Jasper status update processing
- **API Integration**: Jasper API calls for status changes
- **Retry Logic**: HTTP retry policy for failed requests
- **Logging**: Comprehensive success/error logging

---

## 4. Edit Username Data Flow

### Overview
Edit Username changes manage device username/identifier updates.

### Data Flow Diagram

```
┌─────────────────────┐
│ Client Request      │
│ EditUsername        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ Validation          │
│ - New Username      │
│ - Device ICCID      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Integration   │
│ Check               │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessEditUsername │
│ JasperAsync()       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Username    │
│ Update Request      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ AMOP Integration    │
│ Username Update     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Specific      │
│ Success Check       │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐
│ Audit Trail  │
│ Verification │
│ for POD19    │
└──────┬───────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Success      │    │ Failure      │
│ Detected     │    │ Detected     │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Update Local │    │ Log Error    │
│ Username     │    │ Keep Old     │
│              │    │ Username     │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Log Success  │    │ Log Failure  │
│ to Portal    │    │ to Portal    │
└──────────────┘    └──────────────┘
```

### Key Components
- **Input**: BulkChangeEditUsername with new username and device ICCID
- **Processing**: POD19 uses Jasper username editing with special verification
- **Verification**: POD19-specific audit trail checking via `IsEditUsernamePOD19Success`
- **AMOP Integration**: Altaworx Management Operations Platform updates
- **Validation**: Success confirmation through audit trail analysis

---

## 5. Archival Data Flow

### Overview
Archival changes manage device lifecycle and data retention.

### Data Flow Diagram

```
┌─────────────────────┐
│ Client Request      │
│ Archival            │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ Validation          │
│ - Device List       │
│ - Archival Reason   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Integration   │
│ Type Check          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BuildArchival       │
│ ChangeDetails()     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Permission          │
│ Validation          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Device Archive      │
│ Processing          │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐
│ For Each     │
│ Device       │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Archive      │
│ Device Data  │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Update Device│
│ Status to    │
│ Archived     │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Data         │
│ Retention    │
│ Policy Apply │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Log Archival │
│ Results      │
└──────────────┘
```

### Key Components
- **Input**: Archival request with device list and reason
- **Processing**: POD19 follows standard archival processing
- **Permissions**: User permission validation for archival operations
- **Data Management**: Device data archiving and retention policy application
- **Status Updates**: Device status changes to archived state

---

## 6. Change ICCID/IMEI Data Flow

### Overview
ICCID/IMEI changes manage device identifier updates for SIM/device replacements.

### Data Flow Diagram

```
┌─────────────────────┐
│ Client Request      │
│ Change ICCID/IMEI   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ BulkChangeRequest   │
│ Validation          │
│ - Old ICCID/IMEI    │
│ - New ICCID/IMEI    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Integration   │
│ Type Check          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ProcessChange       │
│ ICCIDorIMEI()       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Identifier Type     │
│ Detection           │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ ICCID Change │    │ IMEI Change  │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ ICCID        │    │ IMEI         │
│ Validation   │    │ Validation   │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Update Device│    │ Update Device│
│ ICCID        │    │ IMEI         │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Carrier      │    │ Device Type  │
│ Integration  │    │ Validation   │
│ Update       │    │              │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Customer     │    │ Customer     │
│ Rate Plan    │    │ Rate Plan    │
│ Auto-Update  │    │ Auto-Update  │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌─────────────────────────┐
│ Log Change Results      │
│ to Portal               │
└─────────────────────────┘
```

### Key Components
- **Input**: Old and new ICCID/IMEI identifiers
- **Processing**: POD19 follows standard identifier change processing
- **Validation**: Identifier format and uniqueness validation
- **Integration**: Carrier system updates for new identifiers
- **Auto-Updates**: Automatic customer rate plan updates if applicable

---

## POD19 Integration Pattern

### Common Processing Elements

All POD19 change types share these common elements:

```
┌─────────────────────┐
│ POD19 Request       │
│ Received            │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Integration Type    │
│ Check: POD19        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Route to Jasper     │
│ Processing Pattern  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Change Type         │
│ Specific Processing │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POD19 Specific      │
│ Validations/Checks  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Standard Jasper     │
│ Flow Execution      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Results Logging     │
│ (Portal Specific)   │
└─────────────────────┘
```

### Key Integration Points

1. **Authentication**: Jasper authentication system
2. **Processing**: Jasper processing patterns with POD19 customizations
3. **Logging**: Portal-specific logging (M2M/Mobility)
4. **Error Handling**: Jasper error handling patterns
5. **Retry Logic**: HTTP retry policies for external API calls

### Security and Permissions

- **Tenant-level access control**
- **Integration-specific write permissions**
- **Role-based operation restrictions**
- **Audit trail maintenance**

### Performance Considerations

- **Bulk processing capabilities**
- **Async operation support**
- **Connection pooling**
- **Retry mechanisms for failed operations**

---

## Conclusion

POD19 technical dataflow leverages the robust Jasper processing framework while providing specialized handling for specific operations like username editing verification. Each change type follows a consistent pattern while accommodating the unique requirements of different device management operations.