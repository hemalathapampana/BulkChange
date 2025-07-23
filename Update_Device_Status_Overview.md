# Update Device Status - Change Type Overview

## Overview

### What (2 sentences)
The "Update Device Status" change type is a bulk operation that modifies the operational status of IoT/M2M devices across different carrier networks and service providers. This change type handles status transitions such as activating, suspending, restoring, or deactivating devices through various carrier APIs including ThingSpace, Jasper, Telegence, Teal, and Pond integrations.

### Why (2 sentences)  
This change type is essential for lifecycle management of IoT devices, allowing administrators to control device connectivity and services in bulk rather than individual device management. It enables automated device provisioning, cost control through suspension/restoration, and proper device decommissioning while maintaining audit trails and ensuring proper synchronization between carrier systems and internal databases.

### How (2 sentences)
The system processes status updates by first validating the bulk change request, then routing to the appropriate carrier-specific processor based on the integration type (ThingSpace, Jasper, etc.), where it makes API calls to update device status and subsequently updates internal databases and Rev.IO services. Each status change is logged with detailed audit information, error handling, and includes automatic retry mechanisms for failed operations, ensuring data consistency across all systems.

---

## Data Flow Diagram

```mermaid
graph TB
    A[SQS Message Received] --> B[Lambda Handler Entry Point]
    B --> C{Validate Bulk Change}
    C -->|Invalid| D[Log Error & Exit]
    C -->|Valid| E[Get Bulk Change Details]
    
    E --> F[Identify Change Type = StatusUpdate]
    F --> G[Load Device Changes from DB]
    G --> H[Parse StatusUpdateRequest]
    
    H --> I{Determine Integration Type}
    
    I -->|ThingSpace| J[ProcessThingSpaceStatusUpdateAsync]
    I -->|Jasper/POD19/TMobile/Rogers| K[ProcessJasperStatusUpdateAsync]
    I -->|Telegence| L[ProcessTelegenceStatusUpdateAsync]
    I -->|Teal| M[ProcessTealStatusUpdateAsync]
    I -->|Pond| N[ProcessPondStatusUpdateAsync]
    I -->|eBonding| O[Enqueue to eBonding Queue]
    
    %% ThingSpace Flow
    J --> J1[Get ThingSpace Authentication]
    J1 --> J2[Get Access Token]
    J2 --> J3[Get Session Token]
    J3 --> J4{For Each Device}
    J4 --> J5[Call UpdateThingSpaceDeviceStatusAsync]
    J5 --> J6{Status Type}
    J6 -->|activate| J7[ActivateThingSpaceDeviceAsync]
    J6 -->|suspend| J8[SuspendThingSpaceDeviceAsync]
    J6 -->|restore| J9[RestoreThingSpaceDeviceAsync]
    J6 -->|unsupported| J10[Log Error - Unsupported Status]
    
    J7 --> J11[ThingSpace API Call]
    J8 --> J11
    J9 --> J11
    J11 --> J12[Process API Response]
    J12 --> J13[Update Fields if Newly Activated]
    J13 --> J14[Log API Result]
    J14 --> J15[Process Rev Service Creation]
    J15 --> J16[Mark Device as Processed]
    J16 --> J17{More Devices?}
    J17 -->|Yes| J4
    J17 -->|No| J18[Set Default Site if Needed]
    
    %% Jasper Flow
    K --> K1[Get Jasper Authentication]
    K1 --> K2{For Each Device}
    K2 --> K3[Call UpdateJasperDeviceStatusAsync]
    K3 --> K4[HTTP PUT to Jasper API]
    K4 --> K5[Process API Response]
    K5 --> K6[Log API Result]
    K6 --> K7[Process Rev Service Creation]
    K7 --> K8[Mark Device as Processed]
    K8 --> K9{More Devices?}
    K9 -->|Yes| K2
    K9 -->|No| K10[Complete Jasper Processing]
    
    %% Telegence Flow
    L --> L1[Get Telegence Authentication]
    L1 --> L2{For Each Device}
    L2 --> L3[Call UpdateTelegenceDeviceStatusAsync]
    L3 --> L4[HTTP Request to Telegence API]
    L4 --> L5[Process API Response]
    L5 --> L6[Log API Result]
    L6 --> L7[Process Rev Service Creation]
    L7 --> L8[Mark Device as Processed]
    L8 --> L9{More Devices?}
    L9 -->|Yes| L2
    L9 -->|No| L10[Complete Telegence Processing]
    
    %% Common Post-Processing
    J18 --> P1[Common Post-Processing]
    K10 --> P1
    L10 --> P1
    M --> P1[Process Teal Status Update]
    N --> P1[Process Pond Status Update]
    O --> P1
    
    P1 --> P2[Update M2M Device Change Status]
    P2 --> P3[Create Audit Log Entries]
    P3 --> P4[Update Database Records]
    P4 --> P5{Any Errors Occurred?}
    
    P5 -->|Yes| P6[Mark as ERROR Status]
    P5 -->|No| P7[Mark as PROCESSED Status]
    
    P6 --> P8[Send Error Notifications if Configured]
    P7 --> P9[Send Success Notifications if Configured]
    
    P8 --> P10[Return Processing Result]
    P9 --> P10
    
    P10 --> P11{Need Retry?}
    P11 -->|Yes| P12[Requeue SQS Message with Delay]
    P11 -->|No| P13[Complete Processing]
    
    %% Data Stores
    DB1[(Central Database)]
    DB2[(Rev.IO Database)]
    API1[ThingSpace API]
    API2[Jasper API]
    API3[Telegence API]
    API4[Teal API]
    API5[Pond API]
    
    %% Database Interactions
    E -.-> DB1
    G -.-> DB1
    P2 -.-> DB1
    P4 -.-> DB1
    J15 -.-> DB2
    K7 -.-> DB2
    L7 -.-> DB2
    
    %% API Interactions
    J11 -.-> API1
    K4 -.-> API2
    L4 -.-> API3
    
    %% Queue Systems
    QUEUE1[SQS Device Bulk Change Queue]
    QUEUE2[eBonding Status Change Queue]
    
    A -.-> QUEUE1
    O -.-> QUEUE2
    P12 -.-> QUEUE1
    
    %% Logging System
    LOG1[M2M Log Entries]
    J14 -.-> LOG1
    K6 -.-> LOG1
    L6 -.-> LOG1
    P3 -.-> LOG1

    %% Styling
    classDef processBox fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef decisionBox fill:#fff3e0,stroke:#e65100,stroke-width:2px
    classDef dataStore fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef apiBox fill:#e8f5e8,stroke:#1b5e20,stroke-width:2px
    classDef queueBox fill:#fff8e1,stroke:#f57f17,stroke-width:2px
    classDef errorBox fill:#ffebee,stroke:#c62828,stroke-width:2px
    
    class A,B,E,F,G,H,J,K,L,M,N,O,P1,P2,P3,P4,P10,P12,P13 processBox
    class C,I,J4,J6,J17,K2,K9,L2,L9,P5,P11 decisionBox
    class DB1,DB2,LOG1 dataStore
    class API1,API2,API3,API4,API5 apiBox
    class QUEUE1,QUEUE2 queueBox
    class D,J10,P6,P8 errorBox
```

