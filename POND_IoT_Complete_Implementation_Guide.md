# POND IoT Service Provider - Complete Implementation Guide

## Executive Summary

This document provides the complete implementation details for POND IoT Service Provider supporting 4 bulk change operations in the M2M portal. Based on analysis of the existing codebase, the implementation status is:

✅ **Change Carrier Rate Plan** - Already implemented  
✅ **Update Device Status** - Already implemented  
🔶 **Assign Customer** - Needs POND-specific routing  
🔶 **Change Customer Rate Plan** - Needs POND-specific routing  

## Current Implementation Status

### 1. Change Carrier Rate Plan ✅ COMPLETE
**Location**: `AltaworxDeviceBulkChange.cs` lines 674, 1022-1120  
**Method**: `ProcessPondCarrierRatePlanChange()`

#### Current Flow:
1. User selects devices and target carrier rate plan
2. Frontend sends POST request with `ChangeType: "CarrierRatePlanChange"`
3. `BuildCustomerRatePlanChangeDetails()` validates:
   - ICCID exists in database
   - Carrier rate plan exists and is active
   - Device compatibility with rate plan
   - Retrieves `RatePlanId` for POND integration
4. Lambda routing: `ProcessCarrierRatePlanChangeAsync()` → `ProcessPondCarrierRatePlanChange()`
5. POND API operations:
   - Add new package via `/package/add/{iccid}`
   - Activate new package via `/package/update/{iccid}/{packageId}`
   - Terminate existing packages
   - Update database associations

### 2. Update Device Status ✅ COMPLETE
**Location**: `AltaworxDeviceBulkChange.cs` lines 2770-2820  
**Method**: `ProcessPondStatusUpdateAsync()`

#### Current Flow:
1. User selects devices and target status
2. Frontend sends POST request with `ChangeType: "StatusUpdate"`
3. `BuildStatusUpdateChangeDetails()` validates device eligibility
4. Lambda routing: `ProcessStatusUpdateAsync()` → `ProcessPondStatusUpdateAsync()`
5. POND API status update operations
6. Database status updates and logging

### 3. Assign Customer 🔶 NEEDS POND ROUTING
**Current Status**: Generic implementation exists, needs POND-specific routing

#### Current Generic Flow:
- **Location**: `AltaworxDeviceBulkChange.cs` lines 490-507
- **Method**: `ProcessAssociateCustomerAsync()`
- Routes to either `UpdateAMOPCustomer()` or generic `ProcessAssociateCustomerAsync()`

#### Required Implementation:
Need to add POND-specific customer assignment logic that integrates with POND's customer management API.

### 4. Change Customer Rate Plan 🔶 NEEDS POND ROUTING  
**Current Status**: Generic implementation exists, needs POND-specific routing

#### Current Generic Flow:
- **Location**: `AltaworxDeviceBulkChange.cs` lines 487-489, 2105-2150
- **Method**: `ProcessCustomerRatePlanChangeAsync()`
- Currently only handles database updates, no carrier-specific API calls

#### Required Implementation:
Need to add POND-specific customer rate plan change logic.

## Required Implementation Details

### POND-Specific Customer Assignment

#### 1. Update ProcessAssociateCustomerAsync Routing

Add POND-specific routing in the customer assignment flow:

```csharp
// In ProcessBulkChangeAsync method around line 490
case ChangeRequestType.CustomerAssignment:
    var changeRequest = GetBulkChangeRequest(context, bulkChangeId, bulkChange.PortalTypeId);
    var request = JsonConvert.DeserializeObject<BulkChangeAssociateCustomer>(changeRequest);
    var pageSize = PageSize;
    if (request?.CreateRevService == false)
    {
        pageSize = CommonConstants.PAGE_SIZE_WHEN_NOT_CREATE_SERVICE;
    }
    var associateCustomerChanges = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, pageSize).ToList();
    
    // Add POND-specific routing
    if (bulkChange.IntegrationId == (int)IntegrationType.Pond)
    {
        await ProcessPondCustomerAssignmentAsync(context, logRepo, bulkChange, associateCustomerChanges);
    }
    else if (string.IsNullOrEmpty(request?.RevCustomerId))
    {
        await bulkChangeRepository.UpdateAMOPCustomer(context, logRepo, associateCustomerChanges, bulkChange);
    }
    else
    {
        await ProcessAssociateCustomerAsync(context, logRepo, bulkChange, associateCustomerChanges);
    }
    return true;
```

