# BulkChange System Flow Diagram

## Overview
The BulkChange system is an AWS Lambda-based service that processes various types of device bulk operations through SQS messages. It handles different change request types with appropriate processing flows and error handling.

## Main Flow

```mermaid
graph TD
    A[SQS Event Received] --> B[FunctionHandler]
    B --> C[Parse SQS Message]
    C --> D[Extract BulkChangeId & Parameters]
    D --> E{Message Type?}
    
    E -->|ThingSpace Retry| F[Process ThingSpace Device Activation Retry]
    E -->|Identifier Retry| G[Process Update Identifier Retry]
    E -->|Standard| H[ProcessBulkChangeAsync]
    
    F --> F1[Check Retry Count]
    F1 -->|Max Retries| F2[Mark as PROCESSED]
    F1 -->|Continue| F3[Send Retry Message with 15min delay]
    
    G --> G1[Check Retry Count]
    G1 -->|Max Retries| G2[Mark as PROCESSED]
    G1 -->|Continue| G3[Retry Update Identifier]
    
    H --> I[Get BulkChange Record]
    I --> J{BulkChange Found?}
    J -->|No| K[Log Exception & Exit]
    J -->|Yes| L[Initialize Repositories & Policies]
    L --> M{ChangeRequestType?}
    
    M --> N[StatusUpdate]
    M --> O[ActivateNewService]
    M --> P[Archival]
    M --> Q[CustomerRatePlanChange]
    M --> R[CustomerAssignment]
    M --> S[CarrierRatePlanChange]
    M --> T[CreateRevService]
    M --> U[ChangeICCIDAndIMEI]
    M --> V[EditUsernameCostCenter]
    
    N --> N1[Get Device Changes]
    N1 --> N2[ProcessStatusUpdateAsync]
    
    O --> O1[ProcessNewServiceActivationAsync]
    
    P --> P1[Get Device Changes]
    P1 --> P2[ProcessArchivalAsync]
    
    Q --> Q1[ProcessCustomerRatePlanChangeAsync]
    
    R --> R1[Get Bulk Change Request]
    R1 --> R2{Rev Service?}
    R2 -->|No| R3[UpdateAMOPCustomer]
    R2 -->|Yes| R4[ProcessAssociateCustomerAsync]
    
    S --> S1[ProcessCarrierRatePlanChangeAsync]
    
    T --> T1[Get Device Changes]
    T1 --> T2[ProcessCreateRevServiceAsync]
    
    U --> U1[ProcessChangeEquipmentAsync]
    
    V --> V1[ProcessEditUsernameAsync]
    
    N2 --> W{Continue Processing?}
    O1 --> W
    P2 --> W
    Q1 --> W
    R3 --> W
    R4 --> W
    S1 --> W
    T2 --> W
    U1 --> W
    V1 --> W
    
    W -->|Yes & More Items| X[Enqueue Next Batch with 5s delay]
    W -->|No More Items| Y[Mark BulkChange as PROCESSED]
    
    X --> Z[Continue Processing]
    Y --> AA[NotifyStatusUpdate]
    AA --> BB[Send Webhook Notifications]
    BB --> CC[End]
    
    K --> DD[Error Handling]
    DD --> EE{Retry Available?}
    EE -->|Yes| FF[Increment Retry & Enqueue]
    EE -->|No| GG[Mark as ERROR]
    FF --> HH[Continue with Retry]
    GG --> CC
    HH --> CC
```

## Change Request Types Detail

### 1. StatusUpdate Flow
```mermaid
graph TD
    A[StatusUpdate Request] --> B[Get Device Changes (PageSize)]
    B --> C{Integration Type?}
    C -->|Jasper| D[Process Jasper Status Update]
    C -->|Telegence| E[Process Telegence Status Update]
    C -->|ThingSpace| F[Process ThingSpace Status Update]
    C -->|eBonding| G[Process eBonding Status Update]
    C -->|Teal| H[Process Teal Status Update]
    C -->|Pond| I[Process Pond Status Update]
    
    D --> J[Update Device Status via Jasper API]
    E --> K[Update Device Status via Telegence API]
    F --> L[Update Device Status via ThingSpace API]
    G --> M[Update Device Status via eBonding API]
    H --> N[Update Device Status via Teal API]
    I --> O[Update Device Status via Pond API]
    
    J --> P[Log Results]
    K --> P
    L --> P
    M --> P
    N --> P
    O --> P
    P --> Q[Return Success/Failure]
```

### 2. ActivateNewService Flow
```mermaid
graph TD
    A[ActivateNewService Request] --> B{Integration Type?}
    B -->|Jasper| C[Process Jasper New Service]
    B -->|Telegence| D[Process Telegence New Service]
    B -->|ThingSpace| E[Process ThingSpace New Service]
    B -->|eBonding| F[Process eBonding New Service]
    
    C --> G[Create Jasper Service]
    D --> H[Create Telegence Service]
    E --> I[Create ThingSpace Service]
    F --> J[Create eBonding Service]
    
    G --> K{Success?}
    H --> K
    I --> K
    J --> K
    
    K -->|Yes| L[Update Device Records]
    K -->|No| M[Log Error & Retry Logic]
    
    L --> N[Return Success]
    M --> O{Max Retries?}
    O -->|No| P[Schedule Retry]
    O -->|Yes| Q[Mark as Failed]
```

