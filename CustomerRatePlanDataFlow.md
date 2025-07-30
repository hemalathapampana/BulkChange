# Customer Rate Plan Data Flow Restructuring

## Overview

This document outlines the data flow restructuring for **Customer Rate Plan Changes** as opposed to **Carrier Rate Plan Changes**. The key distinction is that customer rate plans are managed at the customer/tenant level for billing and service allocation purposes, while carrier rate plans are managed at the carrier/service provider level for network connectivity.

## Key Differences: Customer vs Carrier Rate Plans

### Customer Rate Plan
- **Purpose**: Customer-facing billing plans and data allocation
- **Scope**: Tenant-specific, customer-controlled
- **Properties**:
  - `CustomerRatePlanId`: Internal customer plan identifier
  - `CustomerDataAllocationMB`: Data allocation in megabytes
  - `CustomerPoolId`: Customer pool for shared data plans
  - `EffectiveDate`: When the plan change takes effect

### Carrier Rate Plan
- **Purpose**: Carrier-specific network connectivity plans
- **Scope**: Service provider managed
- **Properties**:
  - `CarrierRatePlan`: Carrier plan code/name
  - `CommPlan`: Communication plan
  - `PlanUuid`: Unique plan identifier
  - `RatePlanId`: Carrier's rate plan ID

## Data Models

### Current Customer Rate Plan Structure

```csharp
public class BulkChangeCustomerRatePlanUpdate
{
    public int? CustomerRatePlanId { get; set; }
    public decimal? CustomerDataAllocationMB { get; set; }
    public int? CustomerPoolId { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
```

### Bulk Change Request Structure

```csharp
public class BulkChangeRequest
{
    public int? ServiceProviderId { get; set; }
    public int? ChangeType { get; set; }
    public bool? ProcessChanges { get; set; }
    public string[] Devices { get; set; }
    
    // Customer Rate Plan (Focus of this restructuring)
    public BulkChangeCustomerRatePlanUpdate CustomerRatePlanUpdate { get; set; }
    
    // Carrier Rate Plan (Not used in this flow)
    public CarrierRatePlanUpdate CarrierRatePlanUpdate { get; set; }
}
```

## Data Flow Architecture

### 1. Request Initiation

```
Client Request → BulkChangeRequest → CustomerRatePlanUpdate
```

**Input Parameters:**
- `CustomerRatePlanId`: Target customer rate plan
- `CustomerDataAllocationMB`: Data allocation limit
- `CustomerPoolId`: Shared pool identifier
- `EffectiveDate`: Implementation date

### 2. Processing Pipeline

#### Step 1: Validation and Parsing
```
BulkChangeRequest → ProcessCustomerRatePlanChangeAsync()
```

**Process:**
1. Extract `CustomerRatePlanUpdate` from request
2. Validate customer rate plan ID
3. Parse effective date
4. Validate data allocation parameters

#### Step 2: Immediate vs Scheduled Processing
```
EffectiveDate Check → Immediate Processing | Queue for Future
```

**Decision Logic:**
```csharp
if (effectiveDate == null || effectiveDate?.ToUniversalTime() <= DateTime.UtcNow)
{
    // Immediate processing
    dbResult = await ProcessCustomerRatePlanChangeAsync(bulkChange.Id, 
        customerRatePlanId, effectiveDate, customerDataAllocationMB, 
        customerRatePoolId, connectionString, logger, syncPolicy);
}
else
{
    // Queue for future processing
    dbResult = await ProcessAddCustomerRatePlanChangeToQueueAsync(bulkChange, 
        customerRatePlanId, effectiveDate, customerDataAllocationMB, 
        customerRatePoolId, context);
}
```

#### Step 3: Database Operations

##### Immediate Processing Flow
```
ProcessCustomerRatePlanChangeAsync() → Stored Procedure Execution
```

**Database Call:**
```sql
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = @bulkChangeId,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @customerDataAllocationMB = @customerDataAllocationMB,
    @effectiveDate = @effectiveDate,
    @needToMarkProcessed = @needToMarkProcessed
```

##### Scheduled Processing Flow
```
ProcessAddCustomerRatePlanChangeToQueueAsync() → Device_CustomerRatePlanOrRatePool_Queue Table
```

**Queue Table Structure:**
- `DeviceId`: Target device identifier
- `CustomerRatePlanId`: Plan to apply
- `CustomerRatePoolId`: Pool assignment
- `CustomerDataAllocationMB`: Data limit
- `EffectiveDate`: Scheduled implementation
- `PortalType`: Portal context
- `TenantId`: Tenant scope

### 3. Device-Specific Processing

