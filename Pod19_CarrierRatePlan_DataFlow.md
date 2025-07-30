# Pod19 Service Provider - CHANGE CARRIER RATEPLAN Data Flow Diagram

## Overview

This document outlines the complete data flow for **CHANGE CARRIER RATEPLAN** change type specifically for the **Pod19** service provider integration. Pod19 uses the Jasper API processing flow for carrier rate plan changes, similar to other Jasper-based integrations.

## Key Characteristics: Pod19 Carrier Rate Plan Processing

### Integration Type
- **Integration ID**: `IntegrationType.POD19`
- **Processing Flow**: Jasper API-based processing
- **Authentication**: Jasper authentication system
- **API Endpoints**: Jasper device update endpoints

### Data Model
```csharp
public class CarrierRatePlanUpdate
{
    public string CarrierRatePlan { get; set; }     // Target carrier rate plan code
    public string CommPlan { get; set; }            // Communication plan
    public DateTime? EffectiveDate { get; set; }    // Implementation date
    public string PlanUuid { get; set; }            // Plan unique identifier (for Teal)
    public long RatePlanId { get; set; }            // Rate plan ID (for Pond)
}

public class BulkChangeCarrierRatePlanUpdate
{
    public CarrierRatePlanUpdate CarrierRatePlanUpdate { get; set; }
}
```

## Complete Data Flow Architecture

### 1. Request Initiation and Validation

```
Client UI → BulkChangeRequest → M2MController.CreateBulkChanges()
```

**Input Parameters:**
- `ServiceProviderId`: Pod19 service provider ID
- `ChangeType`: `DeviceChangeType.CarrierRatePlanChange` (value: 7)
- `Devices[]`: Array of device identifiers (ICCIDs)
- `CarrierRatePlanUpdate`: Target rate plan details

### 2. Pre-Processing Validation

```
M2MController → CarrierRatePlanRepository.GetByCarrierRatePlanCode()
```

**Validation Steps:**
1. **Service Provider Integration Check**: Verify Pod19 integration
2. **Carrier Rate Plan Validation**: 
   ```csharp
   var carrierRatePlan = carrierRatePlanRepository.GetByCarrierRatePlanCode(carrierRatePlanCode);
   if (carrierRatePlan == null)
   {
       throw new Exception(string.Format(CommonStrings.CarrierRatePlanNotExist, carrierRatePlanCode));
   }
   ```
3. **Device Validation**: Verify devices exist and belong to service provider
4. **Permission Validation**: Check tenant permissions

### 3. Bulk Change Record Creation

```
M2MController → BulkChangeRepository.CreateBulkChange()
```

**Database Operations:**
1. Create master `BulkChange` record
2. Create individual `BulkChangeDetailRecord` for each device
3. Set initial status to "PENDING"
4. Queue for Lambda processing

### 4. Lambda Processing Pipeline

```
AltaworxDeviceBulkChange.ProcessCarrierRatePlanChangeAsync()
```

#### Step 4.1: Integration Type Resolution
```csharp
switch (serviceProviderId)
{
    case (int)IntegrationType.POD19:
        return await ProcessJasperCarrierRatePlanChange(context, logRepo, bulkChange, serviceProviderId, changes);
}
```

#### Step 4.2: Jasper Authentication Setup
```
ProcessJasperCarrierRatePlanChange() → JasperCommon.GetJasperAuthenticationInformation()
```

**Authentication Process:**
1. Retrieve Jasper authentication credentials for Pod19
2. Validate `WriteIsEnabled` flag
3. Initialize HTTP client factory and retry policies
4. Create Jasper device detail service

### 5. Device-by-Device Processing

```
ForEach Device → JasperDeviceDetailService.UpdateJasperDeviceDetailsAsync()
```

#### Step 5.1: Request Preparation
```csharp
var carrierRatePlan = JsonConvert.DeserializeObject<BulkChangeCarrierRatePlanUpdate>(change.ChangeRequest);
var jasperDeviceDetail = new JasperDeviceDetail
{
    ICCID = change.DeviceIdentifier,
    CarrierRatePlan = carrierRatePlan.CarrierRatePlanUpdate.CarrierRatePlan,
    CommunicationPlan = carrierRatePlan.CarrierRatePlanUpdate.CommPlan,
};
```

#### Step 5.2: Jasper API Call
```
JasperDeviceDetailService → Jasper API Endpoint
```

**API Request:**
- **Method**: HTTP PUT/POST
- **Endpoint**: Jasper device management endpoint
- **Authentication**: Jasper API credentials
- **Payload**: Device details with new rate plan
- **Retry Policy**: Configured HTTP retry policy

#### Step 5.3: API Response Logging
```csharp
logRepo.AddM2MLogEntry(new CreateM2MDeviceBulkChangeLog()
{
    BulkChangeId = bulkChange.Id,
    LogEntryDescription = "Update Jasper Rate Plan: Jasper API",
    M2MDeviceChangeId = change.Id,
    ProcessBy = "AltaworxDeviceBulkChange",
    ResponseStatus = result.HasErrors ? BulkChangeStatus.ERROR : BulkChangeStatus.PROCESSED,
    RequestText = result.ActionText + Environment.NewLine + result.RequestObject,
    ResponseText = JsonConvert.SerializeObject(result.ResponseObject)
});
```

