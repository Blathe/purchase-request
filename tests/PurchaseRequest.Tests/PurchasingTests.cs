using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    public class PurchasingTests
    {
        private readonly TestDb testDb = new();
        private readonly RequestService requests;
        private readonly ApprovalService approvals;
        private readonly PurchasingService purchasing;

        public PurchasingTests()
        {
            requests = testDb.CreateRequestService();
            approvals = testDb.CreateApprovalService();
            purchasing = testDb.CreatePurchasingService();
        }

        // FR-11, US-07, UAT-11
        [Fact]
        public async Task MarkOrderedThenReceived_UpdatesStatusHistoryAndEmails()
        {
            var id = await ApprovedRequestAsync();
            testDb.Email.Sent.Clear();

            await purchasing.MarkOrderedAsync(id, TestDb.User(TestDb.Avery), "PO 1042");
            Assert.Equal(RequestStatus.Ordered, (await LoadAsync(id)).Status);
            await purchasing.MarkReceivedAsync(id, TestDb.User(TestDb.Avery), null);

            var request = await LoadAsync(id);
            Assert.Equal(RequestStatus.Received, request.Status);
            Assert.Contains(request.History, h => h.Action == HistoryAction.Ordered && h.Comment == "PO 1042" && h.UserId == TestDb.Avery);
            Assert.Contains(request.History, h => h.Action == HistoryAction.Received);
            Assert.Collection(testDb.Email.Sent,
                e => Assert.StartsWith("Ordered", e.Subject),
                e => Assert.StartsWith("Received", e.Subject));
            Assert.All(testDb.Email.Sent, e => Assert.Equal(TestDb.EmailOf(TestDb.Riley), e.To));
        }

        // FR-11, US-07: the button is not shown, and the service refuses, unless the request is Approved.
        [Fact]
        public async Task MarkOrdered_OnlyWhenApproved()
        {
            var draft = await requests.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            Assert.False(PurchasingService.CanMarkOrdered(await LoadAsync(draft.Id), TestDb.User(TestDb.Avery)));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => purchasing.MarkOrderedAsync(draft.Id, TestDb.User(TestDb.Avery), null));
        }

        [Fact]
        public async Task MarkReceived_BeforeOrdered_IsRefused()
        {
            var id = await ApprovedRequestAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => purchasing.MarkReceivedAsync(id, TestDb.User(TestDb.Avery), null));
        }

        [Fact]
        public async Task MarkOrdered_NotPurchasing_IsDenied()
        {
            var id = await ApprovedRequestAsync();

            var result = await purchasing.MarkOrderedAsync(id, TestDb.User(TestDb.Riley), null);

            Assert.Equal(AccessResult.Denied, result.Access);
            Assert.False(PurchasingService.CanMarkOrdered(await LoadAsync(id), TestDb.User(TestDb.Riley)));
            Assert.Equal(RequestStatus.Approved, (await LoadAsync(id)).Status);
        }

        [Fact]
        public async Task Queue_ListsApprovedAndOrderedOnly()
        {
            var approved = await ApprovedRequestAsync();
            var ordered = await ApprovedRequestAsync();
            await purchasing.MarkOrderedAsync(ordered, TestDb.User(TestDb.Avery), null);
            await requests.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            var queue = await purchasing.GetQueueAsync();

            Assert.Equal([approved, ordered], queue.Select(r => r.Id));
        }

        private async Task<int> ApprovedRequestAsync()
        {
            var draft = await requests.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());
            await approvals.SubmitAsync(draft.Id, TestDb.Riley);
            await approvals.DecideAsync(draft.Id, TestDb.User(TestDb.Morgan), Decision.Approved, null);
            return draft.Id;
        }

        private async Task<Request> LoadAsync(int id)
        {
            await using var db = testDb.CreateDbContext();
            return await db.Requests.Include(r => r.History).SingleAsync(r => r.Id == id);
        }
    }
}
