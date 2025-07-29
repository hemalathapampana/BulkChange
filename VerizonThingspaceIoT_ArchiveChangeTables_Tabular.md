# Verizon Thingspace IoT - Archive Change Type Tables (Tabular Format)

## Primary Bulk Change Tables

| Table Name | Purpose | Key Fields |
|------------|---------|------------|
| **DeviceBulkChange** | Main bulk change tracking and management table | `Id` (PK), `TenantId`, `Status`, `ChangeRequestTypeId`, `ChangeRequestType`, `ServiceProviderId`, `ServiceProvider`, `IntegrationId`, `Integration`, `PortalTypeId`, `CreatedBy`, `ProcessedDate` |
| **M2M_DeviceChange** | Individual device change tracking for M2M devices | `Id` (PK), `BulkChangeId` (FK), `DeviceId`, `ICCID`, `MSISDN`, `Status`, `ChangeRequest`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted` |
| **Mobility_DeviceChange** | Individual device change tracking for Mobility devices | `Id` (PK), `BulkChangeId` (FK), `ICCID`, `Status`, `ChangeRequest`, `StatusDetails`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted` |
| **LNP_DeviceChange** | Individual device change tracking for LNP devices | `Id` (PK), `BulkChangeId` (FK), `ICCID`, `Status`, `ChangeRequest`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted` |

## Core Device Tables

| Table Name | Purpose | Key Fields |
|------------|---------|------------|
| **Device** | Main device information and status table | `Id` (PK), `ICCID`, `IMEI`, `MSISDN`, `DeviceStatusId`, `ServiceProviderId`, `TenantId`, `IsActive`, `IsDeleted`, `ModifiedBy`, `ModifiedDate`, `IpAddress` |
| **TelegenceDevice** | Telegence-specific device information | `Id` (PK), `SubscriberNumber`, `ICCID`, `ServiceProviderId`, `BillingAccountNumber`, `FoundationAccountNumber`, `IsActive`, `IsDeleted` |

## Audit and Logging Tables

| Table Name | Purpose | Key Fields |
|------------|---------|------------|
| **M2M_DeviceBulkChangeLog** | Comprehensive logging for M2M device operations | `Id` (PK), `BulkChangeId`, `M2MDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |
| **Mobility_DeviceBulkChangeLog** | Comprehensive logging for Mobility device operations | `Id` (PK), `BulkChangeId`, `MobilityDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |
| **LNP_DeviceBulkChangeLog** | Comprehensive logging for LNP device operations | `Id` (PK), `BulkChangeId`, `LNPDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |

## Field Details by Category

### Primary Keys and Foreign Keys

| Table | Primary Key | Foreign Keys | Relationships |
|-------|-------------|--------------|---------------|
| DeviceBulkChange | `Id` | None | Parent to all DeviceChange tables |
| M2M_DeviceChange | `Id` | `BulkChangeId` → DeviceBulkChange.Id | Child of DeviceBulkChange |
| Mobility_DeviceChange | `Id` | `BulkChangeId` → DeviceBulkChange.Id | Child of DeviceBulkChange |
| LNP_DeviceChange | `Id` | `BulkChangeId` → DeviceBulkChange.Id | Child of DeviceBulkChange |
| Device | `Id` | None | Referenced by DeviceChange tables |
| TelegenceDevice | `Id` | None | Supporting device information |

### Status and Control Fields

| Field Name | Tables | Data Type | Purpose | Possible Values |
|------------|--------|-----------|---------|-----------------|
| `Status` | DeviceBulkChange, *_DeviceChange | VARCHAR | Operation status tracking | PROCESSING, PROCESSED, ERROR |
| `IsActive` | *_DeviceChange, Device, TelegenceDevice | BIT | Record active status | 0 (Inactive), 1 (Active) |
| `IsDeleted` | *_DeviceChange, Device, TelegenceDevice | BIT | Record deletion status | 0 (Not Deleted), 1 (Deleted) |
| `IsProcessed` | *_DeviceChange | BIT | Processing completion status | 0 (Not Processed), 1 (Processed) |
| `HasErrors` | *_DeviceBulkChangeLog | BIT | Error occurrence indicator | 0 (No Errors), 1 (Has Errors) |

### Identifier Fields