### 6. Database Update (AMOP)

```
DeviceRepository.UpdateRatePlanAsync() → Database Stored Procedure
```

**Database Operations:**
1. Update device record with new carrier rate plan
2. Update communication plan if provided
3. Set effective date
4. Update audit trail

#### Database Update Details
```csharp
var dbResult = await deviceRepository.UpdateRatePlanAsync(
    jasperDeviceDetail.ICCID,
    jasperDeviceDetail.CarrierRatePlan, 
    jasperDeviceDetail.CommunicationPlan, 
    change.TenantId
);
```

### 7. Final Status Update

```
MarkProcessedForM2MDeviceChangeAsync() → BulkChangeDetailRecord Status Update
```

**Status Resolution:**
- **Success**: "Successfully update Carrier Rate Plan; AMOP Update"
- **Failure**: "Failed to update Carrier Rate Plan: [Error Details]"

## Complete Data Flow Diagram

```
┌─────────────────────┐
│ Client UI Request   │
│ (Pod19 Carrier      │
│ Rate Plan Change)   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ M2MController       │
│ CreateBulkChanges() │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Validate Rate Plan  │
│ Code exists in DB   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Create BulkChange   │
│ & Detail Records    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Queue for Lambda    │
│ Processing          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ AltaworxDevice      │
│ BulkChange Lambda   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Route to Pod19      │
│ (Jasper Flow)       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Get Jasper Auth     │
│ Information         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Initialize Jasper   │
│ Device Service      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ For Each Device:    │
│ Process Rate Plan   │
│ Change              │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Prepare Jasper      │
│ Device Detail       │
│ Request             │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Call Jasper API     │
│ UpdateDevice        │
│ Endpoint            │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Log API Response    │
│ to M2M Log Table    │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐      ┌─────────────────────┐
│ API Success?        │──No──│ Mark Device as      │
│                     │      │ ERROR & Continue    │
└──────────┬──────────┘      └─────────────────────┘
           │Yes
           ▼
┌─────────────────────┐
│ Update Device in    │
│ AMOP Database       │
│ (Rate Plan & Comm   │
│ Plan)               │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Log Database        │
│ Update Result       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Mark Device Change  │
│ as PROCESSED or     │
│ ERROR               │
└─────────────────────┘
```

## Error Handling and Logging

### Error Scenarios

1. **Authentication Failures**
   - Missing Jasper authentication information
   - Disabled write operations for service provider
   - Invalid API credentials

2. **Validation Errors**
   - Non-existent carrier rate plan code
   - Invalid device ICCID
   - Permission denied for tenant

3. **API Errors**
   - Jasper API endpoint unavailable
   - Rate plan not available in Jasper
   - Device not found in Jasper system

4. **Database Errors**
   - AMOP database connection failures
   - Rate plan update stored procedure errors
   - Transaction rollback scenarios

### Logging Strategy

#### M2M Log Entries
```csharp
// API Call Logging
LogEntryDescription: "Update Jasper Rate Plan: Jasper API"
RequestText: [API Request Details]
ResponseText: [API Response JSON]
ResponseStatus: PROCESSED | ERROR

// Database Update Logging  
LogEntryDescription: "Update Jasper Rate Plan: AMOP Update"
RequestText: [Database Request Details]
ResponseText: [Database Response]
ResponseStatus: PROCESSED | ERROR
```

## Integration Points

### 1. Jasper API Integration
- **Authentication**: Service provider-specific Jasper credentials
- **Endpoints**: Jasper device management API
- **Rate Limiting**: Configured retry policies
- **Error Handling**: Comprehensive error response processing

### 2. Database Integration
- **Device Repository**: Rate plan and communication plan updates
- **Audit Tables**: Change tracking and logging
- **Bulk Change Tables**: Process status management

### 3. Monitoring Integration
- **Lambda Metrics**: Processing time and success rates
- **API Metrics**: Jasper API response times and error rates
- **Database Metrics**: Update success rates and performance

## Security Considerations

### Authentication Security
- Encrypted Jasper API credentials storage
- Secure credential retrieval and caching
- API key rotation support

### Data Protection
- Encrypted sensitive data in logs
- Sanitized error messages
- Audit trail maintenance

### Access Control
- Tenant-level device access validation
- Service provider permission enforcement
- Rate plan visibility restrictions

## Performance Optimization

### Async Processing
- Non-blocking Jasper API calls
- Parallel device processing capabilities
- Lambda timeout management

### Retry Mechanisms
- HTTP retry policies for API failures
- Exponential backoff strategies
- Circuit breaker patterns

### Monitoring
- Real-time processing metrics
- Error rate tracking
- Performance bottleneck identification

## Pod19-Specific Configuration

### Integration Settings
- **Integration Type**: `IntegrationType.POD19`
- **Processing Method**: Jasper API flow
- **Authentication Provider**: Jasper authentication system
- **API Version**: Jasper device management API v2+

### Rate Plan Validation
- Pod19 rate plans stored in `JasperCarrierRatePlan` table
- Validation against Pod19-specific rate plan catalog
- Communication plan compatibility checking

This data flow ensures reliable, traceable, and secure carrier rate plan changes for Pod19 service provider devices through the established Jasper API integration framework.