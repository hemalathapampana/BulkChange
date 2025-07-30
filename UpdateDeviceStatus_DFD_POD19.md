# Update Device Status - Data Flow Diagram (AT&T POD19)

## Overview
This Data Flow Diagram represents the Update Device Status process within the AT&T POD19 architecture, showing how device status changes flow through the M2M/Mobility portals, processing components, and external carrier APIs.

## Data Flow Diagram

```mermaid
graph TD
    %% External Entities
    A[Client Portal Request] --> B{Portal Type}
    
    %% Portal Controllers
    B -->|M2M Portal| C[M2MController]
    B -->|Mobility Portal| D[MobilityController]
    
    %% Request Processing
    C --> E[BulkChangeStatusUpdateRequest Validation]
    D --> E
    
    E --> F[Extract StatusUpdateRequest&lt;T&gt;]
    F --> G[Determine Integration Type]
    
    %% Integration Type Decision
    G --> H{Integration Provider}
    
    %% Jasper Integration Path
    H -->|Jasper| I[BuildStatusUpdateChangeDetailsJasper]
    I --> J[Create M2M_DeviceChange Records]
    J --> K[Queue: AltaworxDeviceBulkChange Lambda]
    
    %% ThingSpace Integration Path  
    H -->|ThingSpace| L[BuildStatusUpdateChangeDetailsThingSpace]
    L --> M[Create M2M_DeviceChange Records]
    M --> N[Queue: AltaworxDeviceBulkChange Lambda]
    
    %% Lambda Processing
    K --> O[AltaworxDeviceBulkChange.cs Handler]
    N --> O
    
    O --> P{Change Type Check}
    P -->|StatusUpdate| Q[ProcessDeviceStatusUpdate]
    
    %% Status Update Processing
    Q --> R{Target Status}
    
    %% Immediate vs Scheduled Processing
    R -->|Active/Suspend/Deactivate| S[UpdateJasperDeviceStatusAsync]
    R -->|Pending Activation| T[UpdateThingSpaceDeviceStatusAsync]
    R -->|Other Status| U[Custom Status Handler]
    
    %% Carrier API Calls
    S --> V[Jasper REST API Call]
    T --> W[ThingSpace REST API Call]
    U --> X[Generic Carrier API Call]
    
    %% API Response Processing
    V --> Y[Parse UpdateDeviceStatusResult]
    W --> Z[Parse UpdateThingSpaceDeviceStatusResult]
    X --> AA[Parse API Response]
    
    %% Database Updates
    Y --> BB[SQL: usp_DeviceBulkChange_StatusUpdate_UpdateDeviceRecords]
    Z --> BB
    AA --> BB
    
    BB --> CC[Update Device Table]
    BB --> DD[Update M2M_DeviceChange Table]
    BB --> EE[Update DeviceStatusHistory Table]
    
    %% Callback Processing (ThingSpace specific)
    Z --> FF{Has RequestId?}
    FF -->|Yes| GG[CheckRequestIdExist]
    GG --> HH[SendMessageToCheckThingSpaceDeviceNewActivate]
    HH --> II[FunctionProcessUpdateStatus]
    
    %% Post-Processing Validation
    II --> JJ[ProcessUpdateDeviceAfterActivateThingSpaceDevice]
    JJ --> KK[Get ThingSpace Device Status]
    KK --> LL{Device Active?}
    LL -->|Yes| MM[UpdateMSISDNToM2M_DeviceChange]
    LL -->|No| NN[Schedule Retry via SQS]
    
    %% Logging and Response
    CC --> OO{Portal Type}
    DD --> OO
    EE --> OO
    MM --> OO
    
    OO -->|M2M Portal| PP[AddM2MLogEntry]
    OO -->|Mobility Portal| QQ[AddMobilityLogEntry]
    
    PP --> RR[DeviceChangeResult Response]
    QQ --> RR
    
    %% Error Handling
    NN --> SS[Retry Logic Check]
    SS --> TT{Retry Count < Max?}
    TT -->|Yes| UU[Send SQS Message for Retry]
    TT -->|No| VV[Mark as Failed]
    
    UU --> O
    VV --> RR
    
    RR --> WW[Return to Client]
    
    %% Data Stores
    XX[(Central Database)]
    YY[(Jasper Database)]
    ZZ[(Device Inventory)]
    AAA[(M2M_DeviceChange)]
    BBB[(BulkChange Log)]
    CCC[(SQS Queue)]
    
    %% Data Store Connections
    BB -.-> XX
    CC -.-> XX
    DD -.-> AAA
    EE -.-> XX
    MM -.-> XX
    PP -.-> BBB
    QQ -.-> BBB
    UU -.-> CCC
    
    %% External Systems
    DDD[Jasper Carrier API]
    EEE[ThingSpace Carrier API]
    FFF[Generic Carrier API]
    
    V -.-> DDD
    W -.-> EEE
    X -.-> FFF
    
    %% Styling
    classDef portalClass fill:#e1f5fe,stroke:#0277bd,stroke-width:2px
    classDef processClass fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef dbClass fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef apiClass fill:#fce4ec,stroke:#c2185b,stroke-width:2px
    classDef queueClass fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    
    class A,C,D portalClass
    class S,T,U,Q,O,II,JJ processClass
    class BB,CC,DD,EE,MM,XX,YY,ZZ,AAA,BBB dbClass
    class V,W,X,DDD,EEE,FFF apiClass
    class K,N,UU,CCC queueClass
```

