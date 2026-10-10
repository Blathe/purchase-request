using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    public class RequestServiceTests
    {
        private readonly TestDb testDb = new();
        private readonly RequestService service;

        public RequestServiceTests()
        {
            service = testDb.CreateRequestService();
        }

        // FR-03
        [Fact]
        public void Total_SumsLineItems()
        {
            var model = TestDb.ValidModel();
            model.LineItems.Add(new LineItemModel { Description = "Pens", Quantity = 2, UnitPrice = 4.50m });

            Assert.Equal(69.00m, model.Total);
        }

        // FR-02
        [Fact]
        public async Task Save_ValidDraft_SetsStatusDraft()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            await using var db = testDb.CreateDbContext();
            var saved = await db.Requests.Include(r => r.LineItems).SingleAsync(r => r.Id == request.Id);
            Assert.Equal(RequestStatus.Draft, saved.Status);
            Assert.Equal(60.00m, saved.Total);
            Assert.Single(saved.LineItems);
        }

        // FR-04
        [Fact]
        public async Task SaveDraft_AssignsNumber()
        {
            var first = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());
            var second = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            Assert.Equal("PR-2026-0001", first.Number);
            Assert.Equal("PR-2026-0002", second.Number);
        }

        // FR-10
        [Fact]
        public async Task SaveDraft_WritesCreatedHistory()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            await using var db = testDb.CreateDbContext();
            var history = await db.RequestHistory.SingleAsync(h => h.RequestId == request.Id);
            Assert.Equal(HistoryAction.Created, history.Action);
            Assert.Equal(TestDb.Riley, history.UserId);
            Assert.Equal(RequestStatus.Draft, history.ToStatus);
        }

        [Fact]
        public async Task SaveDraft_InvalidLineItem_Throws()
        {
            var model = TestDb.ValidModel();
            model.LineItems[0].UnitPrice = 0m;

            await Assert.ThrowsAsync<ValidationException>(() => service.CreateDraftAsync(TestDb.Riley, model));
        }

        [Fact]
        public async Task SaveDraft_NoLineItems_Throws()
        {
            var model = TestDb.ValidModel();
            model.LineItems.Clear();

            await Assert.ThrowsAsync<ValidationException>(() => service.CreateDraftAsync(TestDb.Riley, model));
        }

        [Fact]
        public async Task UpdateDraft_ReplacesLineItemsAndTotal()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());
            var model = TestDb.ValidModel(TestDb.Hardware);
            model.LineItems = [new LineItemModel { Description = "Monitor", Quantity = 2, UnitPrice = 150.00m }];

            var result = await service.UpdateDraftAsync(request.Id, TestDb.Riley, model);

            Assert.Equal(AccessResult.Allowed, result.Access);
            await using var db = testDb.CreateDbContext();
            var saved = await db.Requests.Include(r => r.LineItems).SingleAsync(r => r.Id == request.Id);
            Assert.Equal(TestDb.Hardware, saved.CategoryId);
            Assert.Equal(300.00m, saved.Total);
            Assert.Equal("Monitor", Assert.Single(saved.LineItems).Description);
        }

        // NFR-02
        [Fact]
        public async Task UpdateDraft_OtherUser_IsDenied()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            var result = await service.UpdateDraftAsync(request.Id, TestDb.Morgan, TestDb.ValidModel());

            Assert.Equal(AccessResult.Denied, result.Access);
        }

        // BR-05
        [Fact]
        public async Task UpdateDraft_SubmittedRequest_Throws()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());
            await SetStatusAsync(request.Id, RequestStatus.PendingApproval);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateDraftAsync(request.Id, TestDb.Riley, TestDb.ValidModel()));
        }

        // FR-09, NFR-02
        [Fact]
        public async Task List_ShowsOnlyOwnRequests()
        {
            var mine = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());
            await service.CreateDraftAsync(TestDb.Morgan, TestDb.ValidModel());

            var requests = await service.GetMyRequestsAsync(TestDb.Riley);

            Assert.Equal(mine.Id, Assert.Single(requests).Id);
        }

        // NFR-02, UAT-14
        [Fact]
        public async Task OtherUsersRequest_IsDenied()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            var result = await service.GetForViewAsync(request.Id, TestDb.Morgan);

            Assert.Equal(AccessResult.Denied, result.Access);
            Assert.Null(result.Request);
        }

        [Fact]
        public async Task GetForView_MissingRequest_IsNotFound()
        {
            var result = await service.GetForViewAsync(999, TestDb.Riley);

            Assert.Equal(AccessResult.NotFound, result.Access);
        }

        // FR-12
        [Fact]
        public async Task Cancel_PendingRequest_SetsCancelled()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());
            await SetStatusAsync(request.Id, RequestStatus.PendingApproval);

            var result = await service.CancelAsync(request.Id, TestDb.Riley);

            Assert.Equal(AccessResult.Allowed, result.Access);
            await using var db = testDb.CreateDbContext();
            Assert.Equal(RequestStatus.Cancelled, (await db.Requests.SingleAsync(r => r.Id == request.Id)).Status);
            var history = await db.RequestHistory.SingleAsync(h => h.RequestId == request.Id && h.Action == HistoryAction.Cancelled);
            Assert.Equal(RequestStatus.PendingApproval, history.FromStatus);
        }

        [Fact]
        public async Task Cancel_ApprovedRequest_Throws()
        {
            var request = await service.CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());
            await SetStatusAsync(request.Id, RequestStatus.Approved);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(request.Id, TestDb.Riley));
        }

        private async Task SetStatusAsync(int requestId, RequestStatus status)
        {
            await using var db = testDb.CreateDbContext();
            var request = await db.Requests.SingleAsync(r => r.Id == requestId);
            request.Status = status;
            await db.SaveChangesAsync();
        }
    }
}
