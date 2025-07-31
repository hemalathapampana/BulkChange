# Update Device Status Data Flow - POND IoT Service Provider

## Overview

This document outlines the data flow for **Device Status Updates** in the POND IoT service provider system. The process handles device activation, status synchronization, and bulk device status management through various integration points including ThingSpace APIs, SQS messaging, and database operations.

## Key Components

### POND IoT Service Provider
- **Purpose**: IoT connectivity and device management
- **Scope**: Service provider managed device lifecycle
- **Properties**:
  - `ICCID`: SIM card identifier
  - `MSISDN`: Mobile subscriber identifier
  - `DeviceState`: Current activation status
  - `PackageId`: Service package identifier
  - `DistributorId`: POND distributor identifier

### Device Status Types
- **Active**: Device fully activated and operational
- **Pending Activation**: Device awaiting activation
- **Suspended**: Device temporarily suspended
- **Terminated**: Device permanently deactivated

## Data Models

### Device Status Update Request
```csharp
public class StatusUpdateRequest<T>
{
    public string UpdateStatus { get; set; }
    public bool IsIgnoreCurrentStatus { get; set; }
    public int PostUpdateStatusId { get; set; }
    public string AccountNumber { get; set; }
    public T Request { get; set; }
    public BulkChangeAssociateCustomer RevService { get; set; }
    public RevServiceProductCreateModel RevServiceProductCreateModel { get; set; }
    public int IntegrationAuthenticationId { get; set; }
}
```

### SQS Message Structure
```csharp
public class SqsValues
{
    public long BulkChangeId { get; set; }
    public long M2MDeviceChangeId { get; set; }
    public bool IsRetryNewActivateThingSpaceDevice { get; set; }
    public bool IsFromAutomatedUpdateDeviceStatusLambda { get; set; }
    public bool IsRetryUpdateIdentifier { get; set; }
    public int RetryNumber { get; set; }
    public string RequestId { get; set; }
}
```

### Update Device Status Result
```csharp
public class UpdateDeviceStatusResult
{
    public string iccid { get; set; }
}
```

## Data Flow Architecture

### 1. Process Initiation

```
SQS Message → Lambda Function → ProcessUpdateDeviceAfterActivateThingSpaceDevice()
```

**Trigger Sources:**
- Automated status update scheduler
- Manual bulk change requests
- Retry mechanisms for failed operations
- External carrier status notifications

### 2. Device Status Processing Pipeline

#### Step 1: Message Processing and Context Setup
```
SQS Event → SqsValues Parsing → Lambda Context Initialization
```

**Process:**
1. Extract message attributes from SQS
2. Parse BulkChangeId and M2MDeviceChangeId
3. Initialize logging context
4. Validate retry parameters and flags

#### Step 2: Device Retrieval and Authentication
```
BulkChangeId → Get Devices from M2M_DeviceChange → ThingSpace Authentication
```

**Database Query:**
```sql
SELECT ICCID FROM M2M_DeviceChange 
WHERE BulkChangeId = @bulkChangeId AND MSISDN IS NULL
```

**Authentication Flow:**
1. Retrieve ThingSpace authentication for service provider
2. Obtain access token from ThingSpace API
3. Generate session token for API calls
4. Validate authentication credentials

#### Step 3: Device Status Synchronization
```
For Each Device → ThingSpace API Call → Status Validation → Database Update
```

**ThingSpace API Integration:**
```
GET /devices/{iccid} → Device Response → Extract Status and MSISDN
```

**Status Validation Logic:**
```csharp
if (!string.IsNullOrEmpty(state) && state.Equals("active"))
{
    // Update MSISDN in database
    bulkChangeRepository.UpdateMSISDNToM2M_DeviceChange(
        connectionString, bulkChangeId, msisdn, iccid, 
        serviceProviderId, deviceResponse);
}
```

### 3. POND-Specific Processing

#### POND Device Status Management
```
POND Authentication → Package Management → Status Updates
```

