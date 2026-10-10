# Process Maps

## Current process (email and spreadsheet)

```mermaid
flowchart TD
    A["Employee emails manager with request"] --> B{"Manager replies?"}
    B -- "No reply" --> C["Employee sends reminder"] --> B
    B -- "Approved" --> D{"Over limit or IT item?<br/>(decided from memory)"}
    B -- "Rejected" --> Z["Employee told by email"]
    D -- "Yes" --> E["Manager forwards to finance or IT"] --> F{"Approved?"}
    D -- "No / forgotten" --> G["Employee forwards approval email to purchasing"]
    F -- "Yes" --> G
    F -- "No" --> Z
    G --> H["Purchasing re-types into spreadsheet"]
    H --> I["Purchasing places order"]
    I --> J["Status updated in spreadsheet (sometimes)"]
```

Problems shown: reminder loops, rules applied from memory, re-typing, no reliable status.

## Future process (Purchase Request app)

```mermaid
flowchart TD
    A["Requester submits request in app"] --> B["System builds approval chain<br/>(BR-01 to BR-04)"]
    B --> C["Manager approves"]
    C --> D{"Category requires IT?<br/>(BR-03)"}
    D -- "Yes" --> E["IT approver approves"]
    D -- "No" --> F{"Total over threshold?<br/>(BR-02)"}
    E --> F
    F -- "Yes" --> G["Finance approves"]
    F -- "No" --> H["Status: Approved"]
    G --> H
    H --> I["Purchasing marks Ordered"]
    I --> J["Purchasing marks Received"]
    C -. "Reject or Return" .-> X["Requester notified by email"]
    E -. "Reject or Return" .-> X
    G -. "Reject or Return" .-> X
```

Every arrow writes a row to the audit trail and sends an email notification.

## Request lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> PendingApproval: Submit
    PendingApproval --> Draft: Returned for changes
    PendingApproval --> Rejected: Any approver rejects
    PendingApproval --> Approved: Final step approved
    Draft --> Cancelled: Requester cancels
    PendingApproval --> Cancelled: Requester cancels
    Approved --> Ordered: Purchasing
    Ordered --> Received: Purchasing
    Rejected --> [*]
    Cancelled --> [*]
    Received --> [*]
```
