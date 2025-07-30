flowchart TD
    A[SQS Event Received] --> B[Function Handler]
    B --> C[Parse SQS Message]
    C --> D[Extract BulkChangeId & Parameters]
    D --> E{Message Type?}
    E -->|Change Carrier Rateplan| F[ProcessChangeCarrierRateplan Async]
    
    F --> G[Get Device Changes<br/>Change Carrier Rateplan]
    G --> H{Integration Type?}
    H -->|POD19| I[ProcessChangeCarrierRateplan<br/>JasperAsync]
    
    I --> J{Check Write Enabled<br/>for Service Provider}
    J -->|Write Disabled| J1[Log Error:<br/>Write Disabled]
    J -->|Write Enabled| K[Get Jasper Auth<br/>Information]
    
    K --> L[Extract Rateplan<br/>Update Parameters<br/>RatePlan, ActivationDate]
    L --> M{RatePlan<br/>Provided?}
    M -->|No| M1[Skip Rateplan Update]
    M -->|Yes| N[Call Jasper API<br/>ChangeRatePlan<br/>JasperDeviceAsync]
    
    N --> O{Update Successful?}
    O -->|No| O1[Log API Error<br/>Mark as HasErrors]
    O -->|Yes| P[🔍 POD19 SPECIFIC<br/>Call Jasper Audit Trail API<br/>to Verify Rateplan Changed]
    
    P --> Q{IsChangeRateplan<br/>POD19Success?}
    Q -->|No| Q1[❌ Error: Audit Failed<br/>Mark as HasErrors<br/>Enhanced Logging]
    Q -->|Yes| R[✅ Success: Rateplan<br/>Changed & Verified<br/>POD19 Compliant]
    
    M1 --> S[Process Additional<br/>Updates if any]
    R --> S
    Q1 --> S
    O1 --> S
    J1 --> S
    
    S --> T{Additional Updates<br/>Required?}
    T -->|No| U[Skip Additional Updates]
    T -->|Yes| V[Get Additional Service<br/>Information]
    
    V --> W[Process Additional<br/>Service Updates]
    W --> X[Build Update<br/>Request]
    X --> Y[Call Service API<br/>Update Fields]
    
    Y --> Z{Service Update<br/>Successful?}
    Z -->|Yes| AA[Log Success Response]
    Z -->|No| BB[Log Error Response]
    
    U --> CC[Add Bulk Change<br/>Log Entry]
    AA --> CC
    BB --> CC
    
    CC --> DD[Mark M2M Device<br/>Change as Processed]
    DD --> EE{All Changes<br/>Processed?}
    EE -->|No| G
    EE -->|Yes| FF[Mark Bulk Change<br/>as PROCESSED]
    FF --> GG[End]

    %% Styling for POD19 specific elements
    classDef pod19Specific fill:#ffcccc,stroke:#ff0000,stroke-width:2px
    classDef successPath fill:#ccffcc,stroke:#00ff00,stroke-width:2px
    classDef errorPath fill:#ffcccc,stroke:#ff0000,stroke-width:2px
    classDef decision fill:#ffffcc,stroke:#ffaa00,stroke-width:2px
    
    class P,Q pod19Specific
    class R,AA successPath
    class Q1,O1,J1,BB errorPath
    class E,H,J,M,O,Q,T,Z,EE decision