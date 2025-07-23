# Change ICCID/IMEI Process Flow - Detailed Phases

## Complete Process Flow

```
User Interface → Rate Plan Selection → Device Selection → Plan Validation → 
Bulk Change Creation → Queue Processing (SQS) → Background Lambda Processing → 
Authentication & Authorization → Device-by-Device Processing → Database Operations → 
Status Tracking → Error Handling → Completion Processing → Audit Trail Creation → 
Rate Plan Activation Complete
```

## Phase 1: User Interface & Input Collection

### 1.1 User Access & Authentication
**Location**: M2MController.cs
```
User Login → Permission Check → Module Access Validation
├── Check: ModuleEnum.M2M access
├── Validate: Tenant permissions
└── Authorize: Service provider access
```

### 1.2 Bulk Change Initiation
**UI Components**:
- Device selection interface
- ICCID/IMEI input forms
- Rate plan selection dropdown
- Validation rules display

**Key Operations**:
```csharp
// Controller method for bulk change creation
[HttpPost]
public ActionResult CreateBulkChange(BulkChangeRequest request)
{
    // Validate input parameters
    // Check device permissions
    // Create bulk change record
    // Enqueue processing
}
```

## Phase 2: Rate Plan Selection & Validation

### 2.1 Rate Plan Discovery
```
Service Provider → Available Rate Plans → Customer Rate Plans
├── Query: Customer rate plan repository
├── Filter: Service provider specific plans
└── Validate: Tenant access permissions
```

### 2.2 Plan Compatibility Check
**Validation Rules**:
- Service provider compatibility
- Device type compatibility  
- Customer tier permissions
- Effective date validation

## Phase 3: Device Selection & Validation

### 3.1 Device Input Processing
**Input Methods**:
- Manual ICCID/IMEI entry
- CSV file upload
- Device list selection
- Barcode scanning integration

### 3.2 Device Validation
```csharp
// Validation pipeline
foreach (var device in selectedDevices)
{
    ValidateDeviceExists(device.OldIdentifier);
    ValidateNewIdentifierFormat(device.NewIdentifier);
    ValidatePermissions(device.ServiceProviderId);
    ValidateBusinessRules(device);
}
```

**Validation Checks**:
- Device existence in database
- Identifier format validation
- Duplicate detection
- Business rule compliance

## Phase 4: Plan Validation & Business Rules

### 4.1 Pre-Change Validation
```
Device Status Check → Rate Plan Compatibility → Customer Permissions
├── Verify: Device is changeable status
├── Check: Rate plan availability
└── Validate: Customer associations
```

### 4.2 Business Rule Engine
**Rules Applied**:
- Maximum bulk change limits
- Service provider restrictions
- Customer rate plan rules
- Scheduling constraints

## Phase 5: Bulk Change Creation

### 5.1 Master Record Creation
**Location**: BulkChangeRepository.cs
```csharp
public async Task<BulkChange> CreateBulkChangeAsync(BulkChangeRequest request)
{
    var bulkChange = new BulkChange
    {
        ServiceProviderId = request.ServiceProviderId,
        ChangeType = ChangeRequestType.ChangeIdentifier,
        Status = BulkChangeStatus.PENDING,
        CreatedDate = DateTime.UtcNow,
        PortalTypeId = (int)PortalTypes.M2M
    };
    
    // Save to database
    // Create detail records
    // Return bulk change ID
}
```

### 5.2 Detail Record Creation
```csharp
// Create individual device change records
foreach (var deviceChange in request.DeviceChanges)
{
    var detailRecord = new BulkChangeDetailRecord
    {
        BulkChangeId = bulkChange.Id,
        DeviceIdentifier = deviceChange.OldIdentifier,
        ChangeRequest = JsonConvert.SerializeObject(deviceChange),
        Status = BulkChangeStatus.PENDING
    };
    
    await CreateDetailRecordAsync(detailRecord);
}
```

## Phase 6: Queue Processing (SQS)

### 6.1 Message Enqueueing
**Location**: Function.cs - EnqueueDeviceBulkChangesAsync
```csharp
var sqsMessage = new SqsValues
{
    BulkChangeId = bulkChangeId,
    M2MDeviceChangeId = deviceChangeId,
    IsRetryUpdateIdentifier = false,
    RetryNumber = 0,
    RequestId = string.Empty
};

await EnqueueToSQS(sqsMessage, DeviceBulkChangeQueueUrl);
```

