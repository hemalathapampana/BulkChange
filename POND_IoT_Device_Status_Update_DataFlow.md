# POND IoT Service Provider - Update Device Status Data Flow

## Overview
This document provides a comprehensive data flow diagram for the Update Device Status functionality in the POND IoT Service Provider system.

## Data Flow Diagram (Graph Format)

```mermaid
graph TD
    %% Entry Points
    A[Client Request] --> B[M2MController.Index]
    A1[SQS Message] --> B1[Lambda Function Handler]
    
    %% Main Processing Entry
    B --> C[Bulk Change Creation]
    B1 --> C1[ProcessEventAsync]
    
    %% SQS Message Processing
    C1 --> D1[ProcessEventRecordAsync]
    D1 --> E1[SqsValues Parsing]
    E1 --> F1{Message Type Check}
    
    %% Message Type Routing
    F1 -->|New Service Activation| G1[ProcessNewServiceActivationStatusAsync]
    F1 -->|Retry Device Activation| G2[ProcessUpdateDeviceAfterActivateThingSpaceDevice]
    F1 -->|Retry Update Identifier| G3[RetryUpdateIdentifierProcess]
    F1 -->|Standard Processing| G4[ProcessBulkChangeAsync]
    
    %% Bulk Change Processing
    C --> D[BulkChange Record Creation]
    D --> E[Status: NEW]
    G4 --> H[GetBulkChange]
    H --> I[GetDeviceChanges]
    I --> J{Change Request Type}
    
    %% Change Type Routing
    J -->|StatusUpdate| K[ProcessStatusUpdateAsync]
    J -->|CarrierRatePlanChange| L[ProcessCarrierRatePlanChangeAsync]
    J -->|AssociateCustomer| M[ProcessAssociateCustomerAsync]
    J -->|CreateRevService| N[ProcessCreateRevServiceAsync]
    J -->|ChangeICCIDAndIMEI| O[ProcessChangeEquipmentAsync]
    J -->|EditUsernameCostCenter| P[ProcessEditUsernameAsync]
    
    %% Status Update Processing
    K --> Q{Integration Type Check}
    Q -->|IntegrationType.Pond| R[ProcessPondStatusUpdateAsync]
    Q -->|IntegrationType.ThingSpace| S[ProcessThingSpaceStatusUpdateAsync]
    Q -->|IntegrationType.Jasper| T[ProcessJasperStatusUpdateAsync]
    Q -->|IntegrationType.Telegence| U[ProcessTelegenceStatusUpdateAsync]
    Q -->|IntegrationType.Teal| V[ProcessTealStatusUpdateAsync]
    Q -->|IntegrationType.eBonding| W[EnqueueDeviceBulkChangesAsync]
    
    %% POND Specific Processing
    R --> AA[PondRepository.GetPondAuthentication]
    AA --> BB{Authentication Check}
    BB -->|Valid| CC[PondApiService Creation]
    BB -->|Invalid| DD[Log Authentication Error]
    DD --> EE[Mark Change as Error]
    
    CC --> FF{Write Enabled Check}
    FF -->|Disabled| GG[Log Write Disabled Error]
    GG --> EE
    FF -->|Enabled| HH[Process Device Changes]
    
    %% Device Change Processing Loop
    HH --> II[For Each Change Record]
    II --> JJ[Parse StatusUpdateRequest]
    JJ --> KK{Device Status Check}
    KK -->|POND_ACTIVE| LL[Create PondUpdateServiceStatusRequest - Enable]
    KK -->|Other Status| MM[Create PondUpdateServiceStatusRequest - Disable]
    
    %% API Call Processing
    LL --> NN[pondApiService.UpdateServiceStatus]
    MM --> NN
    NN --> OO[API Call to POND]
    OO --> PP{API Response}
    
    %% Response Handling
    PP -->|Success| QQ[ResponseStatus = PROCESSED]
    PP -->|Error| RR[ResponseStatus = ERROR]
    
    QQ --> SS[Log Success Entry]
    RR --> TT[Log Error Entry]
    SS --> UU[ProcessRevServiceCreation]
    TT --> VV[Skip Rev Service Creation]
    
    %% Completion Processing
    UU --> WW[MarkProcessed - Success]
    VV --> XX[MarkProcessed - Error]
    WW --> YY{More Changes?}
    XX --> YY
    YY -->|Yes| II
    YY -->|No| ZZ[Return Processing Result]
    
    %% Database Updates
    WW --> AAA[Update Device Status in DB]
    XX --> BBB[Update Change Status in DB]
    AAA --> CCC[Update M2M_DeviceChange]
    BBB --> CCC
    
    %% Logging and Monitoring
    SS --> DDD[CreateM2MDeviceBulkChangeLog]
    TT --> DDD
    DDD --> EEE[DeviceBulkChangeLogRepository.AddM2MLogEntry]
    
    %% Final Status Updates
    ZZ --> FFF{All Changes Processed?}
    FFF -->|Yes| GGG[MarkBulkChangeStatusAsync - PROCESSED]
    FFF -->|No| HHH{Should Continue?}
    HHH -->|Yes| III[EnqueueDeviceBulkChangesAsync]
    HHH -->|No| JJJ[MarkBulkChangeStatusAsync - ERROR]
    
    %% Queue Management
    III --> KKK[SQS Message Creation]
    KKK --> LLL[Delay Processing]
    LLL --> MMM[Retry Logic]
    MMM --> B1
    
    %% Error Handling
    EE --> NNN{Retry Count Check}
    JJJ --> NNN
    NNN -->|Below Max Retries| III
    NNN -->|Max Retries Reached| OOO[Final Error Status]
    
    %% Notification System
    GGG --> PPP[NotifyStatusUpdate]
    OOO --> PPP
    PPP --> QQQ[AdmWebhookService.NotifyStatusUpdateDone]
    
    %% External Dependencies
    OO -.-> RRR[POND API Service]
    AAA -.-> SSS[AMOP Database]
    CCC -.-> SSS
    EEE -.-> SSS
    UU -.-> TTT[RevIO API Service]
    
    %% Configuration and Authentication
    AAA -.-> UUU[PondAuthentication Config]
    RRR -.-> UUU
    
    %% Styling
    classDef processNode fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef decisionNode fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef errorNode fill:#ffebee,stroke:#b71c1c,stroke-width:2px
    classDef successNode fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef externalNode fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    
    class A,B,B1,C,C1,D,D1,E,E1,H,I,K,R,AA,CC,HH,II,JJ,LL,MM,NN,UU,WW,AAA,CCC,DDD,EEE,GGG,PPP,QQQ processNode
    class F1,J,Q,BB,FF,KK,PP,YY,FFF,HHH,NNN decisionNode
    class DD,EE,GG,RR,TT,VV,XX,BBB,JJJ,OOO errorNode
    class QQ,SS,ZZ,III,KKK successNode
    class RRR,SSS,TTT,UUU externalNode
```

