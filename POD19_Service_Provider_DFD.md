# POD 19 Service Provider - Data Flow Diagram (DFD)

## Overview

This document presents the Data Flow Diagram (DFD) for POD 19 Service Provider, which is a Jasper-based carrier integration that provides M2M (Machine-to-Machine) device management services. POD 19 operates as a specialized variant of the Jasper integration with additional validation and audit capabilities.

## System Context

POD 19 Service Provider is part of the Altaworx M2M device management platform that handles:
- Device lifecycle management
- Bulk device operations
- Customer rate plan management
- Carrier rate plan synchronization
- Status updates and monitoring

## Level 0 DFD - Context Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           POD 19 Service Provider System                        │
│                                                                                 │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐    ┌─────────────┐ │
│  │   M2M       │    │   Mobility   │    │   Customer      │    │   Billing   │ │
│  │   Portal    │◄──►│   Portal     │◄──►│   Management    │◄──►│   System    │ │
│  └─────────────┘    └──────────────┘    └─────────────────┘    └─────────────┘ │
│         ▲                   ▲                      ▲                    ▲       │
│         │                   │                      │                    │       │
│         ▼                   ▼                      ▼                    ▼       │
│  ┌─────────────────────────────────────────────────────────────────────────────┐ │
│  │                    POD 19 Service Provider                                 │ │
│  │                    (Jasper-Based Integration)                              │ │
│  └─────────────────────────────────────────────────────────────────────────────┘ │
│         ▲                   ▲                      ▲                    ▲       │
│         │                   │                      │                    │       │
│         ▼                   ▼                      ▼                    ▼       │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────────────┐    ┌─────────────┐ │
│  │   Jasper    │    │   Rev        │    │   Device        │    │   Audit     │ │
│  │   API       │    │   Customer   │    │   Database      │    │   Trail     │ │
│  │   Gateway   │    │   Service    │    │                 │    │   System    │ │
│  └─────────────┘    └──────────────┘    └─────────────────┘    └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────┘

External Entities:
• M2M Portal Users
• Mobility Portal Users  
• Customer Management System
• Billing System
• Jasper API Gateway
• Rev Customer Service
• Device Database
• Audit Trail System
```

## Level 1 DFD - Main Processes

```
                    ┌─────────────────┐
                    │   Portal Users  │
                    │   (M2M/Mobility)│
                    └─────────┬───────┘
                              │ Bulk Change Requests
                              ▼
    ┌─────────────────────────────────────────────────────┐
    │                                                     │
    │  P1: Process Bulk Change Request                    │
    │  - Validate request parameters                      │
    │  - Determine operation type                         │
    │  - Route to appropriate processor                   │
    │                                                     │
    └─────────────┬───────────────────────┬───────────────┘
                  │                       │
                  │ Validated Requests    │ Change Details
                  ▼                       ▼
    ┌─────────────────────────┐  ┌─────────────────────────┐
    │                         │  │                         │
    │  P2: Customer Rate      │  │  P3: Carrier Rate       │
    │  Plan Management        │  │  Plan Management        │
    │  - Customer billing     │  │  - Jasper API calls     │
    │  - Data allocation      │  │  - POD19 validation     │
    │  - Pool assignment      │  │  - Audit verification   │
    │                         │  │                         │
    └─────────┬───────────────┘  └─────────┬───────────────┘
              │                            │
              │ Customer Updates           │ Carrier Updates
              ▼                            ▼
    ┌─────────────────────────────────────────────────────┐
    │                                                     │
    │  P4: Device Status Management                       │
    │  - Status updates (activate/deactivate/suspend)    │
    │  - Username modifications                           │
    │  - Service activation processing                    │
    │                                                     │
    └─────────────┬───────────────────────┬───────────────┘
                  │                       │
                  │ Status Changes        │ Processing Results
                  ▼                       ▼
    ┌─────────────────────────┐  ┌─────────────────────────┐
    │                         │  │                         │
    │  P5: Logging &          │  │  P6: Queue Management   │
    │  Audit Trail            │  │  - Scheduled changes    │
    │  - M2M log entries      │  │  - Future processing    │
    │  - Mobility logs        │  │  - Retry mechanisms     │
    │  - Error tracking       │  │                         │
    │                         │  │                         │
    └─────────────────────────┘  └─────────────────────────┘

