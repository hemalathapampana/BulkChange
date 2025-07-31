# POND IoT Service Provider - Assign Customer Data Flow

## Overview

This document outlines the complete data flow for the **Assign Customer** change type in the POND IoT Service Provider system. This change type allows associating devices with specific customers, optionally creating Rev services, and updating customer rate plans.

## Change Type Details

- **Change Type ID**: `CustomerAssignment` (ChangeRequestType.CustomerAssignment)
- **Purpose**: Associate IoT devices with customers and optionally create Rev services
- **Scope**: Device-to-customer mapping and service provisioning
- **Portal Support**: Both M2M Portal and Mobility Portal

## Data Models

### BulkChangeAssociateCustomer Request Model

```csharp
public class BulkChangeAssociateCustomer
{
    public string ICCID { get; set; }
    public string Number { get; set; }
    public string RevCustomerId { get; set; }
    public bool CreateRevService { get; set; }
    public string CustomerRatePlan { get; set; }
    public string CustomerRatePool { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int IntegrationAuthenticationId { get; set; }
    public int SiteId { get; set; }
    public string JasperDeviceID { get; set; }
    public string CarrierRatePlan { get; set; }
    public string CommPlan { get; set; }
    public bool AddCustomerRatePlan { get; set; }
}
```

### Bulk Change Request Structure

```csharp
public class BulkChangeRequest
{
    public int? ServiceProviderId { get; set; }
    public int? ChangeType { get; set; }  // CustomerAssignment
    public bool? ProcessChanges { get; set; }
    public string[] Devices { get; set; }
    public BulkChangeAssociateCustomer AssociateCustomerRequest { get; set; }
}
```

## Complete Data Flow Architecture

### 1. Request Initiation and Validation

```mermaid
graph TD
    A[Client Request] --> B[BulkChangeRequest Creation]
    B --> C[CustomerAssignment Type Detection]
    C --> D[Extract BulkChangeAssociateCustomer]
    D --> E[Validate Request Parameters]
    E --> F{RevCustomerId Present?}
    F -->|No| G[AMOP Customer Update Path]
    F -->|Yes| H[Rev Customer Association Path]
```

**Input Parameters:**
- `ICCID`: Device identifier
- `RevCustomerId`: Target customer ID
- `CreateRevService`: Whether to create Rev service
- `CustomerRatePlan`: Customer rate plan ID
- `CustomerRatePool`: Customer rate pool ID
- `EffectiveDate`: When changes take effect
- `IntegrationAuthenticationId`: Authentication context

### 2. Processing Pipeline

#### Step 1: Initial Processing Decision

```mermaid
graph LR
    A[CustomerAssignment Request] --> B{RevCustomerId Empty?}
    B -->|Yes| C[UpdateAMOPCustomer]
    B -->|No| D[ProcessAssociateCustomerAsync]
    C --> E[Complete]
    D --> F[Rev Service Processing]
```

#### Step 2: Rev Customer Association Flow

```mermaid
graph TD
    A[ProcessAssociateCustomerAsync] --> B[Setup Authentication]
    B --> C[Initialize Rev API Client]
    C --> D[Process Each Device Change]
    D --> E{CreateRevService?}
    E -->|Yes| F[CreateRevServiceAsync]
    E -->|No| G[Skip Rev Service Creation]
    F --> H[Service Creation Result]
    G --> H
    H --> I{Service Creation Success?}
    I -->|Yes| J[Process Customer Rate Plan]
    I -->|No| K[Log Error & Continue]
    J --> L[Update Device Links]
    K --> L
```

### 3. Rev Service Creation Process

#### Rev Service Creation Flow

```mermaid
graph TD
    A[CreateRevServiceAsync] --> B[Setup Rev API Authentication]
    B --> C[Create Service Request Model]
    C --> D[Call Rev API]
    D --> E{API Call Success?}
    E -->|Yes| F[Save Rev Service to DB]
    E -->|No| G[Log API Error]
    F --> H[Process Service Products]
    G --> I[Return Error Response]
    H --> J[Return Success Response]
    I --> K[End]
    J --> K
```

