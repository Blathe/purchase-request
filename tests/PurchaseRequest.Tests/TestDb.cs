using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    // A fresh in-memory database per test, seeded with categories, settings and two users.
    public sealed class TestDb : IDbContextFactory<ApplicationDbContext>
    {
        public const int Riley = 1;
        public const int Morgan = 2;
        public const int OfficeSupplies = 1;
        public const int Hardware = 2;

        private readonly DbContextOptions<ApplicationDbContext> options;

        public TestDb()
        {
            options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = CreateDbContext();
            db.Database.EnsureCreated();
            db.Users.AddRange(
                new AppUser { Id = Riley, EntraObjectId = Guid.NewGuid(), DisplayName = "Riley", Email = "riley@example.com" },
                new AppUser { Id = Morgan, EntraObjectId = Guid.NewGuid(), DisplayName = "Morgan", Email = "morgan@example.com" });
            db.SaveChanges();
        }

        public ApplicationDbContext CreateDbContext() => new(options);

        public RequestService CreateRequestService() =>
            new(this, new FakeRequestNumberGenerator(), TimeProvider.System);

        public static RequestFormModel ValidModel(int categoryId = OfficeSupplies) => new()
        {
            Vendor = "Staples",
            CategoryId = categoryId,
            Justification = "Printer paper for the office",
            LineItems = [new LineItemModel { Description = "Paper, 10 reams", Quantity = 3, UnitPrice = 20.00m }]
        };
    }

    public sealed class FakeRequestNumberGenerator : IRequestNumberGenerator
    {
        private int next;

        public Task<string> NextAsync(ApplicationDbContext db, CancellationToken cancellationToken = default) =>
            Task.FromResult($"PR-2026-{Interlocked.Increment(ref next):D4}");
    }
}
