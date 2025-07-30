# Data Flow Diagram: EDIT USERNAME/COST CENTER Change Type - POD19 Service Provider

## Overview

This document provides a simple understanding of the data flow for editing username and cost center information for devices managed by the POD19 service provider integration. The process involves multiple systems including the M2M portal, bulk change processing, Jasper API, and Rev.IO API.

## Key Components

### 1. **Input Data Structure (BulkChangeEditUsername)**
```
┌─────────────────────────────────┐
│     BulkChangeEditUsername      │
├─────────────────────────────────┤
│ • ContactName (Username)        │
│ • CostCenter1                   │
│ • CostCenter2                   │
│ • CostCenter3                   │
└─────────────────────────────────┘
```

### 2. **System Components**
- **M2M Portal**: Web interface for bulk change requests
- **AWS Lambda**: ProcessEditUsername function
- **Jasper API**: POD19 carrier integration for username updates
- **Rev.IO API**: Billing system for cost center updates
- **Central Database**: AMOP database for device records
- **Jasper Database**: Carrier-specific device database

## Data Flow Process

### Phase 1: Request Initiation
```
[User Interface] → [M2M Controller] → [Bulk Change Repository]
      │                    │                     │
      │                    │                     ▼
      │                    │            [Central Database]
      │                    │             - Creates BulkChange record
      │                    │             - Stores device list
      │                    │             - Sets status: PENDING
      │                    ▼
      │            [AWS SQS Message]
      │                    │
      ▼                    ▼
[Change Request JSON]  [Lambda Trigger]
```

### Phase 2: Lambda Processing Initialization
```
[AWS Lambda: ProcessEditUsername]
              │
              ▼
[Context & Configuration Setup]
   ├─ BaseMultiTenantConnectionString
   ├─ TenantRepository initialization  
   ├─ BulkChange record retrieval
   └─ Device changes collection
              │
              ▼
[Integration Type Check: POD19]
              │
              ▼
[ProcessEditUsernameJasperAsync]
```

### Phase 3: Username Update Process (Jasper/POD19)
```
[Jasper Authentication Check]
              │
              ▼
        [Write Enabled?] ─── NO ──→ [ERROR: Write Disabled]
              │ YES                        │
              ▼                            ▼
[Extract Request Data]              [Log Error & Exit]
   ├─ ContactName (Username)
   ├─ CostCenter1
   ├─ CostCenter2  
   └─ CostCenter3
              │
              ▼
[For Each Device in Bulk Change]
              │
              ▼
[Jasper API Call: Update Username]
   ├─ Endpoint: JasperDeviceEditPath
   ├─ Method: PUT/POST
   ├─ Data: { username: ContactName, iccid: device.ICCID }
   └─ Authentication: Jasper credentials
              │
              ▼
[POD19 Specific Validation]
   └─ Audit Trail Check: IsEditUsernamePOD19Success
              │
              ▼
[Update Result Processing]
   ├─ Success: Continue to Cost Center
   └─ Failure: Log error, mark device as failed
```

### Phase 4: Cost Center Update Process (Rev.IO)
```
[Cost Center Check]
   └─ Any of CostCenter1, CostCenter2, CostCenter3 not empty?
              │ YES
              ▼
[Rev.IO Service Lookup]
   ├─ Find AMOP Rev Service by ICCID & TenantId
   ├─ Call revApiClient.LookupRevServiceAsync
   └─ Get service field mappings
              │
              ▼
[Field Mapping Resolution]
   ├─ Map CostCenter1 → COST_CENTER_1 or COST_CENTER
   ├─ Map CostCenter2 → COST_CENTER_2  
   └─ Map CostCenter3 → COST_CENTER_3
              │
              ▼
[Rev.IO API Call: Update Custom Fields]
   ├─ Endpoint: UpdateServiceCustomFieldAsync
   ├─ Method: API call to Rev.IO
   ├─ Data: { fieldIndex: value, ... }
   └─ Authentication: Rev.IO credentials
              │
              ▼
[Cost Center Update Result]
   ├─ Success: Continue to database update
   └─ Failure: Log error, mark as failed
```

