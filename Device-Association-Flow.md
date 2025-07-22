# Device Association Flow - Detailed Step-by-Step Process

## Overview
This document outlines the complete flow for associating devices with customers in the M2M system, from user interaction through database updates and external API integrations.

---

## Phase 1: User Interface and Request Submission

### 1. Navigation
User accesses the Device Association screen through the M2M portal interface.

### 2. Device Selection
User selects one or more devices by their unique identifiers (ICCIDs) from the available inventory.

### 3. Customer Selection
User chooses a Rev Customer from a dropdown list of available customers they have access to.

### 4. Configuration Options
User configures the following settings:
- **Create Rev Service**: Toggle option (Yes/No) to create billing service lines in Rev.io system
- **Add Carrier Rate Plan**: Toggle option (Yes/No) to assign carrier-specific billing plans
- **Add Customer Rate Plan**: Toggle option (Yes/No) to assign customer-specific rate plans
- **Effective Date**: Optional date field - if left blank, uses device activation date

### 5. Request Submission
User submits the device association request.

**Endpoint**: `POST /M2M/AssociateCustomer`

---

## Phase 2: Initial Validation and Processing

### 6. Request Reception
System receives the association request with all selected devices and configuration options.

### 7. Permission Verification
System checks if the user has create permissions for M2M module.

### 8. Device Validation
For each selected device identifier:
- Verify device exists in the device inventory database
- Confirm device belongs to the specified service provider
- Check device is not archived or deleted
- Validate device status allows customer assignment

### 9. Customer Validation
- Verify Rev Customer exists in the system
- Confirm user has access rights to assign devices to this customer
- Retrieve customer's site information and integration authentication details

### 10. Conflict Detection
Check if devices are already assigned to other customers and have active service lines.

---

## Phase 3: Change Record Creation

### 11. Site Information Retrieval
Gather customer site details and billing information.

### 12. Authentication Setup
Retrieve integration authentication credentials for Rev.io API communication.

### 13. Request Packaging
Create structured change request containing:
- Device identifiers and details
- Customer information and site assignments
- Service creation flags and rate plan selections
- Effective dates and billing preferences

### 14. Change Record Generation
Create individual change records for each device in the bulk operation.

### 15. Bulk Change Registration
Register the entire operation as a single bulk change with unique identifier.

---

## Phase 4: Asynchronous Processing Initiation

### 16. Queue Submission
Submit the bulk change to the processing queue for asynchronous handling.

### 17. User Response
Return success confirmation with change tracking identifier to user interface.

### 18. Background Processing
Lambda function receives and begins processing the queued requests.

**Processing Queue**: AWS SQS message containing bulk change details

---

## Phase 5: Device-by-Device Processing

### 19. Change Parsing
Extract device association details from each change record.

### 20. Rev Service Creation
**Condition**: If Create Rev Service = Yes
- Prepare service line creation request for Rev.io billing system
- Include customer billing details, device numbers, service types
- Submit API request to create new service line
- **Rev.io API Endpoint**: `CreateServiceLineAsync` - Creates billing service lines
- Log API response and handle any creation errors

### 21. Database Association Updates
- Update device-tenant relationship table
- Set customer site identifier for proper billing association
- Assign customer account number for billing purposes
- Set integration authentication for Rev.io API access
- Mark device as active and not deleted

### 22. Rate Plan Assignment
**Condition**: If Add Customer Rate Plan = Yes
- Update device with customer-specific rate plan identifier
- Set customer data allocation limits if applicable
- Execute cross-provider device history update procedure
- Handle effective date scheduling for future rate plan changes

---

## Phase 6: Audit and History Management

### 23. Action History Creation
Generate audit trail entries documenting:
- Device assignment to customer
- Service line creation activities
- Rate plan assignment changes
- Processing timestamps and user information

### 24. Status Logging
Record processing results for each device including:
- Success or failure status
- API response details
- Error messages for failed operations
- Processing completion timestamps

---

## Phase 7: Bulk Operation Completion

### 25. Device History Updates
Execute stored procedures to maintain device history across provider systems.

### 26. Service Link Synchronization
Update device-to-service relationships in the database.

### 27. Status Finalization
Mark bulk change operation as completed or identify partial failures.

### 28. Optimization Trigger
Send notification to 2.0 system for performance optimization.

### 29. Completion Logging
Record final bulk operation status and completion metrics.

---

## Technical Endpoints and Components

### Primary Endpoints
- `POST /M2M/AssociateCustomer` - Main device association endpoint

### Database Procedures
- `usp_UpdateCrossProviderDeviceHistory` - Updates device history across providers
- `usp_DeviceBulkChange_RevService_UpdateM2MChange` - Marks M2M changes as processed
- `UPDATE_DEVICE_REV_SERVICE_LINKS` - Synchronizes device-service relationships

### External API Calls
- **Rev.io** `CreateServiceLineAsync` - Creates billing service lines in Rev.io system

### Processing Components
- `ProcessAssociateCustomerAsync` - Main lambda processing method
- `CreateRevServiceAsync` - Rev.io service creation handler
- `BuildAssociateCustomerDeviceChanges` - Change record builder

---

## Key Database Tables

### Device_Tenant
Updated with customer association information:
- `SiteId` - Customer's site identifier
- `AccountNumber` - Rev Customer identifier
- `AccountNumberIntegrationAuthenticationId` - Rev.io API authentication
- `IsActive` - Set to true
- `IsDeleted` - Set to false
- `CustomerRatePlanId` - Customer-specific rate plan
- `CustomerDataAllocationMB` - Data allocation limits

### DeviceActionHistory
Audit trail records for:
- Device-customer assignment actions
- Rate plan changes
- Service creation activities

### M2M_DeviceChange
Individual change tracking for:
- Processing status
- Error details
- Request/response logging

---

## Error Handling

### Validation Errors
- Device not found or archived
- Customer access permissions
- Existing service line conflicts

### Processing Errors
- Rev.io API failures
- Database update failures
- Rate plan assignment issues

### Recovery Mechanisms
- Retry policies for transient failures
- Error logging for debugging
- Partial success handling for bulk operations

---

## Success Criteria

A successful device association includes:
1. ✅ Device validated and accessible
2. ✅ Customer permissions verified
3. ✅ Rev service created (if requested)
4. ✅ Device-tenant relationship updated
5. ✅ Rate plans assigned (if requested)
6. ✅ Audit history recorded
7. ✅ Processing status completed

This flow ensures complete device-to-customer association with proper billing setup, rate plan assignment, and comprehensive audit trailing while handling errors gracefully at each step.