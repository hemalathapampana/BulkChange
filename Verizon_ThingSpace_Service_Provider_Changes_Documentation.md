# Verizon ThingSpace IoT Service Provider Changes Documentation

## Executive Summary
The Verizon ThingSpace IoT Service Provider Change System is a comprehensive AWS Lambda-based pipeline that manages the complete lifecycle of IoT devices through intelligent device management, status updates, and operational optimizations. The system processes device operations, manages device lifecycles, and coordinates change types using advanced algorithms across multiple Lambda functions integrated with Verizon's ThingSpace API platform.

## System Architecture & Flow

### Core Components
1. **AltaworxDeviceBulkChange** - Main orchestration engine for all ThingSpace operations
2. **ProcessChangeICCIDorIMEI** - Handles device identifier changes and updates  
3. **ProcessEditUsername** - Manages device username modifications
4. **FunctionProcessUpdateStatus** - Coordinates post-activation device status updates
5. **M2MController** - Web interface and bulk operations management

### Data Flow Overview
```
API Trigger → Authentication → Change Type Routing → Device Validation → 
ThingSpace API Integration → Operation Execution → Status Validation → 
Callback Processing → Database Updates → Result Compilation → Cleanup & Reporting
```

## ThingSpace Change Types

### 1. Device Status Updates
**High-level Process:**
- Device status transition management (Active, Inventory, Suspend, Deactivate)
- Rate plan validation and assignment
- Device activation with retry mechanisms
- Status reason code processing
- ZIP code validation for activation

**Key Operations:**
- **Add to Inventory** - Register new devices in ThingSpace inventory
- **Activate Device** - Transition devices from pending to active status
- **Suspend Device** - Temporarily disable device connectivity
- **Deactivate Device** - Permanently disable device operations

### 2. Device Identifier Changes (ICCID/IMEI)
**High-level Process:**
- Identifier validation and cross-reference
- ThingSpace API identifier update requests
- Device equipment updates in mobility systems
- Customer rate plan association maintenance
- Change request processing with rollback capabilities

**Key Operations:**
- **ICCID Change** - Update SIM card identifiers
- **IMEI Change** - Update device hardware identifiers
- **Combined Changes** - Simultaneous ICCID and IMEI updates

### 3. Carrier Rate Plan Changes
**High-level Process:**
- Rate plan validation and compatibility checks
- ThingSpace rate plan assignment
- Device detail synchronization
- Usage plan optimization
- Cost calculation updates

**Key Operations:**
- **Rate Plan Assignment** - Apply new carrier rate plans
- **Usage Pool Management** - Manage shared data pools
- **Plan Optimization** - Automatic plan recommendations

### 4. Customer Rate Plan Changes  
**High-level Process:**
- Customer billing plan modifications
- Rate plan effective date management
- Customer pool assignments
- Billing synchronization
- Rate plan hierarchy validation

**Key Operations:**
- **Customer Plan Assignment** - Assign customer-specific rate plans
- **Effective Date Management** - Schedule plan change implementations
- **Pool Association** - Manage customer rate pools

### 5. Device Username Management
**High-level Process:**
- Username validation and formatting
- Device contact information updates
- User identifier synchronization
- Audit trail maintenance
- Permission validation

**Key Operations:**
- **Username Updates** - Modify device user identifiers
- **Contact Management** - Update device contact information

### 6. Device Archival
**High-level Process:**
- Device retirement validation
- Usage history preservation
- Status transition to archived
- Billing finalization
- Asset decommissioning

**Key Operations:**
- **Archive Devices** - Retire devices from active service
- **Usage Validation** - Verify recent usage before archival
- **Asset Management** - Update device inventory status

### 7. Device Deletion
**High-level Process:**
- Device removal validation
- ThingSpace device deletion API calls
- Database cleanup operations
- Audit logging
- Failed operation handling

**Key Operations:**
- **Bulk Device Deletion** - Remove multiple devices
- **Individual Device Deletion** - Single device removal
- **Failure Recovery** - Handle partial deletion scenarios

## Advanced Features

### Authentication & Security
- ThingSpace API authentication management
- Access token lifecycle management
- Session token refresh mechanisms
- Service provider credential validation

### Error Handling & Retry Logic
- HTTP retry policies for API failures
- SQL retry policies for database operations
- Callback result validation
- Failed operation reprocessing

### Monitoring & Logging
- Comprehensive audit trails
- Real-time operation monitoring
- Error escalation procedures
- Performance metrics collection

### Integration Points
- **Verizon ThingSpace API** - Primary service provider interface
- **AWS Lambda** - Serverless execution environment
- **SQS Queues** - Asynchronous processing coordination
- **SQL Server** - Device and operation state management
- **Rev Service** - Customer billing integration

## Technical Implementation Notes

### Change Type Processing Flow
Each change type follows a standardized pattern:
1. **Validation** - Input validation and business rule checks
2. **Authentication** - ThingSpace API credential management
3. **API Integration** - Verizon ThingSpace API calls
4. **Status Monitoring** - Operation status tracking
5. **Callback Processing** - Asynchronous result handling
6. **Database Updates** - State persistence and audit logging
7. **Error Handling** - Failure recovery and notification

### Service Provider Integration
The ThingSpace integration supports:
- Real-time device status synchronization
- Bulk operation processing
- Callback-based status updates
- Rate plan optimization
- Usage monitoring and reporting
- Device lifecycle management

This comprehensive system ensures reliable, scalable, and efficient management of Verizon ThingSpace IoT devices while maintaining data integrity and providing detailed operational visibility.