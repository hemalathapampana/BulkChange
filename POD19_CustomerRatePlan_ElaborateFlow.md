# POD 19 Service Provider - Customer Rate Plan Change Flow (Elaborate)

## Mermaid Flow Diagram

```mermaid
graph TD
    A["🌐 Client Request<br/>POST /api/m2m/bulkchange"] --> B["🔌 M2MController.BulkChange()<br/>API Endpoint"]
    B --> C["✅ Validate Request<br/>- Authentication<br/>- Request Structure<br/>- Service Provider Access"]
    C --> D{"🔍 Valid Request?"}
    D -->|❌ No| E["⚠️ Return Error Response<br/>- 400 Bad Request<br/>- 401 Unauthorized<br/>- 403 Forbidden"]
    D -->|✅ Yes| F["📋 Extract Customer Rate Plan Data<br/>ChangeRequestType = 4<br/>CustomerRatePlanChange"]
    
    F --> G["🏗️ BulkChangeRequest Processing<br/>AltaworxDeviceBulkChange.ProcessBulkChangeAsync()"]
    G --> H["📊 Parse CustomerRatePlanUpdate<br/>- CustomerRatePlanId<br/>- CustomerDataAllocationMB<br/>- CustomerPoolId<br/>- EffectiveDate"]
    H --> I{"⏰ Effective Date Check<br/>effectiveDate <= DateTime.UtcNow?"}
    
    I -->|🚀 Immediate| J["⚡ Process Immediate Change<br/>ProcessCustomerRatePlanChangeAsync()"]
    I -->|📅 Scheduled| K["📋 Add to Queue<br/>ProcessAddCustomerRatePlanChangeToQueueAsync()"]
    
    J --> L["🔧 Execute Stored Procedure<br/>usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices<br/>Parameters:<br/>@bulkChangeId<br/>@customerRatePlanId<br/>@customerRatePoolId<br/>@customerDataAllocationMB<br/>@effectiveDate<br/>@needToMarkProcessed"]
    L --> M["💾 Update Device Records<br/>Tables Updated:<br/>- Device (CustomerRatePlanId)<br/>- DeviceHistory (Audit Trail)<br/>- BulkChangeDetailRecord (Status)"]
    M --> N["📝 Log Success/Error<br/>Portal-Specific Logging"]
    
    K --> O["📦 CustomerRatePlanDeviceQueue<br/>Table: Device_CustomerRatePlanOrRatePool_Queue<br/>Columns:<br/>- DeviceId<br/>- CustomerRatePlanId<br/>- CustomerRatePoolId<br/>- CustomerDataAllocationMB<br/>- EffectiveDate<br/>- PortalType<br/>- TenantId<br/>- CreatedBy/Date<br/>- ModifiedBy/Date<br/>- IsActive"]
    O --> P["⏱️ Schedule Processing<br/>Background Job Processing<br/>Queue-based Execution"]
    P --> Q["📋 Log Queue Status<br/>Queue Entry Logged"]
    
    N --> R1["📊 M2M Portal Logging<br/>Table: M2MDeviceBulkChangeLog<br/>- BulkChangeId<br/>- M2MDeviceChangeId<br/>- LogEntryDescription<br/>- ProcessBy: AltaworxDeviceBulkChange<br/>- RequestText<br/>- ResponseText<br/>- HasErrors<br/>- ResponseStatus"]
    N --> R2["📱 Mobility Portal Logging<br/>Table: MobilityDeviceBulkChangeLog<br/>- BulkChangeId<br/>- MobilityDeviceChangeId<br/>- LogEntryDescription<br/>- ProcessBy: AltaworxDeviceBulkChange<br/>- RequestText<br/>- ResponseText<br/>- HasErrors<br/>- ResponseStatus"]
    
    Q --> R3["📋 Queue Status Logging<br/>Process: Scheduled Change Added<br/>Status: QUEUED"]
    
    R1 --> S["📤 Client Response<br/>JSON Response with:<br/>- Success/Error Status<br/>- Processed Device Count<br/>- Error Details (if any)<br/>- BulkChangeId"]
    R2 --> S
    R3 --> S
    
    S --> T["✅ Complete"]

    %% POD 19 Specific Processing
    subgraph POD19 ["🔌 POD 19 Service Provider Specific"]
        direction TB
        P19A["🔍 Integration Check<br/>IntegrationType.POD19<br/>Service Provider Validation"]
        P19B["🔧 POD 19 Rate Plan Mapping<br/>Customer Rate Plan → POD 19 Format"]
        P19C["📡 POD 19 API Integration<br/>External Service Calls"]
        P19D["💾 POD 19 Audit Trail<br/>Service Provider Specific Logging"]
    end
    
    %% Individual Device Processing Alternative
    subgraph Individual ["👤 Individual Device Processing"]
        direction TB
        ID1["📱 Single Device Request<br/>ProcessCustomerRatePlanChangeBySubNumberAsync()"]
        ID2["🔧 Execute SP by Number<br/>usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber<br/>Parameters:<br/>@bulkChangeId<br/>@subscriberNumber<br/>@customerRatePlanId<br/>@customerRatePoolId<br/>@effectiveDate<br/>@customerDataAllocationMB"]
        ID3["💾 Update Single Device<br/>Device Record Updated"]
        ID1 --> ID2 --> ID3
    end
    
    %% Error Handling
    subgraph ErrorHandling ["⚠️ Error Handling"]
        direction TB
        EH1["🔍 Validation Errors<br/>- Invalid CustomerRatePlanId<br/>- Invalid DataAllocation<br/>- Missing Parameters<br/>- Invalid EffectiveDate"]
        EH2["💥 Processing Errors<br/>- Database Connection Failures<br/>- SP Execution Errors<br/>- Transaction Rollbacks<br/>- Concurrent Modifications"]
        EH3["📝 Error Logging<br/>- Error Details<br/>- Stack Trace<br/>- Reference ID<br/>- Timestamp"]
        EH1 --> EH3
        EH2 --> EH3
    end
    
    %% Database Tables
    subgraph DatabaseTables ["🗄️ Key Database Tables"]
        direction TB
        DT1["📋 BulkChange<br/>- Id (PK)<br/>- ChangeRequestTypeId = 4<br/>- ServiceProviderId<br/>- Status<br/>- PortalTypeId"]
        DT2["📝 BulkChangeDetailRecord<br/>- Id (PK)<br/>- BulkChangeId (FK)<br/>- DeviceId<br/>- ChangeRequest (JSON)<br/>- Status"]
        DT3["📱 Device<br/>- Id (PK)<br/>- CustomerRatePlanId<br/>- CustomerRatePoolId<br/>- ServiceProviderId<br/>- SubscriberNumber"]
        DT4["📊 Device_CustomerRatePlanOrRatePool_Queue<br/>- Id (PK)<br/>- DeviceId (FK)<br/>- CustomerRatePlanId<br/>- CustomerRatePoolId<br/>- EffectiveDate<br/>- PortalType<br/>- TenantId"]
        DT5["📈 CustomerRatePlan<br/>- Id (PK)<br/>- Name<br/>- DataAllocationMB<br/>- TenantId<br/>- IsActive"]
    end
    
    %% Stored Procedures Detail
    subgraph StoredProcedures ["⚙️ Stored Procedures"]
        direction TB
        SP1["🔧 usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices<br/>Purpose: Bulk update all devices in change<br/>Updates: Device.CustomerRatePlanId<br/>Creates: DeviceHistory records<br/>Marks: BulkChangeDetailRecord as processed"]
        SP2["👤 usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber<br/>Purpose: Update single device by subscriber number<br/>Updates: Individual device record<br/>Validates: Device exists and accessible"]
        SP3["🏗️ usp_DeviceBulkChange_CustomerRatePlanChange_UpdateForDevices<br/>Purpose: Alternative bulk update method<br/>Processes: Device-specific rate plan changes<br/>Handles: Complex business rules"]
    end
    
    %% Integration with POD 19
    L -.->|"POD 19 Specific"| P19A
    P19A --> P19B --> P19C --> P19D
    P19D -.-> M
    
    %% Connection to Individual Processing
    J -.->|"Alternative"| ID1
    
    %% Error connections
    C -.->|"Validation Fails"| EH1
    L -.->|"SP Execution Fails"| EH2
    
    %% Styling
    classDef processClass fill:#e1f5fe,stroke:#0277bd,stroke-width:2px
    classDef errorClass fill:#ffebee,stroke:#c62828,stroke-width:2px
    classDef databaseClass fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef pod19Class fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    
    class A,B,G,H,J,L,M,N,P processClass
    class E,EH1,EH2,EH3 errorClass
    class O,DT1,DT2,DT3,DT4,DT5,SP1,SP2,SP3 databaseClass
    class P19A,P19B,P19C,P19D pod19Class
```

