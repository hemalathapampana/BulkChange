# Device Association Flow - Detailed Step-by-Step Process

## Overview
This document outlines the complete flow for associating devices with customers in the M2M system, from user interaction through database updates and external API integrations.

---

## Phase 1: User Interface and Request Submission

User accesses the Device Association screen through the M2M portal interface and selects one or more devices by their unique identifiers (ICCIDs) from the available inventory. User chooses a Rev Customer from a dropdown list of available customers they have access to and configures the following settings: Create Rev Service (Yes/No) to create billing service lines in Rev.io system, Add Carrier Rate Plan (Yes/No) to assign carrier-specific billing plans, Add Customer Rate Plan (Yes/No) to assign customer-specific rate plans, and optionally sets an Effective Date (if left blank, uses device activation date). Finally, user submits the device association request.

**Endpoint**: `POST /M2M/AssociateCustomer`

---

## Phase 2: Initial Validation and Processing

System receives the association request with all selected devices and configuration options. Permission verification checks if the user has create permissions for M2M module. For each selected device identifier, the system verifies device exists in the device inventory database, confirms device belongs to the specified service provider, checks device is not archived or deleted, and validates device status allows customer assignment. Customer validation verifies Rev Customer exists in the system, confirms user has access rights to assign devices to this customer, and retrieves customer's site information and integration authentication details. The system also performs conflict detection to check if devices are already assigned to other customers and have active service lines. During this phase, the **Device** table is queried to validate device existence and status, **Device_Tenant** table is checked for existing assignments, and **RevCustomer** table is accessed to verify customer details and permissions.

---

## Phase 3: Change Record Creation

System gathers customer site details and billing information, then retrieves integration authentication credentials for Rev.io API communication. A structured change request is created containing device identifiers and details, customer information and site assignments, service creation flags and rate plan selections, and effective dates and billing preferences. Individual change records are created for each device in the bulk operation, and the entire operation is registered as a single bulk change with unique identifier. This phase creates entries in **DeviceBulkChange** table to track the overall operation, **M2M_DeviceChange** table for individual device changes, and prepares **BulkChangeAssociateCustomer** JSON payloads for processing.

---

## Phase 4: Asynchronous Processing Initiation

The bulk change is submitted to the processing queue for asynchronous handling through AWS SQS message containing bulk change details. A success confirmation with change tracking identifier is returned to user interface, and the Lambda function receives and begins processing the queued requests in the background.

**Processing Queue**: AWS SQS message containing bulk change details

---

## Phase 5: Device-by-Device Processing

Lambda function extracts device association details from each change record. If Create Rev Service is enabled, the system prepares service line creation request for Rev.io billing system including customer billing details, device numbers, and service types, then submits API request to create new service line using **Rev.io API Endpoint**: `CreateServiceLineAsync`, and logs API response handling any creation errors. The **RevService** table is updated with new service line details if creation is successful.

Database association updates modify the **Device_Tenant** table by setting customer site identifier (`SiteId`) for proper billing association, assigning customer account number (`AccountNumber`) for billing purposes, setting integration authentication (`AccountNumberIntegrationAuthenticationId`) for Rev.io API access, and marking device as active (`IsActive = true`) and not deleted (`IsDeleted = false`).

If Add Customer Rate Plan is enabled, the system updates device with customer-specific rate plan identifier (`CustomerRatePlanId`), sets customer data allocation limits (`CustomerDataAllocationMB`), executes cross-provider device history update procedure (`usp_UpdateCrossProviderDeviceHistory`), and handles effective date scheduling for future rate plan changes.

---

## Phase 6: Audit and History Management

System generates audit trail entries in **DeviceActionHistory** table documenting device assignment to customer, service line creation activities, rate plan assignment changes, and processing timestamps with user information. Status logging records processing results for each device in **DeviceBulkChangeLog** table including success or failure status, API response details, error messages for failed operations, and processing completion timestamps. The **M2M_DeviceChange** table is updated with final processing status and any error details.

---

## Phase 7: Bulk Operation Completion

System executes stored procedures (`usp_UpdateCrossProviderDeviceHistory`) to maintain device history across provider systems and updates device-to-service relationships using `UPDATE_DEVICE_REV_SERVICE_LINKS` procedure. The bulk change operation is marked as completed in **DeviceBulkChange** table or partial failures are identified. A notification is sent to 2.0 system for performance optimization, and final bulk operation status and completion metrics are recorded in **DeviceBulkChangeLog** table.

---

## Technical Endpoints and Components

**Primary Endpoints**:
- `POST /M2M/AssociateCustomer` - Main device association endpoint

**Database Procedures**:
- `usp_UpdateCrossProviderDeviceHistory` - Updates device history across providers
- `usp_DeviceBulkChange_RevService_UpdateM2MChange` - Marks M2M changes as processed
- `UPDATE_DEVICE_REV_SERVICE_LINKS` - Synchronizes device-service relationships

**External API Calls**:
- **Rev.io** `CreateServiceLineAsync` - Creates billing service lines in Rev.io system

**Processing Components**:
- `ProcessAssociateCustomerAsync` - Main lambda processing method
- `CreateRevServiceAsync` - Rev.io service creation handler
- `BuildAssociateCustomerDeviceChanges` - Change record builder

---

## Error Handling

**Validation Errors**: Device not found or archived, customer access permissions, existing service line conflicts

**Processing Errors**: Rev.io API failures, database update failures, rate plan assignment issues

**Recovery Mechanisms**: Retry policies for transient failures, error logging for debugging, partial success handling for bulk operations

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