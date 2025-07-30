# Pod 19 Service Provider - Customer Rate Plan Change DFD

## Data Flow Diagram for Customer Rate Plan Change Type (Pod 19)

### Level 0 DFD - Context Diagram

```mermaid
graph TB
    %% External Entities
    Admin[Administrator/Portal User]
    DB[(Database)]
    Queue[(Queue System)]
    Logger[Logging System]
    
    %% Main System
    System[Pod 19 Customer Rate Plan Change System]
    
    %% Data Flows
    Admin -->|Bulk Change Request| System
    System -->|Device Updates| DB
    System -->|Queue Entries| Queue
    System -->|Audit Logs| Logger
    System -->|Status Response| Admin
    DB -->|Device Data| System
    Queue -->|Scheduled Tasks| System
```

### Level 1 DFD - System Breakdown

```mermaid
graph TB
    %% External Entities
    Admin[Administrator]
    DB[(Central Database)]
    QueueDB[(Queue Database)]
    LogDB[(Log Database)]
    
    %% Main Processes
    P1[1.0 Request Validation<br/>& Parsing]
    P2[2.0 Effective Date<br/>Processing]
    P3[3.0 Immediate<br/>Processing]
    P4[4.0 Scheduled<br/>Processing]
    P5[5.0 Logging &<br/>Audit Trail]
    
    %% Data Stores
    D1[(D1: BulkChange)]
    D2[(D2: CustomerRatePlan)]
    D3[(D3: DeviceQueue)]
    D4[(D4: AuditLogs)]
    
    %% Data Flows
    Admin -->|BulkChangeRequest| P1
    P1 -->|Validated Request| P2
    P1 -->|Store Request| D1
    
    P2 -->|Immediate Processing| P3
    P2 -->|Future Date| P4
    
    P3 -->|Update Devices| DB
    P3 -->|Log Result| P5
    
    P4 -->|Queue Entry| D3
    P4 -->|Queue to Database| QueueDB
    P4 -->|Log Queue Status| P5
    
    P5 -->|Audit Entry| D4
    P5 -->|Write Logs| LogDB
    
    P3 -->|Status| Admin
    P4 -->|Status| Admin
    
    DB -->|Device Info| P3
    D2 -->|Rate Plan Data| P3
    D3 -->|Scheduled Tasks| P3
```

### Level 2 DFD - Detailed Process Flow

```mermaid
graph TB
    %% Input
    Input[BulkChangeRequest<br/>Pod 19 Provider]
    
    %% Validation Process
    subgraph "1.0 Request Validation"
        P11[1.1 Extract Customer<br/>Rate Plan Update]
        P12[1.2 Validate<br/>Parameters]
        P13[1.3 Check Pod 19<br/>Integration]
    end
    
    %% Date Processing
    subgraph "2.0 Effective Date Processing"
        P21[2.1 Parse<br/>Effective Date]
        P22[2.2 Compare with<br/>Current Time]
        P23[2.3 Route Request]
    end
    
    %% Immediate Processing
    subgraph "3.0 Immediate Processing"
        P31[3.1 Execute Stored<br/>Procedure]
        P32[3.2 Update Device<br/>Rate Plans]
        P33[3.3 Validate<br/>Results]
    end
    
    %% Scheduled Processing
    subgraph "4.0 Scheduled Processing"
        P41[4.1 Create Queue<br/>Entry]
        P42[4.2 Store in<br/>DeviceQueue Table]
        P43[4.3 Schedule<br/>Future Processing]
    end
    
    %% Logging
    subgraph "5.0 Logging & Audit"
        P51[5.1 Create Log<br/>Entry]
        P52[5.2 Determine Portal<br/>Type (M2M/Mobility)]
        P53[5.3 Write Audit<br/>Trail]
    end
    
    %% Data Stores
    DB[(Central Database)]
    QueueDB[(CustomerRatePlan<br/>DeviceQueue)]
    LogDB[(Audit Logs)]
    
    %% Flow
    Input --> P11
    P11 --> P12
    P12 --> P13
    P13 --> P21
    P21 --> P22
    P22 --> P23
    
    P23 -->|Immediate| P31
    P23 -->|Scheduled| P41
    
    P31 --> P32
    P32 --> P33
    P33 --> P51
    
    P41 --> P42
    P42 --> P43
    P43 --> P51
    
    P51 --> P52
    P52 --> P53
    
    P31 --> DB
    P42 --> QueueDB
    P53 --> LogDB
```

### Data Flow Details

#### 1. Input Data Structure
```
BulkChangeRequest {
    ServiceProviderId: 19 (Pod 19)
    ChangeType: 4 (Customer Rate Plan Change)
    CustomerRatePlanUpdate: {
        CustomerRatePlanId: Integer
        CustomerDataAllocationMB: Decimal
        CustomerPoolId: Integer
        EffectiveDate: DateTime
    }
    Devices: String[]
}
```

