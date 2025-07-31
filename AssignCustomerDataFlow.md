# Assign Customer Data Flow - POND IoT Service Provider

## Overview

This document outlines the complete data flow for **Assign Customer** (Change Type 1) operations in the POND IoT Service Provider system. The assign customer process handles the association of devices with specific customers, including Rev service creation, customer rate plan assignment, and device configuration updates.

## Key Components

### Change Type Classification
- **Change Type**: 1 (Assign Customer)
- **Change Request Type**: `CustomerAssignment`
- **Primary Model**: `BulkChangeAssociateCustomer`
- **Processing Method**: `ProcessAssociateCustomerAsync`

### Data Models

#### BulkChangeAssociateCustomer Structure
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
    public bool AddCustomerRatePlan { get; set; }
    public int IntegrationAuthenticationId { get; set; }
}
```

## Data Flow Architecture

### 1. Request Initiation and Validation

```
Client Request → M2M/Mobility Portal → BulkChangeRequest → CustomerAssignment Processing
```

**Entry Points:**
- **M2M Portal**: `M2MController.AssociateCustomer()`
- **Mobility Portal**: Device bulk change interface
- **API Endpoint**: Direct bulk change submission

**Request Parameters:**
- `ICCID`: Device identifier
- `RevCustomerId`: Target customer identifier
- `CreateRevService`: Service creation flag
- `CustomerRatePlan`: Customer rate plan ID
- `EffectiveDate`: Implementation timestamp

### 2. Processing Pipeline Flow

```
┌─────────────────────┐
│ BulkChangeRequest   │
│ (Change Type 1)     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Function.Handler    │
│ ProcessBulkChange   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Route to Customer   │
│ Assignment Handler  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Change      │
│ Request Data        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Decision: Has       │
│ RevCustomerId?      │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ RevCustomerId│    │ No Customer  │
│ Present      │    │ ID Present   │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Process      │    │ Update AMOP  │
│ Associate    │    │ Customer     │
│ Customer     │    │ Only         │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Continue     │    │ Log &        │
│ Full Flow    │    │ Complete     │
└──────────────┘    └──────────────┘
```

### 3. Detailed Processing Steps

#### Step 1: Initial Processing Decision
```csharp
// Decision logic in ProcessBulkChangeAsync
case ChangeRequestType.CustomerAssignment:
    var changeRequest = GetBulkChangeRequest(context, bulkChangeId, bulkChange.PortalTypeId);
    var request = JsonConvert.DeserializeObject<BulkChangeAssociateCustomer>(changeRequest);
    
    if (string.IsNullOrEmpty(request?.RevCustomerId))
    {
        // Simple AMOP customer update
        await bulkChangeRepository.UpdateAMOPCustomer(context, logRepo, associateCustomerChanges, bulkChange);
    }
    else
    {
        // Full associate customer processing
        await ProcessAssociateCustomerAsync(context, logRepo, bulkChange, associateCustomerChanges);
    }
```

#### Step 2: Full Associate Customer Processing Flow

```
┌─────────────────────┐
│ ProcessAssociate    │
│ CustomerAsync()     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Initialize Policies │
│ & Authentication    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Extract Integration │
│ Authentication ID   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Setup Rev API       │
│ Client Connection   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device     │
│ Change Record       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check: Create       │
│ Rev Service?        │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Create Rev   │    │ Skip Rev     │
│ Service      │    │ Service      │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Call Create  │    │ Mark as      │
│ RevService   │    │ Successful   │
│ Async()      │    │              │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌─────────────────────────┐
│ Check API Result        │
│ Success?                │
└──────────┬──────────────┘
           │
           ▼
┌─────────────────────────┐
│ Process Customer        │
│ Rate Plan (if needed)   │
└──────────┬──────────────┘
           │
           ▼
┌─────────────────────────┐
│ Update Rev Customer     │
│ (Mobility Portal only)  │
└──────────┬──────────────┘
           │
           ▼
┌─────────────────────────┐
│ Mark Change as          │
│ Processed               │
└──────────┬──────────────┘
           │
           ▼