Data Stores:
D1: Device Database
D2: Bulk Change Tables  
D3: Customer Rate Plan Queue
D4: Log Tables
D5: Rev Customer Data
D6: Configuration Data
```

## Level 2 DFD - Detailed Process Flows

### P2: Customer Rate Plan Management (Detailed)

```
    ┌─────────────────┐
    │ Customer Rate   │
    │ Plan Request    │
    └─────────┬───────┘
              │
              ▼
┌─────────────────────────────────────┐
│ P2.1: Validate Customer Plan       │
│ - Check CustomerRatePlanId          │
│ - Validate data allocation          │
│ - Verify pool assignment            │
└─────────────┬───────────────────────┘
              │ Valid Plan Data
              ▼
┌─────────────────────────────────────┐
│ P2.2: Check Effective Date         │
│ - Compare with current time         │
│ - Determine immediate vs scheduled  │
└─────────────┬───────────────────────┘
              │
              ▼
     ┌────────────────┐    ┌────────────────┐
     │ Immediate      │    │ Scheduled      │
     │ Processing     │    │ Processing     │
     └────────┬───────┘    └────────┬───────┘
              │                     │
              ▼                     ▼
┌─────────────────────┐    ┌─────────────────────┐
│ P2.3: Execute SP    │    │ P2.4: Queue for     │
│ UpdateDevices       │    │ Future Processing   │
│                     │    │                     │
│ D1 ◄─────────────── │    │ D3 ◄─────────────── │
└─────────────────────┘    └─────────────────────┘
```

### P3: Carrier Rate Plan Management (POD19 Specific)

```
    ┌─────────────────┐
    │ Carrier Rate    │
    │ Plan Request    │
    └─────────┬───────┘
              │
              ▼
┌─────────────────────────────────────┐
│ P3.1: Jasper Authentication        │
│ - Retrieve POD19 credentials        │
│ - Validate write permissions       │
└─────────────┬───────────────────────┘
              │ Auth Info
              ▼
┌─────────────────────────────────────┐
│ P3.2: Process Jasper API Call      │
│ - Make carrier plan change request  │
│ - Handle API response               │
└─────────────┬───────────────────────┘
              │ API Response
              ▼
┌─────────────────────────────────────┐
│ P3.3: POD19 Audit Verification     │
│ - Call Jasper Audit Trail API      │
│ - Verify change was successful      │
│ - Special POD19 validation logic    │
└─────────────┬───────────────────────┘
              │ Verification Result
              ▼
┌─────────────────────────────────────┐
│ P3.4: Update Local Records         │
│ - Update device status              │
│ - Log success/failure               │
│                                     │
│ D1 ◄─────────────────────────────── │
│ D4 ◄─────────────────────────────── │
└─────────────────────────────────────┘
```

### P4: Device Status Management

```
    ┌─────────────────┐
    │ Status Update   │
    │ Request         │
    └─────────┬───────┘
              │
              ▼
┌─────────────────────────────────────┐
│ P4.1: Route by Integration Type     │
│ - Check if POD19                    │
│ - Route to Jasper processor         │
└─────────────┬───────────────────────┘
              │ POD19 Route
              ▼
┌─────────────────────────────────────┐
│ P4.2: Process Status Change         │
│ - Activate/Deactivate/Suspend       │
│ - Username updates with POD19 audit │
│ - Service activation processing     │
└─────────────┬───────────────────────┘
              │ Status Result
              ▼
┌─────────────────────────────────────┐
│ P4.3: Rev Customer Integration      │
│ - Update Rev customer records       │
│ - Sync billing information          │
│                                     │
│ D5 ◄─────────────────────────────── │
└─────────────┬───────────────────────┘
              │ Updated Records
              ▼
┌─────────────────────────────────────┐
│ P4.4: Generate Notifications        │
│ - Email notifications               │
│ - Status change alerts              │
└─────────────────────────────────────┘
```

## Data Store Definitions

### D1: Device Database
```
- DeviceId (Primary Key)
- ICCID (SIM Card Identifier)
- MSISDN (Phone Number)
- Status (Active, Inactive, Suspended)
- CustomerRatePlanId
- CarrierRatePlan
- ServiceProviderId
- TenantId
- LastModified
```

### D2: Bulk Change Tables
```
BulkChange:
- Id (Primary Key)
- ServiceProviderId
- IntegrationId (POD19 = Jasper variant)
- ChangeType
- ProcessChanges
- CreatedDate
- Status

