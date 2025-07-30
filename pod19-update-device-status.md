graph TD
    A[Client Request] --> B[M2MController/MobilityController]
    B --> C[BulkChangeRequest Validation]
    C --> D[Extract DeviceStatusUpdate]
    D --> E{Effective Date Check}
    E -->|Immediate: effectiveDate <= NOW| F[ProcessDeviceStatusChangeAsync]
    E -->|Scheduled: effectiveDate > NOW| G[ProcessAddDeviceStatusChangeToQueueAsync]
    F --> H[SQL: usp_DeviceBulkChange_DeviceStatus_UpdateDevices]
    G --> I[Insert to DeviceStatusChangeQueue Table]
    H --> J[Update Device Status Records]
    I --> K[Schedule Future Processing]
    J --> L{Portal Type Check}
    K --> L
    L -->|M2M Portal| M[AddM2MLogEntry]
    L -->|Mobility Portal| N[AddMobilityLogEntry]
    M --> O[Success/Error Response]
    N --> O
    O --> P[Return Result to Client]

    style A fill:#e1f5fe
    style H fill:#fff3e0
    style I fill:#fff3e0
    style O fill:#e8f5e8