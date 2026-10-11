using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    public record SpendRow(string Label, decimal Total, int Count);

    public record DashboardSummary(int PendingApprovals, bool IsManager, int Year, decimal Total, List<SpendRow> ByMonth, List<SpendRow> ByCategory);

    // FR-13: pending approvals plus spend by month and category for the current year.
    public class DashboardService(IDbContextFactory<ApplicationDbContext> dbFactory, ApprovalService approvals, TimeProvider clock)
    {
        // Spend counts once a request is approved, including after it is ordered or received.
        private static readonly RequestStatus[] SpendStatuses = [RequestStatus.Approved, RequestStatus.Ordered, RequestStatus.Received];

        public async Task<DashboardSummary> GetAsync(CurrentUser user, CancellationToken cancellationToken = default)
        {
            var year = clock.GetUtcNow().Year;
            var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var pending = (await approvals.GetQueueAsync(user, cancellationToken)).Count;

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            // A manager is anyone recorded as someone's manager. Managers see their direct reports'
            // spend; everyone else sees only their own.
            var isManager = await db.Users.AnyAsync(u => u.ManagerId == user.Id, cancellationToken);

            var query = db.Requests.AsNoTracking()
                .Where(r => SpendStatuses.Contains(r.Status) && r.SubmittedAt >= yearStart);
            query = isManager
                ? query.Where(r => r.Requester.ManagerId == user.Id)
                : query.Where(r => r.RequesterId == user.Id);

            var spend = await query
                .Select(r => new { SubmittedAt = r.SubmittedAt!.Value, Category = r.Category.Name, r.Total })
                .ToListAsync(cancellationToken);

            var byMonth = spend
                .GroupBy(r => r.SubmittedAt.Month)
                .OrderBy(g => g.Key)
                .Select(g => new SpendRow(new DateTime(year, g.Key, 1).ToString("MMMM", CultureInfo.GetCultureInfo("en-US")), g.Sum(r => r.Total), g.Count()))
                .ToList();

            var byCategory = spend
                .GroupBy(r => r.Category)
                .Select(g => new SpendRow(g.Key, g.Sum(r => r.Total), g.Count()))
                .OrderByDescending(r => r.Total)
                .ToList();

            return new DashboardSummary(pending, isManager, year, spend.Sum(r => r.Total), byMonth, byCategory);
        }
    }
}
