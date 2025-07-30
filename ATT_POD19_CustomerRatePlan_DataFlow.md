# AT&T POD19 Customer Rate Plan Change - Data Flow Graph

## Overview
This document provides a visual representation of the data flow for customer rate plan changes specific to AT&T POD19 service provider integration.

## High-Level Data Flow Architecture

```mermaid
graph TD
    A[Client Request - Customer Rate Plan Change] --> B[BulkChangeRequest Validation]
    B --> C{Service Provider = AT&T POD19?}
    C -->|Yes| D[Extract CustomerRatePlanUpdate Parameters]
    C -->|No| E[Route to Other Provider Handler]
    
    D --> F{Check ChangeType = 4?}
    F -->|Yes| G[Process Customer Rate Plan Change]
    F -->|No| H[Route to Other Change Type]
    
    G --> I{Check Effective Date}
    I -->|Present & Future| J[Scheduled Processing Path]
    I -->|Null or Past/Current| K[Immediate Processing Path]
    
    K --> L[Execute Stored Procedure]
    J --> M[Add to Queue Table]
    
    L --> N[Update All Devices in Bulk]
    M --> O[Schedule Future Processing]
    
    N --> P[Portal-Specific Logging]
    O --> P
    
    P --> Q{Portal Type?}
    Q -->|M2M| R[M2M Log Entry]
    Q -->|Mobility| S[Mobility Log Entry]
    
    R --> T[Response with Success/Error]
    S --> T
```

## Detailed Processing Flow

### 1. Request Initiation and Validation

```mermaid
graph LR
    A[Client Request] --> B[BulkChangeRequest Object]
    B --> C[Request Validation]
    
    subgraph "Request Structure"
        D[ServiceProviderId: AT&T POD19]
        E[ChangeType: 4]
        F[ProcessChanges: boolean]
        G[Devices: string array]
        H[CustomerRatePlanUpdate Object]
    end
    
    C --> D
    C --> E
    C --> F
    C --> G
    C --> H
    
    subgraph "CustomerRatePlanUpdate"
        I[CustomerRatePlanId: int?]
        J[CustomerDataAllocationMB: decimal?]
        K[CustomerPoolId: int?]
        L[EffectiveDate: DateTime?]
    end
    
    H --> I
    H --> J
    H --> K
    H --> L
```

### 2. AT&T POD19 Integration Type Processing

```mermaid
graph TD
    A[Bulk Change Request] --> B{Integration Type Check}
    B -->|POD19| C[Use Jasper-Compatible Processing]
    B -->|ThingSpace| D[ThingSpace Handler]
    B -->|Telegence| E[Telegence Handler]
    B -->|Other| F[Other Integration Handlers]
    
    C --> G[ProcessJasperStatusUpdateAsync]
    
    subgraph "POD19 Processing (Lines 2574-2580)"
        H[case IntegrationType.POD19]
        I[case IntegrationType.Jasper]
        J[case IntegrationType.TMobileJasper]
        K[case IntegrationType.Rogers]
        
        H --> L[Same Processing Logic]
        I --> L
        J --> L
        K --> L
    end
    
    G --> H
```

### 3. Customer Rate Plan Processing Decision Matrix

```mermaid
graph TD
    A[ProcessCustomerRatePlanChangeAsync Entry Point] --> B[Extract Change Request]
    
    subgraph "Parameter Extraction (Lines 2112-2116)"
        C[customerRatePlanId]
        D[customerRatePoolId]
        E[effectiveDate]
        F[customerDataAllocationMB]
    end
    
    B --> C
    B --> D
    B --> E
    B --> F
    
    G{Effective Date Check} --> H[effectiveDate == null OR effectiveDate <= DateTime.UtcNow]
    G --> I[effectiveDate > DateTime.UtcNow]
    
    C --> G
    D --> G
    E --> G
    F --> G
    
    H --> J[Immediate Processing Path]
    I --> K[Scheduled Processing Path]
    
    J --> L[ProcessCustomerRatePlanChangeAsync]
    K --> M[ProcessAddCustomerRatePlanChangeToQueueAsync]
```

### 4. Immediate Processing Path - Database Operations

