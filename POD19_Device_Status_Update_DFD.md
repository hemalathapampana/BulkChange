# POD 19 Service Provider - Device Status Update Data Flow Diagram

```mermaid
graph TD
    A[Client Request - Device Status Update] --> B[M2MController]
    B --> C[BulkChangeRequest Validation]
    C --> D[Extract StatusUpdateRequest]
    D --> E{Integration Type Check}
    E -->|IntegrationType.POD19| F[ProcessJasperStatusUpdateAsync]
    
    F --> G{Authentication Check}
    G -->|WriteIsEnabled = false| H[Log Warning & Mark Error]
    G -->|WriteIsEnabled = true| I[Get Jasper Authentication]
    
    I --> J[Extract Change Requests]
    J --> K[Get Rev Services & Customer Rate Plans]
    K --> L{For Each Device Change}
    
    L --> M{Status = 'deactivated' AND Has Communication Plan?}
    M -->|Yes| N[UpdateJasperRatePlanAsync]
    M -->|No| O[UpdateJasperDeviceStatusAsync]
    N --> O
    
    O --> P[Call Jasper API - Device Status Update]
    P --> Q{API Response}
    Q -->|Success| R[Log Success Entry]
    Q -->|Error| S[Log Error Entry]
    
    R --> T[ProcessRevServiceCreation]
    S --> U[Mark Change as Processed with Error]
    
    T --> V{Account Number Present?}
    V -->|Yes| W[UpdateRevCustomer]
    V -->|No| X[Skip Customer Update]
    
    W --> Y[Mark Change as Processed - Success]
    X --> Y
    
    Y --> Z{More Changes?}
    Z -->|Yes| L
    Z -->|No| AA[Check Update Rate Plan Errors]
    
    AA --> BB{Has Errors?}
    BB -->|Yes| CC[Send Error Email Notification]
    BB -->|No| DD[Return Success]
    
    CC --> DD
    U --> Z
    H --> EE[Return False]
    DD --> FF[Response to Client]
    EE --> FF
    
    %% Data Stores
    GG[(Jasper API)]
    HH[(M2M_DeviceChange Table)]
    II[(DeviceBulkChangeLog Table)]
    JJ[(Rev Service Database)]
    KK[(Customer Rate Plans)]
    
    %% API Connections
    P -.-> GG
    R -.-> II
    S -.-> II
    T -.-> JJ
    W -.-> JJ
    J -.-> HH
    K -.-> KK
    
    %% External Systems
    LL[Email Service - SES]
    CC -.-> LL
    
    %% Styling
    style A fill:#e1f5fe
    style P fill:#fff3e0
    style GG fill:#fff3e0
    style HH fill:#f3e5f5
    style II fill:#f3e5f5
    style JJ fill:#f3e5f5
    style KK fill:#f3e5f5
    style DD fill:#e8f5e8
    style EE fill:#ffebee
    style FF fill:#e8f5e8
    style LL fill:#fff8e1
```

## Process Flow Description

### 1. **Request Initiation**
- Client sends device status update request to M2MController
- Request contains device identifiers (ICCID) and target status

### 2. **Validation & Routing**
- BulkChangeRequest validation occurs
- StatusUpdateRequest is extracted
- Integration type check determines POD19 uses Jasper processing path

### 3. **Authentication & Setup**
- Jasper authentication information retrieved from database
- Write permissions validated
- If writes disabled, process logs warning and returns error

### 4. **Data Preparation**
- Change requests deserialized from JSON
- Rev services and customer rate plans retrieved
- Device changes prepared for batch processing

### 5. **Device Processing Loop**
For each device change:
- **Rate Plan Update**: If status is 'deactivated' and device has communication plan, update rate plan first
- **Status Update**: Call Jasper API to update device status
- **Logging**: Record success/error in DeviceBulkChangeLog
- **Rev Service**: Process Rev service creation if successful
- **Customer Update**: Update Rev customer if account number present

### 6. **Error Handling & Notifications**
- Collect any rate plan update errors
- Send email notifications for errors via AWS SES
- Mark individual changes as processed with success/failure status

### 7. **Response**
- Return success/failure status to client
- Complete bulk change processing

## Key Data Flows

### Input Data:
- Device ICCID
- Target Status (active, deactivated, etc.)
- Account Number
- Post Update Status ID
- Rev Service Information

### External API Calls:
- **Jasper API**: Device status updates
- **Rev Service API**: Service creation/updates
- **AWS SES**: Error notifications

### Database Updates:
- **M2M_DeviceChange**: Device change records
- **DeviceBulkChangeLog**: Process logging
- **Rev Services**: Service status updates
- **Customer Records**: Customer associations

## POD 19 Specific Notes:
- POD 19 uses the same processing path as Jasper integration
- Authentication and API endpoints are Jasper-based
- Rate plan updates occur before status changes for deactivation
- Full logging and error handling with email notifications
- Rev service integration for service line management