## Data Elements and Flows

### 1. Input Data Elements

#### StatusUpdateRequest&lt;T&gt;
- **UpdateStatus**: Target device status (active, suspend, deactivate, etc.)
- **IsIgnoreCurrentStatus**: Flag to bypass current status validation
- **PostUpdateStatusId**: Target status ID after processing
- **AccountNumber**: Customer account identifier
- **Request**: Provider-specific request object (Jasper/ThingSpace)
- **RevService**: Revenue service association
- **IntegrationAuthenticationId**: Provider authentication credentials

#### BulkChangeStatusUpdate
- **TargetStatus**: Desired device status
- **JasperStatusUpdate**: Jasper-specific parameters
- **ThingSpaceStatusUpdate**: ThingSpace-specific parameters

### 2. Processing Components

#### M2MController/MobilityController
- **Input**: HTTP POST request with status change payload
- **Processing**: 
  - Validates request structure and permissions
  - Determines integration provider (Jasper/ThingSpace)
  - Creates M2M_DeviceChange records
  - Queues processing via AWS Lambda
- **Output**: BulkChange ID and processing status

#### AltaworxDeviceBulkChange Lambda
- **Input**: SQS message with BulkChange details
- **Processing**:
  - Retrieves device change records
  - Calls appropriate carrier API
  - Processes API responses
  - Updates database records
  - Handles error scenarios and retries
- **Output**: DeviceChangeResult with success/failure status

### 3. External System Integrations

#### Jasper API Integration
- **Endpoint**: Jasper REST API for device status updates
- **Authentication**: API key-based authentication
- **Request Format**: JSON with device identifiers and target status
- **Response**: UpdateDeviceStatusResult with ICCID confirmation

#### ThingSpace API Integration
- **Endpoint**: Verizon ThingSpace API for device lifecycle management
- **Authentication**: OAuth2 with session tokens
- **Request Format**: JSON with device details and activation parameters
- **Response**: UpdateThingSpaceDeviceStatusResult with requestId for tracking

### 4. Database Operations

#### Primary Stored Procedures
- **usp_DeviceBulkChange_StatusUpdate_UpdateDeviceRecords**: Main status update procedure
- **usp_DeviceBulkChange_UpdateMobilityDeviceChange**: Mobility-specific updates
- **usp_DeviceBulkChange_UpdateM2MChange**: M2M-specific updates

#### Key Database Tables
- **Device**: Core device inventory with status tracking
- **M2M_DeviceChange**: Change request tracking and status
- **DeviceStatusHistory**: Audit trail of status changes
- **BulkChangeLog**: Processing logs and error tracking

### 5. Queue and Retry Mechanisms

#### SQS Message Processing
- **Message Attributes**:
  - BulkChangeId: Processing batch identifier
  - RetryNumber: Current retry attempt
  - IsFromAutomatedUpdateDeviceStatusLambda: Processing source flag
- **Retry Logic**: Configurable retry count with exponential backoff
- **Dead Letter Queue**: Failed messages after max retries

### 6. Status-Specific Processing Flows

#### ThingSpace Pending Activation
1. Validate device eligibility for activation
2. Call ThingSpace activation API
3. Receive requestId for tracking
4. Schedule callback verification (15-minute delay)
5. Monitor activation status via GetDevice API
6. Update MSISDN and IP address once active

#### Jasper Status Updates
1. Direct API call with target status
2. Immediate response processing
3. Database updates with confirmed status
4. No callback mechanism required

### 7. Error Handling and Logging

#### Error Categories
- **Validation Errors**: Invalid request format or missing data
- **API Errors**: Carrier API failures or timeouts
- **Database Errors**: SQL execution failures
- **Authentication Errors**: Invalid or expired credentials

#### Logging Components
- **M2M Log Entries**: Portal-specific processing logs
- **Mobility Log Entries**: Mobility portal processing logs
- **Device Change Logs**: Detailed API request/response tracking
- **Error Logs**: Exception details and stack traces

## AT&T POD19 Specific Considerations

### Integration Points
- **Single Sign-On (SSO)**: AT&T enterprise authentication
- **Network Security**: VPN tunneling for carrier API access
- **Data Residency**: All processing within AT&T network boundaries
- **Compliance**: GDPR and telecommunications regulations

### Performance Requirements
- **Response Time**: < 5 seconds for immediate status changes
- **Throughput**: Support for bulk operations (1000+ devices)
- **Availability**: 99.9% uptime with failover capabilities
- **Scalability**: Auto-scaling based on processing queue depth

### Monitoring and Alerting
- **Real-time Dashboards**: Device status change metrics
- **Error Rate Monitoring**: API failure rate tracking
- **Performance Metrics**: Processing time and queue depth
- **Business Intelligence**: Status change analytics and reporting

This DFD provides a comprehensive view of the Update Device Status process within the AT&T POD19 environment, showing the complete flow from client request through carrier API integration to final database updates and response delivery.