#### 2. Implement ProcessPondCustomerAssignmentAsync Method

```csharp
private async Task ProcessPondCustomerAssignmentAsync(KeySysLambdaContext context, 
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, 
    ICollection<BulkChangeDetailRecord> changes)
{
    LogInfo(context, CommonConstants.SUB, "ProcessPondCustomerAssignmentAsync()");

    var pondRepository = new PondRepository(context.CentralDbConnectionString);
    var pondAuthentication = pondRepository.GetPondAuthentication(ParameterizedLog(context), context.Base64Service, bulkChange.ServiceProviderId);
    var processedBy = context.Context.FunctionName;
    
    if (pondAuthentication == null)
    {
        var errorMessage = string.Format(LogCommonStrings.FAILED_GET_AUTHENTICATION_INFORMATION, CommonConstants.POND_CARRIER_NAME);
        LogInfo(context, CommonConstants.WARNING, errorMessage);
        
        foreach (var change in changes)
        {
            logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog(bulkChange.Id, errorMessage, change.Id, processedBy, BulkChangeStatus.ERROR, change.ChangeRequest, true, "Failed to assign customer"));
        }
        return;
    }

    if (!pondAuthentication.WriteIsEnabled)
    {
        var errorMessage = LogCommonStrings.SERVICE_PROVIDER_IS_DISABLED;
        LogInfo(context, CommonConstants.WARNING, errorMessage);
        return;
    }

    var pondApiService = new PondApiService(pondAuthentication, new HttpRequestFactory(), context.IsProduction);
    var baseUri = context.IsProduction ? pondAuthentication.ProductionURL : pondAuthentication.SandboxURL;

    await UpdatePondCustomerAssignmentForDevices(context, logRepo, bulkChange, changes, pondRepository, pondAuthentication, processedBy, pondApiService, baseUri);
}

private async Task UpdatePondCustomerAssignmentForDevices(KeySysLambdaContext context, 
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, 
    ICollection<BulkChangeDetailRecord> changes, PondRepository pondRepository, 
    PondAuthentication pondAuthentication, string processedBy, 
    PondApiService pondApiService, string baseUri)
{
    foreach (var change in changes)
    {
        if (context.Context.RemainingTime.TotalSeconds < RemainingTimeCutoff)
        {
            break;
        }
        
        try
        {
            var changeRequest = JsonConvert.DeserializeObject<BulkChangeAssociateCustomer>(change.ChangeRequest);
            
            // Call POND API to assign customer
            var pondCustomerAssignmentResponse = await AssignPondCustomer(logRepo, bulkChange, pondAuthentication, pondApiService, baseUri, change, changeRequest);
            
            if (pondCustomerAssignmentResponse.HasErrors)
            {
                await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, false, pondCustomerAssignmentResponse.ResponseObject);
                continue;
            }

            // Update database associations
            SavePondCustomerAssignmentToDatabase(context, logRepo, bulkChange, pondRepository, processedBy, change, changeRequest);
            await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, true, "Customer assignment successful");

            logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
            {
                BulkChangeId = bulkChange.Id,
                HasErrors = false,
                LogEntryDescription = "POND Customer Assignment",
                M2MDeviceChangeId = change.Id,
                ProcessBy = processedBy,
                ProcessedDate = DateTime.UtcNow,
                ResponseStatus = BulkChangeStatus.PROCESSED,
                RequestText = JsonConvert.SerializeObject(changeRequest),
                ResponseText = pondCustomerAssignmentResponse.ResponseObject
            });
        }
        catch (Exception ex)
        {
            LogInfo(context, CommonConstants.ERROR, $"Error processing customer assignment for {change.ICCID}: {ex.Message}");
            await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, false, ex.Message);
        }
    }
}

private async Task<DeviceChangeResult<string, string>> AssignPondCustomer(
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, 
    PondAuthentication pondAuthentication, PondApiService pondApiService, 
    string baseUri, BulkChangeDetailRecord change, 
    BulkChangeAssociateCustomer changeRequest)
{
    try
    {
        var pondCustomerAssignmentApiUrl = $"{baseUri.TrimEnd('/')}/{pondAuthentication.DistributorId}/customer/assign/{change.ICCID}";
        
        var requestBody = new
        {
            customerId = changeRequest.CustomerId,
            customerName = changeRequest.CustomerName,
            effectiveDate = changeRequest.EffectiveDate
        };

        var response = await pondApiService.PostAsync(pondCustomerAssignmentApiUrl, JsonConvert.SerializeObject(requestBody));
        
        var logEntry = $"POND Customer Assignment API call for ICCID: {change.ICCID}";
        logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog(response, bulkChange.Id, change.Id, logEntry));

        return response;
    }
    catch (Exception ex)
    {
        return new DeviceChangeResult<string, string>()
        {
            HasErrors = true,
            ResponseObject = $"Error calling POND customer assignment API: {ex.Message}",
            ActionText = "AssignPondCustomer"
        };
    }
}

private void SavePondCustomerAssignmentToDatabase(KeySysLambdaContext context, 
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, 
    PondRepository pondRepository, string processedBy, 
    BulkChangeDetailRecord change, BulkChangeAssociateCustomer changeRequest)
{
    try
    {
        // Update device-customer associations in database
        pondRepository.UpdateDeviceCustomerAssignment(
            ParameterizedLog(context), 
            change.ICCID, 
            changeRequest.CustomerId, 
            bulkChange.ServiceProviderId, 
            processedBy,
            changeRequest.EffectiveDate
        );
    }
    catch (Exception ex)
    {
        LogInfo(context, CommonConstants.ERROR, $"Error saving customer assignment to database for {change.ICCID}: {ex.Message}");
    }
}
```

