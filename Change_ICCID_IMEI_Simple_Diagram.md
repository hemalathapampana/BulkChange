# Change ICCID/IMEI Data Flow - Visual Representation

## Complete Process Flow Diagram

```
                                CHANGE ICCID/IMEI PROCESS FLOW
                               =====================================

┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ 1. USER         │───►│ 2. RATE PLAN    │───►│ 3. DEVICE       │───►│ 4. PLAN         │
│ INTERFACE       │    │ SELECTION       │    │ SELECTION       │    │ VALIDATION      │
│                 │    │                 │    │                 │    │                 │
│ • Login         │    │ • Customer      │    │ • ICCID/IMEI    │    │ • Compatibility │
│ • Module Access │    │ • Rate Plans    │    │ • Bulk Select   │    │ • Availability  │
│ • Form Submit   │    │ • Plan Details  │    │ • Validation    │    │ • Prerequisites │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │                       │
         └───────────────────────┼───────────────────────┼───────────────────────┘
                                 │                       │
                                 ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ 5. BULK CHANGE  │───►│ 6. QUEUE        │───►│ 7. BACKGROUND   │───►│ 8. AUTH &       │
│ CREATION        │    │ PROCESSING      │    │ LAMBDA          │    │ AUTHORIZATION   │
│                 │    │ (SQS)           │    │ PROCESSING      │    │                 │
│ • Master Record │    │ • Message Queue │    │ • Async Proc    │    │ • OAuth Token   │
│ • Detail Records│    │ • Retry Logic   │    │ • Error Handle  │    │ • Session Mgmt  │
│ • Status Init   │    │ • Dead Letter   │    │ • Status Update │    │ • API Access    │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │                       │
         └───────────────────────┼───────────────────────┼───────────────────────┘
                                 │                       │
                                 ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ 9. DEVICE-BY-   │───►│ 10. DATABASE    │───►│ 11. STATUS      │───►│ 12. ERROR       │
│ DEVICE PROC     │    │ OPERATIONS      │    │ TRACKING        │    │ HANDLING        │
│                 │    │                 │    │                 │    │                 │
│ • ThingSpace    │    │ • Update Records│    │ • Progress      │    │ • Retry Logic   │
│ • API Calls     │    │ • Sync Status   │    │ • Real-time     │    │ • Failed Items  │
│ • Individual    │    │ • Audit Log     │    │ • Notifications │    │ • Error Logging │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │                       │
         └───────────────────────┼───────────────────────┼───────────────────────┘
                                 │                       │
                                 ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ 13. COMPLETION  │───►│ 14. AUDIT TRAIL │───►│ 15. RATE PLAN   │
│ PROCESSING      │    │ CREATION        │    │ ACTIVATION      │
│                 │    │                 │    │ COMPLETE        │
│ • Final Status  │    │ • Full Log      │    │ • Verification  │
│ • Notifications │    │ • Compliance    │    │ • Final Status  │
│ • Summary Report│    │ • Timestamps    │    │ • Success Report│
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Technical Data Flow

```
                                 TECHNICAL ARCHITECTURE
                               =========================

┌─────────────────────────────────────────────────────────────────────────────────────┐
│                                 USER INTERFACE LAYER                                 │
│ ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│ │M2MController│  │Form Render  │  │Validation   │  │Session Mgmt │  │Permission   │ │
│ │.cs          │  │Engine       │  │Engine       │  │Service      │  │Check        │ │
│ └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────────┘
                                          │
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                                BUSINESS LOGIC LAYER                                 │
│ ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│ │BulkChange   │  │Process      │  │Validation   │  │Rate Plan    │  │Device       │ │
│ │Repository   │  │ChangeICCID  │  │Services     │  │Services     │  │Services     │ │
│ │.cs          │  │IMEI.cs      │  │             │  │             │  │             │ │
│ └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────────┘
                                          │
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                                MESSAGING LAYER                                      │
│ ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│ │SQS Queue    │  │Message      │  │Retry        │  │Dead Letter  │  │Lambda       │ │
│ │Service      │  │Processing   │  │Logic        │  │Queue        │  │Function     │ │
│ │             │  │             │  │             │  │             │  │             │ │
│ └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────────┘
                                          │
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                            EXTERNAL INTEGRATION LAYER                               │
│ ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│ │ThingSpace   │  │OAuth2       │  │API          │  │Callback     │  │Response     │ │
│ │API Client   │  │Auth Service │  │Gateway      │  │Handler      │  │Processor    │ │
│ │             │  │             │  │             │  │             │  │             │ │
│ └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────────┘
                                          │
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                                 DATA LAYER                                          │
│ ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│ │SQL Server   │  │Entity       │  │Audit        │  │Status       │  │Error        │ │
│ │Database     │  │Framework    │  │Tables       │  │Tracking     │  │Logging      │ │
│ │             │  │             │  │             │  │             │  │             │ │
│ └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

## Phase-by-Phase Data Movement

### Phase 1: User Input Collection
**Data Sources**: User Form → Session Data → Permission Matrix
**Data Flow**: 
```
User Input ──► Form Validation ──► Session Check ──► Permission Validation ──► Business Logic
     │              │                    │                   │                      │
     ▼              ▼                    ▼                   ▼                      ▼
  Raw Data    Sanitized Data      User Context      Access Rights           Validated Request
```

### Phase 2: Business Processing
**Data Sources**: Validated Input → Database → Business Rules
**Data Flow**:
```
Validated Request ──► Rate Plan Check ──► Device Validation ──► Bulk Change Creation
        │                   │                    │                        │
        ▼                   ▼                    ▼                        ▼
   Request Object      Plan Compatibility    Device Status           Master Record
```

### Phase 3: Queue Processing
**Data Sources**: Bulk Change Records → SQS Configuration → Lambda Functions
**Data Flow**:
```
Master Record ──► Detail Records ──► SQS Messages ──► Lambda Processing
      │                │                 │                 │
      ▼                ▼                 ▼                 ▼
  Change Request    Individual Items   Queue Messages   Async Processing
```

### Phase 4: API Integration
**Data Sources**: Queue Messages → Auth Tokens → ThingSpace API
**Data Flow**:
```
Queue Messages ──► Authentication ──► API Calls ──► Response Processing
      │                │                │              │
      ▼                ▼                ▼              ▼
  Device Changes   OAuth Token      API Request     Status Updates
```

### Phase 5: Database Updates
**Data Sources**: API Responses → Status Updates → Audit Requirements
**Data Flow**:
```
API Responses ──► Status Updates ──► Database Sync ──► Audit Trail
      │                │                │              │
      ▼                ▼                ▼              ▼
  Result Data     Status Changes    Record Updates   Compliance Log
```