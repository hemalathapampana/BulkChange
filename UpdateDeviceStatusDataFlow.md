# Update Device Status Data Flow Diagram (DFD)
## AT&T POD19 Service Provider

## Overview

This document presents the **Data Flow Diagram (DFD)** for the **Update Device Status** process within the AT&T POD19 Service Provider ecosystem. The process encompasses device status management across multiple carrier platforms including ThingSpace, Jasper, Telegence, and other service providers.

## System Context

The Update Device Status process is a critical component of the AT&T POD19 Service Provider infrastructure that:
- Manages device lifecycle status transitions
- Coordinates with multiple carrier platforms
- Handles bulk device operations
- Provides status synchronization across systems
- Maintains audit trails and logging

## Data Flow Diagram Components

### External Entities

1. **AT&T ThingSpace API**
   - Purpose: Primary carrier platform for device management
   - Interface: REST API with OAuth authentication
   - Status Types: active, inactive, suspended, pending activation

2. **Jasper Control Center**
   - Purpose: Legacy carrier platform for device management
   - Interface: SOAP/REST API
   - Status Management: Device activation, deactivation, suspension

3. **Telegence Platform**
   - Purpose: Alternative carrier platform
   - Interface: REST API
   - Operations: New service activation, status polling

4. **AWS SQS (Simple Queue Service)**
   - Purpose: Message queuing for asynchronous processing
   - Message Types: Status update requests, retry mechanisms
   - Processing: Background job orchestration

5. **Customer Portal Interface**
   - Purpose: User-initiated status change requests
   - Access: Web-based M2M Controller
   - Operations: Bulk device status changes

### Core Processes

#### Process 1: **Status Update Request Initiation**
- **Input**: Status change request from customer portal
- **Processing**: Validates request, determines carrier platform
- **Output**: Queued status update message

#### Process 2: **Carrier Platform Selection**
- **Input**: Device information (ICCID, Service Provider ID)
- **Processing**: Routes to appropriate carrier platform
- **Output**: Platform-specific API call

#### Process 3: **ThingSpace Device Status Update**
- **Input**: Device ICCID, target status, authentication tokens
- **Processing**: 
  - Generates access token and session token
  - Constructs ThingSpace API request
  - Handles status transitions (activate, deactivate, suspend)
- **Output**: Request ID for async status tracking

#### Process 4: **Jasper Device Status Update**
- **Input**: Device ICCID, target status
- **Processing**: 
  - Formats Jasper-specific request
  - Handles authentication
  - Processes immediate response
- **Output**: Status confirmation or error

#### Process 5: **Status Polling and Verification**
- **Input**: Request ID from carrier platforms
- **Processing**: 
  - Polls carrier API for status completion
  - Verifies final device status
  - Handles timeout scenarios
- **Output**: Final status confirmation

#### Process 6: **Database Status Synchronization**
- **Input**: Confirmed status from carrier platforms
- **Processing**: 
  - Updates M2M_DeviceChange table
  - Updates MSISDN information
  - Marks bulk change as processed
- **Output**: Synchronized device status

#### Process 7: **Retry and Error Handling**
- **Input**: Failed status update attempts
- **Processing**: 
  - Implements exponential backoff
  - Tracks retry count against threshold
  - Routes to error handling
- **Output**: Retry message or final error

### Data Stores

#### D1: **M2M_DeviceChange**
- **Purpose**: Primary device change tracking
- **Key Fields**:
  - `Id`: Unique identifier
  - `BulkChangeId`: Associated bulk operation
  - `ICCID`: Device identifier
  - `MSISDN`: Mobile number
  - `ProcessedStatus`: Current processing state
  - `ModifiedBy`: System identifier
  - `ModifiedDate`: Timestamp

#### D2: **BulkChange**
- **Purpose**: Bulk operation management
- **Key Fields**:
  - `Id`: Bulk change identifier
  - `ServiceProviderId`: Target carrier platform
  - `TenantId`: Customer tenant
  - `ProcessedBy`: Processing system
  - `Status`: Overall bulk status

#### D3: **ServiceProviderAuthentication**
- **Purpose**: Carrier platform credentials
- **Key Fields**:
  - `ServiceProviderId`: Platform identifier
  - `BaseUrl`: API endpoint
  - `ClientId`: OAuth client ID
  - `ClientSecret`: OAuth secret
  - `AccountNumber`: Carrier account

#### D4: **DeviceBulkChangeLog**
- **Purpose**: Audit trail and error tracking
- **Key Fields**:
  - `BulkChangeId`: Associated operation
  - `MobilityDeviceChangeId`: Device change reference
  - `RequestText`: API request payload
  - `ResponseText`: API response
  - `ErrorText`: Error details
  - `ProcessedDate`: Timestamp

#### D5: **ThingSpaceCallBackLog**
- **Purpose**: ThingSpace async operation tracking
- **Key Fields**:
  - `RequestId`: ThingSpace request identifier
  - `Status`: Current async status
  - `TenantId`: Customer tenant
  - `ServiceProviderId`: Platform identifier