### POND-Specific Customer Rate Plan Change

#### 1. Update ProcessCustomerRatePlanChangeAsync Routing

Add integration-specific routing for customer rate plan changes:

```csharp
private static async Task ProcessCustomerRatePlanChangeAsync(KeySysLambdaContext context,
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, ISyncPolicy syncPolicy)
{
    // Add POND-specific routing
    if (bulkChange.IntegrationId == (int)IntegrationType.Pond)
    {
        await ProcessPondCustomerRatePlanChangeAsync(context, logRepo, bulkChange, syncPolicy);
        return;
    }

    // Existing generic implementation continues...
    var change = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, 1).FirstOrDefault();
    // ... rest of existing code
}
```

#### 2. Implement ProcessPondCustomerRatePlanChangeAsync Method

```csharp
private static async Task ProcessPondCustomerRatePlanChangeAsync(KeySysLambdaContext context,
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange, ISyncPolicy syncPolicy)
{
    LogInfo(context, CommonConstants.SUB, "ProcessPondCustomerRatePlanChangeAsync()");

    var changes = GetDeviceChanges(context, bulkChange.Id, bulkChange.PortalTypeId, PageSize);
    
    var pondRepository = new PondRepository(context.CentralDbConnectionString);
    var pondAuthentication = pondRepository.GetPondAuthentication(ParameterizedLog(context), context.Base64Service, bulkChange.ServiceProviderId);
    var processedBy = context.Context.FunctionName;

    if (pondAuthentication == null || !pondAuthentication.WriteIsEnabled)
    {
        var errorMessage = pondAuthentication == null 
            ? string.Format(LogCommonStrings.FAILED_GET_AUTHENTICATION_INFORMATION, CommonConstants.POND_CARRIER_NAME)
            : LogCommonStrings.SERVICE_PROVIDER_IS_DISABLED;
            
        LogInfo(context, CommonConstants.WARNING, errorMessage);
        return;
    }

    var pondApiService = new PondApiService(pondAuthentication, new HttpRequestFactory(), context.IsProduction);
    var baseUri = context.IsProduction ? pondAuthentication.ProductionURL : pondAuthentication.SandboxURL;

    await UpdatePondCustomerRatePlanForDevices(context, logRepo, bulkChange, changes, pondRepository, pondAuthentication, processedBy, pondApiService, baseUri);
}

private static async Task UpdatePondCustomerRatePlanForDevices(KeySysLambdaContext context,
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange,
    ICollection<BulkChangeDetailRecord> changes, PondRepository pondRepository,
    PondAuthentication pondAuthentication, string processedBy,
    PondApiService pondApiService, string baseUri)
{
    foreach (var change in changes)
    {
        if (context.Context.RemainingTime.TotalSeconds < RemainingTimeCutoff)
        {
            break;
        }

        try
        {
            var changeRequest = JsonConvert.DeserializeObject<BulkChangeRequest>(change.ChangeRequest);
            var customerRatePlanUpdate = changeRequest?.CustomerRatePlanUpdate;

            if (customerRatePlanUpdate == null)
            {
                await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, false, "Invalid customer rate plan update request");
                continue;
            }

            // Call POND API to update customer rate plan
            var pondCustomerRatePlanResponse = await UpdatePondCustomerRatePlan(logRepo, bulkChange, pondAuthentication, pondApiService, baseUri, change, customerRatePlanUpdate);

            if (pondCustomerRatePlanResponse.HasErrors)
            {
                await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, false, pondCustomerRatePlanResponse.ResponseObject);
                continue;
            }

            // Update database
            SavePondCustomerRatePlanToDatabase(context, logRepo, bulkChange, pondRepository, processedBy, change, customerRatePlanUpdate);
            await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, true, "Customer rate plan change successful");

            logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
            {
                BulkChangeId = bulkChange.Id,
                HasErrors = false,
                LogEntryDescription = "POND Customer Rate Plan Change",
                M2MDeviceChangeId = change.Id,
                ProcessBy = processedBy,
                ProcessedDate = DateTime.UtcNow,
                ResponseStatus = BulkChangeStatus.PROCESSED,
                RequestText = JsonConvert.SerializeObject(customerRatePlanUpdate),
                ResponseText = pondCustomerRatePlanResponse.ResponseObject
            });
        }
        catch (Exception ex)
        {
            LogInfo(context, CommonConstants.ERROR, $"Error processing customer rate plan change for {change.ICCID}: {ex.Message}");
            await MarkProcessedForM2MDeviceChangeAsync(context, change.Id, false, ex.Message);
        }
    }
}

private static async Task<DeviceChangeResult<string, string>> UpdatePondCustomerRatePlan(
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange,
    PondAuthentication pondAuthentication, PondApiService pondApiService,
    string baseUri, BulkChangeDetailRecord change,
    BulkChangeCustomerRatePlanUpdate customerRatePlanUpdate)
{
    try
    {
        var pondCustomerRatePlanApiUrl = $"{baseUri.TrimEnd('/')}/{pondAuthentication.DistributorId}/customer-rate-plan/update/{change.ICCID}";
        
        var requestBody = new
        {
            customerRatePlanId = customerRatePlanUpdate.CustomerRatePlanId,
            customerPoolId = customerRatePlanUpdate.CustomerPoolId,
            effectiveDate = customerRatePlanUpdate.EffectiveDate,
            customerDataAllocationMB = customerRatePlanUpdate.CustomerDataAllocationMB
        };

        var response = await pondApiService.PostAsync(pondCustomerRatePlanApiUrl, JsonConvert.SerializeObject(requestBody));
        
        var logEntry = $"POND Customer Rate Plan Change API call for ICCID: {change.ICCID}";
        logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog(response, bulkChange.Id, change.Id, logEntry));

        return response;
    }
    catch (Exception ex)
    {
        return new DeviceChangeResult<string, string>()
        {
            HasErrors = true,
            ResponseObject = $"Error calling POND customer rate plan API: {ex.Message}",
            ActionText = "UpdatePondCustomerRatePlan"
        };
    }
}

private static void SavePondCustomerRatePlanToDatabase(KeySysLambdaContext context,
    DeviceBulkChangeLogRepository logRepo, BulkChange bulkChange,
    PondRepository pondRepository, string processedBy,
    BulkChangeDetailRecord change, BulkChangeCustomerRatePlanUpdate customerRatePlanUpdate)
{
    try
    {
        // Update customer rate plan associations in database
        pondRepository.UpdateDeviceCustomerRatePlan(
            ParameterizedLog(context),
            change.ICCID,
            customerRatePlanUpdate.CustomerRatePlanId,
            customerRatePlanUpdate.CustomerPoolId,
            bulkChange.ServiceProviderId,
            processedBy,
            customerRatePlanUpdate.EffectiveDate,
            customerRatePlanUpdate.CustomerDataAllocationMB
        );
    }
    catch (Exception ex)
    {
        LogInfo(context, CommonConstants.ERROR, $"Error saving customer rate plan to database for {change.ICCID}: {ex.Message}");
    }
}
```

