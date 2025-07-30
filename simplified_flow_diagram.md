# Simplified SQS Event Processing Flow

## Overview
This system processes bulk device changes through SQS messages, handling different types of device updates like username changes, status updates, and equipment changes.

## 1. Message Entry & Routing
```
SQS Message → Process Event → Extract Change Type → Route to Handler
```

**What happens here:**
- System receives a message from SQS queue
- Extracts the type of change requested
- Routes to the appropriate handler

## 2. Main Change Types Supported

### A. Username & Cost Center Changes
```
Username Change Request → Check Integration Type → Process Update → Update Databases
```

### B. Device Status Updates
```
Status Change Request → Update Device Status → Notify Systems
```

### C. Carrier Rate Plan Changes
```
Rate Plan Change → Update Carrier Settings → Update Billing
```

### D. Equipment Changes (ICCID/IMEI)
```
Equipment Change → Update Device Hardware Info → Update Records
```

## 3. Username Processing (Most Complex)

### For Jasper Integration:
```
1. Get device data from database
2. Update username in Jasper system
3. Update cost center in Rev.io billing system  
4. Update local AMOP database
5. Log results and send notifications
```

### For Telegence Integration:
```
1. Get device data from database
2. Get service details from Rev.io
3. Map username fields correctly
4. Update cost center in Rev.io
5. Update local AMOP database
6. Log results and send notifications
```

## 4. Data Sources Used
- **AMOP Database**: Main device and change tracking database
- **Rev.io API**: Billing and service management system
- **Jasper API**: Carrier integration for Jasper devices
- **Telegence API**: Carrier integration for Telegence devices

## 5. Success & Error Handling

### When Everything Works:
```
Success → Mark as PROCESSED → Send Notifications → Update Status
```

### When Something Fails:
```
Error → Mark as API_FAILED → Retry if possible → Send Error Notifications
```

## 6. Key Features
- **Batch Processing**: Handles up to 100 devices at once
- **Parallel Processing**: Multiple requests processed simultaneously
- **Retry Logic**: Failed requests are retried automatically
- **Audit Trail**: All changes are logged for tracking
- **Notifications**: Email alerts sent for completed changes
- **Status Updates**: Real-time status updates via webhooks

## Simple Flow Summary
```
Message In → Identify Change Type → Process with Right Integration → 
Update All Systems → Log Results → Send Notifications → Done
```

## Why This Complexity?
This system needs to:
- Handle multiple carrier integrations (Jasper, Telegence)
- Keep multiple systems in sync (billing, device management, databases)
- Provide reliable processing with error handling and retries
- Maintain audit trails for compliance
- Process large batches efficiently