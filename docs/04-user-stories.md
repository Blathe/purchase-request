# User Stories

Each story will also be filed as a GitHub Issue on the project board. The issue title is the story ID and name.

---

### US-01 Sign in with company account
**As an** employee, **I want** to sign in with my Microsoft account **so that** I do not need another password.
*Covers: FR-01, NFR-01*

- Given I am not signed in, when I open the app, then I am redirected to the Microsoft sign-in page.
- Given I sign in successfully, when I return to the app, then my name is shown in the header.

### US-02 Create and save a draft request
**As a** requester, **I want** to enter a request and save it as a draft **so that** I can finish it later.
*Covers: FR-02, FR-03, FR-04*

- Given I fill in vendor, category, justification and one line item, when I click Save draft, then the request is saved with status Draft.
- Given I enter quantity 3 and unit price $20.00, then the line total shows $60.00 and the request total updates.
- Given a required field is empty, when I click Save draft or Submit, then the field shows a validation message.

### US-03 Submit a request for approval
**As a** requester, **I want** to submit my request **so that** it goes to the right approvers automatically.
*Covers: FR-05, FR-08, BR-01 to BR-04*

- Given a valid draft, when I click Submit, then status becomes PendingApproval and my manager is the first approver.
- Given the total is over the finance threshold, then a Finance step is added at the end of the chain.
- Given the category requires IT approval, then an IT step is added after the manager.
- Given I submit, then my manager receives an email with a link to the request.

### US-04 Review my approval queue
**As an** approver, **I want** one list of requests waiting on me **so that** nothing gets lost.
*Covers: FR-06*

- Given two requests are waiting on me, when I open My approvals, then I see both, oldest first, with requester, total and days waiting.
- Given a request is waiting on someone else, then it does not appear in my queue.

### US-05 Approve, reject or return
**As an** approver, **I want** to decide on a request with a comment **so that** the requester knows the outcome.
*Covers: FR-07, FR-08, FR-10*

- Given I approve and more steps remain, then the next approver is emailed.
- Given I approve the last step, then status becomes Approved and the requester and Purchasing are emailed.
- Given I click Reject or Return without a comment, then I see "A comment is required."
- Given I return a request, then its status becomes Draft and the requester can edit it.

### US-06 Track my requests
**As a** requester, **I want** to see the status and history of my requests **so that** I do not have to email anyone.
*Covers: FR-09, FR-10*

- Given I open My requests, then I see each request's number, total, status and current approver.
- Given I open a request, then I see every action with who did it, when and their comment.

### US-07 Mark ordered and received
**As a** purchasing user, **I want** to mark approved requests as ordered and received **so that** everyone sees delivery status.
*Covers: FR-11*

- Given a request is Approved, when I click Mark ordered, then status becomes Ordered and the requester is emailed.
- Given a request is not Approved, then the Mark ordered button is not shown.

### US-08 Cancel a request
**As a** requester, **I want** to cancel a request I no longer need **so that** approvers do not waste time on it.
*Covers: FR-12*

- Given my request is Draft or PendingApproval, when I click Cancel and confirm, then status becomes Cancelled and it leaves the approver's queue.
- Given my request is Approved, then Cancel is not shown.

### US-09 Manager dashboard
**As a** manager, **I want** to see pending requests and team spend **so that** I can manage my budget.
*Covers: FR-13*

- Given I open Dashboard, then I see a count of requests pending my approval.
- Given my team has approved requests this year, then I see spend by month and by category.
- Given I am not a manager, then I see only my own spend.

### US-10 Configure approval settings
**As an** admin, **I want** to change the finance threshold and IT categories **so that** rules change without a code release.
*Covers: FR-14, BR-02, BR-03*

- Given I change the threshold from $5,000 to $2,500, then requests submitted afterward use $2,500.
- Given a request was submitted before the change, then its approval chain is not changed.
