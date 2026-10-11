using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    // A fresh in-memory database per test, seeded with categories, settings and the UAT test users.
    public sealed class TestDb : IDbContextFactory<ApplicationDbContext>
    {
        public const int Riley = 1;   // Requester, reports to Morgan
        public const int Morgan = 2;  // Manager
        public const int Jordan = 3;  // IT approver
        public const int Casey = 4;   // Finance approver, reports to Morgan
        public const int Avery = 5;   // Purchasing
        public const int Drew = 6;    // Second Finance approver

        public const int OfficeSupplies = 1;
        public const int Hardware = 2;

        private static readonly Dictionary<int, string[]> RolesByUser = new()
        {
            [Jordan] = [AppRoles.ITApprover],
            [Casey] = [AppRoles.FinanceApprover],
            [Avery] = [AppRoles.Purchasing],
            [Drew] = [AppRoles.FinanceApprover]
        };

        private readonly DbContextOptions<ApplicationDbContext> options;

        public TestDb()
        {
            options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = CreateDbContext();
            db.Database.EnsureCreated();
            db.Users.AddRange(
                NewUser(Riley, "Riley", manager: Morgan),
                NewUser(Morgan, "Morgan"),
                NewUser(Jordan, "Jordan"),
                NewUser(Casey, "Casey", manager: Morgan),
                NewUser(Avery, "Avery"),
                NewUser(Drew, "Drew"));
            db.SaveChanges();

            foreach (var user in db.Users)
            {
                Directory.Users[user.EntraObjectId] = new DirectoryUser(user.EntraObjectId, user.DisplayName, user.Email, "Operations");
            }
            foreach (var user in db.Users.Where(u => u.ManagerId != null).ToList())
            {
                var manager = db.Users.Single(u => u.Id == user.ManagerId);
                Directory.Managers[user.EntraObjectId] = Directory.Users[manager.EntraObjectId];
            }
        }

        public FakeDirectoryService Directory { get; } = new();

        public FakeEmailSender Email { get; } = new();

        public ApplicationDbContext CreateDbContext() => new(options);

        public RequestService CreateRequestService() =>
            new(this, new FakeRequestNumberGenerator(), TimeProvider.System);

        public ApprovalService CreateApprovalService()
        {
            var notifications = new NotificationService(this, Email, new ConfigurationBuilder().Build(), NullLogger<NotificationService>.Instance);
            return new ApprovalService(this, Directory, notifications, TimeProvider.System, NullLogger<ApprovalService>.Instance);
        }

        public static CurrentUser User(int id) =>
            new(id, $"User {id}", (RolesByUser.GetValueOrDefault(id) ?? []).ToHashSet());

        public static string EmailOf(int id) => $"user{id}@example.com";

        public static RequestFormModel ValidModel(int categoryId = OfficeSupplies, decimal unitPrice = 20.00m) => new()
        {
            Vendor = "Staples",
            CategoryId = categoryId,
            Justification = "Printer paper for the office",
            LineItems = [new LineItemModel { Description = "Paper, 10 reams", Quantity = 3, UnitPrice = unitPrice }]
        };

        private static AppUser NewUser(int id, string name, int? manager = null) => new()
        {
            Id = id,
            EntraObjectId = Guid.NewGuid(),
            DisplayName = name,
            Email = EmailOf(id),
            ManagerId = manager,
            Roles = AppRoles.ToStored(RolesByUser.GetValueOrDefault(id) ?? [])
        };
    }

    public sealed class FakeRequestNumberGenerator : IRequestNumberGenerator
    {
        private int next;

        public Task<string> NextAsync(ApplicationDbContext db, CancellationToken cancellationToken = default) =>
            Task.FromResult($"PR-2026-{Interlocked.Increment(ref next):D4}");
    }

    public sealed class FakeDirectoryService : IDirectoryService
    {
        public Dictionary<Guid, DirectoryUser> Users { get; } = [];
        public Dictionary<Guid, DirectoryUser> Managers { get; } = [];
        public bool Fail { get; set; }

        public Task<DirectoryUser?> GetUserAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            Fail ? throw new HttpRequestException("Graph is down") : Task.FromResult(Users.GetValueOrDefault(objectId));

        public Task<DirectoryUser?> GetManagerAsync(Guid objectId, CancellationToken cancellationToken = default) =>
            Fail ? throw new HttpRequestException("Graph is down") : Task.FromResult(Managers.GetValueOrDefault(objectId));
    }

    public sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
