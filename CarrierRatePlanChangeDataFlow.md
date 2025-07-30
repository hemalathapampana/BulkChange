# Change Carrier Rate Plan - Data Flow Diagram

## Simple Graph Format

```
┌─────────────────────┐
│     Client/UI       │
│    (Rate Plan       │
│     Request)        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   M2M Controller    │
│  (Bulk Change API)  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   Validation &      │
│   Carrier Lookup    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  AltaworxDevice     │
│  BulkChange Service │
└──────────┬──────────┘
           │
     ┌─────▼─────┐
     │  Process  │
     │ By Carrier│
     └─────┬─────┘
           │
    ┌──────┴──────┐
    │             │
    ▼             ▼
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│ Jasper  │   │ThingSpace│   │  Teal   │   │  Pond   │   │eBonding │
│Carrier  │   │ Carrier  │   │ Carrier │   │ Carrier │   │ Carrier │
└────┬────┘   └────┬────┘   └────┬────┘   └────┬────┘   └────┬────┘
     │             │             │             │             │
     ▼             ▼             ▼             ▼             ▼
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│ Jasper  │   │ThingSpace│   │  Teal   │   │  Pond   │   │eBonding │
│   API   │   │   API    │   │   API   │   │   API   │   │   API   │
└────┬────┘   └────┬────┘   └────┬────┘   └────┬────┘   └────┬────┘
     │             │             │             │             │
     └─────────────┼─────────────┼─────────────┼─────────────┘
                   │             │             │
                   ▼             ▼             ▼
            ┌─────────────────────────────────────┐
            │        Database Update              │
            │     (Device Rate Plan)              │
            └──────────────┬──────────────────────┘
                           │
                           ▼
            ┌─────────────────────────────────────┐
            │          Logging &                  │
            │       Audit Trail                   │
            └─────────────────────────────────────┘
```

## Data Flow Components

### 1. Input Data Structure
```
CarrierRatePlanUpdate {
  ├── CarrierRatePlan: string
  ├── CommPlan: string  
  ├── EffectiveDate: DateTime?
  ├── PlanUuid: string
  └── RatePlanId: long
}
```

### 2. Processing Flow by Carrier

#### A. Jasper Carrier Flow
```
Input → Validation → Jasper API → Database Update → Logging
  │         │           │            │              │
  │         │           │            │              └─ M2M/Mobility Logs
  │         │           │            └─ Device Rate Plan Update
  │         │           └─ Edit Device Rate Plan API
  │         └─ Carrier Rate Plan Code Validation
  └─ BulkChangeRequest with CarrierRatePlanUpdate
```

#### B. ThingSpace Carrier Flow
```
Input → Validation → ThingSpace API → Database Update → Logging
  │         │            │               │              │
  │         │            │               │              └─ M2M/Mobility Logs
  │         │            │               └─ Device Rate Plan Update
  │         │            └─ Change Device Service Plan API
  │         └─ Carrier Rate Plan Code Validation
  └─ BulkChangeRequest with CarrierRatePlanUpdate
```

#### C. Teal Carrier Flow
```
Input → Validation → Teal API → Database Update → Logging
  │         │          │          │              │
  │         │          │          │              └─ M2M/Mobility Logs
  │         │          │          └─ Device Rate Plan Update
  │         │          └─ Update Rate Plan API
  │         └─ Plan UUID Validation
  └─ BulkChangeRequest with CarrierRatePlanUpdate
```

#### D. Pond Carrier Flow
```
Input → Validation → Pond API → Database Update → Logging
  │         │          │          │              │
  │         │          │          │              └─ M2M/Mobility Logs
  │         │          │          └─ Device Rate Plan Update
  │         │          └─ Add/Update Package API
  │         └─ Rate Plan ID Validation
  └─ BulkChangeRequest with CarrierRatePlanUpdate
```

## Key Data Transformations

### Input Processing
```
BulkChangeRequest
    └─ Extract CarrierRatePlanUpdate
        └─ Validate Carrier Rate Plan Code
            └─ Lookup Rate Plan Details
                └─ Set PlanUuid & RatePlanId
```

### Output Processing
```
API Response
    └─ Parse Result
        └─ Update Database
            └─ Create Log Entry
                └─ Return Status
```

## Error Handling Flow

```
┌─────────────────┐
│ Process Start   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌─────────────────┐
│  Validation     │────▶│ Validation      │
│  Success?       │     │ Error Log       │
└────────┬────────┘     └─────────────────┘
         │ Yes
         ▼
┌─────────────────┐     ┌─────────────────┐
│  API Call       │────▶│ API Error       │
│  Success?       │     │ Log & Rollback  │
└────────┬────────┘     └─────────────────┘
         │ Yes
         ▼
┌─────────────────┐     ┌─────────────────┐
│  Database       │────▶│ Database Error  │
│  Update Success?│     │ Log & Rollback  │
└────────┬────────┘     └─────────────────┘
         │ Yes
         ▼
┌─────────────────┐
│ Success Log &   │
│ Response        │
└─────────────────┘
```

## Integration Points

### External APIs
- **Jasper**: Device management and rate plan changes
- **ThingSpace**: Verizon carrier integration
- **Teal**: IoT connectivity platform
- **Pond**: Carrier connectivity management
- **eBonding**: Legacy carrier integration

### Internal Systems
- **M2M Portal**: Device management interface
- **Database**: Device and rate plan storage
- **Logging System**: Audit trail and monitoring
- **Bulk Change Engine**: Batch processing framework

## Data Security & Compliance

```
Input Validation → Secure API Calls → Encrypted Storage → Audit Logging
      │                   │                │               │
      │                   │                │               └─ Compliance Tracking
      │                   │                └─ Data Encryption
      │                   └─ Authentication & Authorization
      └─ Input Sanitization & Validation
```