┌─────────────────────────┐
│ Update Device Rev       │
│ Service Links           │
└─────────────────────────┘
```

#### Step 3: Rev Service Creation Process

```
┌─────────────────────┐
│ CreateRevService    │
│ Async()             │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Build Rev Service   │
│ Request Payload     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ HTTP POST to        │
│ Rev API Endpoint    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Handle API Response │
│ & Error Processing  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Log API Response    │
│ to Change Log       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Return Success/     │
│ Error Status        │
└─────────────────────┘
```

#### Step 4: Customer Rate Plan Processing

```
┌─────────────────────┐
│ Parse Customer      │
│ Rate Plan ID        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Parse Customer      │
│ Rate Pool ID        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check Rate Plan     │
│ Changes Required    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check Effective     │
│ Date vs Current     │
└──────┬──────────────┘
       │
       ▼
┌──────────────┐    ┌──────────────┐
│ Immediate    │    │ Scheduled    │
│ Processing   │    │ Processing   │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Process      │    │ Add to       │
│ Customer     │    │ Customer     │
│ Rate Plan    │    │ Rate Plan    │
│ Change       │    │ Queue        │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌──────────────┐    ┌──────────────┐
│ Execute SP   │    │ Queue Record │
│ Update       │    │ Created      │
└──────┬───────┘    └──────┬───────┘
       │                   │
       ▼                   ▼
┌─────────────────────────┐
│ Log Rate Plan Change    │
│ Success/Error           │
└─────────────────────────┘
```

### 4. Database Operations

#### Primary Stored Procedures
```sql
-- Customer rate plan changes
EXEC usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
    @bulkChangeId = @bulkChangeId,
    @customerRatePlanId = @customerRatePlanId,
    @customerRatePoolId = @customerRatePoolId,
    @customerDataAllocationMB = @customerDataAllocationMB,
    @effectiveDate = @effectiveDate,
    @needToMarkProcessed = @needToMarkProcessed

-- Update mobility device change records
EXEC usp_BulkChange_UpdateModel_MobilityDeviceChange
    @id = @mobilityDeviceChangeId,
    @phoneNumber = @phoneNumber,
    @changeRequest = @changeRequest

-- Mark processed status
EXEC usp_BulkChange_MarkProcessed_[Portal]DeviceChange
    @id = @changeId,
    @hasErrors = @hasErrors,
    @errorText = @errorMessage
