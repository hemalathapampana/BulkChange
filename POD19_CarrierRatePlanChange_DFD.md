# POD 19 Service Provider - Carrier Rate Plan Change Data Flow Diagram

## Overview

This document outlines the data flow diagram (DFD) for **Carrier Rate Plan Changes** specifically for **POD 19 Service Provider** in graph format. POD 19 uses the Jasper integration platform for carrier rate plan management.

## Key Components

### Data Stores
- **BulkChange**: Main bulk change record
- **BulkChangeDetailRecord**: Individual device change records
- **CarrierRatePlanUpdate**: Carrier rate plan change parameters
- **JasperAuthentication**: POD 19/Jasper authentication information
- **DeviceRepository**: Device data storage
- **LogRepository**: Audit and logging storage

### External Entities
- **Client Application**: Initiates rate plan change requests
- **Jasper API**: POD 19's carrier network API
- **AMOP Database**: Internal device management system

### Processes
- **P1**: Request Validation and Parsing
- **P2**: Authentication Verification
- **P3**: Jasper API Integration
- **P4**: Database Update
- **P5**: Logging and Audit

## Data Flow Diagram - Graph Format

```mermaid
graph TB
    %% External Entities
    Client[Client Application]
    JasperAPI[Jasper API - POD 19]
    AMOPDB[(AMOP Database)]
    
    %% Data Stores
    BulkChangeDS[(BulkChange DataStore)]
    DetailRecordDS[(BulkChangeDetailRecord DataStore)]
    AuthDS[(JasperAuthentication DataStore)]
    LogDS[(Log DataStore)]
    DeviceDS[(Device DataStore)]
    
    %% Processes
    P1[P1: Request Validation<br/>& Parsing]
    P2[P2: Authentication<br/>Verification]
    P3[P3: Jasper API<br/>Integration]
    P4[P4: Database<br/>Update]
    P5[P5: Logging<br/>& Audit]
    
    %% Data Flows
    Client -->|1. BulkChangeRequest<br/>(CarrierRatePlanUpdate)| P1
    P1 -->|2. Validated Request| BulkChangeDS
    P1 -->|3. Device Changes| DetailRecordDS
    
    P1 -->|4. Service Provider ID| P2
    P2 -->|5. Authentication Query| AuthDS
    AuthDS -->|6. Jasper Auth Info| P2
    P2 -->|7. Auth Validation Result| P3
    
    DetailRecordDS -->|8. Change Records| P3
    P3 -->|9. JasperDeviceDetail<br/>(ICCID, CarrierRatePlan,<br/>CommunicationPlan)| JasperAPI
    JasperAPI -->|10. API Response| P3
    
    P3 -->|11. Update Result| P5
    P5 -->|12. M2M Log Entry| LogDS
    
    P3 -->|13. Rate Plan Update<br/>(ICCID, CarrierRatePlan,<br/>CommPlan, TenantId)| P4
    P4 -->|14. Database Update| AMOPDB
    AMOPDB -->|15. Update Result| P4
    P4 -->|16. DB Result| P5
    P5 -->|17. AMOP Log Entry| LogDS
    
    P4 -->|18. Processing Status| DetailRecordDS
    
    %% Styling
    classDef external fill:#e1f5fe
    classDef datastore fill:#f3e5f5
    classDef process fill:#e8f5e8
    
    class Client,JasperAPI,AMOPDB external
    class BulkChangeDS,DetailRecordDS,AuthDS,LogDS,DeviceDS datastore
    class P1,P2,P3,P4,P5 process
```

## Detailed Process Flow

### Process P1: Request Validation & Parsing
**Input:** `BulkChangeRequest` with `CarrierRatePlanUpdate`
**Output:** Validated bulk change and detail records

```
Data Elements:
- ServiceProviderId: POD 19 identifier
- ChangeType: 7 (CarrierRatePlanChange)
- Devices: Array of device identifiers
- CarrierRatePlanUpdate:
  - CarrierRatePlan: Target rate plan code
  - CommPlan: Communication plan
  - EffectiveDate: Implementation date
  - PlanUuid: Unique plan identifier
  - RatePlanId: Carrier's rate plan ID
```