---

## Key Components Explained

### 1. **Entry Point**
- **SQS Message Trigger**: Lambda function triggered by SQS message containing bulk change request
- **Message Validation**: Validates bulk change ID and processing parameters
- **Status Check**: Ensures bulk change exists and is in correct processing status

### 2. **Request Processing**
- **StatusUpdateRequest Parsing**: Extracts device identifiers and target status from change request
- **Integration Detection**: Determines carrier integration type (ThingSpace, Jasper, Telegence, etc.)
- **Routing Logic**: Routes to appropriate carrier-specific processor based on integration type

### 3. **Carrier-Specific Processing**

#### **ThingSpace Integration**
- **Authentication Flow**: Multi-step authentication (access token → session token)
- **Status Operations**: 
  - `activate`: Move device from inventory to active status
  - `suspend`: Temporarily disable device connectivity
  - `restore`: Reactivate suspended device
- **Field Updates**: Updates device fields for newly activated devices
- **Site Management**: Sets default site for inventory/pending activate devices

#### **Jasper Integration**
- **Direct API Calls**: HTTP PUT requests to Jasper API endpoints
- **Status Mapping**: Maps internal status to Jasper-specific status codes
- **Authentication**: Basic authentication with encoded credentials

#### **Telegence Integration**
- **HTTP Requests**: Specific API calls for Telegence carrier network
- **Status Processing**: Handles Telegence-specific status transitions
- **Response Handling**: Processes Telegence API responses

