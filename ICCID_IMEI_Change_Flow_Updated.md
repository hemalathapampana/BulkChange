# Updated ICCID/IMEI Change Flow

## Overview
This document describes the updated process flow for changing ICCID/IMEI identifiers based on the new dataflow requirements. The updated flow eliminates callback-based processing in favor of synchronous operations with immediate database updates.

## Phase 1: User Request (Frontend)
1. User selects "Change Identifier" option from M2M Portal
2. User provides:
   - Old ICCID/IMEI (current identifier)
   - New ICCID/IMEI (replacement identifier)
   - Identifier Type (ICCID or IMEI)
   - Optional: Customer Rate Plan changes
   - Optional: Effective Date
3. Frontend sends PostChangeIdentifier request to M2MController

## Phase 2: Controller Processing (M2MController.cs)

### Validation Steps:
1. **Check old identifier exists in Device table**
   - Verify device exists with provided identifier
   - Return error if device not found

2. **Verify device is active status**
   - Check device status is active
   - Return error if device is inactive

3. **Validate new identifier format**
   - ICCID: 19-20 digits
   - IMEI: 15 digits
   - Return error if format is invalid

4. **Check new identifier not already in use**
   - Query Device table for duplicate identifiers
   - Return error if identifier already exists

### Database Operations:
- Create DeviceBulkChange record
- Create M2M_DeviceChange records for each validated device
- Queue message to SQS for Lambda processing

## Phase 3: Lambda Processing (AltaworxDeviceBulkChange.cs → ProcessChangeICCIDorIMEI.cs)

### Authentication and Setup:
1. **Execute usp_ThingSpace_Get_AuthenticationByProviderId**
   - Retrieve authentication credentials
   - Log authentication failure if credentials not found

2. **Get access and session tokens**
   - Call ThingSpace authentication APIs
   - Log failure if token retrieval fails

3. **Check write operations enabled**
   - Verify WRITE_OPERATIONS_ENABLED environment variable
   - Log error and stop if writes are disabled

### Device Processing Loop:
For each device change:

1. **Query Device Table for ICCID/IMEI Data**
   - Retrieve current device information
   - Log error if device not found

2. **Prepare ThingSpace API Request**
   - Build change identifier request object
   - Set appropriate identifier type (ICCID or IMEI)

3. **Send Request to Verizon ThingSpace API**
   - Make synchronous API call
   - Handle API response immediately

4. **Handle API Response**
   - If successful:
     - Update ThingSpaceDevice Table
     - Update Device Table ICCID/IMEI using UpdateIdentifierForThingSpace procedure
     - Update Customer Rate Plan if requested
     - Mark M2M_DeviceChange as successful
     - Log success entry
   - If failed:
     - Mark M2M_DeviceChange as failed
     - Log error entry

### Post-Processing:
1. **Update DeviceBulkChange Status**
   - Set status to PROCESSED if all successful
   - Set status to ERROR if any failures occurred

2. **Send Email Notification**
   - Retrieve email configuration for service provider
   - Send completion summary with success/failure counts
   - Include completion timestamp

## Phase 4: Database Updates

### UpdateIdentifierForThingSpace Stored Procedure:
```sql
UPDATE Device 
SET 
    ICCID = CASE WHEN @IdentifierType = 'ICCID' THEN @NewIdentifier ELSE ICCID END,
    IMEI = CASE WHEN @IdentifierType = 'IMEI' THEN @NewIdentifier ELSE IMEI END,
    ModifiedDate = GETUTCDATE(),
    ModifiedBy = 'AltaworxDeviceBulkChange'
WHERE Id = @DeviceId
```

### ThingSpaceDevice Table Update:
```sql
UPDATE ThingSpaceDevice 
SET 
    ICCID = CASE WHEN @IdentifierType = 'ICCID' THEN @NewIdentifier ELSE ICCID END,
    IMEI = CASE WHEN @IdentifierType = 'IMEI' THEN @NewIdentifier ELSE IMEI END,
    ModifiedDate = GETUTCDATE(),
    ModifiedBy = 'AltaworxDeviceBulkChange'
WHERE ICCID = @OldICCID
```

## Phase 5: Logging Structure

All operations use M2MDeviceBulkChangeLog table with the following log types:

### Success Entries:
- **Authentication Successful**: Valid credentials retrieved
- **API Call Successful**: ThingSpace API call completed
- **Database Update Successful**: Device tables updated
- **Process Complete**: ICCID/IMEI change completed successfully

### Error Entries:
- **Authentication Failed**: Invalid or missing credentials
- **Validation Error**: Device not found, inactive, or identifier issues
- **API Error**: ThingSpace API call failed
- **Database Error**: Database update failed
- **Write Operations Disabled**: Environment configuration prevents writes

## Key Changes from Previous Implementation

1. **Removed Callback Processing**: No longer waits for ThingSpace callbacks
2. **Synchronous Operations**: All database updates happen immediately after API success
3. **Enhanced Validation**: Comprehensive input validation at controller level
4. **Immediate Error Handling**: Errors are processed and logged immediately
5. **Email Notifications**: Added completion notifications with detailed status
6. **Simplified Retry Logic**: Removed complex retry mechanism
7. **Consistent Logging**: Standardized logging structure using M2MDeviceBulkChangeLog

## Configuration Requirements

### Environment Variables:
- `WRITE_OPERATIONS_ENABLED`: Must be "true" to allow processing
- ThingSpace authentication settings (existing)
- Email service configuration (new)

### Database Schema:
- Ensure M2MDeviceBulkChangeLog table supports new log entry types
- ServiceProvider table should have NotificationEmail and FromEmail columns

## Error Handling Strategy

1. **Fail Fast**: Stop processing device if any validation fails
2. **Continue Processing**: Don't stop entire batch for individual device failures
3. **Comprehensive Logging**: Log all errors with detailed messages
4. **Status Reporting**: Provide clear success/failure counts in notifications