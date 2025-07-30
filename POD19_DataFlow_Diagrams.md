# POD19 Service Provider - Data Flow Diagrams

## Overview

This document provides comprehensive data flow diagrams for the 6 primary change types supported by POD19 Service Provider within the AltaWorx M2M device management system.

## POD19 Service Provider Context

POD19 is handled as a Jasper-compatible integration type in the system:
- Integration Type: `IntegrationType.POD19`
- Processing: Uses Jasper-based processing flows
- Authentication: Requires integration authentication ID
- Scope: M2M and Mobility portals

---

## 1. Archive Data Flow

### Overview
The Archive process moves devices to an inactive/archived state, making them ineligible for active operations while preserving historical data.

### Data Flow Diagram

```mermaid
graph TD
    A[Client Request - Archive Devices] --> B[BulkChangeRequest Validation]
    B --> C[Service Provider Check - POD19]
    C --> D[Device Eligibility Validation]
    D --> E{Recent Usage Check}
    E -->|Has Recent Usage| F[Reject - Ineligible for Archive]
    E -->|No Recent Usage| G[Archive Processing]
    G --> H[Execute Archive Stored Procedure]
    H --> I[usp_DeviceBulkChange_Archival_ArchiveDevices]
    I --> J[Update Device Status]
    J --> K[Mark IsActive = False, IsDeleted = True]
    K --> L[Generate Audit Log]
    L --> M[Update Bulk Change Status]
    M --> N[Send Response]
    
    F --> O[Error Response]
    
    style C fill:#e1f5fe
    style I fill:#f3e5f5
    style K fill:#e8f5e8
```

### Key Components

**Input Parameters:**
- `Devices[]`: Array of ICCID identifiers
- `ServiceProviderId`: POD19 service provider ID
- `ChangeType`: Archive (specific type ID)

**Validation Rules:**
- Device must exist in system
- Device must not have recent usage (configurable days)
- Device must not already be archived
- User must have archive permissions

**Database Operations:**
```sql
EXEC usp_DeviceBulkChange_Archival_ArchiveDevices 
    @BulkChangeId = @bulkChangeId,
    @NeedToMarkProcessed = @needToMarkProcessed
```

**Error Scenarios:**
- Device has recent usage: "Device has had usage in the last X days and is ineligible to be archived"
- Device already archived: "M2MDeviceIsArchivedError"
- Device not found: "M2MDeviceNotExistError"

---

## 2. Assign Customer Data Flow

### Overview
The Assign Customer process associates devices with specific customer accounts for billing and management purposes.

### Data Flow Diagram

```mermaid
graph TD
    A[Client Request - Assign Customer] --> B[BulkChangeRequest Validation]
    B --> C[Service Provider Check - POD19]
    C --> D[Customer Assignment Validation]
    D --> E[Device Status Check]
    E --> F{Device Available?}
    F -->|Device Archived| G[Reject - Device Archived]
    F -->|Device Available| H[Customer Assignment Processing]
    H --> I[Build Assignment Data Table]
    I --> J[Execute Assignment Stored Procedure]
    J --> K[usp_DeviceBulkChange_Assign_Non_Rev_Customer]
    K --> L[Update Device-Customer Relationship]
    L --> M[Update Site Assignment]
    M --> N[Generate Assignment Audit Log]
    N --> O[Update Bulk Change Status]
    O --> P[Send Response]
    
    G --> Q[Error Response]
    
    style C fill:#e1f5fe
    style K fill:#f3e5f5
    style L fill:#e8f5e8
```

### Key Components

**Input Parameters:**
- `Devices[]`: Array of ICCID identifiers  
- `CustomerAssignment`: Customer assignment details
- `SiteId`: Target site for assignment
- `NonRevCustomerModel`: Customer model data

**Processing Steps:**
1. **Validation Phase:**
   - Check device existence
   - Verify device not archived
   - Validate customer information
   - Check assignment permissions

2. **Assignment Phase:**
   - Build data table with assignment details
   - Execute stored procedure
   - Update device-customer relationships
   - Update site assignments