```mermaid
graph TD
    A[Immediate Processing] --> B[ProcessCustomerRatePlanChangeAsync Method]
    
    subgraph "Database Execution (Lines 2249-2269)"
        C[Create SQL Connection]
        D[Create SQL Command]
        E[Set Command Type: StoredProcedure]
        F[Set Command Text: usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices]
    end
    
    B --> C
    C --> D
    D --> E
    E --> F
    
    subgraph "SQL Parameters"
        G[@effectiveDate]
        H[@bulkChangeId]
        I[@customerRatePlanId]
        J[@customerRatePoolId]
        K[@customerDataAllocationMB]
        L[@needToMarkProcessed]
    end
    
    F --> G
    F --> H
    F --> I
    F --> J
    F --> K
    F --> L
    
    L --> M[Execute Stored Procedure]
    M --> N{Execution Result}
    N -->|Success| O[Return Success Response]
    N -->|Error| P[Log Error & Return Error Response]
    
    subgraph "Response Structure"
        Q[ActionText: SP Name]
        R[HasErrors: boolean]
        S[RequestObject: Parameters]
        T[ResponseObject: Result/Error]
    end
    
    O --> Q
    P --> Q
    O --> R
    P --> R
    O --> S
    P --> S
    O --> T
    P --> T
```

### 5. Scheduled Processing Path - Queue Management

```mermaid
graph TD
    A[Scheduled Processing] --> B[ProcessAddCustomerRatePlanChangeToQueueAsync]
    
    B --> C[Get Device Changes for Bulk Change ID]
    C --> D[Create DataTable Structure]
    
    subgraph "Queue Table Schema (Lines 2179-2193)"
        E[Id: Primary Key]
        F[DeviceId: Target Device]
        G[CustomerRatePlanId: Plan ID]
        H[CustomerRatePoolId: Pool ID]
        I[CustomerDataAllocationMB: Data Limit]
        J[EffectiveDate: Schedule Date]
        K[PortalType: M2M/Mobility]
        L[TenantId: Tenant Scope]
        M[CreatedBy: Lambda Process]
        N[CreatedDate: UTC Now]
        O[ModifiedBy: Null]
        P[ModifiedDate: Null]
        Q[IsActive: True]
    end
    
    D --> E
    D --> F
    D --> G
    D --> H
    D --> I
    D --> J
    D --> K
    D --> L
    D --> M
    D --> N
    D --> O
    D --> P
    D --> Q
    
    Q --> R[Populate DataTable with Device Changes]
    R --> S[SqlBulkCopy to CustomerRatePlanDeviceQueue]
    S --> T{Bulk Insert Result}
    T -->|Success| U[Return Success Response]
    T -->|Error| V[Log Error & Return Error Response]
```

### 6. Portal-Specific Logging Flow

```mermaid
graph TD
    A[Processing Complete] --> B{Check Portal Type}
    B -->|PortalTypeM2M| C[M2M Portal Logging]
    B -->|Mobility| D[Mobility Portal Logging]
    
    subgraph "M2M Log Entry (Lines 2131-2144)"
        E[CreateM2MDeviceBulkChangeLog]
        F[BulkChangeId]
        G[M2MDeviceChangeId]
        H[LogEntryDescription: Change Customer Rate Plan Update AMOP]
        I[ProcessBy: AltaworxDeviceBulkChange]
        J[RequestText: SP Name + Parameters]
        K[ResponseText: Result]
        L[HasErrors: boolean]
        M[ResponseStatus: PROCESSED/ERROR]
    end
    
    subgraph "Mobility Log Entry (Lines 2147-2161)"
        N[CreateMobilityDeviceBulkChangeLog]
        O[BulkChangeId]
        P[MobilityDeviceChangeId]
        Q[LogEntryDescription: Change Customer Rate Plan Update AMOP]
        R[ProcessBy: AltaworxDeviceBulkChange]
        S[RequestText: SP Name + Parameters]
        T[ResponseText: Result]
        U[HasErrors: boolean]
        V[ResponseStatus: PROCESSED/ERROR]
    end
    
    C --> E
    E --> F
    E --> G
    E --> H
    E --> I
    E --> J
    E --> K
    E --> L
    E --> M
    
    D --> N
    N --> O
    N --> P
    N --> Q
    N --> R
    N --> S
    N --> T
    N --> U
    N --> V
```

### 7. Error Handling and Response Flow

```mermaid
graph TD
    A[Process Execution] --> B{Error Occurred?}
    B -->|No| C[Success Path]
    B -->|Yes| D[Error Path]
    
    subgraph "Success Response"
        E[ActionText: Operation Name]
        F[HasErrors: false]
        G[RequestObject: Input Parameters]
        H[ResponseObject: OK or Success Message]
    end
    
    subgraph "Error Response"
        I[ActionText: Operation Name]
        J[HasErrors: true]
        K[RequestObject: Input Parameters]
        L[ResponseObject: Error Message + Reference ID]
    end
    
    C --> E
    C --> F
    C --> G
    C --> H
    
    D --> I
    D --> J
    D --> K
    D --> L
    
    subgraph "Error Types"
        M[Validation Errors]
        N[Database Connection Failures]
        O[Stored Procedure Errors]
        P[Transaction Conflicts]
        Q[JSON Deserialization Errors]
    end
    
    D --> M
    D --> N
    D --> O
    D --> P
    D --> Q
    
    subgraph "Error Logging"
        R[Generate Unique Log ID]
        S[Log Error with Stack Trace]
        T[Include Reference ID in Response]
    end
    
    M --> R
    N --> R
    O --> R
    P --> R
    Q --> R
    
    R --> S
    S --> T
```