## Required Database Updates

### PondRepository Extensions

Add the following methods to `PondRepository` class:

```csharp
public void UpdateDeviceCustomerAssignment(ILogger logger, string iccid, 
    string customerId, int serviceProviderId, string processedBy, DateTime? effectiveDate)
{
    // Implementation for updating device-customer associations
}

public void UpdateDeviceCustomerRatePlan(ILogger logger, string iccid, 
    int? customerRatePlanId, int? customerPoolId, int serviceProviderId, 
    string processedBy, DateTime? effectiveDate, int? customerDataAllocationMB)
{
    // Implementation for updating customer rate plan associations
}
```

## API Endpoint Requirements

### POND API Endpoints to Implement

1. **Customer Assignment**: `POST /{distributorId}/customer/assign/{iccid}`
2. **Customer Rate Plan Change**: `POST /{distributorId}/customer-rate-plan/update/{iccid}`

## Testing Strategy

### Unit Tests Required

1. **ProcessPondCustomerAssignmentAsync** unit tests
2. **ProcessPondCustomerRatePlanChangeAsync** unit tests
3. **PondRepository** extension method tests
4. **API integration** tests with POND endpoints

### Integration Tests

1. End-to-end customer assignment flow
2. End-to-end customer rate plan change flow
3. Error handling scenarios
4. Authentication and authorization tests

## Deployment Checklist

- [ ] Add new methods to `AltaworxDeviceBulkChange.cs`
- [ ] Extend `PondRepository` with new database methods
- [ ] Update API endpoint configurations
- [ ] Deploy database schema changes if required
- [ ] Configure POND API authentication for new endpoints
- [ ] Test all 4 change types in staging environment
- [ ] Update monitoring and alerting for new operations
- [ ] Deploy to production with feature flags

## Summary

This implementation completes the POND IoT Service Provider support for all 4 change types:

1. ✅ **Change Carrier Rate Plan** - Already complete
2. ✅ **Update Device Status** - Already complete  
3. 🆕 **Assign Customer** - New POND-specific routing and API integration
4. 🆕 **Change Customer Rate Plan** - New POND-specific routing and API integration

The implementation follows established patterns in the codebase and maintains consistency with existing carrier integrations while providing comprehensive POND IoT device management capabilities.