**Database Operations:**
- **Stored Procedure**: `usp_RevService_Create_Service`
- **Parameters**: RevCustomerId, Number, RevServiceId, RevServiceTypeId, ActivatedDate, RevProviderId

### 4. Customer Rate Plan Processing

#### Rate Plan Update Decision Flow

```mermaid
graph TD
    A[Customer Rate Plan Check] --> B{Rate Plan Specified?}
    B -->|No| C[Skip Rate Plan Update]
    B -->|Yes| D{Effective Date Check}
    D -->|Immediate| E[ProcessCustomerRatePlanChangeAsync]
    D -->|Future| F[ProcessAddCustomerRatePlanChangeToQueueAsync]
    E --> G[Execute SP: Update Devices]
    F --> H[Add to Queue Table]
    G --> I[Log Success/Error]
    H --> I
```

**Immediate Processing:**
```sql
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = @bulkChangeId,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @customerDataAllocationMB = @customerDataAllocationMB,
    @effectiveDate = @effectiveDate,
    @needToMarkProcessed = @needToMarkProcessed
```

**Scheduled Processing:**
- **Queue Table**: `Device_CustomerRatePlanOrRatePool_Queue`
- **Fields**: DeviceId, CustomerRatePlanId, CustomerRatePoolId, EffectiveDate, etc.

### 5. AMOP Customer Update Flow

#### AMOP Update Process

```mermaid
graph TD
    A[UpdateAMOPCustomer] --> B[Process Each Device Change]
    B --> C[UpdateRevCustomer Call]
    C --> D[Execute SP: ASSIGN_CUSTOMER_UPDATE_SITE]
    D --> E{Update Success?}
    E -->|Yes| F[Mark Device Processed]
    E -->|No| G[Log Error]
    F --> H[Continue Next Device]
    G --> H
    H --> I{More Devices?}
    I -->|Yes| B
    I -->|No| J[Complete]
```

**Database Operation:**
```sql
EXEC usp_AssignCustomer_UpdateSite
    @revcustomerId = @revCustomerId,
    @tenantId = @tenantId,
    @iccid = @iccid
```

### 6. Logging and Audit Trail

#### M2M Portal Logging

```csharp
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    M2MDeviceChangeId = change.Id,
    LogEntryDescription = "Associate Customer: Update Customer Rate Plan",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = ratePlanChangeResult.ActionText + Environment.NewLine + ratePlanChangeResult.RequestObject,
    ResponseText = ratePlanChangeResult.ResponseObject,
    HasErrors = ratePlanChangeResult.HasErrors,
    ResponseStatus = ratePlanChangeResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED
});
```

#### Mobility Portal Logging

```csharp
logRepo.AddMobilityLogEntry(new CreateMobilityDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    MobilityDeviceChangeId = change.Id,
    LogEntryDescription = "Associate Customer: Update Customer Rate Plan",
    ProcessBy = "AltaworxDeviceBulkChange",
    RequestText = ratePlanChangeResult.RequestObject,
    ResponseText = ratePlanChangeResult.ResponseObject,
    HasErrors = ratePlanChangeResult.HasErrors,
    ResponseStatus = ratePlanChangeResult.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED
});
```

## Complete End-to-End Data Flow Diagram

