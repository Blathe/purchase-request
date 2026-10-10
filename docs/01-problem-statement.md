# Problem Statement and Stakeholders

## Problem

Purchase requests today are approved by email and tracked in a shared spreadsheet.

- Requests get lost in inboxes. Nobody knows who is holding a request.
- Approval rules (amount limits, IT sign-off) are applied from memory, so they are applied inconsistently.
- There is no reliable audit trail. Finding who approved a purchase means searching email.
- Managers cannot see what is pending or what their team has spent without asking finance.
- Purchasing re-types request details from email into the spreadsheet.

## Proposed solution

An internal web app where employees submit purchase requests, the system routes each request to the right approvers based on amount and category, and every step is recorded. Managers get a dashboard of pending requests and spend.

## Stakeholders

| Stakeholder | Role in the process | What they need |
|---|---|---|
| Requester (any employee) | Submits requests | Easy form, visible status, fewer follow-up emails |
| Manager | First approver for their direct reports | One queue, team spend at a glance |
| IT approver | Approves IT hardware, software and SaaS | Sees only IT-category requests |
| Finance approver | Approves requests over the threshold | Consistent enforcement of limits, audit trail |
| Purchasing | Places orders for approved requests | Clean list of approved requests, no re-typing |
| Admin / IT ops | Runs the app, assigns roles | Entra ID sign-in, low maintenance |
| Auditor | Reviews past purchases | Complete, unchangeable history |

## Success measures

Baselines below are assumptions until measured during the first month.

| ID | Measure | Baseline (assumed) | Target |
|---|---|---|---|
| SM-01 | Average time from submit to final approval | 5 business days | 2 business days or less |
| SM-02 | Requests with a complete audit trail | ~60% | 100% |
| SM-03 | "Where is my request?" emails to purchasing | ~10 per week | 2 per week or fewer |
| SM-04 | Requests approved without the required finance or IT step | Unknown | 0 |
| SM-05 | Spreadsheet retired | In use | Retired within 1 month of launch |

## Scope

**In scope (v1):** request form, routing, approvals, email notifications, ordered/received tracking, manager dashboard, audit trail, admin settings.

**Out of scope (v1):** purchase order PDFs, ERP or accounting integration, file attachments, budget tracking.
