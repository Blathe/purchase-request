using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;

namespace PurchaseRequest.Tests
{
    // NFR-03: history rows can be added but never changed or removed.
    public class HistoryTests
    {
        private readonly TestDb testDb = new();

        [Fact]
        public async Task History_UpdateIsRejected()
        {
            await testDb.CreateRequestService().CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            await using var db = testDb.CreateDbContext();
            var history = await db.RequestHistory.FirstAsync();
            history.Comment = "Edited after the fact";

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task History_DeleteIsRejected()
        {
            await testDb.CreateRequestService().CreateDraftAsync(TestDb.Riley, TestDb.ValidModel());

            await using var db = testDb.CreateDbContext();
            db.RequestHistory.Remove(await db.RequestHistory.FirstAsync());

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }
}
