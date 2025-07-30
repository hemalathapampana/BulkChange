# CHANGE CARRIER RATEPLAN Data Flow Diagram

## Overview

This document provides a comprehensive dataflow diagram for the **CHANGE CARRIER RATEPLAN Change Type** (Type 7) process. This process handles changing the carrier-level rate plans for devices across different integration types including Jasper, ThingSpace, Teal, Pond, Telegence, and eBonding.

## Key Components

### Data Models

#### Input Request Structure
```csharp
public class BulkChangeRequest
{
    public int? ServiceProviderId { get; set; }
    public int? ChangeType { get; set; }           // 7 for CarrierRatePlanChange
    public bool? ProcessChanges { get; set; }
    public string[] Devices { get; set; }
    public CarrierRatePlanUpdate CarrierRatePlanUpdate { get; set; }
}

public class CarrierRatePlanUpdate
{
    public string CarrierRatePlan { get; set; }
    public string CommPlan { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string PlanUuid { get; set; }
    public long RatePlanId { get; set; }
}
```

#### Change Detail Record Structure
```csharp
public class BulkChangeDetailRecord
{
    public long Id { get; set; }
    public string DeviceIdentifier { get; set; }   // ICCID or MSISDN
    public long BulkChangeId { get; set; }
    public int ServiceProviderId { get; set; }
    public int IntegrationId { get; set; }
    public int TenantId { get; set; }
    public string ChangeRequest { get; set; }      // JSON serialized carrier rate plan update
    public int ChangeRequestTypeId { get; set; }   // 7 for CarrierRatePlanChange
}
```

## Complete Data Flow Diagram