### Process P2: Authentication Verification
**Input:** Service Provider ID (POD 19)
**Output:** Jasper authentication credentials

```
Authentication Elements:
- WriteIsEnabled: Validation flag
- Jasper API credentials
- Service provider configuration
- Integration type verification (POD19)
```

### Process P3: Jasper API Integration
**Input:** Device change records and authentication
**Output:** Jasper API responses

```
API Request Structure:
{
  "ICCID": "device_identifier",
  "CarrierRatePlan": "target_plan_code",
  "CommunicationPlan": "comm_plan_code"
}

API Response Handling:
- Success: Continue to database update
- Error: Log failure and mark as processed with error
```

### Process P4: Database Update
**Input:** Successful Jasper API response
**Output:** Updated device records in AMOP

```
Database Operations:
- UpdateRatePlanAsync(ICCID, CarrierRatePlan, CommPlan, TenantId)
- Update device rate plan information
- Maintain data consistency
```

### Process P5: Logging & Audit
**Input:** All process results
**Output:** Comprehensive audit logs

```
Log Entry Types:
1. M2M Log Entry (Jasper API interaction)
2. M2M Log Entry (AMOP database update)
3. Process status updates
4. Error handling and tracking
```

## Data Dictionary

### BulkChangeRequest
| Field | Type | Description |
|-------|------|-------------|
| ServiceProviderId | int? | POD 19 service provider identifier |
| ChangeType | int? | 7 for Carrier Rate Plan Change |
| ProcessChanges | bool? | Execute changes flag |
| Devices | string[] | Array of device identifiers |
| CarrierRatePlanUpdate | CarrierRatePlanUpdate | Rate plan change details |

### CarrierRatePlanUpdate
| Field | Type | Description |
|-------|------|-------------|
| CarrierRatePlan | string | Target carrier rate plan code |
| CommPlan | string | Communication plan identifier |
| EffectiveDate | DateTime? | Plan change effective date |
| PlanUuid | string | Unique plan identifier |
| RatePlanId | long | Carrier's internal rate plan ID |

### BulkChangeDetailRecord
| Field | Type | Description |
|-------|------|-------------|
| Id | long | Unique change record identifier |
| DeviceIdentifier | string | Device ICCID or identifier |
| BulkChangeId | long | Parent bulk change ID |
| Status | string | Processing status |
| ChangeRequest | string | Serialized change request JSON |
| TenantId | int | Tenant scope identifier |

## Integration Specifics for POD 19

### Integration Type Routing
```csharp
case (int)IntegrationType.POD19:
    return await ProcessJasperCarrierRatePlanChange(context, logRepo, 
        bulkChange, serviceProviderId, changes);
```

### Jasper API Integration
- Uses JasperDeviceDetailService
- Requires Jasper authentication
- Supports bulk device updates
- Handles communication plan changes

### Error Handling
1. **Authentication Failures**: Invalid or missing Jasper credentials
2. **API Failures**: Jasper service unavailable or errors
3. **Database Failures**: AMOP update failures
4. **Validation Failures**: Invalid rate plan codes or device identifiers

### Logging Strategy
- **Pre-flight Checks**: Authentication and configuration validation
- **API Interactions**: Complete request/response logging
- **Database Operations**: Update success/failure tracking
- **Status Updates**: Change processing status maintenance

## Security Considerations

### Authentication
- Secure Jasper API credential management
- Write permission validation
- Service provider access control

### Data Protection
- Encrypted API communications
- Sanitized error logging
- Audit trail maintenance

### Authorization
- Tenant-level device access control
- Rate plan change permissions
- Service provider isolation

## Performance Optimization

### Batch Processing
- Multiple device changes in single bulk operation
- Efficient database transactions
- Optimized API call patterns

### Error Recovery
- Individual device failure isolation
- Partial success handling
- Retry mechanisms for transient failures

### Monitoring
- API response time tracking
- Success/failure rate monitoring
- Performance bottleneck identification