## Detailed Component Description

### 1. Entry Points
- **Client Request**: Web interface or API call to M2MController
- **SQS Message**: Asynchronous processing via AWS Lambda function

### 2. Message Processing Flow
- **SqsValues**: Parses SQS message attributes including BulkChangeId, RetryNumber, etc.
- **Message Type Routing**: Different paths based on message attributes

### 3. POND Status Update Process
- **Authentication**: Retrieves POND API credentials from PondRepository
- **Write Permission Check**: Validates if write operations are enabled for the service provider
- **Status Determination**: Maps device status to POND service status (ACTIVE/INACTIVE)
- **API Integration**: Calls POND API service to update device status

### 4. Database Operations
- **Device Table**: Updates device status, MSISDN, IP address, and modification metadata
- **M2M_DeviceChange**: Updates processing status and response details
- **DeviceBulkChangeLog**: Logs all operations for audit trail

### 5. Error Handling & Retry Logic
- **Retry Mechanism**: Configurable retry count with exponential backoff
- **Queue Management**: SQS for asynchronous processing and retry management
- **Status Tracking**: Comprehensive logging at each processing step

### 6. Integration Points
- **POND API**: External service for device status updates
- **RevIO API**: Service creation and management
- **AMOP Database**: Central database for device and customer data
- **AWS SQS**: Message queuing for reliable processing

### 7. Key Classes and Methods

#### Main Processing Classes:
- `AltaworxDeviceBulkChange.Function`: Main Lambda function handler
- `ProcessPondStatusUpdateAsync`: POND-specific status update logic
- `PondApiService`: API client for POND service integration
- `BulkChangeRepository`: Database operations for bulk changes
- `DeviceBulkChangeLogRepository`: Logging operations

#### Key Data Models:
- `StatusUpdateRequest<T>`: Request structure for status updates
- `PondUpdateServiceStatusRequest`: POND-specific API request model
- `BulkChangeDetailRecord`: Individual device change record
- `SqsValues`: SQS message parsing and retry management

### 8. Status Flow States
1. **NEW**: Initial state when bulk change is created
2. **PROCESSING**: Active processing state
3. **PROCESSED**: Successfully completed
4. **ERROR**: Failed processing (after retries)

### 9. POND-Specific Status Mapping
- **POND_ACTIVE**: Enables all service statuses for the device
- **Other Statuses**: Disables all service statuses for the device

This data flow ensures reliable, traceable, and scalable device status updates for the POND IoT Service Provider with comprehensive error handling and audit capabilities.