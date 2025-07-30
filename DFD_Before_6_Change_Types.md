# Data Flow Diagram (DFD) - Before Suggested Changes
## 6 Device Change Types - Current State Analysis

### Overview
This document presents the Data Flow Diagram (DFD) for the current implementation of the 6 main device change types in the bulk change processing system **before** any suggested improvements are implemented.

---

## Level 0 DFD - Context Diagram

```
                    ┌─────────────────────────────────────────────────────────┐
                    │                                                         │
                    │            BULK DEVICE CHANGE SYSTEM                   │
                    │                                                         │
                    │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐   │
                    │  │ Customer│  │ Carrier │  │ Status  │  │ Device  │   │
                    │  │ Rate    │  │ Rate    │  │ Update  │  │ Mgmt    │   │
                    │  │ Plan    │  │ Plan    │  │         │  │         │   │
                    │  │ Change  │  │ Change  │  │         │  │         │   │
                    │  └─────────┘  └─────────┘  └─────────┘  └─────────┘   │
                    │                                                         │
                    │  ┌─────────┐  ┌─────────┐                             │
                    │  │ Archival│  │Username │                             │
                    │  │ Process │  │ Edit    │                             │
                    │  │         │  │         │                             │
                    │  └─────────┘  └─────────┘                             │
                    │                                                         │
                    └─────────────────────────────────────────────────────────┘
                              ▲                                    ▲
                              │                                    │
                              │                                    │
              ┌───────────────┴────────────────┐      ┌──────────┴──────────┐
              │                                │      │                     │
              │        M2M/Mobility            │      │   External APIs     │
              │        Portal Users            │      │   (Carriers)        │
              │                                │      │                     │
              └────────────────────────────────┘      └─────────────────────┘
```

---

## Level 1 DFD - Main Process Overview

```
                    Client Request
                         │
                         ▼
                ┌─────────────────┐
                │  1.0            │
                │ Validate        │◄──── Service Provider Config
                │ Request         │
                └─────┬───────────┘
                      │
                      ▼
                ┌─────────────────┐
                │  2.0            │
                │ Determine       │
                │ Change Type     │
                └─────┬───────────┘
                      │
              ┌───────┼───────┐
              │       │       │
              ▼       ▼       ▼
    ┌─────────────┐ ┌──────────────┐ ┌────────────────┐
    │  3.1        │ │  3.2         │ │  3.3           │
    │ Customer    │ │ Carrier      │ │ Status         │
    │ Rate Plan   │ │ Rate Plan    │ │ Update         │
    │ Change      │ │ Change       │ │                │
    └─────┬───────┘ └──────┬───────┘ └────────┬───────┘
          │                │                  │
          │         ┌──────┼───────┐         │
          │         │      │       │         │
          │         ▼      ▼       ▼         ▼
          │   ┌─────────┐ ┌──────────────┐ ┌────────────────┐
          │   │  3.4    │ │  3.5         │ │  3.6           │
          │   │Archival │ │ Username     │ │ Customer       │
          │   │Process  │ │ Edit         │ │ Assignment     │
          │   └─────────┘ └──────────────┘ └────────────────┘
          │                                        │
          └─────────────────┬──────────────────────┘
                           │
                           ▼
                ┌─────────────────┐
                │  4.0            │
                │ Process         │
                │ Changes         │
                └─────┬───────────┘
                      │
                      ▼
                ┌─────────────────┐
                │  5.0            │
                │ Log Results     │
                │ & Notify        │
                └─────────────────┘
```

---

## Level 2 DFD - Detailed Process Flows

### 3.1 Customer Rate Plan Change Process

