# Assign Customer Change Type - Data Flow Diagram

```mermaid
flowchart TD
    A[Customer Change Type Request] --> B[Validate Customer Information]
    
    B --> C{Customer Validation Check}
    
    C -->|Invalid Customer| D[Mark as Invalid]
    C -->|Valid Customer| E[Check Customer Eligibility]
    
    D --> F[Generate Error Response]
    F --> G[Log Invalid Customer]
    
    E --> H{Eligibility Check}
    
    H -->|Not Eligible| I[Mark as Ineligible]
    H -->|Eligible| J[Process Change Type Assignment]
    
    I --> K[Generate Ineligibility Response]
    K --> L[Log Ineligible Customer]
    
    J --> M[Execute Assignment Procedure]
    
    M --> N[usp_CustomerChangeType_AssignChangeType]
    
    N --> O{Portal Type Check}
    
    O -->|Customer Portal| P[Log Customer Portal Entry]
    O -->|Admin Portal| Q[Log Admin Portal Entry]
    
    P --> R[Update Customer Profile]
    Q --> R[Update Customer Profile]
    
    R --> S[Set IsAssigned = true]
    
    S --> T[Set AssignmentDate = current timestamp]
    
    T --> U[Update Change Type Status]
    
    U --> V[Notify Customer]
    
    V --> W[Assignment Complete]
    
    style A fill:#e1f5fe
    style W fill:#c8e6c9
    style F fill:#ffcdd2
    style K fill:#ffcdd2
    style G fill:#fff3e0
    style L fill:#fff3e0
```

## Process Description

### Input
- **Customer Change Type Request**: Request to assign a specific change type to a customer

### Validation Steps
1. **Validate Customer Information**: Verify customer details and request format
2. **Customer Validation Check**: Ensure customer exists and data is complete
3. **Check Customer Eligibility**: Verify customer meets criteria for change type assignment

### Processing Flow
1. **Process Change Type Assignment**: Main processing logic for assignment
2. **Execute Assignment Procedure**: Run stored procedure for database operations
3. **Portal Type Check**: Determine which portal initiated the request (Customer/Admin)
4. **Log Entry**: Record the assignment action in appropriate portal logs

### Database Updates
1. **Update Customer Profile**: Modify customer record with new change type
2. **Set IsAssigned = true**: Mark the change type as assigned
3. **Set AssignmentDate**: Record timestamp of assignment
4. **Update Change Type Status**: Update status tracking

### Completion
1. **Notify Customer**: Send confirmation to customer
2. **Assignment Complete**: Process successfully finished

### Error Handling
- **Invalid Customer**: Log error and generate appropriate response
- **Ineligible Customer**: Log ineligibility reason and notify requester

## Key Components

- **Stored Procedure**: `usp_CustomerChangeType_AssignChangeType`
- **Portal Logging**: Separate logs for Customer Portal and Admin Portal access
- **Status Tracking**: IsAssigned flag and AssignmentDate timestamp
- **Notification System**: Customer notification upon successful assignment