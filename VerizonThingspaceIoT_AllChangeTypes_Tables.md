# Verizon ThingSpace IoT - All Change Types Tables

## 1. Assign Customer Change Type

| Table Name                 | Purpose                                                | Key Fields                                                                                                                                                                |
| -------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DeviceBulkChange**       | Main bulk change tracking and management table         | `Id` (PK), `TenantId`, `Status`, `ChangeRequestTypeId`, `ChangeRequestType`, `ServiceProviderId`, `ServiceProvider`, `IntegrationId`, `Integration`, `PortalTypeId`, `CreatedBy`, `ProcessedDate` |
| **M2M_DeviceChange**       | Individual device change tracking for M2M devices      | `Id` (PK), `BulkChangeId` (FK), `DeviceId`, `ICCID`, `MSISDN`, `Status`, `ChangeRequest`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                       |
| **Mobility_DeviceChange**  | Individual device change tracking for Mobility devices | `Id` (PK), `BulkChangeId` (FK), `ICCID`, `Status`, `ChangeRequest`, `StatusDetails`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                          |
| **Device**                 | Main device information and status table               | `Id` (PK), `ICCID`, `IMEI`, `MSISDN`, `DeviceStatusId`, `ServiceProviderId`, `TenantId`, `IsActive`, `IsDeleted`, `ModifiedBy`, `ModifiedDate`, `IpAddress`             |
| **Device_Tenant**          | Device to tenant/customer assignment mapping           | `Id` (PK), `DeviceId` (FK), `TenantId`, `SiteId`, `AssignedDate`, `UnassignedDate`, `IsActive`, `CreatedBy`, `CreatedDate`                                             |
| **Site**                   | Customer site/location information                     | `Id` (PK), `TenantId`, `SiteName`, `Address`, `City`, `State`, `ZipCode`, `IsActive`, `IsDeleted`, `CreatedBy`, `CreatedDate`                                          |
| **RevCustomer**            | Rev customer information for integration               | `Id` (PK), `RevCustomerId`, `CustomerName`, `TenantId`, `IntegrationAuthenticationId`, `IsActive`, `CreatedBy`, `CreatedDate`                                         |
| **RevServiceDetail**       | Rev service details and configuration                  | `Id` (PK), `RevServiceId`, `ICCID`, `MSISDN`, `TenantId`, `ActivatedDate`, `IntegrationAuthenticationId`, `ServiceStatus`                                             |
| **M2M_DeviceBulkChangeLog** | Comprehensive logging for M2M device operations       | `Id` (PK), `BulkChangeId`, `M2MDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |

## 2. Carrier Rate Plan Change Type

| Table Name                 | Purpose                                                | Key Fields                                                                                                                                                                |
| -------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DeviceBulkChange**       | Main bulk change tracking and management table         | `Id` (PK), `TenantId`, `Status`, `ChangeRequestTypeId`, `ChangeRequestType`, `ServiceProviderId`, `ServiceProvider`, `IntegrationId`, `Integration`, `PortalTypeId`, `CreatedBy`, `ProcessedDate` |
| **M2M_DeviceChange**       | Individual device change tracking for M2M devices      | `Id` (PK), `BulkChangeId` (FK), `DeviceId`, `ICCID`, `MSISDN`, `Status`, `ChangeRequest`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                       |
| **Mobility_DeviceChange**  | Individual device change tracking for Mobility devices | `Id` (PK), `BulkChangeId` (FK), `ICCID`, `Status`, `ChangeRequest`, `StatusDetails`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                          |
| **Device**                 | Main device information and status table               | `Id` (PK), `ICCID`, `IMEI`, `MSISDN`, `DeviceStatusId`, `ServiceProviderId`, `TenantId`, `CarrierRatePlanId`, `RatePlan`, `IsActive`, `IsDeleted`, `ModifiedBy`, `ModifiedDate` |
| **JasperCarrierRatePlan**  | Jasper carrier rate plan definitions                   | `Id` (PK), `ServiceProviderId`, `RatePlanCode`, `RatePlanName`, `Description`, `IsActive`, `IsDeleted`, `CreatedBy`, `CreatedDate`                                     |
| **TelegenceCarrierRatePlan** | Telegence carrier rate plan definitions             | `Id` (PK), `ServiceProviderId`, `CarrierRatePlan`, `Description`, `EffectiveDate`, `IsActive`, `IsDeleted`, `CreatedBy`, `CreatedDate`                                |
| **ThingSpaceCarrierRatePlan** | ThingSpace carrier rate plan definitions            | `Id` (PK), `ServiceProviderId`, `RatePlanCode`, `RatePlanName`, `PlanUuid`, `IsActive`, `IsDeleted`, `CreatedBy`, `CreatedDate`                                        |
| **PondDeviceCarrierRatePlan** | Pond carrier rate plan tracking                     | `Id` (PK), `DeviceId`, `PackageId`, `PackageTypeId`, `Status`, `ServiceProviderId`, `CreatedBy`, `CreatedDate`, `IsActive`                                            |
| **M2M_DeviceBulkChangeLog** | Comprehensive logging for M2M device operations       | `Id` (PK), `BulkChangeId`, `M2MDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |

## 3. Customer Rate Plan Change Type

| Table Name                 | Purpose                                                | Key Fields                                                                                                                                                                |
| -------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DeviceBulkChange**       | Main bulk change tracking and management table         | `Id` (PK), `TenantId`, `Status`, `ChangeRequestTypeId`, `ChangeRequestType`, `ServiceProviderId`, `ServiceProvider`, `IntegrationId`, `Integration`, `PortalTypeId`, `CreatedBy`, `ProcessedDate` |
| **M2M_DeviceChange**       | Individual device change tracking for M2M devices      | `Id` (PK), `BulkChangeId` (FK), `DeviceId`, `ICCID`, `MSISDN`, `Status`, `ChangeRequest`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                       |
| **Mobility_DeviceChange**  | Individual device change tracking for Mobility devices | `Id` (PK), `BulkChangeId` (FK), `ICCID`, `Status`, `ChangeRequest`, `StatusDetails`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                          |
| **Device**                 | Main device information and status table               | `Id` (PK), `ICCID`, `IMEI`, `MSISDN`, `DeviceStatusId`, `ServiceProviderId`, `TenantId`, `CustomerRatePlanId`, `CustomerRatePoolId`, `IsActive`, `IsDeleted`, `ModifiedBy`, `ModifiedDate` |
| **CustomerRatePlan**       | Customer rate plan definitions and configuration       | `Id` (PK), `TenantId`, `ServiceProviderId`, `PlanName`, `Description`, `MonthlyRate`, `DataAllocation`, `IsActive`, `IsDeleted`, `CreatedBy`, `CreatedDate`           |
| **CustomerRatePool**       | Customer rate pool definitions                         | `Id` (PK), `TenantId`, `ServiceProviderId`, `PoolName`, `Description`, `TotalDataAllocation`, `IsActive`, `IsDeleted`, `CreatedBy`, `CreatedDate`                     |
| **Device_CustomerRatePlanOrRatePool_Queue** | Queue table for customer rate plan changes | `Id` (PK), `BulkChangeId`, `DeviceId`, `CustomerRatePlanId`, `CustomerRatePoolId`, `EffectiveDate`, `Status`, `CreatedDate`, `ProcessedDate`                          |
| **DeviceCustomerRatePlan** | Device to customer rate plan assignment history       | `Id` (PK), `DeviceId`, `CustomerRatePlanId`, `EffectiveDate`, `EndDate`, `IsActive`, `CreatedBy`, `CreatedDate`                                                       |
| **M2M_DeviceBulkChangeLog** | Comprehensive logging for M2M device operations       | `Id` (PK), `BulkChangeId`, `M2MDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |
| **Mobility_DeviceBulkChangeLog** | Comprehensive logging for Mobility device operations | `Id` (PK), `BulkChangeId`, `MobilityDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |

## 4. ICCID/IMEI Change Type

| Table Name                 | Purpose                                                | Key Fields                                                                                                                                                                |
| -------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DeviceBulkChange**       | Main bulk change tracking and management table         | `Id` (PK), `TenantId`, `Status`, `ChangeRequestTypeId`, `ChangeRequestType`, `ServiceProviderId`, `ServiceProvider`, `IntegrationId`, `Integration`, `PortalTypeId`, `CreatedBy`, `ProcessedDate` |
| **M2M_DeviceChange**       | Individual device change tracking for M2M devices      | `Id` (PK), `BulkChangeId` (FK), `DeviceId`, `ICCID`, `MSISDN`, `Status`, `ChangeRequest`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                       |
| **Mobility_DeviceChange**  | Individual device change tracking for Mobility devices | `Id` (PK), `BulkChangeId` (FK), `ICCID`, `Status`, `ChangeRequest`, `StatusDetails`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                          |
| **Device**                 | Main device information and status table               | `Id` (PK), `ICCID`, `IMEI`, `MSISDN`, `DeviceStatusId`, `ServiceProviderId`, `TenantId`, `IsActive`, `IsDeleted`, `ModifiedBy`, `ModifiedDate`                          |
| **DeviceIdentifierChangeHistory** | History of ICCID/IMEI changes for audit trail      | `Id` (PK), `DeviceId`, `OldICCID`, `NewICCID`, `OldIMEI`, `NewIMEI`, `ChangeReason`, `ChangedBy`, `ChangeDate`, `BulkChangeId`                                         |
| **TelegenceDevice**        | Telegence-specific device information                  | `Id` (PK), `SubscriberNumber`, `ICCID`, `IMEI`, `ServiceProviderId`, `BillingAccountNumber`, `FoundationAccountNumber`, `IsActive`, `IsDeleted`                       |
| **ThingSpaceCallBackResponseLog** | ThingSpace API callback response tracking         | `Id` (PK), `RequestId`, `ICCID`, `APIStatus`, `APIResponse`, `CallbackDate`, `ProcessedDate`, `BulkChangeId`                                                           |
| **JasperDevice**           | Jasper-specific device tracking                       | `Id` (PK), `ICCID`, `IMEI`, `JasperDeviceId`, `ServiceProviderId`, `Status`, `LastCommunication`, `IsActive`, `IsDeleted`                                             |
| **M2M_DeviceBulkChangeLog** | Comprehensive logging for M2M device operations       | `Id` (PK), `BulkChangeId`, `M2MDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |
| **Mobility_DeviceBulkChangeLog** | Comprehensive logging for Mobility device operations | `Id` (PK), `BulkChangeId`, `MobilityDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |

## 5. Update Device Status Change Type

| Table Name                 | Purpose                                                | Key Fields                                                                                                                                                                |
| -------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DeviceBulkChange**       | Main bulk change tracking and management table         | `Id` (PK), `TenantId`, `Status`, `ChangeRequestTypeId`, `ChangeRequestType`, `ServiceProviderId`, `ServiceProvider`, `IntegrationId`, `Integration`, `PortalTypeId`, `CreatedBy`, `ProcessedDate` |
| **M2M_DeviceChange**       | Individual device change tracking for M2M devices      | `Id` (PK), `BulkChangeId` (FK), `DeviceId`, `ICCID`, `MSISDN`, `Status`, `ChangeRequest`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                       |
| **Mobility_DeviceChange**  | Individual device change tracking for Mobility devices | `Id` (PK), `BulkChangeId` (FK), `ICCID`, `Status`, `ChangeRequest`, `StatusDetails`, `IsProcessed`, `ProcessedDate`, `IsActive`, `IsDeleted`                          |
| **Device**                 | Main device information and status table               | `Id` (PK), `ICCID`, `IMEI`, `MSISDN`, `DeviceStatusId`, `ServiceProviderId`, `TenantId`, `Status`, `IsActive`, `IsDeleted`, `ModifiedBy`, `ModifiedDate`               |
| **DeviceStatus**           | Device status definitions and configurations           | `Id` (PK), `StatusName`, `DisplayName`, `Description`, `IntegrationId`, `AllowsApiUpdate`, `IsActive`, `IsDeleted`, `CreatedBy`, `CreatedDate`                        |
| **DeviceStatusHistory**    | Historical tracking of device status changes          | `Id` (PK), `DeviceId`, `OldStatus`, `NewStatus`, `StatusReason`, `ChangeDate`, `ChangedBy`, `BulkChangeId`, `Comments`                                                |
| **ThingSpaceDeviceStatusReasonCodes** | ThingSpace status reason code mappings       | `Id` (PK), `StatusId`, `ReasonCode`, `ReasonDescription`, `IntegrationId`, `IsActive`, `CreatedBy`, `CreatedDate`                                                     |
| **JasperDeviceStatus**     | Jasper-specific device status tracking                | `Id` (PK), `ICCID`, `DeviceId`, `Status`, `StatusDate`, `ReasonCode`, `ServiceProviderId`, `LastUpdated`                                                              |
| **TelegenceDeviceStatus**  | Telegence-specific device status information          | `Id` (PK), `SubscriberNumber`, `ICCID`, `Status`, `StatusReason`, `EffectiveDate`, `ServiceProviderId`, `LastUpdated`                                                |
| **RevServiceDetail**       | Rev service status and configuration details          | `Id` (PK), `RevServiceId`, `ICCID`, `MSISDN`, `TenantId`, `ServiceStatus`, `ActivatedDate`, `DeactivatedDate`, `IntegrationAuthenticationId`                        |
| **M2M_DeviceBulkChangeLog** | Comprehensive logging for M2M device operations       | `Id` (PK), `BulkChangeId`, `M2MDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |
| **Mobility_DeviceBulkChangeLog** | Comprehensive logging for Mobility device operations | `Id` (PK), `BulkChangeId`, `MobilityDeviceChangeId`, `LogEntryDescription`, `RequestText`, `ResponseText`, `ResponseStatus`, `HasErrors`, `ErrorText`, `ProcessBy`, `ProcessedDate` |

---

## Summary Table Count by Change Type

| Change Type               | Primary Tables | Supporting Tables | Log Tables | Total Tables |
| ------------------------- | -------------- | ----------------- | ---------- | ------------ |
| Assign Customer           | 4              | 4                 | 1          | 9            |
| Carrier Rate Plan Change  | 4              | 4                 | 1          | 9            |
| Customer Rate Plan Change | 4              | 5                 | 2          | 11           |
| ICCID/IMEI Change         | 4              | 5                 | 2          | 11           |
| Update Device Status      | 4              | 7                 | 2          | 13           |

**Total Unique Tables**: 35+ (with some overlap between change types)

---

*This documentation provides a comprehensive view of all tables involved in the five major change types for Verizon ThingSpace IoT Service Provider operations.*