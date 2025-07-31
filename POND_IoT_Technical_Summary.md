# POND IoT Service Provider - Technical Implementation Summary

## Overview
This document provides technical implementation details for the POND IoT Service Provider, including code snippets, API endpoints, stored procedures, and integration points identified from the codebase analysis.

## Integration Configuration

### Service Provider Setup
```csharp
// Integration Type Enumeration
case (int)IntegrationType.Pond:
    return await ProcessPondCarrierRatePlanChange(context, logRepo, bulkChange, serviceProviderId, changes);

// Status Update Processing
case (int)IntegrationType.Pond:
    result = await ProcessPondStatusUpdateAsync(context, logRepo, bulkChange, changes,
        httpRetryPolicy, sqlRetryPolicy, revApiClient, integrationAuthenticationId);
```

### Authentication & Repository Initialization
```csharp
// Repository Initialization
var pondRepository = new PondRepository(context.CentralDbConnectionString);
var pondAuthentication = pondRepository.GetPondAuthentication(ParameterizedLog(context), context.Base64Service, serviceProviderId);

// API Service Initialization
var pondApiService = new PondApiService(pondAuthentication, new HttpRequestFactory(), context.IsProduction);

// Base URI Selection
var baseUri = pondAuthentication.SandboxURL;
if (context.IsProduction)
{
    baseUri = pondAuthentication.ProductionURL;
}
```

## API Endpoints & URLs

### Carrier Rate Plan Management
```csharp
// Add Package Endpoint
var pondAddPackageApiUrl = $"{baseUri.TrimEnd('/')}/{pondAuthentication.DistributorId}/{string.Format(URLConstants.POND_ADD_PACKAGE_END_POINT, change.ICCID)}";

// URL Constants (from codebase analysis)
URLConstants.POND_ADD_PACKAGE_END_POINT = "v1/sim/{0}/package"

// Complete URL Pattern
// {baseUri}/{distributorId}/v1/sim/{iccid}/package
```

### Service Status Management
```csharp
// Update Service Status Call
var updateServiceStatusResult = await pondApiService.UpdateServiceStatus(httpClientFactory.GetClient(), iccid, updateStatsusRequest, context.logger);

// Status Request Objects
if (changeRequest.UpdateStatus == DeviceStatusConstant.POND_ACTIVE)
{
    // Enable all service status
    updateStatsusRequest = new PondUpdateServiceStatusRequest();
}
else
{
    // Disable all service status
    updateStatsusRequest = new PondUpdateServiceStatusRequest(false);
}
```

## Code Implementation Details

### 1. Assign Customer Implementation
```csharp
// Process Flow (from AltaworxDeviceBulkChange.cs lines 490-507)
case ChangeRequestType.CustomerAssignment:
    var changeRequest = GetBulkChangeRequest(context, bulkChangeId, bulkChange.PortalTypeId);
    var request = JsonConvert.DeserializeObject<BulkChangeAssociateCustomer>(changeRequest);
    var pageSize = PageSize;
    if (request?.CreateRevService == false)
    {
        pageSize = CommonConstants.PAGE_SIZE_WHEN_NOT_CREATE_SERVICE;
    }
    var associateCustomerChanges = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, pageSize).ToList();
    
    if (string.IsNullOrEmpty(request?.RevCustomerId))
    {
        await bulkChangeRepository.UpdateAMOPCustomer(context, logRepo, associateCustomerChanges, bulkChange);
    }
    else
    {
        await ProcessAssociateCustomerAsync(context, logRepo, bulkChange, associateCustomerChanges);
    }
```