```mermaid
graph TD
    A[Client Submits Assign Customer Request] --> B[BulkChangeRequest Validation]
    B --> C[Extract BulkChangeAssociateCustomer Model]
    C --> D{RevCustomerId Present?}
    
    D -->|No| E[AMOP Customer Update Path]
    E --> E1[UpdateAMOPCustomer]
    E1 --> E2[Execute ASSIGN_CUSTOMER_UPDATE_SITE SP]
    E2 --> E3[Mark Devices Processed]
    E3 --> Z[Complete]
    
    D -->|Yes| F[Rev Customer Association Path]
    F --> F1[Setup Rev API Authentication]
    F1 --> F2[Process Each Device Change]
    
    F2 --> G{CreateRevService Flag?}
    G -->|Yes| H[Create Rev Service]
    G -->|No| I[Skip Rev Service]
    
    H --> H1[Call Rev API]
    H1 --> H2[Save to Database]
    H2 --> H3{Success?}
    H3 -->|Yes| J[Process Customer Rate Plan]
    H3 -->|No| K[Log Error & Continue]
    
    I --> J
    K --> J
    
    J --> L{Customer Rate Plan Specified?}
    L -->|No| M[Skip Rate Plan Update]
    L -->|Yes| N{Effective Date Check}
    
    N -->|Immediate| O[Execute Rate Plan Update SP]
    N -->|Future| P[Add to Queue Table]
    
    O --> Q[Update Device Customer Rate Plan]
    P --> R[Schedule Future Processing]
    
    Q --> S[Log Operation Result]
    R --> S
    M --> S
    
    S --> T{More Devices?}
    T -->|Yes| F2
    T -->|No| U[Update Device Rev Service Links]
    U --> V[Mark Bulk Change Complete]
    V --> Z
    
    style A fill:#e1f5fe
    style Z fill:#c8e6c9
    style H fill:#fff3e0
    style O fill:#fff3e0
    style P fill:#f3e5f5
```

## Error Handling

### Validation Errors
- Missing or invalid ICCID
- Invalid RevCustomerId format
- Missing required authentication parameters
- Invalid effective date format

### Processing Errors
- Rev API service creation failures
- Database connection timeouts
- Stored procedure execution errors
- Customer rate plan validation failures

### Error Response Structure
```csharp
public class DeviceChangeResult<TRequest, TResponse>
{
    public string ActionText { get; set; }
    public bool HasErrors { get; set; }
    public TRequest RequestObject { get; set; }
    public TResponse ResponseObject { get; set; }
}
```

## Integration Points

### 1. External Service Integration
- **Rev API**: Customer and service management
- **Authentication Services**: Integration authentication
- **Carrier Services**: Rate plan management

### 2. Database Integration
- **Device Tables**: Device-customer associations
- **Rev Service Tables**: Service creation and management
- **Customer Rate Plan Tables**: Rate plan assignments
- **Queue Tables**: Scheduled change management
- **Log Tables**: Audit trail and tracking

### 3. Portal Integration
- **M2M Portal**: IoT device management interface
- **Mobility Portal**: Mobile device management interface

## Security Considerations

### Authorization
- Integration-level authentication required
- Tenant-based access control
- Customer data protection compliance

### Data Protection
- Encrypted API communications
- Secure database connections
- Audit trail maintenance
- Sensitive data sanitization in logs

## Performance Optimization

### Parallel Processing
- Concurrent device processing where possible
- Async database operations
- Connection pooling

### Retry Mechanisms
- HTTP retry policies for API calls
- SQL retry policies for transient failures
- Exponential backoff strategies

### Monitoring
- Processing time metrics
- Error rate tracking
- Success/failure ratios

## Configuration Constants

### Stored Procedures
- `usp_RevService_Create_Service`
- `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices`
- `usp_AssignCustomer_UpdateSite`
- `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber`

### Change Request Types
```csharp
public enum ChangeRequestType
{
    CustomerAssignment = 1,
    CustomerRatePlanChange = 4,
    CarrierRatePlanChange = 7,
    StatusUpdate = 2,
    // ... other types
}
```

### Queue Tables
- `Device_CustomerRatePlanOrRatePool_Queue`
- `TelegenceNewServiceActivation_Staging`

This data flow ensures reliable customer assignment with proper error handling, logging, and support for both immediate and scheduled processing scenarios.