### 6.2 Queue Configuration
- **Queue Type**: Standard SQS Queue
- **Visibility Timeout**: 15 minutes
- **Message Retention**: 14 days
- **Dead Letter Queue**: Configured for failed messages
- **Batch Size**: Configurable (default: 10)

## Phase 7: Background Lambda Processing

### 7.1 Lambda Function Entry Point
**Location**: Function.cs - FunctionHandler
```csharp
public async Task<string> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
{
    foreach (var record in sqsEvent.Records)
    {
        var sqsValues = new SqsValues(lambdaContext, record);
        
        if (sqsValues.IsRetryUpdateIdentifier)
        {
            await RetryUpdateIdentifierProcess(lambdaContext, sqsValues.BulkChangeId, sqsValues);
        }
        else
        {
            await ProcessBulkChange(lambdaContext, sqsValues.BulkChangeId);
        }
    }
}
```

### 7.2 Processing Coordination
```
Lambda Trigger → Message Processing → Bulk Change Routing
├── Parse: SQS message attributes
├── Route: Processing type (new vs retry)
└── Execute: Appropriate processing method
```

## Phase 8: Authentication & Authorization

### 8.1 ThingSpace Authentication
**Location**: ProcessChangeICCIDorIMEI.cs
```csharp
// Authentication flow
var thingSpaceAuth = ThingSpaceCommon.GetThingspaceAuthenticationInformation(
    connectionString, serviceProviderId);

var accessToken = ThingSpaceCommon.GetAccessToken(thingSpaceAuth);
var sessionToken = ThingSpaceCommon.GetSessionToken(thingSpaceAuth, accessToken);
```

### 8.2 Token Management
```
Service Provider Config → Access Token → Session Token → API Calls
├── Retrieve: OAuth credentials
├── Generate: Access token (OAuth2)
├── Create: Session token
└── Attach: Authentication headers
```

**Token Lifecycle**:
- Access tokens: 1 hour validity
- Session tokens: 24 hour validity
- Automatic refresh on expiration
- Error handling for auth failures

## Phase 9: Device-by-Device Processing

### 9.1 Processing Loop
**Location**: ProcessThingSpaceChangeIdentifierAsync
```csharp
foreach (var deviceChange in deviceChanges)
{
    // Check remaining processing time
    if (context.Context.RemainingTime.TotalSeconds < RemainingTimeCutoff)
        break;
    
    // Parse change request
    var changeRequest = JsonConvert.DeserializeObject<StatusUpdateRequest<BulkChangeUpdateIdentifier>>(
        deviceChange.ChangeRequest);
    
    // Process individual device
    await ProcessSingleDeviceChange(deviceChange, changeRequest);
}
```

### 9.2 Individual Device Processing
```
Device Change → Identifier Validation → ThingSpace API Call → Response Handling
├── Extract: Old and new identifiers
├── Validate: Identifier formats
├── Build: ThingSpace request
├── Execute: API call
└── Handle: Response or error
```

## Phase 10: Database Operations

### 10.1 Identifier Update
**Location**: UpdateIdentifierForThingSpace
```csharp
// Database update stored procedure
var parameters = new List<SqlParameter>
{
    new SqlParameter("@ServiceProviderId", deviceChange.ServiceProviderId),
    new SqlParameter("@OldICCID", changeRequest.OldICCID),
    new SqlParameter("@OldIMEI", changeRequest.OldIMEI),
    new SqlParameter("@NewICCID", changeRequest.NewICCID),
    new SqlParameter("@NewIMEI", changeRequest.NewIMEI),
    new SqlParameter("@ProcessedBy", context.Context.FunctionName)
};

var result = SqlQueryHelper.ExecuteStoredProcedure(
    connectionString, "usp_UpdateIdentifierForThingSpace", parameters);
```

### 10.2 Customer Rate Plan Updates
```csharp
// Optional customer rate plan association
if (deviceChangeRequest.AddCustomerRatePlan)
{
    var ratePlanResult = await UpdateCustomerRatePlan(
        context, logRepo, bulkChangeId, deviceChange, deviceChangeRequest);
}
```

