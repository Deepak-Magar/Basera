# Basera — Product Specification

Version 0.1 · 2026-10-13 · Status: agreed, pre-implementation

Basera (बसेरा) is subscription software for student hostels and paying-guest houses in Nepal. An operator runs rooms, residents, monthly billing on the Nepali calendar, payments, mess, complaints and the police guest register from a website that works on a phone; residents and parents use the same site to see bills, pay and read notices. See `ARCHITECTURE.md` for how it is built and `DECISIONS.md` for why.

## 1. Problem

A hostel near a college has 20 to 200 residents, an owner, a warden, a cook and a paper register. Rent is due on the first of the Nepali month and arrives late, in parts, in cash, by bank transfer without a reference, or to the owner's personal wallet. Deposits are forgotten at checkout. Mess is charged flat whether or not the resident ate. The police require a resident register with ID copies and ask for it after incidents. Notices go to a muted chat group. Complaints are verbal. Checkout settlements are negotiated. None of this is served by existing software, which targets apartments or hotels.

## 2. Users and roles

| Role | Who | Primary needs |
| --- | --- | --- |
| Owner | hostel proprietor; the buyer | collections, outstanding, occupancy, leakage control, compliance documents |
| Manager | runs one property for an owner with several | same as owner for one property, no financial deletions |
| Warden | daily operator, usually lives on site | check-in/out, cash receipts, complaints, attendance, mess count |
| Cook | mess | tomorrow's headcount, menu |
| Resident | student or working tenant, 17–30 | bill, pay, notices, meal opt-out, complain, leave |
| Parent | guardian, often the payer | bill, pay, attendance and leave, notices |
| Platform admin | Basera staff | tenants, plans, support (later phase) |

## 3. Tenancy and plans

A tenant is one hostel business with a name, PAN, address, phone, logo and plan. A tenant has one or more properties (branches). Plans by bed count: Starter (≤30 beds), Standard (≤100), Pro (unlimited, multi-property). Trial 60 days. Billing of tenants themselves is manual in phase one (invoice by email, mark paid by platform admin).

## 4. Modules and features

### 4.1 Identity

Tenant registration (owner self-signup with phone verification), staff invitation by phone with role, password login for staff, OTP login for residents and parents, language preference, profile, password reset by OTP. Roles and policies as in `ARCHITECTURE.md` §7.

### 4.2 Properties

- Property: name, address, ward, geo (optional), contact, rules text, QR payment image (phase one payments), active flag.
- Floor: number, label. Room: number, type (single/double/triple/four/dorm), attributes (attached bath, balcony, AC, window), notes. Bed: code, monthly rate (from rate plan or override), status (free, reserved, occupied, maintenance), current resident.
- Rate plan: named set of rates by room type, effective from a BS date; a bed uses the plan unless overridden.
- Floor map: grid by floor, beds colour-coded, click to open the bed (resident, rate, history). Waiting list: name, phone, preferred type, requested from, notes; convert to resident when a bed frees.
- Occupancy history per bed (who, from, to) for the register and reports.

### 4.3 Residents

- Resident: photo, full name (Nepali and English), sex, DOB (BS/AD), ID type and number (citizenship, passport, college ID, other) with image, college/employer and programme, home address (province/district/municipality/ward), phone, email, blood group, emergency contact, guardian(s) (name, relation, phone; becomes a Parent login), agreement date, notes, status.
- Stay: one per admission; bed, check-in date, agreed monthly rate, mess plan, deposit amount, notice date, check-out date, settlement reference. A resident may have several stays over time (room changes create a new stay segment).
- Lifecycle: Reserved → Active → NoticeGiven → CheckedOut; Active ↔ OnLeave. Check-in wizard on the warden's phone: pick bed, capture ID photo, guardian, deposit received (creates a Payments receipt), agreement, send welcome SMS with login link.
- Documents stored privately; residents can view their own.

### 4.4 Billing