```mermaid
graph TD
    A[Client Request<br/>BulkChangeRequest] --> B[Request Validation<br/>ChangeType = 7]
    B --> C[Extract CarrierRatePlanUpdate<br/>- CarrierRatePlan<br/>- CommPlan<br/>- EffectiveDate<br/>- PlanUuid<br/>- RatePlanId]
    
    C --> D[Create BulkChange Record<br/>ChangeRequestTypeId = 7]
    D --> E[Build Change Details<br/>Per Device]
    E --> F[Store BulkChangeDetailRecord<br/>with JSON ChangeRequest]
    
    F --> G[ProcessCarrierRatePlanChangeAsync<br/>Entry Point]
    G --> H[Get Device Changes<br/>PageSize = 100]
    H --> I{Integration Type<br/>Routing}
    
    I -->|Telegence| J[ProcessTelegenceCarrierRatePlanChange]
    I -->|eBonding| K[ProcessEBondingCarrierRatePlanChange]
    I -->|Jasper/TMobile/POD19/Rogers| L[ProcessJasperCarrierRatePlanChange]
    I -->|ThingSpace| M[ProcessThingSpaceCarrierRatePlanChange]
    I -->|Teal| N[ProcessTealCarrierRatePlanChange]
    I -->|Pond| O[ProcessPondCarrierRatePlanChange]
    
    %% Jasper Flow (Most Common)
    L --> L1[Get Jasper Authentication]
    L1 --> L2{Auth Valid?}
    L2 -->|No| L3[Log Error & Exit]
    L2 -->|Yes| L4{Write Enabled?}
    L4 -->|No| L5[Log Warning & Exit]
    L4 -->|Yes| L6[Initialize Services<br/>- JasperDeviceDetailService<br/>- DeviceRepository]
    
    L6 --> L7[For Each Device Change]
    L7 --> L8[Deserialize CarrierRatePlan<br/>from ChangeRequest JSON]
    L8 --> L9[Create JasperDeviceDetail<br/>- ICCID<br/>- CarrierRatePlan<br/>- CommunicationPlan]
    
    L9 --> L10[UpdateJasperDeviceDetailsAsync<br/>Call Jasper API]
    L10 --> L11[Log API Result<br/>M2M Log Entry]
    L11 --> L12{API Success?}
    L12 -->|No| L13[Mark Failed & Continue]
    L12 -->|Yes| L14[UpdateRatePlanAsync<br/>Update AMOP Database]
    
    L14 --> L15[Log DB Result<br/>M2M Log Entry]
    L15 --> L16[MarkProcessedForM2MDeviceChangeAsync<br/>Update Status]
    L16 --> L17{More Devices?}
    L17 -->|Yes| L7
    L17 -->|No| L18[Return Success]
    
    %% ThingSpace Flow
    M --> M1[Initialize ThingSpace Services]
    M1 --> M2[For Each Device Change]
    M2 --> M3[Deserialize CarrierRatePlan]
    M3 --> M4[Create ThingSpaceDeviceDetail]
    M4 --> M5[UpdateThingSpaceDeviceDetailsAsync<br/>Call ThingSpace API]
    M5 --> M6[Log API Result]
    M6 --> M7{API Success?}
    M7 -->|No| M8[Mark Failed & Continue]
    M7 -->|Yes| M9[UpdateRatePlanAsync<br/>Update AMOP Database]
    M9 --> M10[Log DB Result]
    M10 --> M11[Mark Processed]
    M11 --> M12{More Devices?}
    M12 -->|Yes| M2
    M12 -->|No| M13[Return Success]
    
    %% Teal Flow
    N --> N1[Get Teal Authentication]
    N1 --> N2[For Each Device Change]
    N2 --> N3[Deserialize CarrierRatePlan]
    N3 --> N4[Create TealDeviceDetail]
    N4 --> N5[UpdateRatePlanAsync<br/>Direct DB Update]
    N5 --> N6[Log DB Result]
    N6 --> N7[Mark Processed]
    N7 --> N8{More Devices?}
    N8 -->|Yes| N2
    N8 -->|No| N9[Return Success]
    
    %% Pond Flow
    O --> O1[Get Pond Authentication]
    O1 --> O2[Initialize Pond Services]
    O2 --> O3[UpdatePondCarrierRatePlanForDevices]
    O3 --> O4[For Each Device Change]
    O4 --> O5[Deserialize CarrierRatePlan]
    O5 --> O6[CreatePondDeviceCarrierRatePlanRequest]
    O6 --> O7[Call Pond API<br/>Add Package]
    O7 --> O8[Process API Response]
    O8 --> O9[SaveNewPondDeviceCarrierRatePlanToDatabase]
    O9 --> O10[UpdateM2MDeviceRatePlan]
    O10 --> O11[Log Results]
    O11 --> O12{More Devices?}
    O12 -->|Yes| O4
    O12 -->|No| O13[Return Success]
    
    %% Common Logging Flow
    L11 --> P[Logging & Audit Trail]
    L15 --> P
    M6 --> P
    M10 --> P
    N6 --> P
    O11 --> P
    
    P --> P1[M2M Portal Logging<br/>CreateM2MDeviceBulkChangeLog]
    P --> P2[Mobility Portal Logging<br/>CreateMobilityDeviceBulkChangeLog]
    P1 --> P3[Audit Database<br/>- RequestText<br/>- ResponseText<br/>- HasErrors<br/>- ResponseStatus]
    P2 --> P3
    
    %% Error Handling
    L3 --> Q[Error Handling]
    L5 --> Q
    L13 --> Q
    M8 --> Q
    Q --> Q1[Log Error Details]
    Q1 --> Q2[Update Status to ERROR]
    Q2 --> Q3[Create Error Log Entry]
    
    style A fill:#e1f5fe
    style G fill:#f3e5f5
    style I fill:#fff3e0
    style L fill:#e8f5e8
    style M fill:#e8f5e8
    style N fill:#e8f5e8
    style O fill:#e8f5e8
    style P fill:#fff8e1
    style Q fill:#ffebee
```

## Integration-Specific Processing Details

### 1. Jasper Integration (Most Common)
```
Input Data:
├── CarrierRatePlan (string)
├── CommunicationPlan (string)
└── EffectiveDate (DateTime?)

Process Flow:
1. Authentication Validation
2. Write Permission Check
3. API Call to Jasper
4. Database Update (AMOP)
5. Status Logging

API Endpoints:
└── UpdateJasperDeviceDetailsAsync()
```