#### **Other Integrations**
- **Teal**: Carrier-specific API integration for Teal network
- **Pond**: Dedicated processing for Pond carrier services
- **eBonding**: Queues requests for separate eBonding processor

### 4. **Status Update Operations**

| Operation | Description | Typical Flow |
|-----------|-------------|--------------|
| **Activation** | Device provisioning and connectivity enablement | Inventory → Active |
| **Suspension** | Temporary service disconnection for cost control | Active → Suspended |
| **Restoration** | Reactivation of suspended services | Suspended → Active |
| **Deactivation** | Permanent service termination | Any Status → Deactivated |

### 5. **Post-Processing**
- **Rev.IO Service Management**: Creates or updates service lines in Rev.IO system
- **Database Updates**: Synchronizes device status across internal databases
- **Audit Logging**: Comprehensive logging with request/response details
- **Error Handling**: Detailed error tracking and retry mechanisms

### 6. **Data Persistence**

#### **Central Database**
- Device status records
- M2M device change tracking
- Bulk change status management
- Service provider configurations

#### **Rev.IO Database**
- Service line creation/updates
- Customer association management
- Billing and provisioning data

#### **Audit Logging**
- M2M log entries with full request/response details
- Error tracking and debugging information
- Processing timestamps and status tracking

### 7. **Error Handling & Monitoring**

#### **Error Types**
- **API Failures**: Carrier API timeouts, authentication failures
- **Database Errors**: Connection issues, constraint violations
- **Validation Errors**: Invalid device identifiers, unsupported status transitions
- **Authentication Errors**: Token expiration, credential issues

#### **Retry Mechanisms**
- **Automatic Retry**: Configurable retry count for transient failures
- **SQS Requeuing**: Messages requeued with exponential backoff
- **Status Tracking**: PROCESSED, ERROR, PENDING status management

#### **Monitoring & Notifications**
- **Detailed Logging**: Comprehensive audit trail for troubleshooting
- **Error Notifications**: Configurable alerting for failure scenarios
- **Performance Metrics**: Processing time and success rate tracking

---

## Technical Implementation Details

### **Key Classes and Methods**
- `ProcessStatusUpdateAsync`: Main orchestrator for status update processing
- `ProcessThingSpaceStatusUpdateAsync`: ThingSpace-specific implementation
- `ProcessJasperStatusUpdateAsync`: Jasper carrier integration
- `StatusUpdateRequest<T>`: Generic request container for status updates
- `BulkChangeDetailRecord`: Individual device change tracking

### **Integration Points**
- **SQS Queues**: Message-driven processing with retry capabilities
- **Lambda Functions**: Serverless processing with timeout management
- **HTTP APIs**: RESTful integration with carrier systems
- **Database Connections**: SQL Server integration with connection pooling
- **Authentication Systems**: Multi-factor authentication for carrier APIs

### **Configuration Management**
- **Environment Variables**: Carrier API endpoints and authentication settings
- **Service Provider Configuration**: Per-tenant carrier integration settings
- **Retry Policies**: Configurable retry counts and delay intervals
- **Timeout Settings**: API call and database operation timeouts