### Data Flows

#### Flow 1: **Customer Initiated Status Change**
```
Customer Portal → [Process 1: Status Update Request] → SQS Message Queue
```

#### Flow 2: **Platform Routing**
```
SQS Message → [Process 2: Platform Selection] → Carrier-Specific Process
```

#### Flow 3: **ThingSpace Status Update Flow**
```
[Process 3: ThingSpace Update] → ThingSpace API → [Process 5: Status Polling] → [Process 6: DB Sync]
                               ↓
                          [D5: ThingSpaceCallBackLog]
```

#### Flow 4: **Jasper Status Update Flow**
```
[Process 4: Jasper Update] → Jasper API → [Process 6: DB Sync]
```

#### Flow 5: **Error and Retry Flow**
```
Failed Process → [Process 7: Retry Handler] → SQS Retry Queue
                                          ↓
                                     [D4: Error Log]
```

#### Flow 6: **Database Update Flow**
```
[Process 6: DB Sync] → [D1: M2M_DeviceChange] → [D2: BulkChange] → [D4: Log]
```

## Detailed Process Descriptions

### ThingSpace Device Status Update Process

1. **Authentication Phase**
   - Retrieve authentication credentials from D3
   - Generate OAuth access token
   - Obtain session token for API calls

2. **Status Update Request**
   - Construct platform-specific request body
   - Include device identifier (ICCID)
   - Specify target status and reason code
   - Submit asynchronous request

3. **Async Tracking**
   - Receive request ID from ThingSpace
   - Store in D5 for status polling
   - Initiate polling mechanism

4. **Status Verification**
   - Poll ThingSpace status endpoint
   - Verify device reached target status
   - Update local database accordingly

### Retry Mechanism

The system implements a robust retry mechanism:

1. **Retry Threshold**: Configurable via environment variable
2. **Backoff Strategy**: Exponential backoff with jitter
3. **Error Classification**: Transient vs permanent errors
4. **Queue Management**: SQS dead letter queue for failed operations

### Status Types and Transitions

#### Supported Status Transitions

| From Status | To Status | Platform Support |
|-------------|-----------|------------------|
| Pending Activation | Active | ThingSpace, Jasper, Telegence |
| Active | Suspended | ThingSpace, Jasper |
| Active | Inactive | ThingSpace, Jasper |
| Suspended | Active | ThingSpace, Jasper |
| Inactive | Active | ThingSpace (with reason code) |

#### Status Reason Codes

- **Activation**: Customer request, new service
- **Suspension**: Non-payment, customer request
- **Deactivation**: Service termination, device replacement

## Error Handling and Logging

### Error Categories

1. **Authentication Errors**
   - Invalid credentials
   - Token expiration
   - Insufficient permissions

2. **API Errors**
   - Network connectivity issues
   - Rate limiting
   - Service unavailability

3. **Data Validation Errors**
   - Invalid ICCID format
   - Unsupported status transition
   - Missing required fields

4. **Business Logic Errors**
   - Device not found in carrier system
   - Status already in target state
   - Bulk operation constraints

### Logging Strategy

- **Request/Response Logging**: Complete API payloads
- **Error Tracking**: Detailed error messages and stack traces
- **Performance Metrics**: API response times and success rates
- **Audit Trail**: User actions and system changes

## Configuration and Environment Variables

### Key Configuration Parameters

- `THINGSPACE_UPDATE_DEVICE_STATUS_RETRY_NUMBER`: Maximum retry attempts
- `SQSMessageKeyConstant.IS_FROM_AUTOMATED_UPDATE_DEVICE_STATUS_LAMBDA`: Automation flag
- Platform-specific authentication endpoints and credentials

## Security Considerations

1. **Authentication**: OAuth 2.0 for ThingSpace, proprietary auth for others
2. **Data Encryption**: TLS 1.2+ for all API communications
3. **Credential Management**: Secure storage of API keys and secrets
4. **Access Control**: Role-based access to status update operations
5. **Audit Logging**: Complete audit trail of all status changes

## Performance Considerations

1. **Batch Processing**: Bulk operations for efficiency
2. **Async Processing**: Non-blocking operations via SQS
3. **Connection Pooling**: Reuse of HTTP connections
4. **Rate Limiting**: Respect carrier platform limits
5. **Caching**: Authentication token caching to reduce API calls

## Monitoring and Alerting

### Key Metrics

- Status update success rate per platform
- Average processing time per device
- Queue depth and processing lag
- Error rate by category
- Platform availability metrics

### Alert Conditions

- High error rate (>5% in 15 minutes)
- Queue depth exceeding threshold
- Platform authentication failures
- Extended processing times

## Future Enhancements

1. **Real-time Status Sync**: WebSocket-based status updates
2. **Predictive Error Handling**: ML-based error prediction
3. **Multi-tenant Isolation**: Enhanced tenant separation
4. **API Gateway Integration**: Centralized API management
5. **Event-driven Architecture**: Event sourcing for status changes