#### Bulk Device Processing
```
ProcessCustomerRatePlanChangeAsync() → All devices in bulk change
```

#### Individual Device Processing
```
ProcessCustomerRatePlanChangeBySubNumberAsync() → Single device by subscriber number
```

**Individual Device Call:**
```sql
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber
    @bulkChangeId = @bulkChangeId,
    @subscriberNumber = @subscriberNumber,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @effectiveDate = @effectiveDate,
    @customerDataAllocationMB = @customerDataAllocationMB
```

### 4. Logging and Audit Trail

#### M2M Portal Logging
```csharp
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    M2MDeviceChangeId = change.Id,
    LogEntryDescription = "Change Customer Rate Plan: Update AMOP",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
    ResponseText = dbResult.ResponseObject,
    HasErrors = dbResult.HasErrors,
    ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED
});
```

#### Mobility Portal Logging
```csharp
logRepo.AddMobilityLogEntry(new CreateMobilityDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    MobilityDeviceChangeId = change.Id,
    LogEntryDescription = "Change Customer Rate Plan: Update AMOP",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = dbResult.ActionText + Environment.NewLine + dbResult.RequestObject,
    ResponseText = dbResult.ResponseObject,
    HasErrors = dbResult.HasErrors,
    ResponseStatus = dbResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED
});
```

## Processing Flow Diagram

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
    
    J --> M[Insert into Queue Table:<br/>Device_CustomerRatePlanOrRatePool_Queue]
    
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

## Error Handling

### Validation Errors
- Invalid customer rate plan ID
- Invalid data allocation values
- Missing required parameters
- Invalid effective date

### Processing Errors
- Database connection failures
- Stored procedure execution errors
- Transaction rollback scenarios
- Concurrent modification conflicts

### Error Response Structure
```csharp
public class DeviceChangeResult<string, string>
{
    public string ActionText { get; set; }
    public bool HasErrors { get; set; }
    public string RequestObject { get; set; }
    public string ResponseObject { get; set; }
}
```

## Integration Points

### 1. Portal Integration
- **M2M Portal**: Device management interface
- **Mobility Portal**: Mobile device management
- **Tenant Management**: Customer rate plan configuration

### 2. Database Integration
- **Device Tables**: Device-specific rate plan assignments
- **Customer Tables**: Customer rate plan definitions
- **Queue Tables**: Scheduled change management
- **Log Tables**: Audit and tracking

### 3. External Service Integration
- **Rev Customer Service**: Customer management
- **Billing Systems**: Rate plan billing integration
- **Data Allocation Services**: Usage tracking and limits

## Configuration and Constants

### SQL Constants
```csharp
// Stored Procedures
DEVICE_BULK_CHANGE_CUSTOMER_RATE_PLAN_CHANGE_UPDATE_DEVICE
DEVICE_BULK_CHANGE_CUSTOMER_RATE_PLAN_CHANGE_UPDATE_DEVICE_BY_NUMBER

// Table Names
Device_CustomerRatePlanOrRatePool_Queue Table

// Parameters
EFFECTIVE_DATE
BULK_CHANGE_ID
CUSTOMER_RATE_PLAN_ID
CUSTOMER_RATE_POOL_ID
CUSTOMER_DATA_ALLOCATION_MB
NEED_TO_MARK_PROCESSED
```

### Change Request Types
```csharp
public enum ChangeRequestType
{
    CustomerRatePlanChange = 4,
    CarrierRatePlanChange = 7,
    // ... other types
}
```

## Security Considerations

### Authorization
- Tenant-level access control
- Customer rate plan visibility restrictions
- Role-based permissions for rate plan changes

### Data Protection
- Encrypted connection strings
- Sanitized logging (no sensitive data)
- Audit trail maintenance

### Validation
- Input parameter validation
- Business rule enforcement
- Rate limit checking

## Performance Optimization

### Batch Processing
- Bulk device updates
- Transaction batching
- Connection pooling

### Async Operations
- Non-blocking database operations
- Parallel processing capabilities
- Queue-based scheduling

### Monitoring
- Performance metrics collection
- Error rate tracking
- Processing time analysis

## Future Enhancements

### Planned Improvements
1. **Real-time Notifications**: Customer rate plan change notifications
2. **Advanced Scheduling**: Recurring rate plan changes
3. **Integration APIs**: External system integration endpoints
4. **Analytics Dashboard**: Rate plan change analytics and reporting

### Scalability Considerations
1. **Microservice Architecture**: Service decomposition
2. **Event-Driven Processing**: Event sourcing implementation
3. **Caching Layer**: Rate plan data caching
4. **Load Balancing**: Processing load distribution