- Billing period = Bikram Sambat month. Invoice generated on the 1st (job) for every Active stay: rent line (pro-rated by days for partial months using the BS month length), mess line (flat plan or per-day from Mess), extras (laundry, parking, electricity share, custom charge types defined by the owner), discounts (fixed or percent, with reason), late fee lines added by the daily job per the tenant's rule (grace days, fixed or percent, cap).
- Invoice: number `INV-{YYYY}-{MM}-{seq}` (BS), resident, stay, period, lines, subtotal, discount, total, paid, balance, due date (default 7th of the month, configurable), status (draft, issued, partially paid, paid, void, settled). Void only by reversing invoice; never edited after issue.
- Ledger: every invoice, payment, deposit movement and adjustment is a ledger entry against the resident's account; balance is the sum. Advance payments carry forward.
- Deposits: separate ledger; collected at check-in, held, refunded or applied at settlement with itemised deductions (damage, unpaid balance) and photos.
- Settlement at checkout: computes rent to date (pro-rated), mess days, extras, less payments, less deposit, plus deductions; produces a settlement statement PDF for signature; records refund or balance due.
- Invoice PDF with the hostel's name, PAN, address, resident, lines, QR for payment, both calendars.

### 4.5 Payments

- Receipt: number `RCP-...`, resident, amount, method (cash, bank transfer, eSewa, Khalti, Fonepay, card, other), reference, received by, received at, note, slip image. Allocation to one or more invoices (oldest first by default, editable), remainder to advance.
- Phase one methods: cash entered by staff; bank slip uploaded by resident or staff, pending until a staff member confirms; QR "I have paid" from the resident's bill page creating a pending receipt with the resident's reference.
- Phase two: gateway sessions (eSewa, Khalti, Fonepay) with server-side verification and webhooks; automatic allocation; reconciliation screen comparing gateway settlements to receipts.
- Reversal: a receipt is reversed by a reversing receipt with reason; both stay visible.
- Receipt SMS and printable/PDF receipt.

### 4.6 Mess

- Meal plan per property: flat monthly, per-day, or none; meals included (breakfast, lunch, dinner); daily rate for per-day plans; cutoff time for opt-out (e.g. 15:00 for dinner).
- Menu: weekly template with daily overrides, visible to residents.
- Opt-out: resident marks a meal or a date range as not eating before the cutoff; staff can mark on behalf. Leave (Operations) auto-creates opt-outs.
- Headcount: computed for today and tomorrow per meal; cook screen shows it large.
- Charges: per-day plans accrue daily and feed the monthly invoice; flat plans add one line.

### 4.7 Operations

- Attendance: optional nightly roll call; warden ticks residents present, marks absent-with-leave or absent-without-notice; parents of minors (configurable) get an SMS on absent-without-notice.
- Leave: resident requests dates and reason; warden approves; creates mess opt-outs; visible to parent.
- Complaints: category (water, electricity, wifi, cleanliness, food, safety, other), description, photo, room; status (open, assigned, in progress, resolved, closed) with timestamps; assignment to staff; resident rating on close. Owner view of ageing and categories.
- Maintenance tasks: recurring (tank cleaning, pest control, geyser service) and one-off; assignee; due dates; completion photo.

### 4.8 Notifications

- Notices: title, body, audience (all, property, floor, room, individual), pinned, expiry, attachment; in-site inbox with read tracking; optional SMS for important notices.
- Reminder rules per tenant: days before due, on due, days after due; template text in Nepali and English with placeholders; channel (SMS, in-site, both); quiet hours. Reminders record what was sent to whom.
- Transactional messages: welcome, OTP, receipt, invoice issued, leave approved, complaint updated, settlement.
- Development: `/dev/sms` inbox page lists all mock SMS.

### 4.9 Compliance

- Police guest register: for a date range, every resident with name, ID type and number, address, guardian, check-in and check-out, bed; printable PDF in the accepted format with ID thumbnails optional; monthly snapshot stored so historical reports are stable.
- Ward rent-tax statement: rental income per property per BS fiscal year with the applicable rate configured by the owner.
- Owner income statement: billed, collected, outstanding, deposits held, by month and property.

### 4.10 Reporting