**Database Operations:**
```sql
EXEC usp_DeviceBulkChange_Assign_Non_Rev_Customer
    @DataTable = @assignmentDataTable,
    @BulkChangeId = @bulkChangeId,
    @DeviceChangeId = @deviceChangeId
```

**Error Scenarios:**
- Device archived: "M2MDeviceIsArchivedError"
- Invalid customer: Customer validation errors
- Permission denied: User lacks assignment permissions

---

## 3. Change Carrier Rate Plan Data Flow

### Overview
The Change Carrier Rate Plan process updates the service provider's network connectivity plan for devices.

### Data Flow Diagram

```mermaid
graph TD
    A[Client Request - Change Carrier Rate Plan] --> B[BulkChangeRequest Validation]
    B --> C[Service Provider Check - POD19]
    C --> D[Integration Type Routing]
    D --> E[Jasper Rate Plan Processing]
    E --> F[Carrier Rate Plan Validation]
    F --> G[Device Compatibility Check]
    G --> H{Compatible Plan?}
    H -->|Incompatible| I[Reject - Plan Incompatible]
    H -->|Compatible| J[Jasper API Rate Plan Change]
    J --> K[External Jasper Service Call]
    K --> L[Process Jasper Response]
    L --> M{API Success?}
    M -->|Failed| N[Log Error Response]
    M -->|Success| O[Update Local Device Record]
    O --> P[Update Carrier Rate Plan Fields]
    P --> Q[Generate Change Audit Log]
    Q --> R[Update Bulk Change Status]
    R --> S[Send Response]
    
    I --> T[Error Response]
    N --> T
    
    style C fill:#e1f5fe
    style E fill:#fff3e0
    style K fill:#f3e5f5
    style P fill:#e8f5e8
```

### Key Components

**Input Parameters:**
- `Devices[]`: Array of ICCID identifiers
- `CarrierRatePlanUpdate`: Rate plan change details
  - `CarrierRatePlan`: New carrier plan code
  - `CommPlan`: Communication plan
  - `PlanUuid`: Plan unique identifier
  - `RatePlanId`: Carrier rate plan ID

**Integration Flow:**
1. **POD19 Routing:** Routes to Jasper-compatible processing
2. **Jasper API Integration:** External service call for rate plan change
3. **Response Processing:** Handle success/failure from carrier
4. **Local Update:** Update device records with new plan

**Database Operations:**
- Update device carrier rate plan fields
- Log API request/response
- Update bulk change status

**Error Scenarios:**
- Invalid rate plan: Plan not available for device type
- API failure: Carrier service unavailable
- Device incompatible: Device doesn't support target plan

---

## 4. Change Customer Rate Plan Data Flow

### Overview
The Change Customer Rate Plan process updates customer-facing billing plans and data allocations.

### Data Flow Diagram

```mermaid
graph TD
    A[Client Request - Change Customer Rate Plan] --> B[BulkChangeRequest Validation]
    B --> C[Service Provider Check - POD19]
    C --> D[Extract Customer Rate Plan Update]
    D --> E[Effective Date Check]
    E --> F{Immediate or Scheduled?}
    F -->|Immediate| G[Immediate Processing]
    F -->|Scheduled| H[Queue for Future Processing]
    
    G --> I[Execute Customer Rate Plan SP]
    I --> J[usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices]
    J --> K[Update Customer Plan Assignment]
    K --> L[Update Data Allocation]
    L --> M[Update Pool Assignment]
    M --> N[Generate Change Audit Log]
    
    H --> O[Add to CustomerRatePlanDeviceQueue]
    O --> P[Schedule Future Execution]
    P --> Q[Log Queue Status]
    
    N --> R[Update Bulk Change Status]
    Q --> R
    R --> S[Send Response]
    
    style C fill:#e1f5fe
    style J fill:#f3e5f5
    style O fill:#fff3e0
    style L fill:#e8f5e8
```

### Key Components

**Input Parameters:**
- `Devices[]`: Array of ICCID identifiers
- `CustomerRatePlanUpdate`: Rate plan details
  - `CustomerRatePlanId`: Target customer plan
  - `CustomerDataAllocationMB`: Data allocation limit
  - `CustomerPoolId`: Shared pool identifier
  - `EffectiveDate`: Implementation date