### 3. CustomerAssignment Flow
```mermaid
graph TD
    A[CustomerAssignment Request] --> B[Parse Change Request]
    B --> C{Rev Customer ID?}
    C -->|Empty| D[UpdateAMOPCustomer]
    C -->|Present| E[ProcessAssociateCustomerAsync]
    
    D --> F[Build Data Table for Non-Rev Customer]
    F --> G[Execute usp_DeviceBulkChange_Assign_Non_Rev_Customer]
    G --> H[Log Results]
    
    E --> I[Process Rev Customer Association]
    I --> J[Update Customer Assignments]
    J --> K[Log Results]
    
    H --> L[Update Device Change Status]
    K --> L
    L --> M[Return Success]
```

### 4. ChangeICCIDAndIMEI Flow
```mermaid
graph TD
    A[ChangeICCIDAndIMEI Request] --> B[Get Device Changes]
    B --> C{Integration Type?}
    C -->|Telegence| D[ProcessTelegenceChangeEquipmentAsync]
    C -->|ThingSpace| E[ProcessThingSpaceChangeIdentifierAsync]
    
    D --> F[Prepare Telegence Equipment Change]
    F --> G[Call Telegence API]
    G --> H[Update Device Records]
    
    E --> I[Prepare ThingSpace Identifier Change]
    I --> J[Call ThingSpace API]
    J --> K[Update Device Records]
    
    H --> L[Log Results]
    K --> L
    L --> M[Return Success]
```

## Error Handling & Retry Logic

```mermaid
graph TD
    A[Process Error] --> B{Error Type?}
    B -->|Transient SQL| C[SQL Retry Policy]
    B -->|HTTP Timeout| D[HTTP Retry Policy]
    B -->|Business Logic| E[Log & Continue]
    
    C --> F{Max SQL Retries?}
    F -->|No| G[Retry SQL Operation]
    F -->|Yes| H[Mark as Failed]
    
    D --> I{Max HTTP Retries?}
    I -->|No| J[Retry HTTP Request]
    I -->|Yes| K[Mark as Failed]
    
    G --> L[Continue Processing]
    J --> L
    E --> L
    H --> M[Update Status to ERROR]
    K --> M
    L --> N[Next Operation]
    M --> O[End Process]
```

## SQS Message Structure

```mermaid
graph LR
    A[SQS Message] --> B[MessageAttributes]
    B --> C[BULK_CHANGE_ID]
    B --> D[NEW_SERVICE_ACTIVATIONS]
    B --> E[SERVICE_PROVIDER_ID]
    B --> F[RETRY_NUMBER]
    B --> G[IS_RETRY_UPDATE_IDENTIFIER]
    B --> H[M2M_DEVICE_CHANGE_ID]
    B --> I[REQUEST_ID]
    
    A --> J[Body]
    J --> K[Additional Parameters]
```

## Database Interactions

```mermaid
graph TD
    A[BulkChange System] --> B[Central Database]
    B --> C[DeviceBulkChange Table]
    B --> D[M2M_DeviceChange Table]
    B --> E[DeviceBulkChangeLog Table]
    B --> F[Device Tables]
    
    C --> G[Bulk Change Records]
    D --> H[Individual Device Changes]
    E --> I[Processing Logs]
    F --> J[Device Status & Info]
    
    G --> K[Status: PENDING/PROCESSING/PROCESSED/ERROR]
    H --> L[ICCID, MSISDN, Status Updates]
    I --> M[Audit Trail & Error Logs]
    J --> N[Device State Management]
```

## Integration Points

```mermaid
graph TD
    A[BulkChange System] --> B[External APIs]
    B --> C[Jasper API]
    B --> D[Telegence API]
    B --> E[ThingSpace API]
    B --> F[eBonding API]
    B --> G[Teal API]
    B --> H[Pond API]
    B --> I[Rev.io API]
    
    A --> J[Internal Services]
    J --> K[Webhook Service]
    J --> L[Email Notifications]
    J --> M[Audit Logging]
    
    A --> N[AWS Services]
    N --> O[SQS Queues]
    N --> P[Lambda Functions]
    N --> Q[CloudWatch Logs]
```

## Performance Considerations

- **Batch Processing**: Device changes processed in configurable page sizes (default: 100)
- **Parallel Processing**: Configurable parallel request limits via environment variables
- **Retry Mechanisms**: Exponential backoff for transient failures
- **Memory Management**: Batch processing to avoid memory issues with large bulk changes
- **Queue Management**: SQS with delay mechanisms for retry scenarios

## Key Configuration

- `PageSize`: 100 (default batch size for device changes)
- `MAX_PARALLEL_REQUESTS`: Configurable parallel processing limit
- `SQL_TRANSIENT_RETRY_MAX_COUNT`: 3 retries for transient SQL errors
- `HTTP_RETRY_MAX_COUNT`: 3 retries for HTTP failures
- `NEW_SERVICE_ACTIVATION_MAX_COUNT`: 6 retries for new service activations
- `DELAY_IN_SECONDS_FIVE_SECONDS`: 5 second delay between batches
- `DELAY_IN_SECONDS_FIFTEEN_MINUTES`: 15 minute delay for ThingSpace retries