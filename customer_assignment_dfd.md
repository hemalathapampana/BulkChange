# Customer Assignment Process - Data Flow Diagram (DFD)

## Level 0 DFD (Context Diagram)

```
                    Customer Assignment Request
                              │
                              ▼
        ┌─────────────┐  Assignment Response  ┌─────────────┐
        │  Customer   │◄─────────────────────►│   Customer  │
        │   (Entity)  │                       │ Assignment  │
        └─────────────┘                       │   System    │
                                              │ (Process 0) │
                                              └─────────────┘
                                                     │
                                                     ▼
                                              ┌─────────────┐
                                              │  Customer   │
                                              │  Database   │
                                              │(Data Store) │
                                              └─────────────┘
```

## Level 1 DFD (Detailed Process Breakdown)

```
        ┌─────────────┐
        │  Customer   │
        │   (Entity)  │
        └─────────────┘
                │ Assignment Request
                ▼
        ┌─────────────────┐
        │    Process 1    │
        │    Validate     │  Customer Data
        │    Customer     │◄────────────────┐
        │   Information   │                 │
        └─────────────────┘                 │
                │                           │
                │ Validation Result         │
                ▼                           │
        ┌─────────────────┐                 │
        │    Process 2    │                 │
        │   Customer      │                 │
        │   Validation    │                 │
        │     Check       │                 │
        └─────────────────┘                 │
         │              │                   │
         │ Invalid      │ Valid             │
         ▼              ▼                   │
┌─────────────────┐ ┌─────────────────┐    │
│    Process 3    │ │    Process 4    │    │
│    Generate     │ │    Process      │    │
│     Error       │ │   Customer      │    │
│   Response      │ │   Assignment    │    │
└─────────────────┘ └─────────────────┘    │
         │                   │              │
         │ Error Response    │              │
         ▼                   ▼              │
┌─────────────────┐ ┌─────────────────┐    │
│    Process 5    │ │    Process 6    │    │
│  Log Invalid    │ │    Execute      │    │
│   Customer      │ │   Assignment    │    │
│                 │ │   Procedure     │    │
└─────────────────┘ └─────────────────┘    │
         │                   │              │
         │ Log Data          │              │
         ▼                   ▼              │
┌─────────────────┐ ┌─────────────────┐    │
│   Data Store    │ │    Process 7    │    │
│  D1: Error      │ │    Create/      │    │
│      Log        │ │   Update        │    │
└─────────────────┘ │  RevService     │    │
                    └─────────────────┘    │
                             │              │
                             │ Service Data │
                             ▼              │
                    ┌─────────────────┐    │
                    │   Data Store    │    │
                    │ D2: RevService  │    │
                    │    Database     │    │
                    └─────────────────┘    │
                             │              │
                             ▼              │
                    ┌─────────────────┐    │
                    │    Process 8    │    │
                    │    Update       │────┘
                    │   Customer      │
                    │    Profile      │
                    └─────────────────┘
                             │
                             │ Updated Profile
                             ▼
                    ┌─────────────────┐
                    │   Data Store    │
                    │ D3: Customer    │
                    │    Database     │
                    └─────────────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │    Process 9    │
                    │      Set        │
                    │  Assigned =     │
                    │     true        │
                    └─────────────────┘
                             │
                             │ Assignment Status
                             ▼
                    ┌─────────────────┐
                    │    Process 10   │
                    │    Notify       │
                    │   Customer      │
                    └─────────────────┘
                             │
                             │ Notification
                             ▼
                    ┌─────────────────┐
                    │    Customer     │
                    │    (Entity)     │
                    └─────────────────┘
```

## Data Stores:
- **D1: Error Log** - Stores invalid customer information and error details
- **D2: RevService Database** - Stores revenue service records and assignments  
- **D3: Customer Database** - Stores customer profiles and assignment status

## External Entities:
- **Customer** - Initiates assignment requests and receives responses/notifications

## Key Data Flows:
1. **Assignment Request** - Customer → Process 1
2. **Customer Data** - D3 → Process 1  
3. **Validation Result** - Process 1 → Process 2
4. **Error Response** - Process 3 → Customer
5. **Log Data** - Process 5 → D1
6. **Service Data** - Process 7 → D2
7. **Updated Profile** - Process 8 → D3
8. **Assignment Status** - Process 9 → Process 10
9. **Notification** - Process 10 → Customer

## Process Descriptions:
- **Process 1**: Validate Customer Information
- **Process 2**: Customer Validation Check (Decision Point)
- **Process 3**: Generate Error Response
- **Process 4**: Process Customer Assignment
- **Process 5**: Log Invalid Customer
- **Process 6**: Execute Assignment Procedure
- **Process 7**: Create/Update RevService
- **Process 8**: Update Customer Profile
- **Process 9**: Set Assigned Status
- **Process 10**: Notify Customer