**Processing Logic:**
```csharp
if (effectiveDate == null || effectiveDate?.ToUniversalTime() <= DateTime.UtcNow)
{
    // Immediate processing
    await ProcessCustomerRatePlanChangeAsync(bulkChange.Id, 
        customerRatePlanId, effectiveDate, customerDataAllocationMB, 
        customerRatePoolId, connectionString, logger, syncPolicy);
}
else
{
    // Queue for future processing
    await ProcessAddCustomerRatePlanChangeToQueueAsync(bulkChange, 
        customerRatePlanId, effectiveDate, customerDataAllocationMB, 
        customerRatePoolId, context);
}
```

**Database Operations:**
```sql
-- Immediate Processing
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = @bulkChangeId,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @customerDataAllocationMB = @customerDataAllocationMB,
    @effectiveDate = @effectiveDate,
    @needToMarkProcessed = @needToMarkProcessed

-- Scheduled Processing - Insert into Queue Table
CustomerRatePlanDeviceQueue
```

---

## 5. Edit Username/Cost Center Data Flow

### Overview
The Edit Username/Cost Center process updates device username and cost center assignments for POD19 devices.

### Data Flow Diagram

```mermaid
graph TD
    A[Client Request - Edit Username] --> B[BulkChangeRequest Validation]
    B --> C[Service Provider Check - POD19]
    C --> D[Integration Type Routing]
    D --> E[Jasper Username Processing]
    E --> F[Username/Cost Center Validation]
    F --> G[Device Status Check]
    G --> H{Device Active?}
    H -->|Inactive| I[Reject - Device Inactive]
    H -->|Active| J[Jasper API Username Update]
    J --> K[External Jasper Service Call]
    K --> L[POD19 Success Verification]
    L --> M[IsEditUsernamePOD19Success Check]
    M --> N{Edit Success?}
    N -->|Failed| O[Log Error Response]
    N -->|Success| P[Update Local Username Record]
    P --> Q[Update AMOP Database]
    Q --> R[Generate Change Audit Log]
    R --> S[Update Bulk Change Status]
    S --> T[Send Response]
    
    I --> U[Error Response]
    O --> U
    
    style C fill:#e1f5fe
    style E fill:#fff3e0
    style K fill:#f3e5f5
    style M fill:#e1f5fe
    style Q fill:#e8f5e8
```

### Key Components

**Input Parameters:**
- `Devices[]`: Array of ICCID identifiers
- `Username`: Username/cost center update details
  - `NewUsername`: Target username
  - `CostCenter`: Cost center assignment
  - `EffectiveDate`: Implementation date

**POD19 Specific Processing:**
```csharp
case (int)IntegrationType.POD19:
    result = await ProcessEditUsernameJasperAsync(context, logRepo, 
        bulkChange, changes, usernameUpdateRequest, processedBy, httpClientFactory);
    
    // POD19 specific success verification
    if (bulkChange.IntegrationId == (int)IntegrationType.POD19)
    {
        var isEditSuccess = await jasperDeviceService.IsEditUsernamePOD19Success(
            JasperDeviceAuditTrailPath, change.ICCID, 
            Common.CommonString.ERROR_MESSAGE, 
            Common.CommonString.USERNAME_STRING);
    }
```

**Validation Steps:**
1. **Device Validation:** Verify device exists and is active
2. **Username Validation:** Check format and uniqueness requirements
3. **Permission Check:** Verify user can edit usernames
4. **Integration Check:** Confirm POD19 connectivity

**Database Operations:**
- Update device username fields
- Log API request/response
- Update AMOP database records
- Generate audit trail

---

## 6. Update Device Status Data Flow

### Overview
The Update Device Status process changes the operational status of POD19 devices (activate, suspend, restore, etc.).

### Data Flow Diagram

