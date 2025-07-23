# Very Simple Data Flow - Verizon ThingSpace Update Device Status

```mermaid
graph TD
    A[Start: SQS Message] --> B[Authenticate with ThingSpace]
    B --> C[Get Devices to Update]
    
    C --> D{What Status Change?}
    
    D -->|Activate| E[Call Activate API]
    D -->|Suspend| F[Call Suspend API]
    D -->|Restore| G[Call Restore API]
    D -->|Deactivate| H[Call Deactivate API]
    
    E --> I[Update Database]
    F --> I
    G --> I
    H --> I
    
    I --> J[Create Service Record]
    J --> K[Mark as Complete]
    K --> L{More Devices?}
    
    L -->|Yes| D
    L -->|No| M[End: All Done]
    
    %% Styling
    classDef start fill:#4CAF50,color:white,stroke:#2E7D32,stroke-width:3px
    classDef process fill:#2196F3,color:white,stroke:#1565C0,stroke-width:2px
    classDef decision fill:#FF9800,color:white,stroke:#E65100,stroke-width:2px
    classDef api fill:#9C27B0,color:white,stroke:#6A1B9A,stroke-width:2px
    classDef end fill:#F44336,color:white,stroke:#C62828,stroke-width:3px
    
    class A start
    class B,C,I,J,K process
    class D,L decision
    class E,F,G,H api
    class M end
```

## Simple Flow Steps

### 1. **Start**
- Receive message to update device status

### 2. **Authentication**
- Login to Verizon ThingSpace system

### 3. **Get Devices**
- Load list of devices that need status changes

### 4. **Process Each Device**
- **Activate**: Turn on device service
- **Suspend**: Temporarily turn off service
- **Restore**: Turn service back on
- **Deactivate**: Permanently turn off service

### 5. **Update Records**
- Save new status in database
- Create billing service record
- Mark device as processed

### 6. **Repeat**
- Continue until all devices are done

## Key Points
- **Input**: List of devices and desired status
- **Output**: Updated device status in ThingSpace and local database
- **Main Actions**: API calls to change device status
- **Result**: Devices have new operational status