**POND Integration Points:**
1. **Authentication**: Distributor ID and API credentials
2. **Package Management**: Active/Terminated package status
3. **Status Updates**: Device activation and deactivation
4. **Rate Plan Changes**: Carrier rate plan updates

#### POND API Endpoints
```
POST /{distributorId}/devices/{iccid}/packages → Add Package
PUT /{distributorId}/devices/{iccid}/packages/{packageId}/status → Update Status
GET /{distributorId}/devices/{iccid} → Get Device Status
```

### 4. Bulk Change Management

#### Bulk Processing Flow
```
Bulk Change Request → Device List → Parallel Processing → Status Aggregation
```

**Processing Logic:**
```csharp
public async Task<bool> ProcessUpdateDeviceAfterActivateThingSpaceDevice(
    KeySysLambdaContext context, SqsValues sqsValues, BulkChange bulkChange)
{
    // Get devices needing update
    var devices = bulkChangeRepository.GetICCIDM2MDeviceChangeBybulkId(
        context.CentralDbConnectionString, bulkChange.Id);
    
    // Process each device
    foreach (var iccid in devices)
    {
        var deviceResponse = await ThingSpaceCommon.GetThingSpaceDeviceAsync(
            iccid, baseUrl, accessToken, sessionToken, logger);
        
        // Update if active
        if (deviceResponse.state == "active")
        {
            UpdateMSISDNToDatabase(deviceResponse);
        }
    }
    
    // Check completion status
    var remainingDevices = bulkChangeRepository.GetICCIDM2MDeviceChangeBybulkId(
        context.CentralDbConnectionString, bulkChange.Id);
    
    return remainingDevices.Count == 0; // True if all processed
}
```

### 5. Retry and Error Handling

#### Retry Mechanism
```
Failed Operation → SQS Retry Message → Incremental Backoff → Retry Processing
```

**Retry Logic:**
- Maximum retry attempts: Configurable (typically 3-5)
- Exponential backoff: Increasing delay between retries
- Dead letter queue: For permanently failed messages
- Error categorization: Temporary vs permanent failures

#### Error Types and Handling
1. **Authentication Failures**: Credential refresh and retry
2. **API Rate Limiting**: Backoff and retry with delay
3. **Network Timeouts**: Immediate retry with timeout increase
4. **Invalid Device States**: Manual intervention required
5. **Database Errors**: Transaction rollback and retry

## Processing Flow Diagram

```
┌─────────────────────┐
│ SQS Message         │
│ (Device Status      │
│  Update Trigger)    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Lambda Function     │
│ Initialization      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Parse SQS Values    │
│ & Context Setup     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get Device List     │
│ from Database       │
│ (M2M_DeviceChange)  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ ThingSpace          │
│ Authentication      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device:    │
│ Call ThingSpace API │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐      ┌─────────────────────┐
│ Device Status       │ Yes  │ Update MSISDN       │
│ = "active"?         │─────▶│ in Database         │
└──────────┬──────────┘      └─────────────────────┘
           │ No
           ▼
┌─────────────────────┐
│ Log Status          │
│ (Not Active)        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check Remaining     │
│ Unprocessed Devices │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐      ┌─────────────────────┐
│ Any Devices         │ Yes  │ Send SQS Retry      │
│ Remaining?          │─────▶│ Message             │
└──────────┬──────────┘      └─────────────────────┘
           │ No
           ▼
┌─────────────────────┐
│ Mark Bulk Change    │
│ as Complete         │
└─────────────────────┘
```

## POND-Specific Integration Flow

```
┌─────────────────────┐
│ POND Device Status  │
│ Update Request      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ POND Authentication │
│ & Authorization     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get Existing        │
│ Packages for Device │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Add New Package     │
│ (if required)       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐      ┌─────────────────────┐
│ Package Added       │ Yes  │ Update Package      │
│ Successfully?       │─────▶│ Status to ACTIVE    │
└──────────┬──────────┘      └──────────┬──────────┘
           │ No                         │
           ▼                            ▼
┌─────────────────────┐      ┌─────────────────────┐
│ Log Error &         │      │ Terminate Existing  │
│ Mark as Failed      │      │ Packages            │
└─────────────────────┘      └──────────┬──────────┘
                                        │
                                        ▼
                             ┌─────────────────────┐
                             │ Save Package Info   │
                             │ to Database         │
                             └─────────────────────┘
```

