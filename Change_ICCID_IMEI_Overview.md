# Change ICCID/IMEI for Verizon ThingSpace IoT - Comprehensive Overview

## Purpose

The Change ICCID/IMEI functionality enables swapping device identifiers (SIM cards or device hardware) through the Verizon ThingSpace API with asynchronous callback handling. This system supports bulk operations for efficiently managing large numbers of IoT devices while maintaining data integrity and audit trails.

## What, Why, and How

### What
A comprehensive device identifier change system that:
- Swaps ICCID (SIM card identifiers) and IMEI (device hardware identifiers)
- Processes bulk changes efficiently through async queuing
- Integrates with Verizon ThingSpace API for carrier-level updates
- Maintains complete audit trails and error handling
- Supports customer rate plan updates during identifier changes

### Why
- **Device Replacement**: When SIM cards fail or devices are replaced
- **Fleet Management**: Bulk swapping of device identifiers for large IoT deployments
- **Inventory Management**: Moving devices between customers or locations
- **Maintenance Operations**: Scheduled device maintenance requiring identifier changes
- **Compliance**: Maintaining accurate device tracking for regulatory requirements

### How
The system operates through a multi-phase approach:
1. **User Interface**: Web-based bulk change creation
2. **Queue Processing**: SQS-based asynchronous processing
3. **API Integration**: ThingSpace API calls with callback handling
4. **Database Updates**: Local database synchronization
5. **Audit Trail**: Complete logging and status tracking

## System Architecture

### Core Components

#### 1. Bulk Change Management
- **BulkChange Entity**: Master record for change operations
- **BulkChangeDetailRecord**: Individual device change records
- **Status Tracking**: Real-time processing status updates

#### 2. ThingSpace Integration
- **Authentication**: OAuth2 with session token management
- **API Calls**: RESTful API integration with retry logic
- **Callback Handling**: Asynchronous result processing

#### 3. Queue Processing
- **SQS Integration**: AWS Simple Queue Service for async processing
- **Retry Mechanism**: Configurable retry logic with exponential backoff
- **Dead Letter Queue**: Failed message handling

#### 4. Database Operations
- **Device Updates**: ICCID/IMEI field updates
- **Customer Rate Plans**: Optional rate plan association
- **Audit Logging**: Complete change history

## Data Models

### Primary Entities

```csharp
public class BulkChangeUpdateIdentifier
{
    public IdentifierTypeEnum IdentifierType { get; set; }  // ICCID or IMEI
    public string OldICCID { get; set; }
    public string NewICCID { get; set; }
    public string OldIMEI { get; set; }
    public string NewIMEI { get; set; }
    public bool AddCustomerRatePlan { get; set; }
    public string CustomerRatePlan { get; set; }
    public string CustomerRatePool { get; set; }
}

public class BulkChangeDetailRecord
{
    public long Id { get; set; }
    public long BulkChangeId { get; set; }
    public string DeviceIdentifier { get; set; }
    public string ChangeRequest { get; set; }
    public string Status { get; set; }
    public int ServiceProviderId { get; set; }
    public int IntegrationId { get; set; }
    public string StatusDetails { get; set; }
}
```

### ThingSpace Request Structure

```csharp
public class ThingSpaceChangeIdentifierRequest
{
    public List<DeviceId> DeviceIds { get; set; }      // Old identifiers
    public List<DeviceId> DeviceIdsTo { get; set; }    // New identifiers
    public string Change4gOption { get; set; }          // Change type
}

public class DeviceId
{
    public string Id { get; set; }      // Identifier value
    public string Kind { get; set; }    // "iccid" or "imei"
}
```

## Process Flow Overview

The complete flow follows this path:

```
User Interface → Rate Plan Selection → Device Selection → Plan Validation → 
Bulk Change Creation → Queue Processing (SQS) → Background Lambda Processing → 
Authentication & Authorization → Device-by-Device Processing → Database Operations → 
Status Tracking → Error Handling → Completion Processing → Audit Trail Creation → 
Rate Plan Activation Complete
```

## Integration Points

### 1. Verizon ThingSpace API
- **Endpoint**: `/thingspace/v1/updateidentifier`
- **Method**: PUT
- **Authentication**: OAuth2 with session tokens
- **Callback**: Async response handling

### 2. AWS Services
- **Lambda**: Background processing functions
- **SQS**: Message queuing for async operations
- **CloudWatch**: Logging and monitoring

### 3. Database Systems
- **Central Database**: Primary device and change tracking
- **Logging Database**: Audit trail and error logging
- **Queue Tables**: Scheduled change management

## Security and Compliance

### Authentication
- Service provider-specific credentials
- Encrypted connection strings
- Session token management with expiration

### Authorization
- Tenant-level access control
- Role-based permissions
- Service provider isolation

### Audit Trail
- Complete request/response logging
- Status change tracking
- Error condition documentation
- Performance metrics collection

## Error Handling and Recovery

### Error Categories
1. **Authentication Errors**: Token failures, credential issues
2. **API Errors**: ThingSpace service unavailability
3. **Validation Errors**: Invalid identifiers, business rule violations
4. **System Errors**: Database connectivity, processing failures

### Recovery Mechanisms
- **Automatic Retry**: Configurable retry logic with backoff
- **Manual Intervention**: Admin tools for stuck processes
- **Rollback Capability**: Reverting failed changes
- **Dead Letter Queues**: Handling permanently failed messages

## Performance Characteristics

### Scalability
- **Bulk Processing**: Handles thousands of devices per batch
- **Async Processing**: Non-blocking operations
- **Parallel Execution**: Multiple Lambda instances
- **Queue Management**: SQS for load distribution

### Monitoring
- **Real-time Status**: Live progress tracking
- **Performance Metrics**: Processing time analysis
- **Error Rates**: Failure pattern identification
- **Resource Utilization**: System capacity monitoring

## Configuration Management

### Environment Settings
- **API Endpoints**: Production vs Sandbox URLs
- **Queue Configuration**: SQS queue names and settings
- **Retry Policies**: Timeout and retry parameters
- **Logging Levels**: Debug vs Production logging

### Service Provider Settings
- **ThingSpace Credentials**: Per-provider authentication
- **Rate Limits**: API call throttling
- **Feature Flags**: Enabling/disabling functionality
- **Custom Rules**: Provider-specific business logic

This overview provides the foundation for understanding the Change ICCID/IMEI functionality within the broader Verizon ThingSpace IoT ecosystem. The system is designed for reliability, scalability, and comprehensive audit capabilities while maintaining the flexibility to handle various device management scenarios.