```

#### Database Tables Involved
- `DeviceBulkChange`: Main bulk change record
- `M2MDeviceChange` / `MobilityDeviceChange`: Device-specific change records
- `CustomerRatePlanDeviceQueue`: Scheduled rate plan changes
- `M2MDeviceBulkChangeLog` / `MobilityDeviceBulkChangeLog`: Audit logs
- `Device`: Device master records
- `RevService`: Rev service associations

### 5. Integration Points

#### Rev API Integration
```
┌─────────────────────┐
│ Revio API Client    │
│ Authentication      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Service Creation    │
│ Endpoint Call       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Response Processing │
│ & Error Handling    │
└─────────────────────┘
```

#### Portal Type Handling
- **M2M Portal (PortalTypeId = 0)**:
  - Full Rev service creation
  - Customer rate plan assignment
  - M2M-specific logging
  
- **Mobility Portal (PortalTypeId = 2)**:
  - Rev customer updates
  - Mobility-specific logging
  - Subscriber number processing

### 6. Error Handling and Logging

#### Error Scenarios
1. **Authentication Errors**:
   - Invalid integration authentication ID
   - Rev API authentication failure

2. **Validation Errors**:
   - Invalid ICCID format
   - Missing required parameters
   - Invalid customer rate plan ID

3. **Processing Errors**:
   - Rev service creation failure
   - Database operation failures
   - Rate plan assignment errors

#### Logging Framework
```
┌─────────────────────┐
│ DeviceBulkChange    │
│ LogRepository       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Portal-Specific     │
│ Log Entry Creation  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Log Entry Fields:   │
│ - BulkChangeId      │
│ - DeviceChangeId    │
│ - ProcessBy         │
│ - RequestText       │
│ - ResponseText      │
│ - HasErrors         │
│ - ProcessedDate     │
└─────────────────────┘
```

## Complete Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           ASSIGN CUSTOMER DATA FLOW                            │
│                              (Change Type 1)                                   │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────┐
│ Client Request  │
│ (M2M/Mobility)  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Portal Layer    │
│ Validation      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Create Bulk     │
│ Change Request  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ SQS Message     │
│ Queue           │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Lambda Function │
│ Handler         │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ ProcessBulk     │
│ ChangeAsync     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Route to        │
│ Customer        │
│ Assignment      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Extract Change  │
│ Request Data    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Decision Point: │
│ RevCustomerId   │
│ Present?        │
└────┬───────┬────┘
     │       │
     ▼       ▼
┌─────────┐ ┌─────────────┐
│ Simple  │ │ Full        │
│ AMOP    │ │ Associate   │
│ Update  │ │ Customer    │
└────┬────┘ └─────┬───────┘
     │            │
     ▼            ▼
┌─────────────────┐ ┌─────────────────┐
│ Update AMOP     │ │ Initialize      │
│ Customer Only   │ │ Processing      │
└────────┬────────┘ └────────┬────────┘
         │                   │
         ▼                   ▼
┌─────────────────┐ ┌─────────────────┐
│ Log & Complete  │ │ Setup Rev API   │
└─────────────────┘ │ Client          │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ For Each Device │
                    │ Change Record   │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Decision:       │
                    │ Create Rev      │
                    │ Service?        │
                    └────┬───────┬────┘
                         │       │
                         ▼       ▼
                    ┌─────────┐ ┌─────────┐
                    │ Create  │ │ Skip    │
                    │ Rev     │ │ Rev     │
                    │ Service │ │ Service │
                    └────┬────┘ └────┬────┘
                         │           │
                         ▼           ▼
                    ┌─────────────────┐
                    │ Rev Service     │
                    │ Creation Flow   │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ HTTP POST to    │
                    │ Rev API         │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Process API     │
                    │ Response        │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Check Success   │
                    │ Status          │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Customer Rate   │
                    │ Plan Processing │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Parse Rate Plan │
                    │ & Pool IDs      │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Check Effective │
                    │ Date            │
                    └────┬───────┬────┘
                         │       │
                         ▼       ▼
                    ┌─────────┐ ┌─────────┐
                    │ Immed.  │ │ Sched.  │
                    │ Process │ │ Queue   │
                    └────┬────┘ └────┬────┘
                         │           │
                         ▼           ▼
                    ┌─────────────────┐
                    │ Execute SP      │
                    │ Update/Queue    │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Log Rate Plan   │
                    │ Change Result   │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Update Rev      │
                    │ Customer        │
                    │ (Mobility Only) │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Mark Change     │
                    │ as Processed    │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Update Device   │
                    │ Rev Service     │
                    │ Links           │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │ Final Logging   │
                    │ & Cleanup       │
                    └─────────────────┘
```

## Performance Characteristics

### Processing Metrics
- **Page Size**: 100 devices per batch (default)
- **Page Size (No Service Creation)**: 1000 devices per batch
- **Retry Policy**: 3 attempts for SQL operations
- **HTTP Retry**: 3 attempts for API calls
- **Timeout**: Configurable per operation

### Scalability Considerations
- **Parallel Processing**: Multiple device changes processed concurrently
- **Queue-based Scheduling**: Future-dated changes handled via queue
- **Connection Pooling**: Database connection optimization
- **Async Operations**: Non-blocking processing throughout

## Security and Compliance

### Authentication & Authorization
- **Integration Authentication**: Per-service provider credentials
- **Rev API Authentication**: Secure token-based access
- **Tenant Isolation**: Customer data segregation
- **Role-based Access**: Portal-specific permissions

### Data Protection
- **Encrypted Connections**: All API and database communications
- **Audit Logging**: Complete operation tracking
- **Error Sanitization**: No sensitive data in logs
- **Change History**: Full audit trail maintenance

## Monitoring and Observability

### Key Metrics
- **Success Rate**: Percentage of successful assign customer operations
- **Processing Time**: Average time per device assignment
- **Error Rate**: Failure percentage by error type
- **Queue Depth**: Pending scheduled changes

### Alerting
- **High Error Rate**: Above threshold failure rate
- **Processing Delays**: Queue backlog alerts
- **API Failures**: Rev service creation issues
- **Database Connectivity**: Connection failure alerts

## Integration Dependencies

### External Services
- **Rev API**: Customer service management
- **POND Platform**: Device connectivity
- **Carrier Networks**: Service activation
- **Billing Systems**: Rate plan integration

### Internal Dependencies
- **Central Database**: Master data repository
- **SQS Queues**: Message processing
- **Lambda Functions**: Serverless processing
- **CloudWatch**: Logging and monitoring