## Phase 11: Status Tracking

### 11.1 Real-time Status Updates
```
API Response → Status Determination → Database Update → Log Entry
├── Parse: ThingSpace callback
├── Determine: Success/failure status
├── Update: Device change record
└── Log: Audit entry
```

### 11.2 Status States
- **PENDING**: Initial state, awaiting processing
- **PROCESSING**: Currently being processed
- **WAITING**: Waiting for carrier callback
- **PROCESSED**: Successfully completed
- **ERROR**: Failed with error details

## Phase 12: Error Handling

### 12.1 Error Categories & Responses
```csharp
// Error handling matrix
switch (apiResult.ErrorType)
{
    case AuthenticationError:
        await HandleAuthenticationFailure(deviceChange);
        break;
    case ValidationError:
        await MarkDeviceChangeAsFailed(deviceChange, validationMessage);
        break;
    case CarrierError:
        await EnqueueForRetry(deviceChange, retryDelay);
        break;
    case SystemError:
        await LogSystemError(deviceChange, exception);
        break;
}
```

### 12.2 Retry Logic
**Retry Configuration**:
- Maximum retries: 3 attempts
- Backoff strategy: Exponential (3 minutes, 9 minutes, 27 minutes)
- Dead letter queue: After max retries exceeded
- Manual intervention: Admin tools for stuck items

## Phase 13: Completion Processing

### 13.1 Bulk Change Completion
```csharp
// Check if all devices processed
var pendingDevices = GetPendingDevices(bulkChangeId);
if (pendingDevices.Count == 0)
{
    await MarkBulkChangeComplete(bulkChangeId);
    await SendCompletionNotification(bulkChangeId);
}
```

### 13.2 Callback Processing
**ThingSpace Callback Handling**:
- Webhook endpoint receives carrier responses
- Callback data stored in ThingSpaceCallBackResponseLog
- Async processing matches callbacks to requests
- Final status determination and database updates

## Phase 14: Audit Trail Creation

### 14.1 Comprehensive Logging
**Log Types**:
- M2M Device Bulk Change Log
- Mobility Device Bulk Change Log
- ThingSpace API Request/Response Log
- Error and Exception Log

### 14.2 Audit Data Structure
```csharp
public class CreateM2MDeviceBulkChangeLog
{
    public long BulkChangeId { get; set; }
    public long M2MDeviceChangeId { get; set; }
    public string LogEntryDescription { get; set; }
    public string ProcessBy { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string RequestText { get; set; }
    public string ResponseText { get; set; }
    public bool HasErrors { get; set; }
    public BulkChangeStatus ResponseStatus { get; set; }
    public string ErrorText { get; set; }
}
```

## Phase 15: Rate Plan Activation Complete

### 15.1 Final Validation
```
Database Consistency Check → Rate Plan Activation → Completion Notification
├── Verify: All identifiers updated
├── Confirm: Rate plans activated
├── Validate: Customer associations
└── Notify: Stakeholders of completion
```

### 15.2 Post-Processing
- **Notification System**: Email/SMS notifications to administrators
- **Reporting**: Bulk change summary reports
- **Analytics**: Processing metrics and performance data
- **Cleanup**: Temporary data cleanup and archival

## Error Recovery and Monitoring

### Monitoring Points
1. **Queue Depth**: SQS message backlog monitoring
2. **Processing Time**: Lambda execution duration tracking
3. **Error Rates**: Failed device change percentages
4. **API Response Times**: ThingSpace API performance
5. **Database Performance**: Query execution monitoring

### Alert Conditions
- Queue depth exceeding threshold (>1000 messages)
- Error rate above 5% for any bulk change
- API response time exceeding 30 seconds
- Authentication failures
- Database connectivity issues

### Recovery Procedures
1. **Stuck Processing**: Manual queue message replay
2. **Authentication Issues**: Token refresh and retry
3. **API Failures**: Service status check and escalation
4. **Data Inconsistency**: Rollback and reconciliation procedures

This detailed process flow provides a comprehensive view of the Change ICCID/IMEI functionality, from initial user input through final completion and audit trail creation.