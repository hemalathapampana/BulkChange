# Customer Assignment Data Flow - Mermaid Flowchart

```mermaid
graph TD
    A[Bulk Change Request: Customer Assignment] --> B[Validate Input Data]
    B --> C{Device Status Check}
    C -->|Device Inactive| D[Mark as Ineligible]
    C -->|Device Active| E[Validate Customer Access]
    
    E --> F{Customer Permission Check}
    F -->|No Permission| G[Access Denied Error]
    F -->|Permission Valid| H[Validate Site Assignment]
    
    H --> I{Create Rev Service?}
    I -->|Yes| J[Process Revenue Customer]
    I -->|No| K[Process AMOP Customer]
    
    J --> L[Execute usp_Assign_Customer_Update_Site]
    K --> M[Execute usp_DeviceBulkChange_Assign_Non_Rev_Customer]
    
    L --> N{Portal Type Check}
    M --> N
    
    N -->|M2M Portal| O[Log M2M Change Entry]
    N -->|Mobility Portal| P[Log Mobility Change Entry]
    N -->|LNP Portal| Q[Log LNP Change Entry]
    
    O --> R[Update Device_Tenant Table]
    P --> R
    Q --> R
    
    R --> S[Set Customer ID]
    S --> T[Set Site ID]
    T --> U[Update Rate Plan Info]
    U --> V{Carrier Integration Required?}
    
    V -->|Yes| W[Call Carrier API]
    V -->|No| X[Update Timestamps]
    
    W --> Y{API Response}
    Y -->|Success| X
    Y -->|Failed| Z[Mark as Error]
    
    X --> AA[Set Status = PROCESSED]
    AA --> BB[Assignment Complete]
    
    D --> CC[Generate Eligibility Error]
    G --> DD[Generate Permission Error]
    Z --> EE[Generate Integration Error]
    
    CC --> FF[Log Error Entry]
    DD --> FF
    EE --> FF
    
    FF --> GG[Set Status = ERROR]
    
    style A fill:#e1f5fe
    style BB fill:#c8e6c9
    style D fill:#ffcdd2
    style G fill:#ffcdd2
    style Z fill:#ffcdd2
    style L fill:#fff3e0
    style M fill:#fff3e0
    style W fill:#f3e5f5
```

## Process Description

### Input Validation Phase
- **Device Status Check**: Validates that devices are in active status
- **Customer Permission Check**: Ensures user has access to assign the selected customer
- **Site Assignment**: Validates customer sites and assigns appropriate site

### Processing Phase
- **Revenue vs AMOP**: Determines processing path based on CreateRevService flag
- **Database Updates**: Executes appropriate stored procedures for customer assignment
- **Portal Type Logging**: Creates audit entries based on portal type (M2M, Mobility, LNP)

### Integration Phase
- **Carrier API Calls**: Integrates with external carrier systems (ThingSpace, Jasper)
- **Rate Plan Updates**: Applies customer rate plans and data allocations
- **Service Activation**: Activates new services if required

### Completion Phase
- **Status Updates**: Marks bulk change as PROCESSED or ERROR
- **Audit Logging**: Creates comprehensive audit trail
- **Notifications**: Sends success/failure notifications

## Key Decision Points

1. **Device Eligibility**: Only active devices can be assigned
2. **Customer Permissions**: User must have access to the target customer
3. **Service Creation**: Determines if new revenue service needs to be created
4. **Carrier Integration**: Some assignments require external API calls
5. **Error Handling**: Multiple failure points with specific error logging