### 2. Change Carrier Rate Plan Implementation
```csharp
// Main Processing Method (AltaworxDeviceBulkChange.cs lines 1022-1060)
public async Task<bool> ProcessPondCarrierRatePlanChange(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, int serviceProviderId, ICollection<BulkChangeDetailRecord> changes)
{
    LogInfo(context, CommonConstants.SUB, "");

    var pondRepository = new PondRepository(context.CentralDbConnectionString);
    var pondAuthentication = pondRepository.GetPondAuthentication(ParameterizedLog(context), context.Base64Service, serviceProviderId);
    var processedBy = context.Context.FunctionName;
    
    // Authentication validation
    var authenticationErrorMessage = string.Empty;
    if (pondAuthentication == null)
    {
        authenticationErrorMessage = string.Format(LogCommonStrings.FAILED_GET_AUTHENTICATION_INFORMATION, CommonConstants.POND_CARRIER_NAME);
    }
    else if (!pondAuthentication.WriteIsEnabled)
    {
        authenticationErrorMessage = LogCommonStrings.SERVICE_PROVIDER_IS_DISABLED;
    }

    if (!string.IsNullOrWhiteSpace(authenticationErrorMessage))
    {
        // Log error and return false
        LogInfo(context, CommonConstants.WARNING, authenticationErrorMessage);
        var firstChange = changes.FirstOrDefault();
        var logEntry = string.Format(LogCommonStrings.FAILED_TO_UPDATE, PondHelper.CommonString.POND_CARRIER_RATE_PLAN);
        if (firstChange != null)
        {
            logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog(bulkChange.Id, authenticationErrorMessage, firstChange.Id, processedBy, BulkChangeStatus.ERROR, firstChange.ChangeRequest, true, logEntry));
        }
        return false;
    }

    var pondApiService = new PondApiService(pondAuthentication, new HttpRequestFactory(), context.IsProduction);
    var baseUri = pondAuthentication.SandboxURL;
    if (context.IsProduction)
    {
        baseUri = pondAuthentication.ProductionURL;
    }
    await UpdatePondCarrierRatePlanForDevices(context, logRepo, bulkChange, serviceProviderId, changes, pondRepository, pondAuthentication, processedBy, pondApiService, baseUri);

    return true;
}

// Device Processing Logic (lines 1062-1120)
public async Task UpdatePondCarrierRatePlanForDevices(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, int serviceProviderId, ICollection<BulkChangeDetailRecord> changes, PondRepository pondRepository, PondAuthentication pondAuthentication, string processedBy, PondApiService pondApiService, string baseUri)
{
    foreach (var change in changes)
    {
        if (context.Context.RemainingTime.TotalSeconds < RemainingTimeCutoff)
        {
            break;
        }
        try
        {
            var existingPackageIds = pondRepository.GetExistingPackages(ParameterizedLog(context), change.ICCID, serviceProviderId, PondHelper.PackageStatus.ACTIVE);
            var carrierRatePlan = JsonConvert.DeserializeObject<BulkChangeCarrierRatePlanUpdate>(change.ChangeRequest);
            
            // Add new package
            var pondAddPackageResponse = await AddNewPondPackage(logRepo, bulkChange, pondAuthentication, pondApiService, baseUri, change, carrierRatePlan);
            
            if (!pondAddPackageResponse.HasErrors)
            {
                var pondPackage = JsonConvert.DeserializeObject<PondDeviceCarrierRatePlanResponse>(pondAddPackageResponse.ResponseObject);
                
                // Activate new package
                var updateStatusResult = await UpdateStatusForNewPondPackageOnApi(logRepo, bulkChange, pondAuthentication, pondApiService, baseUri, change, pondAddPackageResponse, pondPackage.PackageId, PondHelper.PackageStatus.ACTIVE);
                
                if (!updateStatusResult.HasErrors && existingPackageIds.Any())
                {
                    // Terminate existing packages
                    updateStatusResult = await TerminateExistingPackages(context, existingPackageIds, pondApiService, pondAuthentication, baseUri, processedBy, pondRepository, bulkChange.Id, change.Id, logRepo);
                }
                
                if (!updateStatusResult.HasErrors)
                {
                    // Save to database
                    SaveNewPondDeviceCarrierRatePlanToDatabase(context, logRepo, bulkChange, serviceProviderId, pondRepository, processedBy, change, pondPackage);
                }
                else
                {
                    // Terminate the new package if existing termination failed
                    updateStatusResult = await UpdateStatusForNewPondPackageOnApi(logRepo, bulkChange, pondAuthentication, pondApiService, baseUri, change, pondAddPackageResponse, pondPackage.PackageId, PondHelper.PackageStatus.TERMINATED);
                }
            }
        }
        catch (Exception ex)
        {
            LogInfo(context, CommonConstants.EXCEPTION, $"{PondHelper.CommonString.POND_CARRIER_RATE_PLAN}: {ex.Message} - {ex.StackTrace}");
            var errorMessage = $"{string.Format(LogCommonStrings.FAILED_TO_UPDATE, PondHelper.CommonString.POND_CARRIER_RATE_PLAN)}: {LogCommonStrings.DATABASE}.";
            logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog(bulkChange.Id, errorMessage, change.Id, processedBy, BulkChangeStatus.ERROR, change.ChangeRequest, true, errorMessage));
        }
    }
}
```

