# POD 19 Service Provider - Device Change Types and Database Tables

## Overview

This document outlines the six change types supported by the POD 19 Service Provider and the database tables involved in each operation. POD 19 operates as a Jasper-based integration that processes device management operations through standardized bulk change workflows.

## Service Provider Context

**POD 19 Service Provider:**
- **Integration Type**: Jasper (IntegrationType.POD19)
- **Processing Method**: Asynchronous bulk change processing
- **Portal Type**: M2M Portal
- **Authentication**: Jasper-based authentication mechanisms

## Change Types

### 1. Archive
**Purpose**: Permanently archives devices that are no longer in active use, removing them from active inventory while maintaining historical records.

**Processing Method**: `ProcessArchivalAsync()`

**Database Tables Involved:**

| Table Name | Purpose | Operations |
|------------|---------|------------|
| Device | Core device information | UPDATE - Sets `IsActive = false`, `IsDeleted = true` |
| M2M_DeviceChange | Tracks bulk change requests | INSERT/UPDATE - Records archival request details |
| DeviceBulkChange | Main bulk change record | UPDATE - Updates status to PROCESSED |
| DeviceBulkChangeLog | Logging for M2M portal | INSERT - Logs success/failure for each device |
| RevService | Rev.IO service records | UPDATE - Deactivates associated services |
| Device_Tenant | Device-tenant associations | UPDATE - Removes active associations |
| DeviceHistory | Device status history | INSERT - Records archival event |

**Key Stored Procedures:**
- `usp_DeviceBulkChange_Archival_ArchiveDevices` - Archives devices and updates related records

**Business Rules:**
- Devices with usage in the last 30 days are ineligible for archival
- All active Rev.IO services must be terminated before archival
- Archival is irreversible through standard workflows

---

### 2. Assign Customer
**Purpose**: Associates devices with customers and optionally creates Rev.IO services for billing and management purposes.

**Processing Method**: `ProcessAssociateCustomerAsync()`

**Database Tables Involved:**

| Table Name | Purpose | Operations |
|------------|---------|------------|
| Device | Core device information | UPDATE - Associates device with customer site |
| M2M_DeviceChange | Tracks bulk change requests | INSERT/UPDATE - Records assignment request details |
| DeviceBulkChange | Main bulk change record | UPDATE - Updates status to PROCESSED |
| DeviceBulkChangeLog | Logging for M2M portal | INSERT - Logs success/failure for each device |
| RevCustomer | Rev.IO customer information | SELECT - Validates customer exists |
| RevService | Rev.IO service records | INSERT - Creates new service if opted-in |
| Site | Customer site information | UPDATE - Links device to customer site |
| CustomerRatePlan | Customer billing plans | INSERT/UPDATE - Associates customer rate plan |
| CustomerRatePool | Customer rate pool assignments | INSERT/UPDATE - Associates rate pool if specified |
| Device_Tenant | Device-tenant relationships | INSERT/UPDATE - Creates customer associations |

**Key Stored Procedures:**
- `usp_Assign_Customer_Update_Site` - Updates site associations
- `usp_DeviceBulkChange_Assign_Non_Rev_Customer` - Handles non-Rev customer assignments
- `usp_RevService_Create_Service` - Creates Rev.IO services when applicable

**Business Rules:**
- Customer must exist in Rev.IO system for billing integration
- Site assignment is mandatory for customer association
- Rate plan assignment is optional but recommended

---

### 3. Change Carrier Rate Plan
**Purpose**: Modifies carrier-specific rate plans and communication settings for network connectivity optimization.

**Processing Method**: `ProcessCarrierRatePlanChangeAsync()`

**Database Tables Involved:**

