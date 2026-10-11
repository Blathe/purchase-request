using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    public class DashboardTests
    {
        private static readonly int Year = DateTime.UtcNow.Year;

        private readonly TestDb testDb = new();
        private readonly DashboardService dashboard;
        private int nextNumber;

        public DashboardTests()
        {
            dashboard = testDb.CreateDashboardService();
        }

        // FR-13, US-09, UAT-16: Riley and Casey report to Morgan.
        [Fact]
        public async Task TeamSpend_GroupsByMonthAndCategory()
        {
            Seed(TestDb.Riley, RequestStatus.Approved, TestDb.OfficeSupplies, 100m, month: 1);
            Seed(TestDb.Casey, RequestStatus.Received, TestDb.Hardware, 900m, month: 1);
            Seed(TestDb.Riley, RequestStatus.Ordered, TestDb.Hardware, 300m, month: 2);

            var result = await dashboard.GetAsync(TestDb.User(TestDb.Morgan));

            Assert.True(result.IsManager);
            Assert.Equal(1300m, result.Total);
            Assert.Equal([new SpendRow("January", 1000m, 2), new SpendRow("February", 300m, 1)], result.ByMonth);
            Assert.Equal([new SpendRow("Hardware", 1200m, 2), new SpendRow("Office Supplies", 100m, 1)], result.ByCategory);
        }

        // Spend counts only once a request is approved, and only for the current year.
        [Fact]
        public async Task TeamSpend_ExcludesUnapprovedAndPriorYears()
        {
            Seed(TestDb.Riley, RequestStatus.Approved, TestDb.OfficeSupplies, 100m, month: 1);
            Seed(TestDb.Riley, RequestStatus.PendingApproval, TestDb.OfficeSupplies, 200m, month: 1);
            Seed(TestDb.Riley, RequestStatus.Rejected, TestDb.OfficeSupplies, 300m, month: 1);
            Seed(TestDb.Riley, RequestStatus.Cancelled, TestDb.OfficeSupplies, 400m, month: 1);
            Seed(TestDb.Riley, RequestStatus.Approved, TestDb.OfficeSupplies, 500m, month: 12, year: Year - 1);

            var result = await dashboard.GetAsync(TestDb.User(TestDb.Morgan));

            Assert.Equal(100m, result.Total);
        }

        // US-09: someone who is not a manager sees only their own spend.
        [Fact]
        public async Task NotManager_SeesOnlyOwnSpend()
        {
            Seed(TestDb.Riley, RequestStatus.Approved, TestDb.OfficeSupplies, 100m, month: 1);
            Seed(TestDb.Casey, RequestStatus.Approved, TestDb.OfficeSupplies, 900m, month: 1);

            var result = await dashboard.GetAsync(TestDb.User(TestDb.Riley));

            Assert.False(result.IsManager);
            Assert.Equal(100m, result.Total);
        }

        [Fact]
        public async Task PendingApprovals_CountsQueue()
        {
            var requests = testDb.CreateRequestService();
            var approvals = testDb.CreateApprovalService();
            foreach (var requester in new[] { TestDb.Riley, TestDb.Casey })
            {
                var draft = await requests.CreateDraftAsync(requester, TestDb.ValidModel());
                await approvals.SubmitAsync(draft.Id, requester);
            }

            var result = await dashboard.GetAsync(TestDb.User(TestDb.Morgan));

            Assert.Equal(2, result.PendingApprovals);
        }

        private void Seed(int requester, RequestStatus status, int categoryId, decimal total, int month, int? year = null)
        {
            using var db = testDb.CreateDbContext();
            var submitted = new DateTime(year ?? Year, month, 15, 12, 0, 0, DateTimeKind.Utc);
            db.Requests.Add(new Request
            {
                Number = $"PR-TEST-{++nextNumber:D4}",
                RequesterId = requester,
                CategoryId = categoryId,
                Vendor = "Vendor",
                Justification = "Test",
                Status = status,
                Total = total,
                CreatedAt = submitted,
                SubmittedAt = submitted
            });
            db.SaveChanges();
        }
    }
}
