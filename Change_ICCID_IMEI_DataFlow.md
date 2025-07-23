# Change ICCID/IMEI Data Flow Diagram

## High-Level Data Flow Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   User Portal   │    │   Rate Plan     │    │   Device        │
│   Interface     │◄──►│   Selection     │◄──►│   Selection     │
└─────────┬───────┘    └─────────────────┘    └─────────────────┘
          │
          ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Validation    │    │   Bulk Change   │    │   Queue         │
│   Engine        │◄──►│   Creation      │◄──►│   Processing    │
└─────────────────┘    └─────────┬───────┘    └─────────┬───────┘
                                 │                      │
                                 ▼                      ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Database      │    │   AWS Lambda    │    │   SQS Message   │
│   Storage       │◄──►│   Processing    │◄──►│   Queue         │
└─────────────────┘    └─────────┬───────┘    └─────────────────┘
                                 │
                                 ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   ThingSpace    │    │   Authentication│    │   API Response  │
│   API           │◄──►│   & Session     │◄──►│   Processing    │
└─────────┬───────┘    └─────────────────┘    └─────────┬───────┘
          │                                            │
          ▼                                            ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Carrier       │    │   Status        │    │   Audit Trail   │
│   Processing    │◄──►│   Tracking      │◄──►│   & Logging     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Detailed Data Flow Breakdown

### 1. User Interface Layer

```
[User Input] ──► [Form Validation] ──► [Session Validation] ──► [Permission Check]
     │                │                      │                      │
     │                ▼                      ▼                      ▼
     │        ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
     │        │  Input       │    │  User        │    │  Module      │
     │        │  Sanitization│    │  Session     │    │  Access      │
     │        └──────────────┘    └──────────────┘    └──────────────┘
     │
     ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Data Transformation                          │
│                                                                 │
│  Form Data ──► BulkChangeRequest ──► JSON Serialization        │
│                                                                 │
│  • Device Identifiers (ICCID/IMEI)                           │
│  • Change Type Selection                                      │
│  • Rate Plan Information                                      │
│  • Customer Association Data                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Validation and Business Logic Layer

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Device        │    │   Identifier    │    │   Business      │
│   Existence     │    │   Format        │    │   Rules         │
│   Validation    │    │   Validation    │    │   Engine        │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          ▼                      ▼                      ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Validation Results                           │
│                                                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│  │   Valid     │  │   Warning   │  │   Error     │            │
│  │   Devices   │  │   Conditions│  │   Conditions│            │
│  └─────────────┘  └─────────────┘  └─────────────┘            │
│                                                                 │
│  Success Path ──► Continue Processing                          │
│  Error Path   ──► Return Validation Errors                     │
└─────────────────────────────────────────────────────────────────┘
```

### 3. Database Transaction Layer

```
[Bulk Change Creation] ──► [Detail Record Creation] ──► [Status Initialization]
         │                           │                           │
         ▼                           ▼                           ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   BulkChange     │    │   BulkChange     │    │   Initial        │
│   Master Record  │    │   DetailRecord   │    │   Status         │
│                  │    │   (per device)   │    │                  │
│   • ID           │    │   • DeviceId     │    │                  │
│   • Type         │    │   • ChangeReq    │    │   • Created      │
│   • Status       │    │   • Status       │    │   • Queued       │
│   • Created      │    │   • ServiceProv  │    │   • Processing   │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

### 4. Queue Processing Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    SQS Message Structure                        │
│                                                                 │
│  {                                                              │
│    "BulkChangeId": 12345,                                      │
│    "M2MDeviceChangeId": 67890,                                 │
│    "IsRetryUpdateIdentifier": false,                           │
│    "RetryNumber": 0,                                           │
│    "RequestId": ""                                             │
│  }                                                              │
└─────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Message       │    │   Lambda        │    │   Processing    │
│   Dequeue       │──► │   Trigger       │──► │   Router        │
└─────────────────┘    └─────────────────┘    └─────────┬───────┘
                                                        │
                                                        ▼
                              ┌─────────────────┐    ┌─────────────────┐
                              │   New Request   │    │   Retry         │
                              │   Processing    │    │   Processing    │
                              └─────────────────┘    └─────────────────┘
```

### 5. Authentication Data Flow

```
[Service Provider Config] ──► [OAuth2 Token Request] ──► [Session Token]
         │                             │                         │
         ▼                             ▼                         ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Credentials    │    │   Access Token   │    │   Session Token  │
│                  │    │                  │    │                  │
│   • Client ID    │    │   • Bearer Token │    │   • Session ID   │
│   • Secret       │    │   • Expires In   │    │   • Valid 24hrs  │
│   • API URLs     │    │   • Token Type   │    │   • API Access   │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                 │                         │
                                 └─────────┬───────────────┘
                                           │
                                           ▼
                              ┌─────────────────────────┐
                              │   API Request Headers   │
                              │                         │
                              │   Authorization: Bearer │
                              │   VZ-M2M-Token: Session │
                              └─────────────────────────┘
```

### 6. ThingSpace API Data Flow