| Table Name | Purpose | Operations |
|------------|---------|------------|
| Device | Core device information | UPDATE - Updates carrier rate plan associations |
| M2M_DeviceChange | Tracks bulk change requests | INSERT/UPDATE - Records rate plan change details |
| DeviceBulkChange | Main bulk change record | UPDATE - Updates status to PROCESSED |
| DeviceBulkChangeLog | Logging for M2M portal | INSERT - Logs success/failure for each device |
| CarrierRatePlan | Carrier plan definitions | SELECT - Validates carrier plan exists |
| DeviceHistory | Device change history | INSERT - Records rate plan change event |
| JasperCarrierRatePlan | Jasper-specific rate plans | SELECT/UPDATE - Jasper plan mappings |
| Device_Tenant | Device-tenant associations | UPDATE - Updates plan effective dates |

**Key Stored Procedures:**
- `usp_DeviceBulkChange_CarrierRatePlanChange_UpdateDevices` - Updates carrier rate plans
- `usp_UpdateCrossProviderDeviceHistory` - Records cross-provider changes

**Business Rules:**
- New carrier rate plan must be compatible with device type
- Plan changes may require carrier API validation
- Effective date scheduling supported for future plan changes

---

### 4. Change Customer Rate Plan
**Purpose**: Modifies customer-facing billing plans and data allocation for service management and billing optimization.

**Processing Method**: `ProcessCustomerRatePlanChangeAsync()`

**Database Tables Involved:**

| Table Name | Purpose | Operations |
|------------|---------|------------|
| Device | Core device information | UPDATE - Updates customer rate plan associations |
| M2M_DeviceChange | Tracks bulk change requests | INSERT/UPDATE - Records rate plan change details |
| DeviceBulkChange | Main bulk change record | UPDATE - Updates status to PROCESSED |
| DeviceBulkChangeLog | Logging for M2M portal | INSERT - Logs success/failure for each device |
| CustomerRatePlan | Customer plan definitions | SELECT - Validates customer plan exists |
| CustomerRatePool | Customer rate pool assignments | INSERT/UPDATE - Pool associations |
| DeviceHistory | Device change history | INSERT - Records rate plan change event |
| CustomerRatePlanDeviceQueue | Scheduled changes queue | INSERT - For future-dated changes |
| Device_Tenant | Device-tenant associations | UPDATE - Updates billing relationships |

**Key Stored Procedures:**
- `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices` - Bulk customer rate plan updates
- `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber` - Individual device updates

**Business Rules:**
- Customer rate plan must be active and valid for tenant
- Data allocation changes take effect immediately unless scheduled
- Pool assignments are optional and customer-specific

---

### 5. Edit UserName/Cost Center
**Purpose**: Updates device usernames and cost center assignments for organizational management and cost allocation.

**Processing Method**: `ProcessEditUsernameAsync()`

**Database Tables Involved:**

| Table Name | Purpose | Operations |
|------------|---------|------------|
| Device | Core device information | UPDATE - Updates username and cost center fields |
| M2M_DeviceChange | Tracks bulk change requests | INSERT/UPDATE - Records username change details |
| DeviceBulkChange | Main bulk change record | UPDATE - Updates status to PROCESSED |
| DeviceBulkChangeLog | Logging for M2M portal | INSERT - Logs success/failure for each device |
| DeviceHistory | Device change history | INSERT - Records username change event |
| RevService | Rev.IO service records | UPDATE - Updates service descriptions if applicable |
| JasperDeviceAuditTrail | Jasper audit logging | SELECT - Validates POD19 username changes |

**Key Stored Procedures:**
- `usp_Update_Username_Device` - Updates device username and cost center
- `usp_UpdateCrossProviderDeviceHistory` - Records change history

**Business Rules:**
- Username must be unique within customer scope
- Cost center assignment affects billing allocation
- Changes are validated against Jasper audit trail for POD19

---

### 6. Update Device Status
**Purpose**: Changes device operational status for lifecycle management, activation, and deactivation workflows.

**Processing Method**: `ProcessStatusUpdateAsync()`

**Database Tables Involved:**

