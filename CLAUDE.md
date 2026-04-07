# Trucking Operations Platform — Project Brief

Read this file fully before writing any code or asking any questions. It contains all schema decisions, business rules, and design context agreed upon during planning.

---

## Project Overview

An internal web application for a small trucking company. Replaces manual spreadsheets and PDFs with a centralized platform for load tracking, driver pay calculation, invoicing, and analytics.

**Timeline:** 6–8 week working prototype  
**Stack:** To be decided, but must use SQL-based relational database (schema below)  
**Users:** Managers and truck drivers (two distinct roles)

---

## Core Modules

1. **Load management** — create and track loads/jobs
2. **Invoicing** — auto-generate invoices from load data, export to PDF
3. **Driver pay** — calculate weekly driver compensation, export pay sheet
4. **Analytics dashboard** — revenue by route/driver/time period, fuel spend analysis, AI chat

---

## Authentication & Roles

Two roles stored on the `users` table via an `enum` field:

- **Manager** — full access to all modules, all drivers, all loads
- **Trucker** — read-only access to their own loads and pay summaries only

A trucker's user account links directly to their driver record via `driver_id` (FK). Manager accounts leave `driver_id` null.

---

## Database Schema

### Table: `users`
| Column | Type | Notes |
|---|---|---|
| user_id | INT PK | Auto-increment |
| username | VARCHAR(50) | Unique |
| password_hash | VARCHAR(255) | |
| role | ENUM('trucker','manager') | |
| driver_id | INT FK | Nullable — links to drivers |
| created_at | DATETIME | Default current timestamp |

---

### Table: `drivers`
| Column | Type | Notes |
|---|---|---|
| driver_id | INT PK | Auto-increment |
| unit_number | VARCHAR(20) | Truck unit number e.g. "6690" |
| first_name | VARCHAR(50) | |
| last_name | VARCHAR(50) | |
| address | VARCHAR(255) | |
| commission_rate | DECIMAL(5,4) | Default 0.18 (18%) — see pay rules below |
| created_at | DATETIME | |

---

### Table: `loads`
| Column | Type | Notes |
|---|---|---|
| load_id | INT PK | Auto-increment |
| load_number | VARCHAR(50) | Unique, human-facing e.g. "1061447" |
| ship_date | DATE | |
| origin | VARCHAR(255) | Free text e.g. "Haleyville, AL" |
| destination | VARCHAR(255) | Free text e.g. "Shipshewana, IN" |
| description | VARCHAR(255) | Cargo type e.g. "Steel Coil" |
| line_haul_rate | DECIMAL(10,2) | Base freight rate |
| fsc_rate | DECIMAL(10,2) | Fuel surcharge — driver keeps 100% |
| terms | VARCHAR(50) | Default "Net 30" |
| status | ENUM('pending','complete','invoiced','paid') | Default 'pending' |
| bill_to_name | VARCHAR(255) | Free text — not a relational FK |
| bill_to_address | VARCHAR(255) | Free text |
| consignee_name | VARCHAR(255) | Free text |
| consignee_address | VARCHAR(255) | Free text |
| created_at | DATETIME | |

**Note:** Bill to and consignee are free text fields. They are not normalized into a separate customers table — this was a deliberate design decision.

**Note:** There is NO estimation field. It was removed from scope.

---

### Table: `load_drivers` (junction)
| Column | Type | Notes |
|---|---|---|
| load_driver_id | INT PK | Auto-increment |
| load_id | INT FK | References loads |
| driver_id | INT FK | References drivers |

**Relationship:** One load can have multiple drivers (1:M loads → drivers). The junction table handles this. In practice most loads will have one driver — the structure supports both. Unique constraint on (load_id, driver_id) prevents duplicate assignments.

---

### Table: `invoices`
| Column | Type | Notes |
|---|---|---|
| invoice_id | INT PK | Auto-increment |
| invoice_number | VARCHAR(50) | Human-facing ID e.g. "101630" — unique |
| load_id | INT FK | One invoice per load |
| invoice_date | DATE | |
| due_date | DATE | Auto-calculated: invoice_date + 30 days for Net 30 |
| payment_status | ENUM('unpaid','paid') | Default 'unpaid' |
| paid_date | DATE | Nullable |
| created_at | DATETIME | |

Invoice is auto-generated from load data when manager clicks "Generate Invoice". All fields (bill to, consignee, rate, terms, load number, ship date) pull from the linked load record. Must export to PDF matching the sample invoice format.

**Sample invoice format:**
- Header: company name/address top left, "Invoice" title top right
- Box: Date + Invoice # top right
- Box: Bill To (left) | Consignee (right)
- Table: Load No. | Terms | Ship Date | Unit No.
- Table: Description | Quantity | Rate | Amount
- Footer note: "Rate Confirmation attached" | Total

---