### 3. Customer Rate Plan Implementation
```csharp
// Processing Flow (lines 487-489)
case ChangeRequestType.CustomerRatePlanChange:
    await ProcessCustomerRatePlanChangeAsync(context, logRepo, bulkChange, sqlRetryPolicy);
    return false;

// Database Update Logic
var ratePlanChangeResult = await ProcessCustomerRatePlanChangeAsync(bulkChange.Id,
    customerRatePlanIdToSubmit, requestChangeCusRP.EffectiveDate, null, customerRatePoolIdToSubmit,
    context.CentralDbConnectionString, context.logger, sqlRetryPolicyCustomerRatePlan);
```

### 4. Update Device Status Implementation
```csharp
// Status Update Processing (lines 2771-2881)
private async Task<bool> ProcessPondStatusUpdateAsync(KeySysLambdaContext context, DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, ICollection<BulkChangeDetailRecord> changes, IAsyncPolicy httpRetryPolicy, IAsyncPolicy sqlRetryPolicy, RevioApiClient revApiClient, int integrationAuthenticationId)
{
    var pondRepository = new PondRepository(context.CentralDbConnectionString, context.logger);
    var base64Service = new Base64Service();
    var pondAuthentication = pondRepository.GetPondAuthentication(ParameterizedLog(context), base64Service, bulkChange.ServiceProviderId);

    var pondApiService = new PondApiService(pondAuthentication, _httpRequestFactory, context.IsProduction);
    
    if (!pondAuthentication.WriteIsEnabled)
    {
        string message = string.Format(LogCommonStrings.WRITE_IS_DISABLED_FOR_SERVICE_PROVIDER_ID, bulkChange.ServiceProviderId);
        LogInfo(context, CommonConstants.WARNING, message);
        
        var change = changes.First();
        var changeRequest = JsonConvert.DeserializeObject<StatusUpdateRequest<dynamic>>(change.ChangeRequest);
        
        logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
        {
            BulkChangeId = bulkChange.Id,
            ErrorText = message,
            HasErrors = true,
            LogEntryDescription = LogCommonStrings.UPDATE_POND_STATUS_WITH_POND_API,
            M2MDeviceChangeId = change.Id,
            ProcessBy = LogCommonStrings.ALTAWORX_DEVICE_BULK_CHANGE,
            ProcessedDate = DateTime.UtcNow,
            ResponseStatus = BulkChangeStatus.ERROR,
            RequestText = JsonConvert.SerializeObject(change),
            ResponseText = message
        });
        
        MarkProcessed(context, bulkChange.Id, change.Id, false, changeRequest.PostUpdateStatusId, message);
        return false;
    }

    foreach (var change in changes)
    {
        if (context.Context.RemainingTime.TotalSeconds < RemainingTimeCutoff)
        {
            return true;
        }

        var changeRequest = JsonConvert.DeserializeObject<StatusUpdateRequest<dynamic>>(change.ChangeRequest);
        var iccid = change.DeviceIdentifier;

        PondUpdateServiceStatusRequest updateStatsusRequest;
        if (changeRequest.UpdateStatus == DeviceStatusConstant.POND_ACTIVE)
        {
            // Enable all service status
            updateStatsusRequest = new PondUpdateServiceStatusRequest();
        }
        else
        {
            // Disable all service status
            updateStatsusRequest = new PondUpdateServiceStatusRequest(false);
        }

        // Update service statuses
        var updateServiceStatusResult = await pondApiService.UpdateServiceStatus(httpClientFactory.GetClient(), iccid, updateStatsusRequest, context.logger);

        string responseStatus = BulkChangeStatus.PROCESSED;
        if (updateServiceStatusResult.HasErrors)
        {
            responseStatus = BulkChangeStatus.ERROR;
        }

        logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
        {
            BulkChangeId = bulkChange.Id,
            ErrorText = updateServiceStatusResult.ResponseObject,
            HasErrors = updateServiceStatusResult.HasErrors,
            LogEntryDescription = LogCommonStrings.UPDATE_POND_STATUS_WITH_POND_API,
            M2MDeviceChangeId = change.Id,
            ProcessBy = LogCommonStrings.ALTAWORX_DEVICE_BULK_CHANGE,
            ProcessedDate = DateTime.UtcNow,
            ResponseStatus = responseStatus,
            RequestText = updateServiceStatusResult.ActionText + Environment.NewLine + updateServiceStatusResult.RequestObject,
            ResponseText = updateServiceStatusResult.ResponseObject
        });

        // Process rev service if successful
        if (!updateServiceStatusResult.HasErrors)
        {
            await ProcessRevServiceCreation<ThingSpaceStatusUpdateRequest>(context, logRepo, httpRetryPolicy, sqlRetryPolicy,
                revApiClient, bulkChange, new List<BulkChangeDetailRecord>() { change }, integrationAuthenticationId);
        }

        // Mark item processed
        MarkProcessed(context, bulkChange.Id, change.Id, !updateServiceStatusResult.HasErrors, changeRequest.PostUpdateStatusId,
            updateServiceStatusResult.ResponseObject);
    }
    return true;
}
```