## Key Components Summary

### 🔌 POD 19 Service Provider Details
- **Integration Type**: IntegrationType.POD19
- **Service Provider ID**: Specific to POD 19 configuration
- **Special Handling**: Custom rate plan mapping and API integration

### 📊 Change Request Type
- **Numeric Value**: 4 (CustomerRatePlanChange)
- **Description**: Customer-facing rate plan changes (not carrier rate plans)
- **Scope**: Tenant-specific billing and data allocation

### 🗄️ Key Database Tables
1. **BulkChange**: Main change tracking table
2. **BulkChangeDetailRecord**: Individual device change records
3. **Device**: Core device information with rate plan assignments
4. **Device_CustomerRatePlanOrRatePool_Queue**: Scheduled changes queue
5. **CustomerRatePlan**: Rate plan definitions and configurations

### ⚙️ Stored Procedures
1. **usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices**: Bulk device updates
2. **usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber**: Single device by subscriber number
3. **usp_DeviceBulkChange_CustomerRatePlanChange_UpdateForDevices**: Alternative bulk update method

### 📝 Logging Tables
1. **M2MDeviceBulkChangeLog**: M2M portal specific logging
2. **MobilityDeviceBulkChangeLog**: Mobility portal specific logging
3. **DeviceHistory**: Device change audit trail

### 🚀 Processing Types
1. **Immediate Processing**: EffectiveDate ≤ Current DateTime
2. **Scheduled Processing**: EffectiveDate > Current DateTime (queued)

### 🔍 Error Handling
- Validation errors (invalid parameters)
- Processing errors (database/API failures)
- Comprehensive error logging with reference IDs
- Transaction rollback capabilities

This elaborate flow diagram provides complete visibility into the POD 19 service provider's customer rate plan change process, including all database interactions, stored procedures, logging mechanisms, and error handling scenarios.