Dashboard tiles (today's cash, month collection %, empty beds, open complaints, due today). Reports: occupancy by month and property, collections vs billed, outstanding ageing (0–7, 8–30, 31–60, 60+ days), mess cost per resident-day, complaint resolution time, resident churn and average stay, staff cash collected per day. Export CSV. Read models refreshed nightly and on demand.

### 4.11 Audit

Immutable log of financial postings, resident profile and document changes, role changes, settings changes, cross-tenant admin access; viewable by Owner for their tenant.

## 5. Screens by area

**Owner (desktop first):** Dashboard · Properties (list, edit, floor map, rate plans, waiting list) · Residents (list, profile, documents, stays) · Billing (invoices, charge types, discounts, late-fee rules, deposits, settlements) · Payments (receipts, pending confirmations, reversals, reconciliation) · Mess (plans, menu, headcount, charges) · Operations (complaints board, maintenance, attendance summary, leave) · Notices · Compliance (register, rent-tax, income) · Reports · Settings (hostel profile and PAN, staff and roles, reminder rules, payment QR, language, plan and billing) · Audit log.

**Staff (phone first):** Today (due today, cash collected, assigned complaints, headcount) · Record payment · Check-in wizard · Check-out and settlement · Mess count and menu · Attendance roll call · Leave approvals · Complaint queue · Post notice · Resident search.

**Resident (phone first):** My bill (balance, current invoice, lines, pay options) · Receipts · Notices · Menu and meal opt-out · Complaints (new, mine) · Leave request · My documents and stay details · Hostel rules and contacts · Language.

**Parent (phone first):** Bill and pay · Attendance and leave · Notices · Contact warden.

**Platform admin (later):** Tenants, plans, usage, support impersonation with audit.

## 6. Business rules (normative)

1. Pro-rating: rent for a partial month = monthly rate × days occupied ÷ days in that BS month, rounded to the nearest rupee; check-in day counts, check-out day does not.
2. Invoice due date defaults to the 7th of the BS month; late fee applies after `graceDays` past due, computed once per day by the job, capped by `maxLateFee` if set.
3. Payment allocation: oldest unpaid invoice first unless the staff member chooses otherwise; overpayment becomes advance; advance is applied to the next invoice automatically.
4. Deposit is never applied to invoices while the stay is Active; only at settlement.
5. Mess per-day charge accrues for every day the resident is Active and not opted out before the cutoff; opt-outs after the cutoff do not count.
6. A bed can hold one Active stay at a time; reserving a bed blocks it for `reservationDays` (default 7).
7. Financial rows are never edited or deleted; corrections are reversing entries referencing the original, with a reason.
8. Residents see only their own data; parents see only linked residents' billing, attendance, leave and notices; staff see the properties they are assigned to; owners see all tenant data.
9. All tenant data queries are filtered by `TenantId`; the only exception is platform admin with an audit record.
10. Dates are stored in AD; the BS calendar is the display and billing-period calendar.

## 7. Non-functional requirements

Works on a 2 GB Android phone over 3G for Staff/Resident/Parent areas (pages under 300 KB, no heavy JS). Nepali and English. 99% monthly availability target in phase one. Nightly backups, 30-day retention. Personal data (ID images) encrypted at rest and never publicly addressable. Audit log retained for 7 years. Page response under 500 ms for list pages with 200 residents.

## 8. Phases

- Phase one (sellable): Identity, Properties, Residents, Billing (invoices, deposits, ledger, invoice PDF), Payments (cash, slip, QR confirm), Notifications (notices, reminders, mock SMS), Compliance (register PDF), basic dashboard.
- Phase two: Mess, Operations, settlement at checkout, payment gateways, reconciliation, parent area, real SMS.
- Phase three: Reporting suite, rent-tax and income statements, multi-property, waiting list, maintenance schedules, platform admin, PWA polish, Play Store wrapper.

## 9. Out of scope (version one)

Accounting system integration, multi-currency, hotel-style daily bookings, biometric attendance hardware, mobile app, public API for third parties, marketplace for finding hostels.

## 10. Glossary

Tenant (hostel business) · Property (branch) · Stay (one admission of a resident in a bed) · BS (Bikram Sambat) · Settlement (final account at checkout) · Opt-out (skipping a meal) · Register (police guest register) · Outbox (reliable event delivery table).