### 2. ThingSpace Integration
```
Input Data:
├── CarrierRatePlan (string)
└── EffectiveDate (DateTime?)

Process Flow:
1. Service Initialization
2. API Call to ThingSpace
3. Database Update (AMOP)
4. Status Logging

API Endpoints:
└── UpdateThingSpaceDeviceDetailsAsync()
```

### 3. Teal Integration
```
Input Data:
├── PlanUuid (string)
└── CarrierRatePlan (string)

Process Flow:
1. Authentication Check
2. Direct Database Update
3. Status Logging

Database Operations:
└── UpdateRatePlanAsync() - Direct AMOP Update
```

### 4. Pond Integration
```
Input Data:
├── RatePlanId (long)
└── CarrierRatePlan (string)

Process Flow:
1. Authentication Setup
2. API Call to Pond (Add Package)
3. Save Pond Response to Database
4. Update AMOP Database
5. Status Logging

API Endpoints:
└── Pond API - Add Package Request
```

## Database Operations Flow

```mermaid
graph LR
    A[BulkChange Table<br/>ChangeRequestTypeId = 7] --> B[BulkChangeDetailRecord Table<br/>Per Device]
    B --> C[Device-Specific Processing]
    C --> D[External API Calls<br/>Carrier Systems]
    D --> E[AMOP Database Update<br/>UpdateRatePlanAsync]
    E --> F[Log Tables<br/>M2MDeviceBulkChangeLog]
    F --> G[Status Update<br/>Processed/Error]
```

### Key Database Tables
1. **BulkChange** - Master change record
2. **BulkChangeDetailRecord** - Individual device changes
3. **M2MDeviceBulkChangeLog** - Audit trail and logging
4. **Device Tables** - Final rate plan assignments

## Error Handling & Logging

### Error Types
1. **Authentication Failures**
   - Invalid credentials
   - Expired tokens
   - Missing authentication data

2. **API Failures**
   - Carrier system unavailable
   - Invalid rate plan codes
   - Network timeouts

3. **Database Failures**
   - Connection issues
   - Constraint violations
   - Transaction failures

### Logging Structure
```csharp
CreateM2MDeviceBulkChangeLog {
    BulkChangeId: long,
    M2MDeviceChangeId: long,
    LogEntryDescription: string,
    ProcessBy: "AltaworxDeviceBulkChange",
    RequestText: string,              // API request details
    ResponseText: string,             // API response
    HasErrors: boolean,
    ResponseStatus: BulkChangeStatus, // PROCESSED, ERROR, PENDING
    ProcessedDate: DateTime
}
```

## Performance Considerations

### Batch Processing
- **Page Size**: 100 devices per batch
- **Parallel Processing**: Up to 10 concurrent requests
- **Retry Logic**: 3 attempts for transient failures

### Optimization Features
- Connection pooling for database operations
- HTTP retry policies for API calls
- Async/await pattern throughout
- Bulk logging operations

## Integration Constants

### Change Type
```csharp
public enum ChangeRequestType {
    CarrierRatePlanChange = 7
}
```

### Integration Types
```csharp
public enum IntegrationType {
    Telegence = 1,
    eBonding = 2,
    Jasper = 3,
    ThingSpace = 4,
    Teal = 5,
    Pond = 6,
    TMobileJasper = 7,
    POD19 = 8,
    Rogers = 9
}
```

### Processing Status
```csharp
public enum BulkChangeStatus {
    PENDING,
    PROCESSED,
    ERROR
}
```

## Security & Compliance

### Authentication Methods
- **Jasper**: Username/Password + API Keys
- **ThingSpace**: OAuth Token-based
- **Teal**: API Key authentication
- **Pond**: Token-based authentication

### Data Protection
- Encrypted connection strings
- Sanitized logging (no credentials)
- Tenant-level isolation
- Audit trail maintenance

## Monitoring & Metrics

### Key Metrics
1. **Processing Time**: Average time per device change
2. **Success Rate**: Percentage of successful changes
3. **Error Rate**: Failed changes by integration type
4. **Throughput**: Devices processed per minute

### Alerting
- Failed authentication attempts
- High error rates (>5%)
- Processing time exceeding thresholds
- API availability issues