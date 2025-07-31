# POND IoT Service Provider - Device Change Types and Database Tables

## Overview
This document outlines the four change types supported by the POND IoT Service Provider and the database tables involved in each operation.

## Change Types

### 1. Assign Customer

**Purpose**: Associates devices with customers and optionally creates Rev.IO services for billing and management purposes.

**Processing Method**: `ProcessAssociateCustomerAsync()`

**Database Tables Involved**:

| Table Name | Purpose | Operations |
|------------|---------|------------|
| **Device** | Core device information | UPDATE - Associates device with customer site |
| **M2M_DeviceChange** | Tracks bulk change requests | INSERT/UPDATE - Records change request details |
| **DeviceBulkChange** | Main bulk change record | UPDATE - Updates status to PROCESSED |
| **DeviceBulkChangeLog** | Logging for M2M portal | INSERT - Logs success/failure for each device |
| **RevCustomer** | Rev.IO customer information | SELECT - Validates customer exists |
| **RevService** | Rev.IO service records | INSERT - Creates new service if opted-in |
| **Site** | Customer site information | UPDATE - Links device to customer site |
| **CustomerRatePlan** | Customer billing plans | INSERT/UPDATE - Associates customer rate plan |
| **CustomerRatePool** | Customer rate pool assignments | INSERT/UPDATE - Associates rate pool if specified |

**Key Stored Procedures**:
- `usp_Assign_Customer_Update_Site` - Updates site associations
- `usp_DeviceBulkChange_Assign_Non_Rev_Customer` - Handles non-Rev customer assignments

---

### 2. Change Carrier Rate Plan

**Purpose**: Updates carrier-level pricing and service configurations while maintaining service continuity.

**Processing Method**: `ProcessPondCarrierRatePlanChange()`

**Database Tables Involved**:

| Table Name | Purpose | Operations |
|------------|---------|------------|
| **Device** | Core device information | UPDATE - Updates carrier rate plan association |
| **M2M_DeviceChange** | Tracks bulk change requests | INSERT/UPDATE - Records change request details |
| **DeviceBulkChange** | Main bulk change record | UPDATE - Updates status to PROCESSED |
| **DeviceBulkChangeLog** | Logging for M2M portal | INSERT - Logs API calls and results |
| **PondPackage** | Pond carrier rate plan packages | INSERT - Creates new package records |
| **PondPackageHistory** | Package status history | INSERT - Tracks package status changes |
| **CarrierRatePlan** | Carrier rate plan definitions | SELECT - Validates rate plan exists |
| **DeviceCarrierRatePlan** | Device-to-carrier rate plan mapping | UPDATE - Updates active carrier rate plan |
| **PackageTemplate** | Pond package templates | SELECT - Gets package template for new rate plan |

**Key Operations**:
- Add new Pond package via API
- Activate new package status
- Terminate existing active packages
- Update device carrier rate plan associations

---

### 3. Change Customer Rate Plan

**Purpose**: Modifies customer billing configurations and rate plan assignments for cost management.

**Processing Method**: `ProcessCustomerRatePlanChangeAsync()`

**Database Tables Involved**:

| Table Name | Purpose | Operations |
|------------|---------|------------|
| **Device** | Core device information | UPDATE - Updates customer rate plan references |
| **M2M_DeviceChange** | Tracks bulk change requests | INSERT/UPDATE - Records change request details |
| **DeviceBulkChange** | Main bulk change record | UPDATE - Updates status to PROCESSED |
| **DeviceBulkChangeLog** | Logging for M2M portal | INSERT - Logs processing results |
| **CustomerRatePlan** | Customer rate plan definitions | SELECT - Validates customer rate plan |
| **DeviceCustomerRatePlan** | Device-to-customer rate plan mapping | INSERT/UPDATE - Creates or updates associations |
| **CustomerRatePool** | Customer rate pool information | SELECT/UPDATE - Handles rate pool assignments |
| **CustomerRatePlanQueue** | Scheduled rate plan changes | INSERT - For future effective dates |
| **CustomerDataAllocation** | Data allocation tracking | UPDATE - Updates data allocation limits |

**Key Stored Procedures**:
- `usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber` - Updates customer rate plan associations
- `usp_AddCustomerRatePlanChangeToQueue` - Schedules future rate plan changes

---

### 4. Update Device Status

**Purpose**: Changes device operational status (Active, Suspended, Terminated) and updates carrier service status accordingly.

**Processing Method**: `ProcessPondStatusUpdateAsync()`

**Database Tables Involved**:

| Table Name | Purpose | Operations |
|------------|---------|------------|
| **Device** | Core device information | UPDATE - Updates DeviceStatusId and related fields |
| **M2M_DeviceChange** | Tracks bulk change requests | INSERT/UPDATE - Records change request details |
| **DeviceBulkChange** | Main bulk change record | UPDATE - Updates status to PROCESSED |
| **DeviceBulkChangeLog** | Logging for M2M portal | INSERT - Logs status update results |
| **DeviceStatus** | Device status definitions | SELECT - Validates target status |
| **PondServiceStatus** | Pond service status tracking | UPDATE - Updates service status via API |
| **RevService** | Rev.IO service records | UPDATE - Updates service status for billing |
| **DeviceStatusHistory** | Device status change history | INSERT - Tracks status change timeline |
| **ServiceStatusLog** | Service status change logging | INSERT - Logs carrier API interactions |

**Key Operations**:
- Update device status in AMOP database
- Call Pond API to update service status (enable/disable all services)
- Update Rev.IO service status for billing alignment
- Log all status transitions for audit purposes

---

## Common Infrastructure Tables

These tables are used across all change types:

| Table Name | Purpose |
|------------|---------|
| **DeviceBulkChange** | Main bulk change tracking record |
| **M2M_DeviceChange** | Individual device change records for M2M portal |
| **MobilityDeviceChange** | Individual device change records for Mobility portal |
| **DeviceBulkChangeLog** | Detailed logging for M2M operations |
| **MobilityDeviceBulkChangeLog** | Detailed logging for Mobility operations |
| **ServiceProvider** | Service provider configuration |
| **IntegrationAuthentication** | Authentication credentials for external APIs |

## Process Flow Summary

1. **Validation Phase**: Check device exists, validate rate plans/customers, verify authentication
2. **API Integration**: Call Pond/Rev.IO APIs for carrier/billing operations  
3. **Database Updates**: Update AMOP database with changes
4. **Logging**: Record success/failure in appropriate log tables
5. **Status Updates**: Mark bulk change as PROCESSED or ERROR

## Error Handling

All change types implement comprehensive error handling:
- Database transaction rollback on failures
- Detailed error logging in DeviceBulkChangeLog tables
- Retry mechanisms for transient failures
- Status tracking for troubleshooting and auditing