```
    BulkChangeRequest
           │
           ▼
    ┌─────────────────┐
    │ 3.1.1           │
    │ Extract         │
    │ Customer Rate   │──── CustomerRatePlanUpdate
    │ Plan Data       │
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.1.2           │
    │ Check           │
    │ Effective Date  │
    └─────┬───────────┘
          │
    ┌─────┼─────┐
    │     │     │
    ▼     ▼     ▼
┌──────┐ ┌──────────┐ ┌────────────┐
│3.1.3a│ │  3.1.3b  │ │  D1        │
│Immed │ │ Schedule │ │ Customer   │
│Proc  │ │ Future   │ │ Rate Plan  │
│      │ │ Process  │ │ Queue      │
└──┬───┘ └────┬─────┘ └────────────┘
   │          │
   ▼          ▼
┌──────────┐ ┌────────────┐
│   D2     │ │    D3      │
│ Device   │ │ Queue      │
│ Database │ │ Table      │
└──────────┘ └────────────┘
```

### 3.2 Carrier Rate Plan Change Process

```
    BulkChangeRequest
           │
           ▼
    ┌─────────────────┐
    │ 3.2.1           │
    │ Extract         │
    │ Carrier Rate    │──── CarrierRatePlanUpdate
    │ Plan Data       │
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.2.2           │
    │ Validate        │
    │ Carrier Plan    │◄──── D4 Carrier Plans
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.2.3           │
    │ Apply to        │
    │ Devices         │────► D2 Device Database
    └─────────────────┘
```

### 3.3 Status Update Process

```
    BulkChangeRequest
           │
           ▼
    ┌─────────────────┐
    │ 3.3.1           │
    │ Extract Status  │
    │ Update Request  │──── StatusUpdateRequest
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.3.2           │
    │ Determine       │
    │ Integration     │◄──── D5 Service Provider
    │ Type            │
    └─────┬───────────┘
          │
    ┌─────┼─────┐
    │     │     │
    ▼     ▼     ▼
┌─────────┐ ┌─────────┐ ┌─────────┐
│ 3.3.3a  │ │ 3.3.3b  │ │ 3.3.3c  │
│ Jasper  │ │ThingSpace│ │Telegence│
│ API     │ │   API    │ │   API   │
└─────────┘ └─────────┘ └─────────┘
```

### 3.4 Archival Process

```
    BulkChangeRequest
           │
           ▼
    ┌─────────────────┐
    │ 3.4.1           │
    │ Validate        │
    │ Archival        │◄──── D6 Usage History
    │ Eligibility     │
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.4.2           │
    │ Check Recent    │
    │ Usage (30 days) │
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.4.3           │
    │ Archive         │
    │ Device          │────► D2 Device Database
    └─────────────────┘
```

### 3.5 Username Edit Process

```
    BulkChangeRequest
           │
           ▼
    ┌─────────────────┐
    │ 3.5.1           │
    │ Extract         │
    │ Username        │──── BulkChangeEditUsername
    │ Change Data     │
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.5.2           │
    │ Determine       │
    │ Integration     │◄──── D5 Service Provider
    │ Type            │
    └─────┬───────────┘
          │
    ┌─────┼─────┐
    │     │     │
    ▼     ▼     ▼
┌─────────┐ ┌─────────┐ ┌─────────┐
│ 3.5.3a  │ │ 3.5.3b  │ │ 3.5.3c  │
│ Jasper  │ │Telegence│ │  AMOP   │
│Username │ │Username │ │Username │
│ Update  │ │ Update  │ │ Update  │
└─────────┘ └─────────┘ └─────────┘
```

### 3.6 Customer Assignment Process

```
    BulkChangeRequest
           │
           ▼
    ┌─────────────────┐
    │ 3.6.1           │
    │ Extract         │
    │ Assignment      │──── CustomerAssignment
    │ Data            │
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.6.2           │
    │ Validate        │
    │ Customer        │◄──── D7 Customer Database
    │ Information     │
    └─────┬───────────┘
          │
          ▼
    ┌─────────────────┐
    │ 3.6.3           │
    │ Update Device   │
    │ Assignment      │────► D2 Device Database
    └─────────────────┘
```

---

## Data Stores

