# Requirements

Priority uses MoSCoW: **M** = must, **S** = should, **C** = could.

## Functional requirements

| ID | Requirement | Priority |
|---|---|---|
| FR-01 | Users sign in with their Microsoft Entra ID company account. | M |
| FR-02 | A requester can create a request with vendor, category, justification, needed-by date and one or more line items (description, quantity, unit price, optional link). | M |
| FR-03 | The system calculates line totals and the request total automatically. | M |
| FR-04 | A requester can save a request as a draft and edit it until submitted. | M |
| FR-05 | On submit, the system builds the approval chain using BR-01 to BR-04. | M |
| FR-06 | Each approver sees a queue of requests waiting on them. | M |
| FR-07 | An approver can approve, reject or return a request. A comment is required to reject or return. | M |
| FR-08 | The system emails the next approver on submit and after each approval, and emails the requester on every decision. | M |
| FR-09 | A requester sees a "My requests" list with current status and full history. | M |
| FR-10 | Every status change is recorded with user, timestamp, action and comment. | M |
| FR-11 | Purchasing can mark approved requests as Ordered and then Received. | M |
| FR-12 | A requester can cancel a request before it is Approved. | S |
| FR-13 | Managers see a dashboard of pending requests and team spend by month and category. | S |
| FR-14 | An admin can change the finance threshold and the categories that require IT approval. | S |
| FR-15 | Finance can export requests for a date range to CSV. | C |

## Quality (non-functional) requirements

| ID | Requirement | Priority |
|---|---|---|
| NFR-01 | **Security:** only authenticated Entra users can access the app. Access is role-based (see roles below). | M |
| NFR-02 | **Security:** requesters can view only their own requests. Approvers can view requests assigned to them. Finance, Purchasing and Admin can view all. | M |
| NFR-03 | **Audit integrity:** history records are never updated or deleted by the application. | M |
| NFR-04 | **Performance:** pages load in under 2 seconds with 50 concurrent users. | S |
| NFR-05 | **Availability:** 99% during business hours (07:00–19:00, Mon–Fri). | S |
| NFR-06 | **Accessibility:** forms and dashboards meet WCAG 2.1 AA for keyboard use, labels and contrast. | S |
| NFR-07 | **Retention:** requests and history are kept for 7 years. | S |

## Roles

| Role | Source | Can do |
|---|---|---|
| Requester | Every signed-in user | Create, submit, cancel own requests |
| Manager | Entra manager relationship (no role needed) | Approve direct reports' requests, view team dashboard |
| IT Approver | Entra app role | Approve IT-category step |
| Finance Approver | Entra app role | Approve over-threshold step, view all, export |
| Purchasing | Entra app role | Mark Ordered and Received, view all |
| Admin | Entra app role | Change settings, reassign stuck approvals, view all |
