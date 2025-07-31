# POND IoT Service Provider - Device Change Implementation

## Overview
The POND IoT Service Provider supports 4 bulk change operations that enable comprehensive device management through the M2M portal. This document outlines the implementation for each change type following the established pattern used in the existing codebase.

## 4 Change Types Supported

1. **Assign Customer** - Associates devices with customers
2. **Change Carrier Rate Plan** - Updates carrier-level pricing and service configurations
3. **Change Customer Rate Plan** - Modifies customer-specific rate plans
4. **Update Device Status** - Changes device operational status

## System Architecture

### Components
- **M2MController** - Handles UI requests and validation
- **AltaworxDeviceBulkChange Lambda** - Processes bulk change operations
- **PondRepository** - Database operations for POND-specific data
- **PondApiService** - API communication with POND carrier
- **DeviceBulkChangeLogRepository** - Logging and audit trail

### Data Flow
```
User Interface → M2MController.BulkChange() → Validation → Queue (SQS) → 
AltaworxDeviceBulkChange Lambda → Carrier API Calls → Database Update → 
Portal-Specific Logging → BulkChangeStatus.PROCESSED
```

## Implementation Details

### 1. Assign Customer

#### Flow
1. User selects devices and target customer from M2M UI
2. Frontend sends POST request to M2MController.BulkChange()
   - ChangeType: "CustomerAssignment"
   - Devices: List of ICCIDs
   - Customer: Target customer details
3. Controller validation via ValidateBulkChange()
4. Queue processing through SQS
5. Lambda processes via ProcessAssociateCustomerAsync()

#### M2MController Changes
```csharp
// In BuildChangeDetails method
case DeviceChangeType.CustomerAssignment:
    return BuildCustomerAssignmentChangeDetails(awxDb, session, bulkChange, serviceProviderId);
```

#### Lambda Processing
```csharp
// In ProcessBulkChangeAsync switch statement
case ChangeRequestType.CustomerAssignment:
    var associateCustomerChanges = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, pageSize).ToList();
    await ProcessPondCustomerAssignmentAsync(context, logRepo, bulkChange, associateCustomerChanges);
    return true;
```

### 2. Change Carrier Rate Plan

#### Flow (Already Implemented)
1. User selects devices and target carrier rate plan
2. Frontend sends POST request with ChangeType: "CarrierRatePlanChange"
3. BuildCustomerRatePlanChangeDetails() validates:
   - ICCID exists in database
   - Carrier rate plan exists and is active
   - Device compatibility with rate plan
   - Retrieve rate plan metadata (RatePlanId for POND)
4. ProcessPondCarrierRatePlanChange() executes:
   - Add new package via POND API
   - Activate new package
   - Terminate existing packages
   - Update database associations

#### Current Implementation Location
- **M2MController**: Lines 1647-1697 (BuildCustomerRatePlanChangeDetails)
- **Lambda**: Lines 1022-1120 (ProcessPondCarrierRatePlanChange)

### 3. Change Customer Rate Plan

#### Flow
1. User selects devices and target customer rate plan
2. Frontend sends POST request to M2MController.BulkChange()
   - ChangeType: "CustomerRatePlanChange"
   - Devices: List of ICCIDs
   - CustomerRatePlan: Target rate plan details
3. Controller validation for customer rate plan eligibility
4. Queue processing and lambda execution
5. Update customer billing configurations

#### Implementation Pattern
```csharp
// Lambda processing
case ChangeRequestType.CustomerRatePlanChange:
    return await ProcessPondCustomerRatePlanChangeAsync(context, logRepo, bulkChange, sqlRetryPolicy);
```

### 4. Update Device Status

#### Flow (Partially Implemented)
1. User selects devices and target status
2. Frontend sends POST request with ChangeType: "StatusUpdate"
3. BuildStatusUpdateChangeDetails() validates device eligibility
4. ProcessPondStatusUpdateAsync() executes:
   - Validate authentication and permissions
   - Call POND API for status updates
   - Update device status in database
   - Log all operations

#### Current Implementation Location
- **Lambda**: Lines 2770-2820 (ProcessPondStatusUpdateAsync)

## Database Schema Considerations

### Tables Involved
- **M2M_DeviceChange** - Individual device change records
- **DeviceBulkChange** - Bulk operation metadata
- **DeviceBulkChangeLog** - Audit trail and logging
- **Device** - Device master data
- **PondDeviceCarrierRatePlan** - POND-specific rate plan associations

### Key Fields
```sql
-- M2M_DeviceChange
BulkChangeId (FK)
ICCID
ChangeRequest (JSON)
Status
ProcessedDate

-- DeviceBulkChange  
ChangeRequestTypeId
ServiceProviderId
Status
CreatedDate
```

## API Integration Points

### POND API Endpoints
- **Add Package**: `/{distributorId}/package/add/{iccid}`
- **Update Package Status**: `/{distributorId}/package/update/{iccid}/{packageId}`
- **Device Status Update**: POND-specific status endpoint
- **Customer Association**: Customer management endpoint

### Authentication
- Uses PondAuthentication object
- Supports both sandbox and production URLs
- Includes distributor ID and API credentials

## Error Handling

### Validation Errors
- Invalid ICCID
- Rate plan not found or inactive
- Device incompatibility
- Missing EID (for Teal integration)

### API Errors
- Authentication failures
- POND API errors
- Network timeouts
- Rate limiting

### Logging Strategy
- All operations logged to DeviceBulkChangeLog
- Error details captured with context
- Success/failure status tracking
- Performance metrics

## Security Considerations

### Permissions
- User must have M2M module access
- Create permissions required for bulk changes
- Service provider access validation

### Data Protection
- Sensitive data encrypted in transit
- API credentials secured
- Audit trail maintained

## Performance Optimizations

### Batch Processing
- Configurable page sizes
- Lambda timeout management
- Parallel processing where possible

### Database Efficiency
- Bulk insert operations
- Connection pooling
- Parameterized queries

## Monitoring and Alerting

### Key Metrics
- Processing time per change type
- Success/failure rates
- API response times
- Queue depth

### Alerting
- Failed authentication
- High error rates
- Processing delays
- API availability issues

## Testing Strategy

### Unit Tests
- Validation logic
- Change request building
- Error handling

### Integration Tests  
- End-to-end flows
- API interactions
- Database operations

### Load Testing
- Bulk change performance
- Concurrent user scenarios
- Resource utilization

## Configuration Management

### Environment Variables
- POND API endpoints
- Authentication credentials
- Timeout values
- Feature flags

### Deployment Considerations
- Blue/green deployments
- Database migrations
- API versioning
- Rollback procedures

## Compliance and Auditing

### Audit Requirements
- All changes logged with timestamps
- User attribution
- Before/after states
- Compliance reporting

### Data Retention
- Log retention policies
- Archive strategies
- Compliance with regulations

This implementation follows the established patterns in the codebase while providing comprehensive support for all 4 POND IoT Service Provider change types. The architecture ensures scalability, reliability, and maintainability while meeting business requirements for device management operations.