| Field Name | Tables | Data Type | Purpose | Description |
|------------|--------|-----------|---------|-------------|
| `ICCID` | Device, *_DeviceChange, TelegenceDevice | VARCHAR | SIM card identifier | Integrated Circuit Card Identifier |
| `IMEI` | Device | VARCHAR | Device identifier | International Mobile Equipment Identity |
| `MSISDN` | Device, M2M_DeviceChange | VARCHAR | Mobile phone number | Mobile Station International Subscriber Directory Number |
| `SubscriberNumber` | TelegenceDevice | VARCHAR | Subscriber phone number | Telegence-specific subscriber identifier |
| `TenantId` | DeviceBulkChange, Device | INT | Customer/tenant identifier | Multi-tenant system identifier |
| `ServiceProviderId` | DeviceBulkChange, Device, TelegenceDevice | INT | Service provider identifier | Carrier/provider reference |

### Timestamp Fields

| Field Name | Tables | Data Type | Purpose |
|------------|--------|-----------|---------|
| `ProcessedDate` | DeviceBulkChange, *_DeviceChange, *_DeviceBulkChangeLog | DATETIME | When operation was completed |
| `ModifiedDate` | Device | DATETIME | Last modification timestamp |

### Configuration Fields

| Field Name | Tables | Data Type | Purpose | Description |
|------------|--------|-----------|---------|-------------|
| `ChangeRequestTypeId` | DeviceBulkChange | INT | Type of change operation | Numeric identifier for Archive type |
| `ChangeRequestType` | DeviceBulkChange | VARCHAR | Human-readable change type | "Archival" for archive operations |
| `PortalTypeId` | DeviceBulkChange | INT | Portal type identifier | 1=M2M, 2=Mobility, 3=LNP |
| `IntegrationId` | DeviceBulkChange | INT | Integration type identifier | 1=Telegence, 2=ThingSpace, etc. |
| `DeviceStatusId` | Device | INT | Current device status | Status code for device state |

### JSON and Large Text Fields

| Field Name | Tables | Data Type | Purpose | Content Example |
|------------|--------|-----------|---------|-----------------|
| `ChangeRequest` | *_DeviceChange | NVARCHAR(MAX) | JSON change details | `{"archiveReason": "end-of-life"}` |
| `StatusDetails` | Mobility_DeviceChange | NVARCHAR(MAX) | Additional status info | Processing details and results |
| `RequestText` | *_DeviceBulkChangeLog | NVARCHAR(MAX) | Request details | API call details, parameters |
| `ResponseText` | *_DeviceBulkChangeLog | NVARCHAR(MAX) | Response details | API response, success/error info |
| `ErrorText` | *_DeviceBulkChangeLog | NVARCHAR(MAX) | Error information | Detailed error messages |

## Portal Type Mapping

| Portal Type ID | Portal Name | Device Change Table | Log Table | Description |
|----------------|-------------|-------------------|-----------|-------------|
| 1 | M2M | M2M_DeviceChange | M2M_DeviceBulkChangeLog | Machine-to-Machine devices |
| 2 | Mobility | Mobility_DeviceChange | Mobility_DeviceBulkChangeLog | Cellular/mobile devices |
| 3 | LNP | LNP_DeviceChange | LNP_DeviceBulkChangeLog | Local Number Portability devices |

## Integration Type Mapping

| Integration ID | Integration Name | Description | Supported Operations |
|----------------|------------------|-------------|---------------------|
| 1 | Telegence | Primary Verizon integration | Archive, Status Update, Rate Plan Change |
| 2 | ThingSpace | Verizon IoT platform | Archive, Activation, Monitoring |
| 3 | Jasper | Legacy Cisco integration | Archive, Status Update |
| 4 | Teal | Alternative carrier | Archive, Rate Plan Change |

## Archive Operation Table Usage Flow

| Step | Phase | Tables Involved | Operations |
|------|-------|----------------|------------|
| 1 | Initiation | DeviceBulkChange | INSERT new bulk change record |
| 2 | Device Setup | *_DeviceChange (based on portal type) | INSERT individual device change records |
| 3 | Validation | Device | SELECT to validate device eligibility |
| 4 | Processing | All tables via stored procedure | UPDATE device status, mark as archived |
| 5 | Logging | *_DeviceBulkChangeLog | INSERT operation logs and results |
| 6 | Completion | DeviceBulkChange, *_DeviceChange | UPDATE status to PROCESSED |

## Key Stored Procedure Parameters

| Stored Procedure | Parameter | Data Type | Purpose |
|------------------|-----------|-----------|---------|
| usp_DeviceBulkChange_Archival_ArchiveDevices | @bulkChangeId | BIGINT | Bulk change operation identifier |
| usp_DeviceBulkChange_Archival_ArchiveDevices | @changeIds | VARCHAR(MAX) | Comma-separated device change IDs |

---

*This tabular documentation provides a structured view of all tables involved in Verizon Thingspace IoT Archive Change Type operations.*