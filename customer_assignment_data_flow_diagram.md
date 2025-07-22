# Customer Assignment Data Flow Diagram

## Overview
This diagram shows the data flow for assigning customers to devices in the M2M (Machine-to-Machine) system.

## Data Flow Process

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   User Input    │    │  Web Interface  │    │  M2M Controller │
│                 │    │                 │    │                 │
│ • Device ICCID  │───▶│ • Form Data     │───▶│ AssociateCustomer│
│ • Customer ID   │    │ • Validation    │    │ Method          │
│ • Service Data  │    │ • UI Controls   │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Data Preparation Layer                       │
│                                                                 │
│ • BulkChangeAssociateCustomerModel                             │
│ • Device Validation (Active Status)                           │
│ • Customer Validation (RevCustomer)                           │
│ • Site Assignment Logic                                        │
│ • Permission Checks                                            │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Bulk Change Creation                         │
│                                                                 │
│ • DeviceBulkChange Entity                                      │
│ • ChangeRequestType: CustomerAssignment                       │
│ • Status: NEW                                                 │
│ • M2M_DeviceChange Records                                    │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Queue Processing                             │
│                                                                 │
│ • AWS SQS Queue                                                │
│ • Lambda Function Trigger                                     │
│ • AltaworxDeviceBulkChange.cs                                 │
│ • ProcessBulkChange Method                                     │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Change Processing Logic                      │
│                                                                 │
│ ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│ │ AMOP Customer   │  │ Rev Customer    │  │ Service         │ │
│ │ Assignment      │  │ Assignment      │  │ Activation      │ │
│ │                 │  │                 │  │                 │ │
│ │ • Non-Revenue   │  │ • Revenue       │  │ • New Service   │ │
│ │ • Site Update   │  │ • Site Update   │  │ • Rate Plan     │ │
│ └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Database Operations                          │
│                                                                 │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │              Stored Procedures                              │ │
│ │                                                             │ │
│ │ • usp_Assign_Customer_Update_Site                          │ │
│ │ • usp_DeviceBulkChange_Assign_Non_Rev_Customer             │ │
│ │ • usp_Update_Device_Rev_Service_Links                      │ │
│ │ • usp_DeviceBulkChange_Customer_Rate_Plan_Change_Update    │ │
│ └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Data Updates                                 │
│                                                                 │
│ ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│ │ Device_Tenant   │  │ M2M_Device      │  │ RevService      │ │
│ │                 │  │                 │  │                 │ │
│ │ • Customer ID   │  │ • Site ID       │  │ • Service       │ │
│ │ • Site ID       │  │ • Status        │  │   Details       │ │
│ │ • Rate Plan     │  │ • ICCID         │  │ • Rate Plan     │ │
│ │ • Allocation    │  │ • Subscriber #  │  │ • Pool ID       │ │
│ └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Integration Layer                            │
│                                                                 │
│ ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│ │ Carrier APIs    │  │ Service         │  │ Notification    │ │
│ │                 │  │ Activation      │  │ System          │ │
│ │ • ThingSpace    │  │                 │  │                 │ │
│ │ • Jasper        │  │ • Telegence     │  │ • Email Alerts  │ │
│ │ • Rate Plan     │  │ • Rate Plan     │  │ • Status Update │ │
│ │   Updates       │  │   Application   │  │ • Audit Log     │ │
│ └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Status & Logging                             │
│                                                                 │
│ • BulkChangeStatus Updates (NEW → PROCESSING → PROCESSED)      │
│ • M2MDeviceBulkChangeLog Entries                              │
│ • Error Handling & Retry Logic                                │
│ • Audit Trail Generation                                       │
│ • Success/Failure Notifications                               │
└─────────────────────────────────────────────────────────────────┘
```

## Key Data Entities

### Input Data Models
```
BulkChangeAssociateCustomerModel
├── Devices[] (ICCID array)
├── RevCustomerId
├── ServiceProviderId
├── EffectiveDate
├── CreateRevService (boolean)
├── AddCarrierRatePlan (boolean)
└── CustomerDataAllocationMB
```

### Processing Data Models
```
BulkChangeAssociateCustomer
├── ICCID
├── RevCustomerId
├── Number (Subscriber Number)
├── ServiceId
├── CustomerRatePlanId
├── CustomerRatePoolId
├── CustomerDataAllocationMB
└── EffectiveDate
```

### Database Entities
```
DeviceBulkChange
├── Id (Primary Key)
├── ChangeRequestTypeId (CustomerAssignment)
├── ServiceProviderId
├── TenantId
├── Status
├── CreatedDate
├── CreatedBy
└── M2M_DeviceChange[] (Collection)

M2M_DeviceChange
├── Id
├── BulkChangeId
├── DeviceId
├── ICCID
├── ChangeRequest (JSON)
├── IsProcessed
└── ProcessedDate
```

## Process Flow Steps

1. **Input Validation**
   - User selects devices and customer
   - System validates device status (must be active)
   - System validates customer permissions
   - System checks for existing assignments

2. **Bulk Change Creation**
   - Creates DeviceBulkChange record
   - Generates M2M_DeviceChange records for each device
   - Sets status to NEW
   - Queues for processing

3. **Queue Processing**
   - AWS Lambda picks up the bulk change
   - Processes based on ChangeRequestType.CustomerAssignment
   - Handles different scenarios (AMOP vs Rev customers)

4. **Database Updates**
   - Updates Device_Tenant table with new customer
   - Updates site assignments
   - Creates or updates RevService records
   - Applies rate plan changes

5. **External Integration**
   - Calls carrier APIs for service activation
   - Updates rate plans with service providers
   - Handles IP provisioning if required

6. **Status Updates**
   - Updates bulk change status to PROCESSED
   - Creates audit log entries
   - Sends notifications if configured

## Error Handling

- **Validation Errors**: Invalid device status, customer permissions
- **Processing Errors**: Database connection issues, carrier API failures
- **Retry Logic**: Automatic retry for transient failures
- **Status Tracking**: ERROR status with detailed error messages
- **Rollback**: Compensation logic for partial failures

## Key Files Involved

- `M2MController.cs`: Web API endpoints
- `AltaworxDeviceBulkChange.cs`: Main processing logic
- `BulkChangeRepository.cs`: Database operations
- `ProcessNewServiceActivationStatus.cs`: Service activation
- Various model classes for data transfer