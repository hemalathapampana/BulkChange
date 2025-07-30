# AT&T POD19 Archive Change Type - Data Flow Diagram

## Process Flow Overview

```
[Bulk Change Request Archive] 
           ↓
[Validate Device Eligibility]
           ↓
    [Recent Usage Check]
           ↓
    ◇ Decision Point ◇
    /                 \
   /                   \
No Recent Usage    Usage within 30 days
   ↓                   ↓
[Process Archival]  [Mark as Ineligible]
   ↓                   ↓
[Execute Stored    [Generate Error Response]
 Procedure]             ↓
   ↓              [Log Ineligible Device]
[usp_DeviceBulkChange_
 Archival_ArchiveDevices]
   ↓
◇ Portal Type Check ◇
/                    \
M2M Portal      Mobility Portal
   ↓                   ↓
[Log M2M         [Log Mobility
 Change Entry]    Change Entry]
   ↓                   ↓
   \                   /
    \                 /
     ↓               ↓
    [Update Device Status]
           ↓
    [Set IsActive = false]
           ↓
    [Set IsDeleted = true]
           ↓
    [Update Timestamps]
           ↓
    [Archive Complete]
```

## Process Components

### 1. Input Data
- **Bulk Change Request Archive**: Initial request containing device information for archival

### 2. Validation Steps
- **Validate Device Eligibility**: Checks if devices meet archival criteria
- **Recent Usage Check**: Determines if device has been used within the last 30 days

### 3. Decision Points
- **Recent Usage Check**: 
  - Path A: No Recent Usage → Proceed with archival
  - Path B: Usage within 30 days → Mark as ineligible

### 4. Archival Process (Path A - No Recent Usage)
- **Process Archival**: Initiates the archival workflow
- **Execute Stored Procedure**: Runs `usp_DeviceBulkChange_Archival_ArchiveDevices`
- **Portal Type Check**: Determines logging destination based on portal type
  - M2M Portal → Log M2M Change Entry
  - Mobility Portal → Log Mobility Change Entry

### 5. Error Handling (Path B - Recent Usage)
- **Mark as Ineligible**: Flags device as not eligible for archival
- **Generate Error Response**: Creates error message for ineligible device
- **Log Ineligible Device**: Records the ineligible device for audit purposes

### 6. Status Updates (Successful Archival)
- **Update Device Status**: Modifies device record in database
- **Set IsActive = false**: Marks device as inactive
- **Set IsDeleted = true**: Flags device as deleted
- **Update Timestamps**: Records archival timestamp
- **Archive Complete**: Finalizes the archival process

## Data Stores
- Device Database (implied)
- M2M Portal Logs
- Mobility Portal Logs
- Error Logs
- Audit Trails

## External Entities
- **M2M Portal**: Machine-to-Machine portal system
- **Mobility Portal**: Mobile device management portal
- **Database Systems**: Backend storage for device records and logs

## Process Rules
1. Devices with usage within 30 days cannot be archived
2. All archival activities must be logged based on portal type
3. Archived devices must have IsActive set to false and IsDeleted set to true
4. Timestamps must be updated for audit trail purposes
5. Error responses must be generated for ineligible devices