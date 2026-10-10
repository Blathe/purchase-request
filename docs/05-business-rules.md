# Business Rules Catalog

| ID | Rule | Trigger | Source | Configurable |
|---|---|---|---|---|
| BR-01 | The first approver is the requester's manager, as recorded in Entra ID. | Submit | Purchasing policy | No |
| BR-02 | Requests with a total **over** the finance threshold (default $5,000.00) need Finance approval as the final step. | Submit | Finance policy | Yes, by Admin |
| BR-03 | Requests in a category flagged "Requires IT approval" (default: Hardware, Software, SaaS) need IT approval after the manager. | Submit | IT security policy | Yes, by Admin |
| BR-04 | Nobody approves their own request. If an approver in the chain is the requester, that step goes to the approver's manager instead. | Submit | Internal controls | No |
| BR-05 | A submitted request cannot be edited. A returned request goes back to Draft, and resubmitting builds a new approval chain from the first step. | Return / Submit | Internal controls | No |

## Details and edge cases

**BR-01**
- If the requester has no manager in Entra ID, the step goes to the Admin role, who reassigns it.

**BR-02**
- A total exactly equal to the threshold does **not** need Finance approval.
- The threshold in force at submit time is stored on the request. Later changes do not affect it.

**BR-03**
- The category list in force at submit time applies, same as BR-02.

**BR-04**
- Example: a finance approver submits a $10,000 request. Their manager approves step 1. The Finance step cannot go to the requester, so any other Finance approver can approve it.

**BR-05**
- Previous approvals are kept in the history but do not count toward the new chain.

## Approval chain order

1. Manager (BR-01, BR-04)
2. IT approver, if BR-03 applies
3. Finance approver, if BR-02 applies