```
[Device Change Request] ──► [API Request Builder] ──► [ThingSpace API]
         │                           │                        │
         ▼                           ▼                        ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Change Data    │    │   API Payload    │    │   HTTP Request   │
│                  │    │                  │    │                  │
│   • Old ICCID    │    │   {              │    │   PUT /update    │
│   • New ICCID    │    │     "deviceIds": │    │   Content-Type:  │
│   • Change Type  │    │     "deviceIdsTo"│    │   application/   │
│   • Service ID   │    │     "change4g.." │    │   json           │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                 │                        │
                                 └─────────┬──────────────┘
                                           │
                                           ▼
                              ┌─────────────────────────┐
                              │   API Response          │
                              │                         │
                              │   • Request ID          │
                              │   • Status Code         │
                              │   • Response Body       │
                              │   • Callback Info       │
                              └─────────────────────────┘
```

### 7. Callback Processing Data Flow

```
[ThingSpace Callback] ──► [Webhook Endpoint] ──► [Callback Storage]
         │                        │                       │
         ▼                        ▼                       ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Carrier        │    │   Webhook        │    │   Callback Log   │
│   Response       │    │   Processing     │    │   Table          │
│                  │    │                  │    │                  │
│   • Request ID   │    │   • Validation   │    │   • Request ID   │
│   • Status       │    │   • Parsing      │    │   • API Status   │
│   • Result Code  │    │   • Storage      │    │   • Response     │
│   • Details      │    │   • Notification │    │   • Timestamp    │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │   Processing Trigger    │
                    │                         │
                    │   • SQS Retry Message   │
                    │   • Status Update       │
                    │   • Completion Check    │
                    └─────────────────────────┘
```

### 8. Database Update Data Flow

```
[API Success Response] ──► [Database Update] ──► [Status Tracking]
         │                        │                    │
         ▼                        ▼                    ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Identifier     │    │   Stored         │    │   Status         │
│   Update         │    │   Procedure      │    │   Update         │
│                  │    │                  │    │                  │
│   • Old Values   │    │   usp_Update     │    │   • PROCESSED    │
│   • New Values   │    │   Identifier     │    │   • Timestamp    │
│   • Service ID   │    │   ForThingSpace  │    │   • Details      │
│   • Processed By │    │                  │    │   • Success Flag │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │   Customer Rate Plan    │
                    │   Update (Optional)     │
                    │                         │
                    │   • Plan Association    │
                    │   • Pool Assignment     │
                    │   • Effective Date      │
                    └─────────────────────────┘
```

### 9. Audit Trail Data Flow

```
[Processing Events] ──► [Log Entry Creation] ──► [Audit Storage]
         │                       │                      │
         ▼                       ▼                      ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Event Data     │    │   Log Formatter  │    │   Audit Tables   │
│                  │    │                  │    │                  │
│   • Action Type  │    │   • Timestamp    │    │   • M2M Logs     │
│   • Request Data │    │   • User Context │    │   • Mobility     │
│   • Response     │    │   • Error Info   │    │   • API Logs     │
│   • Error Info   │    │   • Correlation  │    │   • Error Logs   │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │   Searchable Audit      │
                    │   Trail                 │
                    │                         │
                    │   • Full Traceability   │
                    │   • Error Analysis      │
                    │   • Performance Metrics │
                    └─────────────────────────┘
```

### 10. Error Handling Data Flow

```
[Error Detection] ──► [Error Classification] ──► [Recovery Action]
         │                      │                       │
         ▼                      ▼                       ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Error Types    │    │   Error Handler  │    │   Recovery       │
│                  │    │                  │    │   Mechanism      │
│   • Auth Failure │    │   • Categorize   │    │   • Retry        │
│   • API Error    │    │   • Log          │    │   • Queue DLQ    │
│   • Validation   │    │   • Route        │    │   • Manual       │
│   • System Error │    │   • Alert        │    │   • Rollback     │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │   Error Resolution      │
                    │   Tracking              │
                    │                         │
                    │   • Status Updates      │
                    │   • Retry Attempts      │
                    │   • Final Disposition   │
                    └─────────────────────────┘
```

## Data Persistence Strategy

### Primary Data Stores

1. **Central Database**
   - Device records with ICCID/IMEI
   - Bulk change master and detail records
   - Customer and rate plan associations
   - User and permission data

2. **Logging Database**
   - API request/response logs
   - Processing audit trails
   - Error condition logs
   - Performance metrics

3. **Queue Storage**
   - SQS message persistence
   - Dead letter queue storage
   - Retry attempt tracking

### Data Consistency

- **ACID Transactions** for critical database updates
- **Eventual Consistency** for audit logs and metrics
- **Idempotency** for API operations and retries
- **Conflict Resolution** for concurrent modifications

## Security Data Flow

```
[User Request] ──► [Authentication] ──► [Authorization] ──► [Data Access]
         │               │                   │                   │
         ▼               ▼                   ▼                   ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   Session    │ │   User       │ │   Permission │ │   Data       │
│   Validation │ │   Identity   │ │   Matrix     │ │   Filtering  │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
                         │
                         ▼
                ┌─────────────────┐
                │   Tenant        │
                │   Isolation     │
                │                 │
                │   • Service     │
                │     Provider    │
                │   • Customer    │
                │     Scope       │
                │   • Device      │
                │     Access      │
                └─────────────────┘
```

This comprehensive data flow documentation provides a complete view of how data moves through the Change ICCID/IMEI system, from initial user input through final audit trail creation.