## Database Schema Integration

### Key Tables
1. **M2M_DeviceChange**: Device change tracking
2. **BulkChange**: Bulk operation management
3. **PondDeviceCarrierRatePlan**: POND-specific device plans
4. **DeviceBulkChangeLog**: Operation audit trail

### Database Operations
```sql
-- Get devices needing status update
SELECT ICCID FROM M2M_DeviceChange 
WHERE BulkChangeId = @bulkChangeId AND MSISDN IS NULL

-- Update device MSISDN after activation
UPDATE M2M_DeviceChange 
SET MSISDN = @msisdn, 
    LastModified = GETUTCDATE(),
    ProcessedBy = @processedBy
WHERE ICCID = @iccid AND BulkChangeId = @bulkChangeId
```

## Monitoring and Logging

### Log Categories
1. **Info Logs**: Normal processing flow
2. **Warning Logs**: Retry attempts and recoverable errors
3. **Error Logs**: Failed operations requiring intervention
4. **Debug Logs**: Detailed API request/response data

### Key Metrics
- **Processing Time**: Average time per device update
- **Success Rate**: Percentage of successful status updates
- **Retry Rate**: Frequency of retry operations
- **API Response Times**: ThingSpace and POND API performance

### Alerting Triggers
- High error rate (>5% failures)
- Extended processing times (>30 minutes for bulk operations)
- Authentication failures
- API rate limit exceeded

## Security Considerations

### Authentication & Authorization
- Service provider scoped access
- Encrypted API credentials storage
- Token-based authentication with expiration
- Audit trail for all operations

### Data Protection
- Encrypted database connections
- Sanitized logging (no sensitive data exposure)
- GDPR compliance for device identifiers
- Secure API communication (HTTPS)

## Performance Optimization

### Batch Processing
- Parallel device processing
- Connection pooling for database operations
- Bulk API calls where supported
- Efficient retry queuing

### Caching Strategy
- Authentication token caching
- Service provider configuration caching
- Device status caching for frequent queries

## Error Scenarios and Recovery

### Common Error Scenarios
1. **ThingSpace API Unavailable**: Retry with exponential backoff
2. **Invalid ICCID**: Skip device and log error
3. **Database Connection Issues**: Retry with connection refresh
4. **POND Authentication Failure**: Refresh credentials and retry
5. **Rate Limiting**: Implement throttling and queue management

### Recovery Procedures
1. **Manual Retry**: Admin interface for failed bulk changes
2. **Status Reconciliation**: Periodic sync with carrier systems
3. **Dead Letter Processing**: Manual review of permanently failed messages
4. **Data Consistency Checks**: Regular validation of device status accuracy

## Integration Points

### External Systems
1. **ThingSpace API**: Primary device status source
2. **POND API**: POND-specific device management
3. **AWS SQS**: Asynchronous message processing
4. **Database**: Device and bulk change persistence
5. **Logging System**: Centralized log aggregation

### Internal Components
1. **M2M Controller**: Web interface for device management
2. **Bulk Change Repository**: Database access layer
3. **Authentication Services**: Credential management
4. **Notification Services**: Status change notifications

## Future Enhancements

### Planned Improvements
1. **Real-time Status Updates**: WebSocket-based status streaming
2. **Advanced Analytics**: Device status trend analysis
3. **Automated Recovery**: Self-healing failure recovery
4. **Multi-Carrier Support**: Extended carrier integration
5. **Mobile App Integration**: Mobile device status management

### Scalability Considerations
1. **Microservice Architecture**: Service decomposition for better scalability
2. **Event-Driven Processing**: Event sourcing for status changes
3. **Horizontal Scaling**: Load balancing across multiple instances
4. **Caching Layer**: Redis-based distributed caching