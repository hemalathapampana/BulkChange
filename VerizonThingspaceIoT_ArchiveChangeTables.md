# Verizon Thingspace IoT - Archive Change Type Tables

## Overview
This document provides a comprehensive list of all database tables involved in Archive Change Type bulk operations within the Verizon Thingspace IoT platform.

## Table Categories

### 1. Primary Bulk Change Tables

#### DeviceBulkChange
**Purpose**: Main bulk change tracking and management table
- **Description**: Stores bulk change requests with their status, metadata, and processing information
- **Key Fields**:
  - `Id` (Primary Key) - Unique bulk change identifier
  - `TenantId` - Tenant/customer identifier
  - `Status` - Current status of the bulk change operation
  - `ChangeRequestTypeId` - Type of change request (Archive = specific ID)
  - `ChangeRequestType` - Human-readable change type description
  - `ServiceProviderId` - Service provider identifier
  - `ServiceProvider` - Service provider name
  - `IntegrationId` - Integration type identifier
  - `Integration` - Integration type name
  - `PortalTypeId` - Portal type (M2M, Mobility, LNP)
  - `CreatedBy` - User who initiated the bulk change
  - `ProcessedDate` - When the operation was completed

#### M2M_DeviceChange
**Purpose**: Individual device change tracking for M2M (Machine-to-Machine) devices
- **Description**: Stores individual device changes within a bulk archive operation for M2M devices
- **Key Fields**:
  - `Id` (Primary Key) - Unique device change identifier
  - `BulkChangeId` (Foreign Key) - References DeviceBulkChange.Id
  - `DeviceId` - Reference to the device being archived
  - `ICCID` - SIM card identifier
  - `MSISDN` - Mobile phone number
  - `Status` - Current status of the device change
  - `ChangeRequest` - JSON containing change details
  - `IsProcessed` - Boolean indicating if change is complete
  - `ProcessedDate` - When the device change was processed
  - `IsActive` - Boolean indicating if record is active
  - `IsDeleted` - Boolean indicating if record is deleted

#### Mobility_DeviceChange
**Purpose**: Individual device change tracking for Mobility devices
- **Description**: Stores individual device changes within a bulk archive operation for mobility/cellular devices
- **Key Fields**:
  - `Id` (Primary Key) - Unique device change identifier
  - `BulkChangeId` (Foreign Key) - References DeviceBulkChange.Id
  - `ICCID` - SIM card identifier
  - `Status` - Current status of the device change
  - `ChangeRequest` - JSON containing change details
  - `StatusDetails` - Additional status information
  - `IsProcessed` - Boolean indicating if change is complete
  - `ProcessedDate` - When the device change was processed
  - `IsActive` - Boolean indicating if record is active
  - `IsDeleted` - Boolean indicating if record is deleted

#### LNP_DeviceChange
**Purpose**: Individual device change tracking for LNP (Local Number Portability) devices
- **Description**: Stores individual device changes within a bulk archive operation for LNP devices
- **Key Fields**: Similar structure to other DeviceChange tables but specific to LNP operations

### 2. Core Device Tables

#### Device
**Purpose**: Main device information and status table
- **Description**: Central repository for all device information, updated during archive operations
- **Key Fields**:
  - `Id` (Primary Key) - Unique device identifier
  - `ICCID` - SIM card identifier
  - `IMEI` - Device identifier
  - `MSISDN` - Mobile phone number
  - `DeviceStatusId` - Current device status
  - `ServiceProviderId` - Service provider identifier
  - `TenantId` - Tenant/customer identifier
  - `IsActive` - Boolean indicating if device is active (set to 0 during archive)
  - `IsDeleted` - Boolean indicating if device is deleted (set to 1 during archive)
  - `ModifiedBy` - User/system that last modified the record
  - `ModifiedDate` - Last modification timestamp
  - `IpAddress` - Device IP address (if applicable)

### 3. Audit and Logging Tables

#### M2M_DeviceBulkChangeLog
**Purpose**: Comprehensive logging for M2M device bulk change operations
- **Description**: Tracks all operations, results, and errors for M2M device archive operations
- **Key Fields**:
  - `Id` (Primary Key) - Unique log entry identifier
  - `BulkChangeId` - Reference to bulk change operation
  - `M2MDeviceChangeId` - Reference to specific device change
  - `LogEntryDescription` - Description of the operation
  - `RequestText` - Details of the request made
  - `ResponseText` - Response received from the operation
  - `ResponseStatus` - Status of the operation (PROCESSED, ERROR, etc.)
  - `HasErrors` - Boolean indicating if errors occurred
  - `ErrorText` - Error details if applicable
  - `ProcessBy` - System/user that processed the operation
  - `ProcessedDate` - When the operation was logged