```mermaid
graph TD
    A[Client Request - Update Device Status] --> B[BulkChangeRequest Validation]
    B --> C[Service Provider Check - POD19]
    C --> D[Integration Type Routing]
    D --> E[Jasper Status Update Processing]
    E --> F[Status Transition Validation]
    F --> G[Current Status Check]
    G --> H{Valid Transition?}
    H -->|Invalid| I[Reject - Invalid Status Transition]
    H -->|Valid| J[Jasper API Status Update]
    J --> K[External Jasper Service Call]
    K --> L[Process Jasper Response]
    L --> M{API Success?}
    M -->|Failed| N[Log Error Response]
    M -->|Success| O[Update Local Device Status]
    O --> P[Update Device Status Fields]
    P --> Q[Update Post-Status Processing]
    Q --> R[Handle Status-Specific Actions]
    R --> S[Generate Status Change Audit Log]
    S --> T[Update Bulk Change Status]
    T --> U[Send Response]
    
    I --> V[Error Response]
    N --> V
    
    style C fill:#e1f5fe
    style E fill:#fff3e0
    style K fill:#f3e5f5
    style P fill:#e8f5e8
    style R fill:#ffebee
```

### Key Components

**Input Parameters:**
- `Devices[]`: Array of ICCID identifiers
- `StatusUpdate`: Status change details
  - `UpdateStatus`: Target status (activate, suspend, restore, etc.)
  - `IsIgnoreCurrentStatus`: Override current status checks
  - `PostUpdateStatusId`: Status after update completion
  - `AccountNumber`: Associated account

**Status Transition Matrix:**
- **Activate:** Test → Active, Suspended → Active
- **Suspend:** Active → Suspended
- **Restore:** Suspended → Active
- **Deactivate:** Active/Suspended → Deactivated

**Integration Processing:**
```csharp
case IntegrationType.POD19:
    return BuildStatusUpdateChangeDetailsJasper(awxDb, session, 
        serviceProviderId, iccids, statusUpdate, revService, integrationType);
```

**Database Operations:**
- Update device status fields
- Log status transition
- Update billing status if needed
- Generate comprehensive audit trail

**Error Scenarios:**
- Invalid status transition: Current status doesn't allow target status
- API failure: Jasper service unavailable
- Device not found: ICCID doesn't exist
- Permission denied: User lacks status update permissions

---

## Common Infrastructure Components

### Authentication & Authorization
```mermaid
graph LR
    A[Request] --> B[Authentication Check]
    B --> C[POD19 Integration Auth]
    C --> D[Permission Validation]
    D --> E[Tenant Scope Check]
    E --> F[Proceed with Operation]
```

### Error Handling Framework
```mermaid
graph TD
    A[Operation Error] --> B[Error Classification]
    B --> C{Error Type}
    C -->|Validation| D[Client Error Response]
    C -->|Integration| E[Service Error Response]
    C -->|System| F[Server Error Response]
    D --> G[Log Error]
    E --> G
    F --> G
    G --> H[Update Bulk Change Status]
    H --> I[Send Error Response]
```

### Logging & Audit Trail
```mermaid
graph TD
    A[Operation Start] --> B[Log Request]
    B --> C[Process Operation]
    C --> D[Log API Calls]
    D --> E[Log Database Operations]
    E --> F[Log Response]
    F --> G[Update Audit Trail]
    G --> H[Store in Log Repository]
```

---

## Performance & Scalability Considerations

### Batch Processing
- **Bulk Operations:** All change types support batch processing
- **Transaction Management:** Ensures data consistency
- **Connection Pooling:** Optimizes database performance

### Async Processing
- **Non-blocking Operations:** Uses async/await patterns
- **Queue Management:** Scheduled changes use queue tables
- **Parallel Processing:** Supports concurrent device updates

### Error Recovery
- **Retry Logic:** Implements exponential backoff
- **Partial Success:** Handles mixed success/failure scenarios
- **Rollback Capability:** Supports transaction rollback

---

## Security Considerations

### Data Protection
- **Encrypted Connections:** All API calls use HTTPS
- **Sanitized Logging:** No sensitive data in logs
- **Access Control:** Role-based permissions

### Validation
- **Input Sanitization:** Prevents injection attacks
- **Business Rule Enforcement:** Validates business logic
- **Rate Limiting:** Prevents abuse

### Compliance
- **Audit Requirements:** Complete audit trail
- **Data Retention:** Configurable retention policies
- **Privacy Protection:** PII handling compliance