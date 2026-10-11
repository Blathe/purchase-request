using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    public class ApprovalServiceTests
    {
        private readonly TestDb testDb = new();
        private readonly RequestService requests;
        private readonly ApprovalService approvals;

        public ApprovalServiceTests()
        {
            requests = testDb.CreateRequestService();
            approvals = testDb.CreateApprovalService();
        }

        // FR-05, UAT-03
        [Fact]
        public async Task Submit_SetsPendingAndSnapshotsThreshold()
        {
            var id = await SubmitAsync(TestDb.Riley);

            var request = await LoadAsync(id);
            Assert.Equal(RequestStatus.PendingApproval, request.Status);
            Assert.Equal(5000.00m, request.ThresholdAtSubmit);
            Assert.NotNull(request.SubmittedAt);
            var step = Assert.Single(request.ApprovalSteps);
            Assert.Equal(TestDb.Morgan, step.ApproverId);
            Assert.Contains(request.History, h => h.Action == HistoryAction.Submitted);
        }

        // FR-08, UAT-03
        [Fact]
        public async Task Submit_EmailsFirstApprover()
        {
            await SubmitAsync(TestDb.Riley);

            var email = Assert.Single(testDb.Email.Sent);
            Assert.Equal(TestDb.EmailOf(TestDb.Morgan), email.To);
            Assert.StartsWith("Approval needed", email.Subject);
        }

        // BR-01: the manager comes from Entra ID at submit time, not the stored value.
        [Fact]
        public async Task Submit_UsesCurrentManagerFromDirectory()
        {
            var jordan = testDb.Directory.Users.Values.Single(u => u.DisplayName == "Jordan");
            var riley = testDb.Directory.Users.Values.Single(u => u.DisplayName == "Riley");
            testDb.Directory.Managers[riley.Id] = jordan;

            var id = await SubmitAsync(TestDb.Riley);

            Assert.Equal(TestDb.Jordan, Assert.Single((await LoadAsync(id)).ApprovalSteps).ApproverId);
        }

        [Fact]
        public async Task Submit_DirectoryUnavailable_UsesStoredManager()
        {
            testDb.Directory.Fail = true;

            var id = await SubmitAsync(TestDb.Riley);

            Assert.Equal(TestDb.Morgan, Assert.Single((await LoadAsync(id)).ApprovalSteps).ApproverId);
        }

        [Fact]
        public async Task Submit_NoManager_GoesToAdmin()
        {
            var riley = testDb.Directory.Users.Values.Single(u => u.DisplayName == "Riley");
            testDb.Directory.Managers.Remove(riley.Id);

            var id = await SubmitAsync(TestDb.Riley);

            Assert.Equal(AppRoles.Admin, Assert.Single((await LoadAsync(id)).ApprovalSteps).ApproverRole);
        }

        // FR-07, FR-08, UAT-10
        [Fact]
        public async Task Approve_AllSteps_SetsApprovedAndEmailsPurchasing()
        {
            var id = await SubmitAsync(TestDb.Riley, TestDb.Hardware, unitPrice: 2000m);

            await approvals.DecideAsync(id, TestDb.User(TestDb.Morgan), Decision.Approved, null);
            Assert.Equal(RequestStatus.PendingApproval, (await LoadAsync(id)).Status);
            await approvals.DecideAsync(id, TestDb.User(TestDb.Jordan), Decision.Approved, null);
            await approvals.DecideAsync(id, TestDb.User(TestDb.Casey), Decision.Approved, "OK for Q4 budget");

            var request = await LoadAsync(id);
            Assert.Equal(RequestStatus.Approved, request.Status);
            Assert.All(request.ApprovalSteps, s => Assert.Equal(Decision.Approved, s.Decision));
            Assert.Contains(testDb.Email.Sent, e => e.To == TestDb.EmailOf(TestDb.Avery) && e.Subject.StartsWith("Ready to order"));
            Assert.Contains(testDb.Email.Sent, e => e.To == TestDb.EmailOf(TestDb.Riley) && e.Subject.StartsWith("Approved"));
        }

        [Fact]
        public async Task Approve_WithMoreSteps_EmailsNextApprovers()
        {
            var id = await SubmitAsync(TestDb.Riley, TestDb.Hardware);
            testDb.Email.Sent.Clear();

            await approvals.DecideAsync(id, TestDb.User(TestDb.Morgan), Decision.Approved, null);

            Assert.Contains(testDb.Email.Sent, e => e.To == TestDb.EmailOf(TestDb.Jordan) && e.Subject.StartsWith("Approval needed"));
            Assert.Contains(testDb.Email.Sent, e => e.To == TestDb.EmailOf(TestDb.Riley) && e.Subject.StartsWith("Step approved"));
        }

        [Fact]
        public async Task Decide_NotCurrentApprover_IsDenied()
        {
            var id = await SubmitAsync(TestDb.Riley, TestDb.Hardware);

            // Jordan holds the IT step, but the manager step comes first.
            var result = await approvals.DecideAsync(id, TestDb.User(TestDb.Jordan), Decision.Approved, null);

            Assert.Equal(AccessResult.Denied, result.Access);
        }

        // BR-04, UAT-12
        [Fact]
        public async Task Decide_RequesterHoldsStepRole_CannotApproveOwnRequest()
        {
            var id = await SubmitAsync(TestDb.Casey, unitPrice: 2000m);
            await approvals.DecideAsync(id, TestDb.User(TestDb.Morgan), Decision.Approved, null);

            var own = await approvals.DecideAsync(id, TestDb.User(TestDb.Casey), Decision.Approved, null);
            var other = await approvals.DecideAsync(id, TestDb.User(TestDb.Drew), Decision.Approved, null);

            Assert.Equal(AccessResult.Denied, own.Access);
            Assert.Equal(AccessResult.Allowed, other.Access);
            Assert.Equal(RequestStatus.Approved, (await LoadAsync(id)).Status);
        }

        // FR-07, BR-05, UAT-08, UAT-09
        [Fact]
        public async Task Return_RequiresComment_ResetsToDraft()
        {
            var id = await SubmitAsync(TestDb.Riley);

            await Assert.ThrowsAsync<ValidationException>(
                () => approvals.DecideAsync(id, TestDb.User(TestDb.Morgan), Decision.Returned, "  "));
            await approvals.DecideAsync(id, TestDb.User(TestDb.Morgan), Decision.Returned, "Add a second quote");

            var request = await LoadAsync(id);
            Assert.Equal(RequestStatus.Draft, request.Status);
            Assert.Equal(Decision.Returned, Assert.Single(request.ApprovalSteps).Decision);
            Assert.Contains(testDb.Email.Sent, e => e.To == TestDb.EmailOf(TestDb.Riley) && e.Body.Contains("Add a second quote"));
        }

        [Fact]
        public async Task Reject_SetsRejectedAndSupersedesRemainingSteps()
        {
            var id = await SubmitAsync(TestDb.Riley, TestDb.Hardware);

            await approvals.DecideAsync(id, TestDb.User(TestDb.Morgan), Decision.Rejected, "Not in budget");

            var request = await LoadAsync(id);
            Assert.Equal(RequestStatus.Rejected, request.Status);
            Assert.Equal([Decision.Rejected, Decision.Superseded], request.ApprovalSteps.OrderBy(s => s.Sequence).Select(s => s.Decision));
        }

        // BR-05, UAT-09
        [Fact]
        public async Task Resubmit_AfterReturn_StartsNewRound()
        {
            var id = await SubmitAsync(TestDb.Riley, TestDb.Hardware);
            await approvals.DecideAsync(id, TestDb.User(TestDb.Morgan), Decision.Approved, null);
            await approvals.DecideAsync(id, TestDb.User(TestDb.Jordan), Decision.Returned, "Wrong model");

            await approvals.SubmitAsync(id, TestDb.Riley);

            var request = await LoadAsync(id);
            var current = ApprovalService.CurrentStep(request)!;
            Assert.Equal(2, current.Round);
            Assert.Equal(StepType.Manager, current.StepType);
            Assert.Equal(4, request.ApprovalSteps.Count);
        }

        // FR-06, UAT-07
        [Fact]
        public async Task Queue_ShowsOnlyAssignedRequests()
        {
            var managerStep = await SubmitAsync(TestDb.Riley, TestDb.Hardware);
            var itStep = await SubmitAsync(TestDb.Casey, TestDb.Hardware);
            await approvals.DecideAsync(itStep, TestDb.User(TestDb.Morgan), Decision.Approved, null);

            var morganQueue = await approvals.GetQueueAsync(TestDb.User(TestDb.Morgan));
            var jordanQueue = await approvals.GetQueueAsync(TestDb.User(TestDb.Jordan));
            var rileyQueue = await approvals.GetQueueAsync(TestDb.User(TestDb.Riley));

            Assert.Equal(managerStep, Assert.Single(morganQueue).RequestId);
            Assert.Equal(itStep, Assert.Single(jordanQueue).RequestId);
            Assert.Empty(rileyQueue);
        }

        // FR-12
        [Fact]
        public async Task Cancel_SubmittedRequest_SupersedesPendingSteps()
        {
            var id = await SubmitAsync(TestDb.Riley);

            await requests.CancelAsync(id, TestDb.Riley);

            Assert.Equal(Decision.Superseded, Assert.Single((await LoadAsync(id)).ApprovalSteps).Decision);
            Assert.Empty(await approvals.GetQueueAsync(TestDb.User(TestDb.Morgan)));
        }

        // NFR-02
        [Fact]
        public async Task View_ApproverAndFinanceCanView_OthersCannot()
        {
            var id = await SubmitAsync(TestDb.Riley);

            Assert.Equal(AccessResult.Allowed, (await requests.GetForViewAsync(id, TestDb.User(TestDb.Morgan))).Access);
            Assert.Equal(AccessResult.Allowed, (await requests.GetForViewAsync(id, TestDb.User(TestDb.Casey))).Access);
            Assert.Equal(AccessResult.Denied, (await requests.GetForViewAsync(id, TestDb.User(TestDb.Jordan))).Access);
        }

        private async Task<int> SubmitAsync(int requester, int categoryId = TestDb.OfficeSupplies, decimal unitPrice = 20.00m)
        {
            var draft = await requests.CreateDraftAsync(requester, TestDb.ValidModel(categoryId, unitPrice));
            await approvals.SubmitAsync(draft.Id, requester);
            return draft.Id;
        }

        private async Task<Request> LoadAsync(int id)
        {
            await using var db = testDb.CreateDbContext();
            return await db.Requests
                .Include(r => r.ApprovalSteps)
                .Include(r => r.History)
                .SingleAsync(r => r.Id == id);
        }
    }
}
