using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    // Builds the approval steps for a submitted request (docs/05-business-rules.md).
    public static class ApprovalChainBuilder
    {
        public static List<ApprovalStep> Build(int round, AppUser? manager, Category category, decimal total, decimal financeThreshold)
        {
            var sequence = 1;
            var steps = new List<ApprovalStep>
            {
                // BR-01: the requester's manager first. With no manager in Entra ID, Admin takes the step.
                manager is null
                    ? new ApprovalStep { Round = round, Sequence = sequence++, StepType = StepType.Manager, ApproverRole = AppRoles.Admin }
                    : new ApprovalStep { Round = round, Sequence = sequence++, StepType = StepType.Manager, Approver = manager }
            };

            // BR-03: IT approval for flagged categories.
            if (category.RequiresItApproval)
            {
                steps.Add(new ApprovalStep { Round = round, Sequence = sequence++, StepType = StepType.IT, ApproverRole = AppRoles.ITApprover });
            }

            // BR-02: Finance approval only when the total is over the threshold.
            if (total > financeThreshold)
            {
                steps.Add(new ApprovalStep { Round = round, Sequence = sequence++, StepType = StepType.Finance, ApproverRole = AppRoles.FinanceApprover });
            }

            return steps;
        }
    }
}