BulkChangeDetailRecord:
- Id (Primary Key)
- BulkChangeId (Foreign Key)
- ICCID
- DeviceId
- ChangeRequest (JSON)
- Status
- ProcessedDate
```

### D3: Customer Rate Plan Queue
```
CustomerRatePlanDeviceQueue:
- DeviceId
- CustomerRatePlanId
- CustomerRatePoolId
- CustomerDataAllocationMB
- EffectiveDate
- PortalType
- TenantId
- QueuedDate
- ProcessedDate
```

### D4: Log Tables
```
M2MDeviceBulkChangeLog:
- BulkChangeId
- M2MDeviceChangeId
- LogEntryDescription
- ProcessBy
- RequestText
- ResponseText
- HasErrors
- ResponseStatus
- CreatedDate

MobilityDeviceBulkChangeLog:
- BulkChangeId
- MobilityDeviceChangeId
- LogEntryDescription
- ProcessBy
- RequestText
- ResponseText
- HasErrors
- ResponseStatus
- CreatedDate
```

### D5: Rev Customer Data
```
RevServiceDetail:
- ServiceId
- CustomerId
- IntegrationAuthenticationId
- ServiceStatus
- BillingAccount
- RatePlanInfo
```

### D6: Configuration Data
```
JasperAuthentication:
- ServiceProviderId
- Username
- Password
- APIEndpoint
- WriteEnabled
- POD19SpecificSettings

IntegrationType:
- POD19 = Jasper variant with audit validation
```

## POD19 Specific Features

### 1. Enhanced Audit Validation
```
┌─────────────────────────────────────┐
│ Standard Jasper Flow:               │
│ Request → API Call → Response       │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ POD19 Enhanced Flow:                │
│ Request → API Call → Response →     │
│ Audit Trail Verification →          │
│ Confirmation/Retry                  │
└─────────────────────────────────────┘
```

### 2. Username Update with Verification
```
Process Flow:
1. Submit username update to Jasper API
2. Receive API response
3. Call Jasper Audit Trail API (POD19 specific)
4. Verify username was actually changed
5. Mark as success/failure based on audit result
6. Log detailed audit information
```

### 3. Integration Points
```
POD19 integrates with:
• Jasper API Gateway (Primary carrier interface)
• Rev Customer Service (Billing integration)
• Audit Trail System (POD19 validation)
• M2M Portal (Device management UI)
• Mobility Portal (Mobile device UI)
• Queue Management System (Scheduled operations)
```

## Error Handling and Retry Logic

### Error Types in POD19:
1. **Authentication Errors**: Invalid Jasper credentials
2. **API Errors**: Jasper service unavailable or timeout
3. **Audit Verification Failures**: POD19 specific validation fails
4. **Data Validation Errors**: Invalid device data or parameters
5. **Business Rule Violations**: Write operations disabled

### Retry Strategy:
```
┌─────────────────────────────────────┐
│ Error Occurred                      │
└─────────────┬───────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│ Check Error Type & Retry Count      │
└─────────────┬───────────────────────┘
              │
              ▼
     ┌────────────────┐    ┌────────────────┐
     │ Retryable      │    │ Fatal Error    │
     │ Error          │    │                │
     └────────┬───────┘    └────────┬───────┘
              │                     │
              ▼                     ▼
┌─────────────────────┐    ┌─────────────────────┐
│ Exponential         │    │ Mark as Failed      │
│ Backoff Retry       │    │ Log Error Details   │
└─────────────────────┘    └─────────────────────┘
```

## Performance Characteristics

### Throughput Metrics:
- **Bulk Operations**: 100-1000 devices per batch
- **Processing Time**: 2-5 seconds per device (including POD19 audit)
- **Queue Processing**: Real-time + scheduled processing
- **API Rate Limits**: Jasper API throttling compliance

### Scalability Features:
- **Asynchronous Processing**: Non-blocking operations
- **Batch Processing**: Efficient bulk operations
- **Connection Pooling**: Database optimization
- **Retry Policies**: Resilient error handling

## Monitoring and Observability

### Key Metrics:
1. **Success Rate**: Percentage of successful operations
2. **Processing Time**: Average time per operation
3. **Error Rate**: Failed operations by category
4. **Queue Depth**: Pending scheduled operations
5. **POD19 Audit Failures**: Specific to POD19 validation

### Logging Strategy:
```
Log Levels:
• INFO: Successful operations
• WARN: Retry scenarios  
• ERROR: Failed operations
• DEBUG: Detailed API interactions

Log Destinations:
• M2M Portal Logs
• Mobility Portal Logs
• Central Audit Trail
• Error Tracking System
```

This DFD provides a comprehensive view of the POD 19 service provider architecture, highlighting its unique position as a Jasper-based integration with enhanced audit and validation capabilities.