| Table Name | Purpose | Operations |
|------------|---------|------------|
| Device | Core device information | UPDATE - Updates device status and related fields |
| M2M_DeviceChange | Tracks bulk change requests | INSERT/UPDATE - Records status change details |
| DeviceBulkChange | Main bulk change record | UPDATE - Updates status to PROCESSED |
| DeviceBulkChangeLog | Logging for M2M portal | INSERT - Logs success/failure for each device |
| DeviceStatus | Status definitions | SELECT - Validates target status |
| DeviceHistory | Device change history | INSERT - Records status change event |
| RevService | Rev.IO service records | UPDATE - Updates service status if applicable |
| JasperStatusAuditTrail | Jasper status tracking | INSERT - Records Jasper API interactions |

**Key Stored Procedures:**
- `usp_DeviceBulkChange_StatusUpdate_UpdateDevices` - Bulk status updates
- `usp_UpdateCrossProviderDeviceHistory` - Records status change history

**Business Rules:**
- Status transitions must follow defined workflow rules
- Some status changes require carrier API validation
- Deactivation may trigger automatic service termination

## Processing Flow Architecture

### 1. Request Initiation
```
Client Request → BulkChangeRequest → Change Type Specific Processing
```

**Common Input Parameters:**
- `ServiceProviderId`: POD 19 provider identifier
- `ChangeType`: Specific change type enumeration
- `Devices`: Array of device identifiers (ICCID/IMEI)
- `ChangeRequest`: JSON payload with change-specific parameters

### 2. Processing Pipeline

#### Step 1: Validation and Routing
```
BulkChangeRequest → Validate POD19 Integration → Route to Specific Handler
```

**Process:**
1. Validate service provider is POD 19
2. Verify integration type is Jasper
3. Parse change-specific request payload
4. Route to appropriate processing method

#### Step 2: Change Type Processing
```
Change Type Router → Specific Processing Method → Database Operations
```

**Decision Logic:**
```csharp
switch (bulkChange.ChangeRequestType.ToLowerInvariant())
{
    case ChangeRequestType.Archival:
        await ProcessArchivalAsync(context, logRepo, bulkChange.Id, changes);
        break;
    case ChangeRequestType.CustomerAssignment:
        await ProcessAssociateCustomerAsync(context, logRepo, bulkChange, changes);
        break;
    case ChangeRequestType.CarrierRatePlanChange:
        await ProcessCarrierRatePlanChangeAsync(context, logRepo, bulkChange, changes);
        break;
    case ChangeRequestType.CustomerRatePlanChange:
        await ProcessCustomerRatePlanChangeAsync(context, logRepo, bulkChange, changes);
        break;
    case ChangeRequestType.EditUsernameCostCenter:
        await ProcessEditUsernameAsync(context, logRepo, bulkChange, changes);
        break;
    case ChangeRequestType.StatusUpdate:
        await ProcessStatusUpdateAsync(context, logRepo, bulkChange, changes);
        break;
}
```

### 3. Jasper Integration Processing

#### POD 19 Specific Handling
```
Change Processing → Jasper API Interaction → Audit Trail Validation
```

**Jasper Integration Features:**
- POD 19 specific authentication mechanisms
- Audit trail validation for username changes
- Status update confirmation through Jasper APIs
- Rate plan validation against Jasper offerings

### 4. Logging and Audit Trail

#### M2M Portal Logging
```csharp
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    M2MDeviceChangeId = change.Id,
    LogEntryDescription = $"{changeType}: Update AMOP",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = requestDetails,
    ResponseText = responseDetails,
    HasErrors = hasErrors,
    ResponseStatus = hasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED
});
```

## Processing Flow Diagram

```
┌─────────────────────┐
│ POD 19 Client       │
│ Request             │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate POD 19     │
│ Integration         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Parse Change Type   │
│ & Request Data      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Route to Specific   │
│ Change Handler      │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Archive      │  │ Assign       │  │ Change       │
│ Processing   │  │ Customer     │  │ Rate Plan    │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                 │                 │
       ▼                 ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Edit         │  │ Update       │  │ Jasper API   │
│ Username     │  │ Status       │  │ Integration  │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                 │                 │
       ▼                 ▼                 ▼
┌─────────────────────────────────────────────────┐
│ Database Operations & Audit Logging            │
└─────────────────────────────────────────────────┘
```