#### Mobility_DeviceBulkChangeLog
**Purpose**: Comprehensive logging for Mobility device bulk change operations
- **Description**: Tracks all operations, results, and errors for Mobility device archive operations
- **Key Fields**:
  - `Id` (Primary Key) - Unique log entry identifier
  - `BulkChangeId` - Reference to bulk change operation
  - `MobilityDeviceChangeId` - Reference to specific device change
  - `LogEntryDescription` - Description of the operation
  - `RequestText` - Details of the request made
  - `ResponseText` - Response received from the operation
  - `ResponseStatus` - Status of the operation (PROCESSED, ERROR, etc.)
  - `HasErrors` - Boolean indicating if errors occurred
  - `ErrorText` - Error details if applicable
  - `ProcessBy` - System/user that processed the operation
  - `ProcessedDate` - When the operation was logged

#### LNP_DeviceBulkChangeLog
**Purpose**: Comprehensive logging for LNP device bulk change operations
- **Description**: Tracks all operations, results, and errors for LNP device archive operations
- **Key Fields**: Similar structure to other logging tables but specific to LNP operations

### 4. Supporting Tables

#### TelegenceDevice
**Purpose**: Telegence-specific device information
- **Description**: Contains additional device information for Telegence integration
- **Key Fields**:
  - `Id` (Primary Key) - Unique device identifier
  - `SubscriberNumber` - Subscriber phone number
  - `ICCID` - SIM card identifier
  - `ServiceProviderId` - Service provider identifier
  - `BillingAccountNumber` - Billing account reference
  - `FoundationAccountNumber` - Foundation account reference
  - `IsActive` - Boolean indicating if device is active
  - `IsDeleted` - Boolean indicating if device is deleted

## Key Stored Procedures

### usp_DeviceBulkChange_Archival_ArchiveDevices
**Purpose**: Main stored procedure for executing archive operations
- **Parameters**:
  - `@bulkChangeId` - The bulk change operation identifier
  - `@changeIds` - Comma-separated list of device change IDs to process
- **Functionality**:
  - Updates device status to archived/inactive
  - Marks devices as deleted in the system
  - Updates bulk change status to processed
  - Handles error conditions and rollback scenarios

## Archive Operation Flow

### 1. Initiation Phase
- Record created in `DeviceBulkChange` table with Archive change type
- Individual device records created in appropriate `*_DeviceChange` table based on portal type

### 2. Processing Phase
- `usp_DeviceBulkChange_Archival_ArchiveDevices` stored procedure executed
- Device status updated in `Device` table:
  - `IsActive` set to `0`
  - `IsDeleted` set to `1`
  - `DeviceStatusId` updated to archived status

### 3. Completion Phase
- Bulk change status updated to `PROCESSED`
- Individual device change records marked as processed
- Comprehensive logging entries created in appropriate log tables

### 4. Validation Checks
- Recent usage validation (devices with usage in last X days cannot be archived)
- Device eligibility verification
- Duplicate archive prevention

## Portal Type Differentiation

| Portal Type | Device Change Table | Log Table | Description |
|-------------|-------------------|-----------|-------------|
| M2M | M2M_DeviceChange | M2M_DeviceBulkChangeLog | Machine-to-Machine devices |
| Mobility | Mobility_DeviceChange | Mobility_DeviceBulkChangeLog | Cellular/mobile devices |
| LNP | LNP_DeviceChange | LNP_DeviceBulkChangeLog | Local Number Portability devices |

## Status Values

### Bulk Change Status
- `PROCESSING` - Operation in progress
- `PROCESSED` - Operation completed successfully
- `ERROR` - Operation failed with errors

### Device Change Status
- `PENDING` - Awaiting processing
- `PROCESSING` - Currently being processed
- `PROCESSED` - Successfully completed
- `ERROR` - Failed with errors
- `API_FAILED` - API call failed

## Integration Types

The system supports multiple integration types for different carriers:
- **Telegence** - Primary integration for Verizon services
- **ThingSpace** - Verizon's IoT platform integration
- **Jasper** - Legacy Cisco Jasper integration
- **Teal** - Alternative carrier integration

## Best Practices

### Archive Operation Guidelines
1. **Validation**: Always validate device eligibility before archiving
2. **Batch Size**: Process archives in manageable batches to avoid timeouts
3. **Error Handling**: Implement comprehensive error handling and logging
4. **Rollback**: Ensure rollback capabilities for failed operations
5. **Audit Trail**: Maintain complete audit trails for compliance

### Performance Considerations
- Use appropriate indexing on ICCID and BulkChangeId fields
- Implement proper connection pooling for database operations
- Consider asynchronous processing for large batch operations
- Monitor log table growth and implement archiving strategies

## Troubleshooting

### Common Issues
1. **Device Already Archived**: Check `IsActive` and `IsDeleted` status in Device table
2. **Recent Usage Conflicts**: Verify usage validation rules and timeframes
3. **Permission Issues**: Ensure proper tenant and service provider access
4. **API Timeouts**: Review batch sizes and implement retry mechanisms

### Log Analysis
- Check appropriate `*_DeviceBulkChangeLog` table for detailed operation history
- Review `ErrorText` field for specific error conditions
- Analyze `ResponseStatus` patterns for systemic issues

---

*This documentation is based on the Verizon Thingspace IoT platform codebase analysis and reflects the current table structure for Archive Change Type operations.*