# Data Flow Diagram (DFD) - POD 19 Service Provider: Edit Username/Cost Center

## Overview
This document provides a comprehensive Data Flow Diagram for the POD 19 service provider's "Edit username/cost center" functionality within the AltaworxDeviceBulkChange system.

## Level 0 DFD (Context Diagram)

```
┌─────────────────┐    Request     ┌─────────────────────────────┐    Update     ┌─────────────────┐
│                 │──────────────→ │                             │──────────────→│                 │
│   User/Client   │                │   POD 19 Edit Username/    │               │  External APIs  │
│                 │←─────────────── │   Cost Center System       │←─────────────── │  (Jasper/Rev)   │
└─────────────────┘    Response    └─────────────────────────────┘    Response   └─────────────────┘
                                                │
                                                │ Store/Retrieve
                                                ▼
                                    ┌─────────────────────────────┐
                                    │                             │
                                    │      Database Systems       │
                                    │   (Central DB, Jasper DB)   │
                                    └─────────────────────────────┘
```

## Level 1 DFD (System Processes)

```
┌─────────────────┐                    ┌─────────────────────────────┐
│                 │   1. Bulk Change   │                             │
│   User/Portal   │   Request          │    1.0 Process Request     │
│                 │──────────────────→ │    Validation               │
└─────────────────┘                    └─────────────────────────────┘
                                                      │
                                                      │ Validated Request
                                                      ▼
┌─────────────────────────────┐              ┌─────────────────────────────┐
│                             │              │                             │
│    D1: BulkChange Store     │◄─────────────│    2.0 Parse and Store     │
│                             │   2. Store   │    Bulk Change Data         │
└─────────────────────────────┘   Request    └─────────────────────────────┘
                                                      │
                                                      │ Device Changes
                                                      ▼
┌─────────────────────────────┐              ┌─────────────────────────────┐
│                             │              │                             │
│ D2: Device Change Details   │◄─────────────│    3.0 Process POD19       │
│                             │   3. Store   │    Username/Cost Updates    │
└─────────────────────────────┘   Changes    └─────────────────────────────┘
                                                      │
                                      ┌───────────────┼───────────────┐
                                      │               │               │
                                      ▼               ▼               ▼
                        ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
                        │                 │ │                 │ │                 │
                        │ 4.0 Update      │ │ 5.0 Update      │ │ 6.0 Update      │
                        │ Jasper API      │ │ Rev API         │ │ Local Database  │
                        │ (Username)      │ │ (Cost Centers)  │ │ Records         │
                        └─────────────────┘ └─────────────────┘ └─────────────────┘
                                      │               │               │
                                      ▼               ▼               ▼
                        ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
                        │                 │ │                 │ │                 │
                        │ D3: Jasper      │ │ D4: Rev API     │ │ D5: Central     │
                        │ External System │ │ External System │ │ Database        │
                        └─────────────────┘ └─────────────────┘ └─────────────────┘
                                      │               │               │
                                      └───────────────┼───────────────┘
                                                      │
                                                      ▼
                                      ┌─────────────────────────────┐
                                      │                             │
                                      │    7.0 Generate Logs       │
                                      │    and Notifications        │
                                      └─────────────────────────────┘
                                                      │
                                                      ▼
                                      ┌─────────────────────────────┐
                                      │                             │
                                      │  D6: Audit Logs &          │
                                      │  Email Notifications        │
                                      └─────────────────────────────┘
```

## Level 2 DFD (Detailed Process Flow)

### 3.0 Process POD19 Username/Cost Updates (Detailed)

```
                                ┌─────────────────────────────┐
                                │                             │
                                │   3.1 Get Authentication    │
                                │   Information               │
                                └─────────────────────────────┘
                                              │
                                              │ Auth Data
                                              ▼
                        ┌─────────────────────────────────────────────┐
                        │                                             │
                        │   3.2 Extract Request Parameters           │
                        │   (ContactName, CostCenter1-3)              │
                        └─────────────────────────────────────────────┘
                                              │
                        ┌─────────────────────┼─────────────────────┐
                        │                     │                     │
                        ▼                     ▼                     ▼
        ┌─────────────────────────┐ ┌─────────────────────────┐ ┌─────────────────────────┐
        │                         │ │                         │ │                         │
        │  3.3 Check Write        │ │  3.4 Get Device         │ │  3.5 Get Rev Service    │
        │  Permissions            │ │  Changes (ICCIDs)       │ │  Details                │
        └─────────────────────────┘ └─────────────────────────┘ └─────────────────────────┘
                        │                     │                     │
                        └─────────────────────┼─────────────────────┘
                                              │
                                              ▼
                        ┌─────────────────────────────────────────────┐
                        │                                             │
                        │   3.6 Process Each Device Change           │
                        │   (Loop through ICCID list)                │
                        └─────────────────────────────────────────────┘
                                              │
                        ┌─────────────────────┼─────────────────────┐
                        │                     │                     │
                        ▼                     ▼                     ▼
        ┌─────────────────────────┐ ┌─────────────────────────┐ ┌─────────────────────────┐
        │                         │ │                         │ │                         │
        │  3.7 Update Username    │ │  3.8 Update Cost        │ │  3.9 Update Local      │
        │  via Jasper API         │ │  Centers via Rev API    │ │  Database Records       │
        └─────────────────────────┘ └─────────────────────────┘ └─────────────────────────┘
                        │                     │                     │
                        ▼                     ▼                     ▼
        ┌─────────────────────────┐ ┌─────────────────────────┐ ┌─────────────────────────┐
        │                         │ │                         │ │                         │
        │  3.10 POD19 Audit       │ │  3.11 Rev API          │ │  3.12 SQL Stored       │
        │  Trail Verification     │ │  Response Processing    │ │  Procedure Execution    │
        └─────────────────────────┘ └─────────────────────────┘ └─────────────────────────┘
                        │                     │                     │
                        └─────────────────────┼─────────────────────┘
                                              │
                                              ▼
                        ┌─────────────────────────────────────────────┐
                        │                                             │
                        │   3.13 Generate Response and Logs          │
                        │   Mark Device Change as Processed          │
                        └─────────────────────────────────────────────┘
```

