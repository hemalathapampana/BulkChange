# POD19 Change Types & Username/Cost Center Changes - Simplified Guide

## Overview
This document provides a simplified view of POD19 change types and Username/Cost Center change functionality in the AltaworxDeviceBulkChange system.

## POD19 Integration

### What is POD19?
POD19 is an integration type that works with Jasper API services for device management operations. It handles device username updates with additional validation through audit trail verification.

### POD19 Change Processing
POD19 follows the same processing flow as other Jasper integrations but includes additional verification steps:

```
1. Process change request → 2. Update via Jasper API → 3. Verify through audit trail → 4. Confirm success
```

### Supported Operations
- **Username Updates**: Modify device username/contact name
- **Cost Center Updates**: Update cost center fields (1, 2, 3)
- **Combined Updates**: Update both username and cost centers simultaneously

## Username/Cost Center Change Type

### Change Request Type: EditUsernameCostCenter

This change type allows updating:
- **Contact Name/Username**: Primary identifier for the device user
- **Cost Center 1**: Primary cost center assignment
- **Cost Center 2**: Secondary cost center assignment  
- **Cost Center 3**: Tertiary cost center assignment

### Processing Flow

#### 1. Integration Type Routing
```
Switch (IntegrationType):
├── Jasper/POD19/TMobileJasper/Rogers → ProcessEditUsernameJasperAsync()
├── ThingSpace → (TODO: Not implemented)
├── Telegence → ProcessEditUsernameTelegenceAsync()
└── Default → Exception (Unsupported)
```

#### 2. POD19 Specific Processing

**Step 1: Standard Jasper Update**
- Authenticate with Jasper API
- Send username update request
- Receive initial response

**Step 2: POD19 Audit Verification** (Unique to POD19)
- Call Jasper Audit Trail API
- Verify username was actually updated
- Check for error messages in audit log
- Confirm successful change

**Step 3: Result Processing**
- Log success/failure
- Update database status
- Generate appropriate response messages

### Request Structure

```json
{
  "ContactName": "new_username",
  "CostCenter1": "CC001",
  "CostCenter2": "CC002", 
  "CostCenter3": "CC003"
}
```

### Status Types

| Status | Description |
|--------|-------------|
| NEW | Change request created, not yet processed |
| PENDING | Change request queued for processing |
| PROCESSING | Currently being processed |
| PROCESSED | Successfully completed |
| API_FAILED | API call failed during processing |

### Error Handling

#### POD19 Specific Errors
- **Username Not Found**: "Unsuccessfully updated the Username. The Username ({username}) not exists."
- **Write Disabled**: "Writes disabled for service provider"
- **Audit Verification Failed**: Username update appears successful but audit trail shows errors

#### Common Error Scenarios
1. **Invalid Username**: Provided username doesn't exist in the system
2. **API Authentication Failure**: Jasper API credentials invalid
3. **Audit Trail Mismatch**: Update reported successful but audit shows failure
4. **Cost Center Validation**: Invalid cost center values provided

### API Integration Points

#### For POD19:
1. **Jasper Device Update API**: Updates username/contact name
2. **Jasper Audit Trail API**: Verifies the change was successful
3. **Rev.IO API**: Updates cost center information (when applicable)

#### For Other Integrations:
1. **Telegence API**: Alternative processing for Telegence integration
2. **ThingSpace API**: (Future implementation)

### Logging and Monitoring

#### Log Types:
- **Info**: "Update Username/Cost Center of devices"
- **Info (Rev.IO)**: "Update Username/Cost Center of devices: Rev.IO API"
- **Info (Jasper)**: "Update Username of device: Jasper API"
- **Exception**: Error conditions and failures

#### Success Messages:
- "THE_USERNAME_AND_COST_CENTER_WAS_UPDATED_SUCCESSFULLY"
- "UPDATING_USERNAME_AND_COST_CENTER"

## Configuration Requirements

### POD19 Setup Requirements:
1. **Jasper Authentication**: Valid API credentials
2. **Write Permissions**: Must be enabled for service provider
3. **Audit Trail Access**: Required for verification step
4. **Cost Center Integration**: Rev.IO API access (if using cost centers)

### Database Configuration:
- Service Provider ID mapping
- Integration ID configuration (POD19 = specific enum value)
- Tenant permissions and access control

## Best Practices

### For POD19 Changes:
1. **Always Verify**: Use audit trail verification for critical updates
2. **Error Recovery**: Implement proper retry logic for failed verifications
3. **Logging**: Maintain detailed logs for audit purposes
4. **Validation**: Pre-validate usernames before attempting updates

### For Username/Cost Center Updates:
1. **Batch Processing**: Process multiple devices efficiently
2. **Validation**: Verify cost center values before processing
3. **Rollback**: Plan for rollback scenarios in case of partial failures
4. **Monitoring**: Track success rates and failure patterns

## Troubleshooting

### Common Issues:

#### POD19 Specific:
1. **Audit Verification Fails**: Check audit trail API connectivity and permissions
2. **Username Update Succeeds but Verification Fails**: Review audit log format and parsing logic

#### General Username/Cost Center:
1. **API Authentication**: Verify service provider credentials
2. **Permission Denied**: Check write permissions for service provider
3. **Invalid Cost Centers**: Validate cost center format and existence
4. **Bulk Update Failures**: Review individual change logs for specific failure reasons

### Diagnostic Steps:
1. Check service provider write permissions
2. Verify API credentials and connectivity
3. Review audit trail logs for POD19
4. Validate input data format and values
5. Check tenant permissions and access rights

## Summary

POD19 is a specialized integration type that enhances standard Jasper API operations with additional audit verification. The Username/Cost Center change type supports comprehensive device metadata updates across multiple integration platforms, with POD19 providing the most robust verification mechanism through its dual-phase update and verification process.