## Error Handling

### POD 19 Specific Errors
- Jasper authentication failures
- Invalid device status for POD 19 network
- Carrier rate plan compatibility issues
- Username validation failures
- Audit trail discrepancies

### Processing Errors
- Database connection failures
- Stored procedure execution errors
- Transaction rollback scenarios
- Concurrent modification conflicts
- Invalid change type requests

### Error Response Structure
```csharp
public class DeviceChangeResult<string, string>
{
    public string ActionText { get; set; }
    public bool HasErrors { get; set; }
    public string RequestObject { get; set; }
    public string ResponseObject { get; set; }
    public string ErrorDetails { get; set; }
}
```

## Integration Points

### 1. Jasper Network Integration
- **Authentication**: POD 19 specific credentials
- **API Endpoints**: Jasper device management APIs
- **Audit Trail**: Change validation and tracking
- **Rate Plans**: Carrier plan validation and assignment

### 2. Database Integration
- **Device Tables**: Core device information management
- **Change Tracking**: Comprehensive audit trail
- **Customer Management**: Rev.IO integration
- **Billing Integration**: Rate plan and cost center management

### 3. Portal Integration
- **M2M Portal**: Primary management interface
- **Bulk Operations**: Efficient multi-device processing
- **User Management**: Role-based access control
- **Reporting**: Change history and analytics

## Configuration and Constants

### POD 19 Constants
```csharp
// Integration Type
IntegrationType.POD19 = Jasper-based integration

// Change Types
public enum DeviceChangeType
{
    Archival = 1,
    CustomerAssignment = 2,
    CarrierRatePlanChange = 3,
    CustomerRatePlanChange = 4,
    EditUsername = 5,
    StatusUpdate = 6
}

// Stored Procedures
DEVICE_BULK_CHANGE_ARCHIVAL_ARCHIVE_DEVICES
DEVICE_BULK_CHANGE_ASSIGN_NON_REV_CUSTOMER
DEVICE_BULK_CHANGE_CUSTOMER_RATE_PLAN_CHANGE_UPDATE_DEVICES
UPDATE_USERNAME_DEVICE
```

### Processing Parameters
```csharp
// Common Parameters
BULK_CHANGE_ID
SERVICE_PROVIDER_ID = POD19
INTEGRATION_ID = Jasper
PORTAL_TYPE_ID = M2M

// Change Specific Parameters
CUSTOMER_RATE_PLAN_ID
CARRIER_RATE_PLAN_ID
TARGET_STATUS
USERNAME
COST_CENTER
EFFECTIVE_DATE
```

## Security Considerations

### Authorization
- POD 19 service provider access control
- Tenant-level device permissions
- Change type specific role requirements
- Audit trail access restrictions

### Data Protection
- Encrypted Jasper credentials
- Sanitized logging (no sensitive device data)
- Secure API communication
- Change request validation

### Validation
- Device ownership verification
- Change type authorization
- Business rule enforcement
- Rate limit checking

## Performance Optimization

### Batch Processing
- Bulk device updates per change type
- Transaction batching for efficiency
- Connection pooling for database operations
- Parallel processing where applicable

### Async Operations
- Non-blocking database operations
- Asynchronous Jasper API calls
- Queue-based change processing
- Background audit trail updates

### Monitoring
- Change processing metrics
- Error rate tracking by change type
- Jasper API response times
- Database operation performance

## Future Enhancements

### Planned Improvements
1. **Real-time Notifications**: Device change status notifications
2. **Enhanced Validation**: Pre-change validation workflows
3. **API Extensions**: REST API endpoints for external integrations
4. **Analytics Dashboard**: POD 19 specific reporting and analytics
5. **Automated Rollback**: Failed change recovery mechanisms

### Scalability Considerations
1. **Microservice Architecture**: Change type service decomposition
2. **Event-Driven Processing**: Event sourcing for change tracking
3. **Caching Layer**: Device and rate plan data caching
4. **Load Balancing**: Processing load distribution across instances