### Phase 5: Database Update Process
```
[Database Update: UpdateUsernameDeviceForAMOP]
              │
              ▼
[Stored Procedure Call]
   ├─ Procedure: usp_Update_Username_Device
   ├─ Parameters:
   │  ├─ @ServiceProviderId
   │  ├─ @IntegrationId  
   │  ├─ @ICCID
   │  ├─ @Username (ContactName)
   │  ├─ @CostCenter1
   │  ├─ @CostCenter2
   │  ├─ @CostCenter3
   │  ├─ @PortalTypeId
   │  ├─ @ProcessedBy
   │  ├─ @BulkChangeId
   │  ├─ @MSISDN
   │  ├─ @TenantId
   │  └─ @JasperDbName
   └─ Updates both Central and Jasper databases
              │
              ▼
[Database Update Result]
   ├─ Success: Update device status to PROCESSED
   └─ Failure: Update device status to ERROR
```

### Phase 6: Logging and Completion
```
[Comprehensive Logging]
   ├─ M2MDeviceBulkChangeLog entries
   ├─ Request/Response logging
   ├─ Error tracking
   └─ Processing timestamps
              │
              ▼
[Device Status Update]
   ├─ MarkProcessedForM2MDeviceChangeAsync
   ├─ Status: PROCESSED or ERROR
   └─ Include error messages if applicable
              │
              ▼
[Bulk Change Completion]
   ├─ All devices processed
   ├─ Overall status determination
   └─ Final bulk change status update
```

## Data Flow Summary

### Complete Flow Diagram
```
┌─────────────┐    ┌──────────────┐    ┌─────────────────┐    ┌──────────────┐
│   M2M       │───▶│   Bulk       │───▶│   AWS Lambda    │───▶│   Jasper     │
│   Portal    │    │   Change     │    │   Function      │    │   API        │
│             │    │   Queue      │    │                 │    │   (POD19)    │
└─────────────┘    └──────────────┘    └─────────────────┘    └──────────────┘
                                                 │                     │
                                                 ▼                     ▼
┌─────────────┐    ┌──────────────┐    ┌─────────────────┐    ┌──────────────┐
│   Central   │◀───│   Database   │◀───│   Rev.IO API    │◀───│   Username   │
│   Database  │    │   Updates    │    │   (Cost Center) │    │   Update     │
│             │    │              │    │                 │    │   Result     │
└─────────────┘    └──────────────┘    └─────────────────┘    └──────────────┘
```

## Error Handling

### Common Error Scenarios
1. **Write Disabled**: Service provider has write operations disabled
2. **Authentication Failure**: Jasper or Rev.IO API authentication issues
3. **Device Not Found**: ICCID not found in carrier system
4. **Field Mapping Error**: Cost center fields not configured in Rev.IO
5. **Database Connection**: Connection issues to Central or Jasper databases
6. **Validation Failure**: POD19 audit trail validation fails

### Error Recovery Process
```
[Error Detected] → [Log Error Details] → [Mark Device as ERROR] → [Continue with Next Device]
                                    ↓
                            [Update Bulk Change Log]
                                    ↓
                            [Send Error Notification]
```

## Integration Points

### 1. **POD19 Specific Processing**
- Uses Jasper API framework
- Includes audit trail validation
- Specific error handling for POD19 responses

### 2. **Rev.IO Integration**
- Dynamic field mapping for cost centers
- Supports both "Cost Center" and "Cost Center 1/2/3" fields
- Custom field updates via Rev.IO API

### 3. **Database Integration**
- Updates both Central (AMOP) and Jasper databases
- Maintains data consistency across systems
- Transactional updates with rollback capability

## Performance Considerations

- **Batch Processing**: Devices processed individually but within bulk operation
- **Retry Logic**: Built-in retry policies for HTTP and SQL operations
- **Timeout Management**: Configurable timeouts for external API calls
- **Logging Optimization**: Structured logging for debugging and monitoring

## Security Features

- **Authentication**: Secure API credentials for all external systems
- **Authorization**: Service provider-specific access controls
- **Audit Trail**: Complete logging of all changes and access
- **Data Validation**: Input validation and sanitization