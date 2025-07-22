# Device Association Flow - Step-by-Step Process

## Overview
This document explains how to connect devices to customers in the system, from when a user starts the process until everything is complete and recorded.

---

## Phase 1: User Starts the Process

The user opens the device connection screen and picks one or more devices from the list using their ID numbers. They choose which customer should own these devices from a dropdown menu. The user then sets up options: whether to create a billing service (yes or no), whether to add carrier billing plans (yes or no), whether to add customer billing plans (yes or no), and when this should start (if not chosen, uses when device was first turned on). Finally, the user clicks submit to start the process.

**Website Address**: Send request to `/M2M/AssociateCustomer`

---

## Phase 2: System Checks Everything is Valid

The system receives the request with all the devices and settings. It checks if the user has permission to make these changes. For each device, the system makes sure the device exists in the database, belongs to the right service provider, is not deleted or archived, and can be assigned to customers. The system also checks the customer exists, the user can assign devices to this customer, and gets the customer's location and billing information. It looks for problems like devices already belonging to other customers or having active services. During this step, the system looks up devices in the main device list, checks current device ownership records, and verifies customer details and permissions.

---

## Phase 3: System Creates Work Orders

The system gathers the customer's location details and billing information, then gets the login credentials needed to talk to the billing system. It creates a detailed work order containing all device information, customer details and locations, which services to create, which billing plans to use, and when everything should start. The system makes individual work orders for each device and registers the whole job with a tracking number. This step creates entries in the main job tracking table, individual device work order table, and prepares detailed instructions for the background processor.

---

## Phase 4: System Starts Background Work

The job is sent to a background work queue so it doesn't slow down the website. The user gets a success message with a tracking number to check progress later. A background worker program picks up the job and starts processing all the work orders.

**Background System**: Work queue holds all the job details

---

## Phase 5: System Processes Each Device

The background worker reads the details for each device work order. If billing service creation is turned on, the system prepares a request to create a new billing account including customer billing details, device phone numbers, and service types, then sends this request to the billing company's computer system and records whether it worked or failed. The billing services table gets updated with new account details if successful.

The system then updates the device ownership records by setting which customer location owns the device, recording the customer's account number for billing, saving the billing system login information, and marking the device as active and not deleted.

If customer billing plans are turned on, the system updates the device with the customer's specific billing plan, sets data usage limits, runs a procedure to update device history across all systems, and schedules future billing plan changes if needed.

---

## Phase 6: System Records What Happened

The system creates history records showing device was assigned to customer, billing services were created, billing plans were changed, and when everything happened with user information. It also logs the results for each device including whether it worked or failed, details from the billing system, any error messages, and completion times. The main device work order table gets updated with final status and error details.

---

## Phase 7: System Finishes Everything

The system runs cleanup procedures to keep device history updated across all provider systems and updates device-to-service connections using database maintenance procedures. The main job is marked as complete or shows which parts failed. A notification is sent to the newer system version for performance improvements, and final job status and completion statistics are recorded in the job log table.

---

## Technical Information

**Main Website Address**:
- Send device assignment requests to `/M2M/AssociateCustomer`

**Database Maintenance Procedures**:
- Update device history across providers
- Mark device changes as complete
- Sync device-to-service connections

**External System Calls**:
- Billing company system creates new billing accounts

**Background Processing Parts**:
- Main device assignment processor
- Billing service creator
- Work order builder

---

## When Things Go Wrong

**Validation Problems**: Device not found or deleted, user doesn't have permission, device already belongs to someone else

**Processing Problems**: Billing system fails, database updates fail, billing plan assignment fails

**How System Handles Problems**: Tries again for temporary failures, logs errors for debugging, handles partial success when some devices work but others don't

---

## How to Know if it Worked

A successful device assignment includes:
1. ✅ Device found and accessible
2. ✅ User has permission
3. ✅ Billing service created (if requested)
4. ✅ Device ownership updated
5. ✅ Billing plans assigned (if requested)
6. ✅ History recorded
7. ✅ Processing completed

This process ensures devices are properly connected to customers with correct billing setup and complete record keeping while handling problems smoothly at each step.