## Stored Procedures

### Customer Assignment
```sql
-- From codebase analysis
usp_DeviceBulkChange_Assign_Non_Rev_Customer
usp_DeviceBulkChange_RevService_UpdateM2MChange
usp_DeviceBulkChange_RevService_UpdateMobilityChange
```

### Customer Rate Plan Changes
```sql
-- From codebase analysis  
usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDevices
usp_DeviceBulkChange_CustomerRatePlanChange_UpdateDeviceByNumber
```

### Device Status Updates
```sql
-- From codebase analysis
usp_DeviceBulkChange_StatusUpdate_UpdateDeviceRecords
usp_DeviceBulkChange_UpdateMobilityDeviceChange
```

### POND Repository Methods
```csharp
// Database operations identified in code
pondRepository.GetPondAuthentication(ParameterizedLog(context), context.Base64Service, serviceProviderId);
pondRepository.GetExistingPackages(ParameterizedLog(context), change.ICCID, serviceProviderId, PondHelper.PackageStatus.ACTIVE);
pondRepository.AddDeviceCarrierRatePlan(ParameterizedLog(context), pondPackage, serviceProviderId, processedBy);
pondRepository.UpdateDeviceCarrierRatePlanStatus(ParameterizedLog(context), string.Join(",", existingPackageIds.ToArray()), PondHelper.PackageStatus.TERMINATED, processedBy);
```

## Constants & Helper Classes

### POND Helper Constants
```csharp
// From code analysis
CommonConstants.POND_CARRIER_NAME
PondHelper.CommonString.POND_CARRIER_RATE_PLAN
PondHelper.PackageStatus.ACTIVE
PondHelper.PackageStatus.TERMINATED
DeviceStatusConstant.POND_ACTIVE
```

### Log Message Constants
```csharp
// Logging constants identified
LogCommonStrings.FAILED_GET_AUTHENTICATION_INFORMATION
LogCommonStrings.SERVICE_PROVIDER_IS_DISABLED
LogCommonStrings.FAILED_TO_UPDATE
LogCommonStrings.POND_ADD_CARRIER_RATE_PLAN
LogCommonStrings.POND_UPDATE_CARRIER_RATE_PLAN_STATUS
LogCommonStrings.UPDATE_POND_STATUS_WITH_POND_API
LogCommonStrings.ALTAWORX_DEVICE_BULK_CHANGE
```

## Change Request Types

### Supported Operations
```csharp
// From switch statement analysis (lines 476-522)
ChangeRequestType.StatusUpdate
ChangeRequestType.CustomerRatePlanChange  
ChangeRequestType.CustomerAssignment
ChangeRequestType.CarrierRatePlanChange
```

### Request Models
```csharp
// Request object types identified
BulkChangeAssociateCustomer
BulkChangeCarrierRatePlanUpdate
BulkChangeCustomerRatePlanUpdate  
StatusUpdateRequest<dynamic>
PondUpdateServiceStatusRequest
```

## Error Handling & Resilience

### Retry Policies
```csharp
// Retry policy initialization
var httpRetryPolicy = GetHttpRetryPolicy(context);
var sqlRetryPolicy = GetSqlTransientRetryPolicy(context);
var sqlRetryPolicyCustomerRatePlan = GetSqlTransientRetryPolicy(context);
```

### Error Logging Pattern
```csharp
// Standard error logging structure
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    ErrorText = errorMessage,
    HasErrors = true,
    LogEntryDescription = logEntry,
    M2MDeviceChangeId = change.Id,
    ProcessBy = processedBy,
    ProcessedDate = DateTime.UtcNow,
    ResponseStatus = BulkChangeStatus.ERROR,
    RequestText = requestText,
    ResponseText = responseText
});
```

## Environment Configuration

### URL Configuration
```csharp
// Environment-based URL selection
var baseUri = pondAuthentication.SandboxURL;
if (context.IsProduction)
{
    baseUri = pondAuthentication.ProductionURL;
}
```

### Authentication Validation
```csharp
// Write permission validation
if (!pondAuthentication.WriteIsEnabled)
{
    authenticationErrorMessage = LogCommonStrings.SERVICE_PROVIDER_IS_DISABLED;
}
```

This technical summary provides the complete implementation details for the POND IoT Service Provider based on the actual codebase analysis, showing how all four change types are implemented with their respective API calls, database operations, and error handling mechanisms.