### Table: `driver_advances`
| Column | Type | Notes |
|---|---|---|
| advance_id | INT PK | Auto-increment |
| driver_id | INT FK | References drivers |
| load_id | INT FK | **Nullable** — optional link to a specific load/route |
| advance_date | DATE | |
| advance_type | ENUM('Fuel','EzPass','Cash','Insurance','WorkersComp','Other') | |
| amount | DECIMAL(10,2) | Positive = money out to driver. Negative values for repayments. |
| notes | VARCHAR(255) | |

**Note:** `load_id` is optional but important — when a fuel advance is linked to a load, it enables fuel spend analysis by route on the analytics dashboard.

---

### Table: `driver_pay_summaries`
| Column | Type | Notes |
|---|---|---|
| summary_id | INT PK | Auto-increment |
| driver_id | INT FK | References drivers |
| pay_period_start | DATE | |
| pay_period_end | DATE | Typically weekly |
| total_line_haul | DECIMAL(10,2) | Sum of line_haul_rate across loads in period |
| commission_rate | DECIMAL(5,4) | Snapshot of driver's rate at time of pay — do NOT recalculate from current driver record |
| total_fsc | DECIMAL(10,2) | Sum of fsc_rate across loads in period |
| total_advances | DECIMAL(10,2) | Sum of all advance amounts in period |
| insurance_deduction | DECIMAL(10,2) | |
| workers_comp_deduction | DECIMAL(10,2) | |
| net_pay | DECIMAL(10,2) | See formula below |
| created_at | DATETIME | |

---

## Driver Pay Calculation

Derived from the actual pay sheet used by the company. Formula:

```
gross_driver_pay = (total_line_haul × (1 - commission_rate)) + total_fsc

net_pay = gross_driver_pay - total_advances - insurance_deduction - workers_comp_deduction
```

**Key rules:**
- Company takes `commission_rate` (default 18%) of line haul only
- Driver keeps 100% of FSC — no commission deducted from fuel surcharge
- Advances (Fuel, EzPass, Cash) are deducted from net pay
- Insurance and Worker's Comp are separate line-item deductions
- `commission_rate` is snapshotted onto the pay summary at generation time so historical records stay accurate if the rate changes later

**Weekly pay sheet export** must match the format of the sample Excel file:
- Driver name, unit number, address at top
- Table: Date | Load (route) | Line Haul | FSC
- Totals, then deductions listed below
- Right-side column: advances log with dates and types
- Net pay at bottom

---

## Analytics Dashboard

All analytics are derived from existing data — no additional data entry required.

| View | Data source |
|---|---|
| Revenue by route | GROUP BY origin + destination on loads |
| Revenue by driver | JOIN load_drivers → loads, GROUP BY driver |
| Revenue by time period | Filter loads by ship_date range |
| Fuel spend by route | driver_advances WHERE advance_type='Fuel' AND load_id IS NOT NULL, JOIN loads for route |
| Fuel spend by driver | driver_advances WHERE advance_type='Fuel', GROUP BY driver |
| Basic profitability | Revenue vs total advance costs per period |

**AI chat feature:** A natural language interface over the analytics data. Manager can ask plain-English questions and get answers without manually running reports.

---

## Entity Relationships Summary

```
users           }o--||  drivers             (user account → driver profile, optional)
loads           ||--|{  load_drivers        (load has many drivers)
drivers         ||--|{  load_drivers        (driver assigned to many loads)
loads           ||--o|  invoices            (one load → one invoice, optional)
drivers         ||--o{  driver_advances     (driver receives many advances)
loads           ||--o{  driver_advances     (advance optionally tied to a load)
drivers         ||--|{  driver_pay_summaries (driver has many pay summaries)
```

---

## Sample Data Reference

**Sample load (from rate confirmation):**
- Load number: 1061447
- Ship date: 1/19/2026
- Unit: 6690
- Description: Steel Coil
- Rate: $1,143.08
- Terms: Net 30
- Bill to: Bob Supplier, 123 First St, Chatanooga TN
- Consignee: Todd Supplier, 456 Second St, Huntsville AL

**Sample pay period (from pay sheet):**
- Driver: John Doe, Unit 1516
- Period: 2/13/2026 – 2/18/2026
- Loads: 4 loads across AL, IN, MS, TN routes
- Total line haul: $5,878.96
- Total FSC: $873.48
- Commission deducted: 18% of line haul = -$1,058.21
- FSC added back (no commission): +$873.48
- Advances (fuel + EzPass + cash): -$1,877.62
- Insurance: -$284.20
- Worker's Comp: -$33.70
- Net pay: $3,498.71

---

## Out of Scope (for now)

- Multi-company or multi-location support
- Customer/shipper relational records (bill-to and consignee are free text)
- Mileage tracking
- Maintenance logs
- Any external API integrations (fuel price feeds, mapping, etc.)