### 8. Data Model Relationships

```mermaid
erDiagram
    BulkChangeRequest ||--|| CustomerRatePlanUpdate : contains
    BulkChangeRequest ||--o{ Device : targets
    BulkChangeRequest ||--|| BulkChange : creates
    
    BulkChange ||--o{ BulkChangeDetailRecord : contains
    BulkChange ||--|| ServiceProvider : belongs_to
    
    CustomerRatePlanUpdate {
        int CustomerRatePlanId
        decimal CustomerDataAllocationMB
        int CustomerPoolId
        DateTime EffectiveDate
    }
    
    BulkChangeRequest {
        int ServiceProviderId
        int ChangeType
        bool ProcessChanges
        string[] Devices
    }
    
    BulkChange {
        long Id
        int ChangeRequestType
        int IntegrationId
        int PortalTypeId
        int TenantId
    }
    
    Device {
        long DeviceId
        string ICCID
        string MSISDN
        int ServiceProviderId
    }
    
    CustomerRatePlanDeviceQueue {
        long Id
        long DeviceId
        int CustomerRatePlanId
        int CustomerRatePoolId
        decimal CustomerDataAllocationMB
        DateTime EffectiveDate
        int PortalType
        int TenantId
        bool IsActive
    }
    
    CustomerRatePlanDeviceQueue ||--|| Device : schedules_for
```

### 9. Integration Points and External Systems

```mermaid
graph LR
    subgraph "AT&T POD19 System"
        A[AT&T POD19 Service Provider]
        B[Jasper-Compatible API]
        C[Device Management Interface]
    end
    
    subgraph "Internal Systems"
        D[AMOP Portal - M2M]
        E[AMOP Portal - Mobility]
        F[Rev.IO Customer Service]
        G[Central Database]
        H[Billing Systems]
    end
    
    subgraph "AWS Infrastructure"
        I[Lambda Functions]
        J[SQS Queues]
        K[CloudWatch Logging]
    end
    
    A --> B
    B --> C
    C --> D
    C --> E
    
    D --> G
    E --> G
    
    F --> G
    G --> H
    
    I --> G
    I --> J
    I --> K
    
    G --> CustomerRatePlanDeviceQueue
```

### 10. Processing Timeline and Sequence

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant Lambda
    participant Database
    participant Queue
    participant Logger
    
    Client->>API: Customer Rate Plan Change Request
    API->>Lambda: Trigger Bulk Change Processing
    
    Lambda->>Lambda: Validate Request (ChangeType=4)
    Lambda->>Lambda: Extract CustomerRatePlanUpdate
    
    alt Immediate Processing (effectiveDate <= now)
        Lambda->>Database: Execute usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
        Database-->>Lambda: Update Result
        Lambda->>Logger: Log Success/Error (M2M/Mobility)
    else Scheduled Processing (effectiveDate > now)
        Lambda->>Database: Get Device Changes
        Database-->>Lambda: Device List
        Lambda->>Queue: Bulk Insert to CustomerRatePlanDeviceQueue
        Queue-->>Lambda: Insert Result
        Lambda->>Logger: Log Queue Status
    end
    
    Logger-->>Lambda: Log Entry Created
    Lambda-->>API: Processing Result
    API-->>Client: Response (Success/Error)
    
    Note over Queue: Future processing will be handled by scheduled job
```

## Key Constants and Configuration

### Change Request Types
```
CustomerRatePlanChange = 4
CarrierRatePlanChange = 7
```

### Integration Types
```
POD19 = [Specific ID for AT&T POD19]
Jasper = [Compatible processing]
```

### Stored Procedures
```
usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber
```

### Database Tables
```
CustomerRatePlanDeviceQueue
M2M_DeviceChange
Mobility_DeviceChange
Device
BulkChange
```

## Performance Considerations

### Batch Processing
- Processes multiple devices in single stored procedure call
- Uses SqlBulkCopy for queue operations
- Implements connection pooling

### Async Operations
- Non-blocking database operations
- Parallel processing capabilities
- Queue-based scheduling for future changes

### Error Resilience
- SQL retry policies
- Comprehensive error logging
- Transaction rollback capabilities
- Unique error reference IDs

## Security and Compliance

### Authorization
- Tenant-level access control
- Service provider validation
- Portal-specific permissions

### Data Protection
- Encrypted connection strings
- Sanitized logging (no sensitive data)
- Audit trail maintenance

### Validation
- Input parameter validation
- Business rule enforcement
- Rate limit checking