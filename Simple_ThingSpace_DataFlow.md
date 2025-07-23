# Simplified Data Flow Diagram - Verizon ThingSpace Update Device Status

## Simple ThingSpace Data Flow

```mermaid
graph TD
    A[SQS Message Received] --> B[Validate Bulk Change Request]
    B --> C[Get ThingSpace Authentication]
    
    C --> D[Get OAuth Access Token]
    D --> E[Get VZ-M2M Session Token]
    E --> F[Load Devices to Process]
    
    F --> G{For Each Device}
    G --> H{Device Status Operation}
    
    H -->|activate| I[Activate Device]
    H -->|suspend| J[Suspend Device]
    H -->|restore| K[Restore Device]
    H -->|deactive| L[Deactivate Device]
    H -->|inventory| M[Add to Inventory]
    
    %% Activation Flow
    I --> I1[Call ThingSpace Activate API]
    I1 --> I2[Get Request ID]
    I2 --> I3{Has PPU Info?}
    I3 -->|Yes| I4[Wait for Callback/Poll Status]
    I3 -->|No| I5[Update Device Fields]
    I4 --> I6[Extract MSISDN & Details]
    I5 --> I6
    I6 --> I7[Update Database]
    
    %% Other Operations
    J --> J1[Call ThingSpace Suspend API]
    K --> K1[Call ThingSpace Restore API]
    L --> L1[Call ThingSpace Deactivate API]
    M --> M1[Add Device to ThingSpace Inventory]
    
    J1 --> N[Log Result]
    K1 --> N
    L1 --> N
    M1 --> N
    I7 --> N
    
    N --> O[Update Device Status in DB]
    O --> P[Create Rev.IO Service if Needed]
    P --> Q[Mark Device as Processed]
    
    Q --> R{More Devices?}
    R -->|Yes| G
    R -->|No| S[Complete Processing]
    
    %% External Systems
    TS_API[ThingSpace API]
    DB[(Database)]
    REV[(Rev.IO)]
    
    %% Connections
    I1 -.-> TS_API
    J1 -.-> TS_API
    K1 -.-> TS_API
    L1 -.-> TS_API
    
    O -.-> DB
    I7 -.-> DB
    P -.-> REV
    
    %% Styling
    classDef startEnd fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef process fill:#fff3e0,stroke:#e65100,stroke-width:2px
    classDef decision fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef operation fill:#e8f5e8,stroke:#1b5e20,stroke-width:2px
    classDef external fill:#ffebee,stroke:#c62828,stroke-width:2px
    
    class A,S startEnd
    class B,C,D,E,F,N,O,P,Q process
    class G,H,I3,R decision
    class I,J,K,L,M,I1,I2,I4,I5,I6,I7,J1,K1,L1,M1 operation
    class TS_API,DB,REV external
```

## Flow Steps Explained

### 1. **Initialization**
- Receive SQS message with bulk change request
- Validate the request and get ThingSpace configuration
- Authenticate with ThingSpace (OAuth + Session token)

### 2. **Device Processing Loop**
- Load all devices that need status updates
- Process each device individually
- Determine the required status operation

### 3. **Status Operations**
- **Activate**: Call activate API, handle PPU if needed, wait for confirmation
- **Suspend**: Call suspend API to temporarily disable service
- **Restore**: Call restore API to reactivate suspended device
- **Deactivate**: Call deactivate API to permanently terminate service
- **Inventory**: Add device to ThingSpace inventory without activation

### 4. **Post-Processing**
- Log the API operation result
- Update device status in local database
- Create Rev.IO service line if successful
- Mark device change record as processed

### 5. **Completion**
- Continue processing remaining devices
- Complete when all devices are processed

## Key ThingSpace Features

- **Dual Authentication**: OAuth access token + VZ-M2M session token required
- **PPU Handling**: Primary Place of Use validation for device activation
- **Callback Integration**: Asynchronous confirmation for activation operations
- **MSISDN Assignment**: Automatic phone number assignment during activation
- **Comprehensive Logging**: Full audit trail of all operations