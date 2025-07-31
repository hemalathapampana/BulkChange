# POND IoT Carrier Rate Plan Change Flow

```mermaid
graph TD
    A[Client Submits Carrier Rate Plan Change Request] --> B[BulkChangeRequest Validation]
    B --> C[Extract CarrierRatePlanUpdate Model]
    C --> D{Service Provider Check}
    
    D -->|Not POND| E[Other Service Provider Path]
    E --> Z[Complete]
    
    D -->|POND IoT| F[POND Authentication Setup]
    F --> F1[Get POND Authentication]
    F1 --> F2{Authentication Valid?}
    
    F2 -->|No| G[Log Authentication Error]
    G --> H[Mark Change as Failed]
    H --> Z
    
    F2 -->|Yes| I{Write Enabled?}
    I -->|No| J[Service Provider Disabled Error]
    J --> H
    
    I -->|Yes| K[Initialize POND API Service]
    K --> L[Determine Base URI]
    L --> M[Process Each Device Change]
    
    M --> N[Get Existing Active Packages]
    N --> O[Add New POND Package]
    O --> P{Package Added Successfully?}
    
    P -->|No| Q[Mark Device Failed]
    Q --> R{More Devices?}
    
    P -->|Yes| S[Parse Package Response]
    S --> T{Package Valid?}
    T -->|No| Q
    
    T -->|Yes| U[Update New Package Status to ACTIVE]
    U --> V{Status Update Success?}
    
    V -->|No| W[Skip Device Update]
    V -->|Yes| X{Existing Packages Found?}
    
    X -->|No| Y[Set shouldUpdateDeviceRatePlan = true]
    X -->|Yes| AA[Terminate Existing Packages]
    AA --> BB{Termination Success?}
    
    BB -->|Yes| Y
    BB -->|No| CC[Set New Package to TERMINATED]
    CC --> DD[Set shouldUpdateDeviceRatePlan = false]
    
    Y --> EE[Save Package to Database]
    DD --> EE
    W --> EE
    
    EE --> FF{Should Update Device Rate Plan?}
    FF -->|No| GG[Mark Device Processed]
    FF -->|Yes| HH[Update M2M Device Rate Plan]
    HH --> II[Update Device Repository]
    II --> JJ[Log Database Update]
    JJ --> GG
    
    GG --> R
    R -->|Yes| M
    R -->|No| KK[Complete Bulk Change]
    KK --> Z
    
    style A fill:#e1f5fe
    style Z fill:#c8e6c9
    style O fill:#fff3e0
    style U fill:#fff3e0
    style AA fill:#f3e5f5
    style HH fill:#fff3e0
```

## Key Components:

### Authentication Phase:
- **POND Authentication**: Retrieves service provider credentials
- **Write Permission Check**: Validates if carrier rate plan changes are enabled
- **API Service Setup**: Initializes POND API service with production/sandbox URLs

### Package Management:
- **Get Existing Packages**: Retrieves currently active packages for the device
- **Add New Package**: Creates new package using PackageTypeId from rate plan
- **Package Status Management**: Updates package status (ACTIVE/TERMINATED)
- **Existing Package Termination**: Terminates old packages when new one is activated

### Device Update Process:
- **Database Persistence**: Saves package information to AMOP database
- **Device Rate Plan Update**: Updates the device's carrier rate plan in M2M system
- **Status Tracking**: Marks devices as processed with success/failure status

### Error Handling:
- Authentication failures are logged and bulk change is marked as failed
- Individual device failures don't stop processing of other devices
- Package creation failures skip device update but continue processing
- API errors are logged with specific error messages

### Integration Points:
- **POND API**: External carrier API for package management
- **AMOP Database**: Internal database for device and package tracking
- **M2M Device Repository**: Device management system updates