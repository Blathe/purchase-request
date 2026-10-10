# UAT Cases and Traceability Matrix

## UAT cases

Test users: **Riley** (requester), **Morgan** (Riley's manager), **Jordan** (IT approver), **Casey** (Finance approver), **Avery** (Purchasing). Finance threshold: $5,000.

| ID | Steps | Expected result |
|---|---|---|
| UAT-01 | Open the app signed out. Sign in as Riley. | Redirected to Microsoft sign-in, then home page shows "Riley". |
| UAT-02 | As Riley, create a request: Office Supplies, 3 × $20.00. Save draft. | Status Draft, total $60.00, number PR-YYYY-NNNN assigned. |
| UAT-03 | As Riley, submit the UAT-02 request. | Status PendingApproval. Morgan gets an email. Request is in Morgan's queue. |
| UAT-04 | As Riley, submit a Software request for $500. | Chain is Manager → IT. No Finance step. |
| UAT-05 | As Riley, submit an Office Supplies request for exactly $5,000.00. | Chain is Manager only (BR-02: equal is not over). |
| UAT-06 | As Riley, submit a Hardware request for $5,000.01. | Chain is Manager → IT → Finance. |
| UAT-07 | As Morgan, open My approvals. | Riley's pending requests are listed, oldest first. |
| UAT-08 | As Morgan, click Reject with no comment. | Message "A comment is required." Status unchanged. |
| UAT-09 | As Morgan, return the UAT-03 request with a comment. Riley edits and resubmits. | Status goes Draft, then PendingApproval with a new chain. Old step shows Superseded in history. |
| UAT-10 | Approve all steps of the UAT-06 request as Morgan, Jordan, Casey. | Status Approved. Riley and Avery get emails. |
| UAT-11 | As Avery, mark the UAT-10 request Ordered, then Received. | Status Ordered, then Received. Riley gets emails. |
| UAT-12 | As Casey, submit a $6,000 request. Morgan approves. | Finance step cannot be approved by Casey. Another Finance user can approve (BR-04). |
| UAT-13 | As Riley, cancel a PendingApproval request. | Status Cancelled. Request leaves Morgan's queue. |
| UAT-14 | As Riley, open another user's request by URL. | Access denied page. |
| UAT-15 | Open history for the UAT-10 request. | Every action listed with user, time and comment. No edit or delete controls. |
| UAT-16 | As Morgan, open Dashboard. | Pending count and team spend by month and category are shown. |
| UAT-17 | As Admin, change threshold to $2,500. Riley submits a $3,000 request. | Finance step added. Requests submitted earlier are unchanged. |

## Traceability matrix

Automated test names are planned. Update this table when each test is written.

| Requirement | Business rule | User story | UAT case | Automated test |
|---|---|---|---|---|
| FR-01 | | US-01 | UAT-01 | *Manual (Entra sign-in)* |
| FR-02 | | US-02 | UAT-02 | `RequestFormTests.Save_ValidDraft_SetsStatusDraft` |
| FR-03 | | US-02 | UAT-02 | `PurchaseRequestTests.Total_SumsLineItems` |
| FR-04 | | US-02 | UAT-02 | `RequestServiceTests.SaveDraft_AssignsNumber` |
| FR-05 | BR-01 | US-03 | UAT-03 | `ApprovalChainTests.Build_FirstStepIsManager` |
| FR-05 | BR-02 | US-03 | UAT-05, UAT-06 | `ApprovalChainTests.Build_OverThreshold_AddsFinance` |
| FR-05 | BR-03 | US-03 | UAT-04, UAT-06 | `ApprovalChainTests.Build_ItCategory_AddsItStep` |
| FR-05 | BR-04 | US-03 | UAT-12 | `ApprovalChainTests.Build_ApproverIsRequester_Escalates` |
| FR-06 | | US-04 | UAT-07 | `ApprovalQueueTests.Queue_ShowsOnlyAssignedRequests` |
| FR-07 | BR-05 | US-05 | UAT-08, UAT-09 | `ApprovalServiceTests.Return_RequiresComment_ResetsToDraft` |
| FR-08 | | US-03, US-05 | UAT-03, UAT-10 | `NotificationTests.Submit_EmailsFirstApprover` |
| FR-09 | | US-06 | UAT-15 | `MyRequestsTests.List_ShowsOnlyOwnRequests` |
| FR-10 | | US-05, US-06 | UAT-15 | `HistoryTests.EveryTransition_WritesHistoryRow` |
| FR-11 | | US-07 | UAT-11 | `PurchasingTests.MarkOrdered_OnlyWhenApproved` |
| FR-12 | | US-08 | UAT-13 | `RequestServiceTests.Cancel_PendingRequest_SetsCancelled` |
| FR-13 | | US-09 | UAT-16 | `DashboardTests.TeamSpend_GroupsByMonthAndCategory` |
| FR-14 | BR-02, BR-03 | US-10 | UAT-17 | `SettingsTests.ThresholdChange_DoesNotAffectSubmitted` |
| FR-15 | | *Not yet written* | | |
| NFR-01 | | US-01 | UAT-01 | `AuthorizationTests.AllPages_RequireAuthentication` |
| NFR-02 | | US-06 | UAT-14 | `AuthorizationTests.OtherUsersRequest_Returns403` |
| NFR-03 | | US-06 | UAT-15 | `HistoryTests.History_HasNoUpdateOrDeletePath` |
| NFR-04 to NFR-07 | | | | *Verified at release (load test, accessibility check)* |