## Data Stores

| Store ID | Name | Description |
|----------|------|-------------|
| D1 | BulkChange Store | Main bulk change request records |
| D2 | Device Change Details | Individual device change records with ICCID/MSISDN |
| D3 | Jasper External System | External POD19/Jasper API for username updates |
| D4 | Rev API External System | External Rev API for cost center updates |
| D5 | Central Database | Local database storing device and service information |
| D6 | Audit Logs & Notifications | System logs and email notification records |

## Data Flow Details

### Input Data Flows

1. **Bulk Change Request**
   - Source: User/Portal
   - Content: BulkChangeEditUsername object
   - Fields: ContactName, CostCenter1, CostCenter2, CostCenter3
   - Format: JSON

2. **Authentication Data**
   - Source: Database
   - Content: Jasper authentication credentials, Rev API authentication
   - Security: Encrypted credentials

3. **Device Information**
   - Source: Database queries
   - Content: ICCID, MSISDN, RevServiceId, TenantId
   - Query: Based on service provider and tenant

### Processing Data Flows

4. **Username Update Request**
   - Target: Jasper API (POD19)
   - Content: ICCID, new ContactName
   - Method: HTTP API call
   - Verification: Audit trail check for POD19

5. **Cost Center Update Request**
   - Target: Rev API
   - Content: RevServiceId, cost center field mappings
   - Method: RevioApiClient.UpdateServiceCustomFieldAsync
   - Fields: Cost Center 1, Cost Center 2, Cost Center 3

6. **Database Update Request**
   - Target: SQL Server
   - Content: usp_Update_Username_Device stored procedure
   - Parameters: ServiceProviderId, ICCID, Username, CostCenters, etc.

### Output Data Flows

7. **API Responses**
   - Source: External APIs
   - Content: Success/failure status, response data
   - Processing: Error handling and logging

8. **Audit Logs**
   - Target: DeviceBulkChangeLog repository
   - Content: Request/response data, timestamps, error messages
   - Types: M2MDeviceBulkChangeLog entries

9. **Email Notifications**
   - Target: Amazon SES
   - Content: Username update reports
   - Recipients: Configured admin email addresses

## POD19 Specific Processing

### Key Differences from Standard Jasper Processing

1. **Audit Trail Verification**
   ```
   if (bulkChange.IntegrationId == (int)IntegrationType.POD19)
   {
       var isEditSuccess = await jasperDeviceService.IsEditUsernamePOD19Success(
           JasperDeviceAuditTrailPath, 
           change.ICCID, 
           Common.CommonString.ERROR_MESSAGE, 
           Common.CommonString.USERNAME_STRING
       );
   }
   ```

2. **Integration Type Routing**
   - POD19 uses same Jasper API endpoints as standard Jasper
   - Additional verification step for POD19 specific to ensure username update success
   - Same authentication mechanism as Jasper integration

### Error Handling

1. **Write Permission Check**
   - Validates jasperAuthentication.WriteIsEnabled
   - Returns error if write operations are disabled

2. **Connection String Validation**
   - Checks BaseMultiTenantConnectionString availability
   - Handles TenantRepository initialization errors

3. **API Response Validation**
   - Validates Jasper API responses
   - Checks Rev API responses for cost center updates
   - POD19 specific audit trail verification

4. **Database Transaction Management**
   - SQL retry policies for transient failures
   - Rollback on critical errors
   - Bulk change status management

## Security Considerations

1. **Authentication**
   - Encrypted credential storage
   - Base64 encoded authentication for APIs

2. **Authorization**
   - Service provider level permissions
   - Tenant-based access control

3. **Audit Trail**
   - Complete request/response logging
   - Timestamp tracking for all operations
   - POD19 specific verification logging

## Performance Optimizations

1. **Batch Processing**
   - Processes multiple devices in single operation
   - Configurable page size for large bulk changes

2. **Connection Management**
   - Reuses HTTP client connections
   - Database connection pooling

3. **Timeout Management**
   - Lambda execution time monitoring
   - Graceful handling of timeout scenarios

This DFD provides a comprehensive view of the POD 19 service provider's Edit username/cost center functionality, showing the complete data flow from user request through external API calls to database updates and audit logging.