#### 2. Processing Decision Logic
```mermaid
flowchart TD
    Start([Start: Pod 19 Request])
    
    ValidateInput{Validate Input<br/>Parameters?}
    ValidateInput -->|Invalid| Error[Return Error]
    ValidateInput -->|Valid| CheckDate{Effective Date<br/>Check}
    
    CheckDate -->|Null or <= Now| Immediate[Immediate Processing]
    CheckDate -->|Future Date| Schedule[Schedule Processing]
    
    Immediate --> ExecuteSP[Execute SP:<br/>usp_DeviceBulkChange_<br/>CustomerRatePlanChange_<br/>UpdateDevices]
    
    Schedule --> CreateQueue[Create Queue Entry<br/>in CustomerRatePlan<br/>DeviceQueue]
    
    ExecuteSP --> LogResult[Log Processing<br/>Result]
    CreateQueue --> LogQueue[Log Queue<br/>Status]
    
    LogResult --> End([End: Success])
    LogQueue --> End
    Error --> End
```

#### 3. Database Interactions

```mermaid
erDiagram
    BulkChange ||--o{ BulkChangeDetailRecord : contains
    BulkChangeDetailRecord ||--|| Device : updates
    Device ||--|| CustomerRatePlan : assigned
    CustomerRatePlan ||--o{ CustomerPool : belongs_to
    
    CustomerRatePlanDeviceQueue ||--|| Device : scheduled_for
    CustomerRatePlanDeviceQueue ||--|| CustomerRatePlan : target_plan
    
    M2MDeviceBulkChangeLog ||--|| BulkChangeDetailRecord : logs
    MobilityDeviceBulkChangeLog ||--|| BulkChangeDetailRecord : logs
    
    BulkChange {
        int Id PK
        int ServiceProviderId
        int ChangeType
        int IntegrationId
        datetime CreatedDate
        int TenantId
    }
    
    CustomerRatePlanDeviceQueue {
        int DeviceId PK
        int CustomerRatePlanId
        int CustomerRatePoolId
        decimal CustomerDataAllocationMB
        datetime EffectiveDate
        int PortalType
        int TenantId
    }
    
    Device {
        int Id PK
        string ICCID
        string SubscriberNumber
        int CustomerRatePlanId
        decimal DataAllocationMB
    }
```

#### 4. Integration Specific Flow for Pod 19

```mermaid
graph LR
    subgraph "Pod 19 Integration Layer"
        A[Pod 19 Request] --> B{Integration Type<br/>Check}
        B -->|POD19| C[Jasper-Compatible<br/>Processing]
        B -->|Other| D[Different Handler]
    end
    
    subgraph "Customer Rate Plan Processing"
        C --> E[Standard Customer<br/>Rate Plan Flow]
        E --> F[Database Update<br/>Operations]
        F --> G[Audit Logging]
    end
    
    subgraph "Pod 19 Specific Considerations"
        H[Pod 19 Authentication]
        I[Pod 19 Validation Rules]
        J[Pod 19 Error Handling]
    end
    
    C -.-> H
    E -.-> I
    G -.-> J
```

### Error Handling Flow

```mermaid
graph TD
    Process[Processing Step]
    
    Process --> Check{Success?}
    Check -->|Yes| Success[Log Success<br/>Continue Flow]
    Check -->|No| ErrorType{Error Type?}
    
    ErrorType -->|Validation| ValidationError[Validation Error<br/>Return to User]
    ErrorType -->|Database| DatabaseError[Database Error<br/>Retry/Rollback]
    ErrorType -->|Integration| IntegrationError[Pod 19 Integration<br/>Error Handling]
    
    ValidationError --> LogError[Log Error<br/>Details]
    DatabaseError --> LogError
    IntegrationError --> LogError
    
    LogError --> EndError[End with<br/>Error Status]
    Success --> EndSuccess[End with<br/>Success Status]
```

### Performance and Monitoring

```mermaid
graph TB
    subgraph "Performance Metrics"
        M1[Request Volume<br/>per Pod 19]
        M2[Processing Time<br/>per Request]
        M3[Success/Error<br/>Rates]
        M4[Queue Depth<br/>Monitoring]
    end
    
    subgraph "Monitoring Alerts"
        A1[High Error Rate<br/>Alert]
        A2[Processing Delay<br/>Alert]
        A3[Queue Overflow<br/>Alert]
        A4[Integration Failure<br/>Alert]
    end
    
    M1 --> A1
    M2 --> A2
    M3 --> A1
    M4 --> A3
    
    A1 --> NotificationSystem[Notification<br/>System]
    A2 --> NotificationSystem
    A3 --> NotificationSystem
    A4 --> NotificationSystem
```

## Summary

This DFD represents the complete data flow for customer rate plan changes specific to Pod 19 service provider. Key characteristics:

1. **Pod 19 Integration**: Uses Jasper-compatible processing for carrier operations but follows standard flow for customer rate plan changes
2. **Dual Processing Paths**: Immediate execution or scheduled queue processing based on effective date
3. **Comprehensive Logging**: Both M2M and Mobility portal logging with full audit trails
4. **Error Handling**: Multi-layered error handling with specific considerations for Pod 19 integration
5. **Database Operations**: Uses standardized stored procedures for consistency across all service providers
6. **Queue Management**: Sophisticated scheduling system for future-dated changes

The system ensures data consistency, provides comprehensive audit trails, and maintains high availability for Pod 19 service provider operations.