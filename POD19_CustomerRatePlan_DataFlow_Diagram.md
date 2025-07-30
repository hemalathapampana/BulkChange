# POD 19 Service Provider - Customer Rate Plan Change Type Data Flow Diagram

## Overview
This diagram illustrates the complete data flow for changing customer rate plan change type specifically for POD 19 service provider integration.

## Data Flow Diagram (Graph Format)

```mermaid
graph TD
    A[Client Request<br/>Customer Rate Plan Change] --> B[BulkChangeRequest<br/>Validation & Parsing]
    
    B --> C{Service Provider<br/>Check}
    C --> |POD19| D[POD19 Integration<br/>Processing Path]
    C --> |Other| E[Other Service Provider<br/>Processing]
    
    D --> F[Extract CustomerRatePlanUpdate<br/>from BulkChangeRequest]
    
    F --> G[Parse Request Parameters:<br/>• CustomerRatePlanId<br/>• CustomerPoolId<br/>• CustomerDataAllocationMB<br/>• EffectiveDate]
    
    G --> H{Effective Date<br/>Validation}
    
    H --> |Immediate<br/>(≤ Current Time)| I[ProcessCustomerRatePlanChangeAsync<br/>Immediate Processing]
    H --> |Scheduled<br/>(> Current Time)| J[ProcessAddCustomerRatePlanChangeToQueueAsync<br/>Queue for Future]
    
    I --> K[Execute Stored Procedure:<br/>usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices]
    
    K --> L[Bulk Device Update:<br/>• Update CustomerRatePlanId<br/>• Set CustomerPoolId<br/>• Apply DataAllocationMB<br/>• Set EffectiveDate]
    
    J --> M[Insert into Queue Table:<br/>CustomerRatePlanDeviceQueue]
    
    M --> N[Queue Record Contains:<br/>• DeviceId<br/>• CustomerRatePlanId<br/>• CustomerRatePoolId<br/>• CustomerDataAllocationMB<br/>• EffectiveDate<br/>• PortalType<br/>• TenantId]
    
    L --> O[Database Transaction<br/>Commit/Rollback]
    N --> P[Schedule Future Processing<br/>via Queue Processor]
    
    O --> Q{Processing<br/>Success?}
    P --> R[Queue Status<br/>Update]
    
    Q --> |Success| S[Log Success Entry<br/>M2M/Mobility Portal]
    Q --> |Error| T[Log Error Entry<br/>& Rollback Transaction]
    
    R --> U[Log Queue Status<br/>for Monitoring]
    
    S --> V[Update Device History<br/>Audit Trail]
    T --> W[Error Response<br/>to Client]
    U --> X[Scheduled Processing<br/>Completion]
    
    V --> Y[Success Response<br/>to Client]
    X --> Z[Future Processing<br/>via Queue Worker]
    
    Z --> AA[Execute Individual Device Update:<br/>usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber]
    
    AA --> BB[Individual Device Processing:<br/>Apply Rate Plan Changes<br/>by Subscriber Number]
    
    BB --> CC[Final Success/Error<br/>Logging & Response]
    
    %% Styling
    classDef pod19 fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef process fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef database fill:#e8f5e8,stroke:#1b5e20,stroke-width:2px
    classDef decision fill:#fff3e0,stroke:#e65100,stroke-width:2px
    classDef success fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef error fill:#ffebee,stroke:#c62828,stroke-width:2px
    
    class A,D pod19
    class B,F,G,I,J,K,L,M,AA,BB process
    class O,V,N database
    class C,H,Q decision
    class S,V,Y,CC success
    class T,W error
```

## POD 19 Specific Processing Details

### Integration Type Mapping
```
POD19 → IntegrationType.POD19 (Enum Value)
Processing Path → Uses Jasper Processing Pipeline
```

### Key Components for POD 19

#### 1. Service Provider Identification
- **ServiceProviderId**: Identifies POD 19 service provider
- **IntegrationId**: Maps to `IntegrationType.POD19`
- **Processing Method**: Uses `ProcessJasperCarrierRatePlanChange` method

#### 2. Data Models for POD 19
```
BulkChangeRequest {
    ServiceProviderId: POD19_ID
    ChangeType: CustomerRatePlanChange (4)
    CustomerRatePlanUpdate: {
        CustomerRatePlanId: int
        CustomerDataAllocationMB: decimal
        CustomerPoolId: int
        EffectiveDate: DateTime
    }
}
```

#### 3. Processing Flow for POD 19
```
POD19 Request → Jasper Processing Pipeline → Customer Rate Plan Update
```

### Database Operations

#### Immediate Processing
```sql
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = {bulkChangeId},
    @customerRatePlanId = {customerRatePlanId},
    @customerRatePoolId = {customerRatePoolId},
    @customerDataAllocationMB = {customerDataAllocationMB},
    @effectiveDate = {effectiveDate},
    @needToMarkProcessed = true
```

#### Individual Device Processing
```sql
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber
    @bulkChangeId = {bulkChangeId},
    @subscriberNumber = {subscriberNumber},
    @customerRatePlanId = {customerRatePlanId},
    @customerRatePoolId = {customerRatePoolId},
    @effectiveDate = {effectiveDate},
    @customerDataAllocationMB = {customerDataAllocationMB}
```

### Error Handling for POD 19

#### Validation Errors
- Invalid POD 19 service provider ID
- Invalid customer rate plan parameters
- Missing authentication information
- Write operations disabled for POD 19

#### Processing Errors
- Database connection failures
- Jasper authentication failures
- Transaction rollback scenarios
- Queue processing errors

### Logging and Audit Trail

#### M2M Portal Logging
```csharp
CreateM2MDeviceBulkChangeLog {
    BulkChangeId: {bulkChangeId},
    M2MDeviceChangeId: {changeId},
    LogEntryDescription: "Change Customer Rate Plan: Update AMOP",
    ProcessBy: "AltaworxDeviceBulkChange",
    RequestText: "Stored Procedure + Parameters",
    ResponseText: "Database Response",
    HasErrors: boolean,
    ResponseStatus: PROCESSED | ERROR
}
```

## Performance Considerations for POD 19

### Batch Processing
- Multiple devices processed in single transaction
- Connection pooling for database operations
- Async operations for non-blocking processing

### Queue Management
- Future-dated changes queued for scheduled processing
- Queue worker processes scheduled changes
- Monitoring and retry mechanisms for failed operations

### Security for POD 19
- Tenant-level access control
- POD 19 specific authentication validation
- Encrypted connection strings
- Audit trail maintenance for compliance