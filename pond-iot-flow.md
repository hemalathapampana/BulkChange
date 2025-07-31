# POND IoT Service Provider Flow

```mermaid
graph TD
    A[SQS Message Queue] --> B[AltaworxDeviceBulkChange Lambda]
    B --> C{Parse SQS Message}
    C --> D[Extract BulkChange Details]
    D --> E{Check Change Request Type}
    
    E -->|StatusUpdate| F[ProcessStatusUpdateAsync]
    F --> G{Check Integration Type}
    
    G -->|Pond| H[ProcessPondStatusUpdateAsync]
    
    H --> M[POND API Authentication]
    M --> N[Update Device Status via POND API]
    N --> O[Update Service Status via POND API]
    O --> P{API Success?}
    
    P -->|Yes| Q[Create Rev Service]
    P -->|No| R[Log Error]
    
    Q --> S[Update Database Status]
    R --> T[Mark as Error]
    S --> U[Log Success]
    T --> V[DeviceBulkChangeLog]
    U --> V
    
    V --> W[Mark Device as Processed]
    W --> X{More Devices?}
    X -->|Yes| H
    X -->|No| Y[Complete Bulk Change]
    
    Y --> Z[Update BulkChange Status]
    Z --> AA[Process Complete]

    %% Data Stores
    DB1[(Central Database)]
    DB2[(M2M_DeviceChange)]
    DB3[(DeviceBulkChangeLog)]
    
    D -.-> DB1
    S -.-> DB2
    V -.-> DB3
    W -.-> DB2
    Z -.-> DB1

    %% External APIs
    API1[POND Service API]
    API2[Rev Service API]
    
    N -.-> API1
    O -.-> API1
    Q -.-> API2

    %% Retry Logic
    RET[Retry Queue]
    P -->|Retry Needed| RET
    RET -.-> A

    style A fill:#e1f5fe
    style H fill:#fff3e0
    style API1 fill:#f3e5f5
    style DB1 fill:#e8f5e8
    style AA fill:#ffebee
```

## Key Components for POND IoT Provider:

### Main Flow:
1. **SQS Message Processing** - Receives bulk change requests
2. **Message Parsing** - Extracts device change details
3. **POND Integration** - Routes to POND-specific processing
4. **API Operations** - Authenticates and updates device/service status
5. **Database Updates** - Records changes and logs
6. **Completion** - Marks process as complete

### External Dependencies:
- **POND Service API** - Primary integration for device management
- **Rev Service API** - Secondary service creation
- **Central Database** - Main data store
- **Device Change Tables** - Audit and tracking

### Error Handling:
- API failure logging
- Retry queue mechanism
- Error state management