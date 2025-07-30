# Customer Rate Plan Change - Basic Data Flow Diagram

```mermaid
graph TD
    %% External Entities
    Client[Client Application]
    
    %% Processes
    P1[1.0 Validate Request]
    P2[2.0 Process Rate Plan Change]
    P3[3.0 Update Device Records]
    P4[4.0 Log Changes]
    
    %% Data Stores
    DS1[(BulkChange)]
    DS2[(BulkChangeDetailRecord)]
    DS3[(Device)]
    DS4[(Device_CustomerRatePlanOrRatePool_Queue)]
    DS5[(M2MDeviceBulkChangeLog)]
    DS6[(CustomerRatePlan)]
    
    %% Stored Procedures
    SP1[usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices]
    SP2[usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber]
    
    %% Data Flows
    Client -->|Request| P1
    P1 -->|Valid Request| P2
    P1 -->|Store Change| DS1
    P1 -->|Store Details| DS2
    
    P2 -->|Read Plan Info| DS6
    P2 -->|Immediate Update| SP1
    P2 -->|Individual Update| SP2
    P2 -->|Queue Future Changes| DS4
    
    SP1 -->|Update Records| P3
    SP2 -->|Update Records| P3
    P3 -->|Update Device| DS3
    P3 -->|Log Activity| P4
    
    P4 -->|Write Logs| DS5
    P4 -->|Response| Client
    
    %% Styling
    classDef process fill:#e3f2fd,stroke:#1976d2,stroke-width:2px
    classDef datastore fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef external fill:#e8f5e8,stroke:#388e3c,stroke-width:2px
    classDef sp fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    
    class P1,P2,P3,P4 process
    class DS1,DS2,DS3,DS4,DS5,DS6 datastore
    class Client external
    class SP1,SP2 sp
```

## Data Stores
- **BulkChange** - Main change request
- **BulkChangeDetailRecord** - Device-specific changes
- **Device** - Device information and rate plans
- **Device_CustomerRatePlanOrRatePool_Queue** - Scheduled changes
- **CustomerRatePlan** - Rate plan definitions
- **M2MDeviceBulkChangeLog** - Audit trail

## Stored Procedures
- **usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices**
- **usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber**

## Process Summary
1. **Validate Request** - Check input and permissions
2. **Process Rate Plan Change** - Handle immediate or queue scheduled
3. **Update Device Records** - Execute database changes
4. **Log Changes** - Record audit trail and respond