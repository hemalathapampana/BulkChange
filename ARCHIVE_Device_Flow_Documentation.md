# ARCHIVE Device Flow

## Overview

### What
The ARCHIVE Device Flow is a bulk change operation that permanently deactivates devices in the system by marking them as inactive and deleted. This process ensures devices that are no longer in use are properly removed from active management while maintaining audit trails and data integrity.

### Why
- **Resource Management**: Remove unused devices from active monitoring and billing cycles
- **Data Integrity**: Maintain clean, accurate device inventories by eliminating obsolete entries
- **Compliance**: Ensure proper device lifecycle management and audit trail requirements
- **Cost Optimization**: Reduce overhead costs associated with managing inactive devices
- **Security**: Prevent unauthorized access to devices that should no longer be accessible

### How
The archival process validates device eligibility (checking for recent usage within 30 days), executes bulk database updates through stored procedures, and logs all changes across different portal types (M2M, Mobility, LNP) while maintaining comprehensive audit trails.

---

## Archive Change Type Process Flow

```
User Interface → M2MController.BulkChange() → BuildArchivalChangeDetails() → Validation (30-day usage check) → GetArchivalChanges() → DeviceChangeRequest Creation → Queue (SQS) → AltaworxDeviceBulkChange Lambda → ProcessArchivalAsync() → GetDeviceChanges() → usp_DeviceBulkChange_Archival_ArchiveDevices → Database Update (IsActive=false, IsDeleted=true) → Portal-Specific Logging (M2M/Mobility/LNP) → BulkChangeStatus.PROCESSED → Archive Complete
```

### Detailed Flow Breakdown:

**Frontend Tier:**
```
User Interface → Archive Request Submission
```

**Controller Tier:**
```
M2MController.BulkChange() → DeviceChangeType.Archival → BuildArchivalChangeDetails()
```

**Validation Tier:**
```
GetArchivalChanges() → ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS (30 days) → Device Eligibility Check → Error Generation (if ineligible)
```

**Queue Tier:**
```
DeviceChangeRequest → SQS Message → Device Bulk Change Queue
```

**Lambda Processing Tier:**
```
AltaworxDeviceBulkChange Lambda → ChangeRequestType.Archival → ProcessArchivalAsync() → GetDeviceChanges()
```

**Database Tier:**
```
usp_DeviceBulkChange_Archival_ArchiveDevices → SqlConnection → CommandType.StoredProcedure → Device Status Update
```

**Logging Tier:**
```
Portal Type Check → M2M/Mobility/LNP Logging → DeviceBulkChangeLogRepository → BulkChangeStatus Update → Audit Trail Creation
```

---

## Process Flow

```mermaid
graph TD
    A[Bulk Change Request: Archival] --> B[Validate Device Eligibility]
    B --> C{Recent Usage Check}
    C -->|Usage within 30 days| D[Mark as Ineligible]
    C -->|No recent usage| E[Process Archival]
    
    E --> F[Execute Stored Procedure]
    F --> G[usp_DeviceBulkChange_Archival_ArchiveDevices]
    G --> H{Portal Type Check}
    
    H -->|M2M Portal| I[Log M2M Change Entry]
    H -->|Mobility Portal| J[Log Mobility Change Entry]  
    H -->|LNP Portal| K[Log LNP Change Entry]
    
    I --> L[Update Device Status]
    J --> L
    K --> L
    
    L --> M[Set IsActive = false]
    M --> N[Set IsDeleted = true]
    N --> O[Update Timestamps]
    O --> P[Archive Complete]
    
    D --> Q[Generate Error Response]
    Q --> R[Log Ineligible Device]
    
    style A fill:#e1f5fe
    style P fill:#c8e6c9
    style D fill:#ffcdd2
    style G fill:#fff3e0
```

---

## Change Type Process Flow

```mermaid
graph LR
    A[Change Request Received] --> B{ChangeRequestType}
    
    B -->|"StatusUpdate"| C[Status Update Process]
    B -->|"ActivateNewService"| D[Service Activation Process]
    B -->|"Archival"| E[📦 ARCHIVAL PROCESS]
    B -->|"CustomerRatePlanChange"| F[Rate Plan Change Process]
    B -->|"CustomerAssignment"| G[Customer Assignment Process]
    B -->|"CarrierRatePlanChange"| H[Carrier Rate Plan Process]
    B -->|"CreateRevService"| I[Rev Service Creation Process]
    B -->|"ChangeICCIDAndIMEI"| J[Equipment Change Process]
    B -->|"EditUsernameCostCenter"| K[Username/Cost Center Update]
    
    E --> E1[Get Device Changes]
    E1 --> E2[Validate Eligibility]
    E2 --> E3[Execute Archival SP]
    E3 --> E4[Log Portal-Specific Entry]
    E4 --> E5[Mark as Processed]
    
    style E fill:#ffeb3b
    style E1 fill:#fff3e0
    style E2 fill:#fff3e0
    style E3 fill:#fff3e0
    style E4 fill:#fff3e0
    style E5 fill:#c8e6c9
```

---

## Key Components

### Validation Rules
- **Recent Usage Check**: Devices with usage within the last 30 days (`ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS`) are ineligible for archival
- **Active Status Check**: Only active devices can be archived
- **Already Archived Check**: Prevents re-archival of already processed devices

### Database Operations
- **Stored Procedure**: `usp_DeviceBulkChange_Archival_ArchiveDevices`
- **Device Status Update**: Sets `IsActive = false` and `IsDeleted = true`
- **Audit Trail**: Comprehensive logging across all portal types

### Portal Type Support
- **M2M Portal** (PortalTypeId: 0): Machine-to-Machine devices
- **Mobility Portal** (PortalTypeId: 2): Mobile devices  
- **LNP Portal** (PortalTypeId: 1): Local Number Portability devices

### Error Handling
- SQL exceptions with detailed logging
- Invalid operation exceptions
- General exception handling with unique log references
- Graceful degradation with appropriate error responses

---

## Implementation Details

### Core Method
```csharp
case ChangeRequestType.Archival:
    var archivalChanges = GetDeviceChanges(context, bulkChangeId, bulkChange.PortalTypeId, PageSize).ToList();
    await ProcessArchivalAsync(context, logRepo, bulkChange.Id, archivalChanges);
    return true;
```

### Key Constants
- `ARCHIVAL_RECENT_USAGE_VALIDATION_DAYS = 30`
- `PageSize = 100` (for batch processing)
- Portal Type Constants: M2M (0), LNP (1), Mobility (2)

### Logging Strategy
- **Action Text**: `usp_DeviceBulkChange_Archival_ArchiveDevices`
- **Log Description**: `Archive Devices: Update AMOP`
- **Status Tracking**: `BulkChangeStatus.PROCESSED` or `BulkChangeStatus.ERROR`
- **Audit Trail**: Complete request/response logging with timestamps