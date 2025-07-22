# Device Association Flow - Point-wise Algorithm

## Phase 1: User Request
1. User navigates to Device Association screen
2. User selects devices using their unique identifiers
3. User selects Rev Customer from dropdown menu
4. User configures options:
   - Create Rev Service: Yes/No
   - Add Carrier Rate Plan: Yes/No
   - Add Customer Rate Plan: Yes/No
   - Effective Date (optional)
5. User submits device association request

**Primary Endpoint**: `POST /M2M/AssociateCustomer`

## Phase 2: Initial System Processing
6. System receives device association request
7. System performs validation checks:
   a. Verify all device identifiers exist in Device table
   b. Validate Rev Customer exists and user has access
   c. Check devices are not already assigned to another customer in Device_Tenant table
   d. Validate device status allows assignment
8. System creates change tracking records:
   a. Get Site information for customer from Sites table
   b. Get authentication credentials for Rev billing system
   c. Create structured change request with all device details
   d. Create individual device change records in M2M_DeviceChange table

## Phase 3: Background Processing
9. Background processor handles the request queue
10. For each device in the request:
    a. Extract device association details from change request
    
    b. IF Create Rev Service = Yes:
       - Send request to Rev billing system to create service line
       - Include customer billing details:
         * Customer identifier from Rev system
         * Device phone number or identifier
         * Service type, effective date, and other billing details
       - Record result of billing system call
    
    c. Update Device_Tenant table with:
       - Set SiteId = customer's location identifier
       - Set AccountNumber = Rev Customer identifier
       - Set authentication information for billing system access
       - Set IsActive = true, IsDeleted = false
    
    d. IF Add Customer Rate Plan = Yes:
       - Update Device_Tenant table CustomerRatePlanId field
       - Update Device_Tenant table CustomerDataAllocationMB field
       - Execute usp_UpdateCrossProviderDeviceHistory stored procedure
    
    e. Create DeviceActionHistory table record for audit trail

## Phase 4: Final Database Updates
11. Execute final database operations:
    a. Save all Device_Tenant table updates
    b. Update DeviceBulkChangeLog table with processing results
    c. Update device history across all provider systems
    d. Send notification to newer system version for optimization

## URLs and Endpoints

### Primary API Endpoints
- **Device Association**: `POST /M2M/AssociateCustomer`
- **Device Inventory**: `GET /M2M/Index`
- **Device Export**: `GET /M2M/DeviceInventoryExport`
- **Bulk Change Status**: `GET /M2M/BulkChangeStatus/{id}`
- **Rev Customer List**: `GET /M2M/GetRevCustomers`

### External API Endpoints
- **Rev.io Service Creation**: `POST https://api.rev.io/v1/service-lines`
- **Rev.io Authentication**: `POST https://api.rev.io/v1/auth/token`
- **Rev.io Customer Validation**: `GET https://api.rev.io/v1/customers/{customerId}`
- **Rev.io Service Products**: `GET https://api.rev.io/v1/service-products`

### Internal Processing Endpoints
- **AWS SQS Queue**: `https://sqs.{region}.amazonaws.com/{account}/device-bulk-change-queue`
- **Lambda Function**: `arn:aws:lambda:{region}:{account}:function:AltaworxDeviceBulkChange`
- **Database Connection**: `Server={server};Database=AltaworxCentral;Integrated Security=true`

### Database API Endpoints
- **Device Validation**: `GET /api/devices/validate/{iccid}`
- **Customer Sites**: `GET /api/customers/{customerId}/sites`
- **Rate Plans**: `GET /api/rate-plans/customer/{customerId}`
- **Device History**: `GET /api/devices/{deviceId}/history`

### Web Application Routes
- **M2M Portal**: `/M2M/Index`
- **Device Association Screen**: `/M2M/AssociateCustomer`
- **Bulk Change Monitor**: `/M2M/BulkChanges`
- **Device Details**: `/M2M/DeviceDetails/{deviceId}`

## Database Tables Used:
- **Device**: Stores device inventory and details
- **Device_Tenant**: Stores device ownership and billing assignments
- **Sites**: Stores customer location information
- **M2M_DeviceChange**: Tracks individual device change requests
- **DeviceActionHistory**: Audit trail of all device actions
- **DeviceBulkChangeLog**: Logs bulk operation results
- **RevCustomer**: Customer billing information

## Stored Procedures Used:
- **usp_UpdateCrossProviderDeviceHistory**: Updates device history across provider systems
- **usp_DeviceBulkChange_RevService_UpdateM2MChange**: Marks device changes as processed
- **UPDATE_DEVICE_REV_SERVICE_LINKS**: Synchronizes device-to-service relationships

## External System Calls:
- **Rev Billing System**: Creates new billing service lines for devices

## Success Indicators:
✅ Device found and validated
✅ User permissions confirmed
✅ Billing service created (if requested)
✅ Device ownership transferred
✅ Rate plans assigned (if requested)
✅ History recorded
✅ Processing completed