### D1 - Customer Rate Plan Queue
- **Type**: Database Table (`CustomerRatePlanDeviceQueue`)
- **Purpose**: Stores future-scheduled customer rate plan changes
- **Key Fields**: `DeviceId`, `CustomerRatePlanId`, `EffectiveDate`, `CustomerDataAllocationMB`

### D2 - Device Database
- **Type**: Database Tables (Device-related tables)
- **Purpose**: Primary device information and configurations
- **Key Fields**: `DeviceId`, `ICCID`, `IMEI`, `Status`, `CustomerRatePlanId`, `CarrierRatePlan`

### D3 - Queue Table
- **Type**: Database Table (General processing queue)
- **Purpose**: Manages scheduled processing tasks
- **Key Fields**: `QueueId`, `ProcessingDate`, `Status`, `ChangeType`

### D4 - Carrier Plans
- **Type**: Database Tables (Carrier configuration)
- **Purpose**: Available carrier rate plans and configurations
- **Key Fields**: `CarrierRatePlan`, `PlanUuid`, `CommPlan`, `RatePlanId`

### D5 - Service Provider
- **Type**: Database Table (`ServiceProvider`)
- **Purpose**: Service provider configurations and integration settings
- **Key Fields**: `ServiceProviderId`, `IntegrationId`, `IntegrationType`

### D6 - Usage History
- **Type**: Database Tables (Usage tracking)
- **Purpose**: Device usage data for validation
- **Key Fields**: `DeviceId`, `LastUsageDate`, `UsageAmount`, `Date`

### D7 - Customer Database
- **Type**: Database Tables (Customer information)
- **Purpose**: Customer and tenant information
- **Key Fields**: `CustomerId`, `TenantId`, `CustomerName`, `BillingInfo`

---

## External Entities

### 1. M2M/Mobility Portal Users
- **Role**: Initiate bulk change requests
- **Interactions**: Submit change requests, view processing status
- **Access**: Role-based permissions

### 2. External APIs (Carriers)
- **Jasper API**: Device management for Jasper integration
- **ThingSpace API**: Verizon ThingSpace device management
- **Telegence API**: Telegence platform integration
- **AMOP API**: Altaworx Mobility Operations Platform

---

## Current State Issues (Before Improvements)

### 1. **Lack of Unified Processing**
- Each change type has separate processing logic
- Inconsistent error handling across change types
- No standardized validation framework

### 2. **Complex Conditional Logic**
- Multiple nested if-else statements
- Hard-coded integration-specific logic
- Difficult to maintain and extend

### 3. **Sequential Processing**
- No parallel processing capabilities
- Inefficient for large bulk changes
- Poor performance for time-sensitive changes

### 4. **Limited Error Recovery**
- Basic error logging without retry mechanisms
- No rollback capabilities for partial failures
- Manual intervention required for error resolution

### 5. **Integration-Specific Coupling**
- Tight coupling between change types and integrations
- Difficulty adding new carriers or change types
- Code duplication across similar processes

### 6. **Inconsistent Data Flow**
- Different data validation rules per change type
- Inconsistent logging and audit trails
- No standardized response formats

---

## Processing Flow Summary

### Current State Flow Pattern:
```
Request → Validate → Branch by Type → Execute → Log
    ↑         ↑           ↑             ↑       ↑
    │         │           │             │       │
 Manual    Basic      Hard-coded    Sequential  Basic
 Input    Check      Logic         Processing  Logging
```

### Change Type Complexity Matrix:
| Change Type           | Integration Types | Validation Rules | Processing Complexity |
|----------------------|-------------------|------------------|----------------------|
| CustomerRatePlanChange| Universal        | Medium           | Medium               |
| CarrierRatePlanChange | Carrier-Specific | High             | High                 |
| StatusUpdate         | All 3 Carriers   | High             | High                 |
| Archival             | Universal        | High             | Medium               |
| EditUsername         | 3 Carriers       | Medium           | Medium               |
| CustomerAssignment   | Universal        | Low              | Low                  |

This DFD represents the current state **before** any suggested improvements, showing the existing complexity and areas for potential optimization in the 6 change types system.