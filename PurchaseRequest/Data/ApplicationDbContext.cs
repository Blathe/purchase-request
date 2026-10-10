using Microsoft.EntityFrameworkCore;

namespace PurchaseRequest.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public const string RequestNumberSequence = "RequestNumbers";

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Request> Requests => Set<Request>();
        public DbSet<LineItem> LineItems => Set<LineItem>();
        public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
        public DbSet<RequestHistory> RequestHistory => Set<RequestHistory>();
        public DbSet<Setting> Settings => Set<Setting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Source for the NNNN part of request numbers (PR-YYYY-NNNN).
            modelBuilder.HasSequence<int>(RequestNumberSequence);

            modelBuilder.Entity<AppUser>(user =>
            {
                user.HasIndex(u => u.EntraObjectId).IsUnique();
                user.Property(u => u.DisplayName).HasMaxLength(200);
                user.Property(u => u.Email).HasMaxLength(256);
                user.Property(u => u.Department).HasMaxLength(100);
                user.HasOne(u => u.Manager).WithMany()
                    .HasForeignKey(u => u.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(category =>
            {
                category.HasIndex(c => c.Name).IsUnique();
                category.Property(c => c.Name).HasMaxLength(100);
                category.HasData(
                    new Category { Id = 1, Name = "Office Supplies" },
                    new Category { Id = 2, Name = "Hardware", RequiresItApproval = true },
                    new Category { Id = 3, Name = "Software", RequiresItApproval = true },
                    new Category { Id = 4, Name = "SaaS", RequiresItApproval = true },
                    new Category { Id = 5, Name = "Furniture" },
                    new Category { Id = 6, Name = "Training" },
                    new Category { Id = 7, Name = "Other" });
            });

            modelBuilder.Entity<Request>(request =>
            {
                request.ToTable("PurchaseRequests");
                request.HasIndex(r => r.Number).IsUnique();
                request.HasIndex(r => r.Status);
                request.Property(r => r.Number).HasMaxLength(20);
                request.Property(r => r.Vendor).HasMaxLength(200);
                request.Property(r => r.Justification).HasMaxLength(2000);
                request.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
                request.Property(r => r.Total).HasPrecision(18, 2);
                request.Property(r => r.ThresholdAtSubmit).HasPrecision(18, 2);
                request.Property(r => r.RowVersion).IsRowVersion();

                request.HasOne(r => r.Requester).WithMany()
                    .HasForeignKey(r => r.RequesterId)
                    .OnDelete(DeleteBehavior.Restrict);
                request.HasOne(r => r.Category).WithMany()
                    .HasForeignKey(r => r.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
                request.HasMany(r => r.LineItems).WithOne()
                    .HasForeignKey(l => l.RequestId)
                    .OnDelete(DeleteBehavior.Cascade);
                request.HasMany(r => r.ApprovalSteps).WithOne()
                    .HasForeignKey(s => s.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);
                request.HasMany(r => r.History).WithOne()
                    .HasForeignKey(h => h.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LineItem>(line =>
            {
                line.Property(l => l.Description).HasMaxLength(500);
                line.Property(l => l.UnitPrice).HasPrecision(18, 2);
                line.Property(l => l.Url).HasMaxLength(1000);
                line.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_LineItems_Quantity", "[Quantity] >= 1");
                    t.HasCheckConstraint("CK_LineItems_UnitPrice", "[UnitPrice] >= 0.01");
                });
            });

            modelBuilder.Entity<ApprovalStep>(step =>
            {
                step.HasIndex(s => new { s.RequestId, s.Sequence });
                step.HasIndex(s => new { s.ApproverId, s.Decision });
                step.Property(s => s.StepType).HasConversion<string>().HasMaxLength(20);
                step.Property(s => s.Decision).HasConversion<string>().HasMaxLength(20);
                step.Property(s => s.ApproverRole).HasMaxLength(50);
                step.Property(s => s.Comment).HasMaxLength(1000);
                step.HasOne(s => s.Approver).WithMany()
                    .HasForeignKey(s => s.ApproverId)
                    .OnDelete(DeleteBehavior.Restrict);
                step.HasOne(s => s.DecidedBy).WithMany()
                    .HasForeignKey(s => s.DecidedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RequestHistory>(history =>
            {
                history.HasIndex(h => h.RequestId);
                history.Property(h => h.Action).HasConversion<string>().HasMaxLength(30);
                history.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
                history.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);
                history.Property(h => h.Comment).HasMaxLength(1000);
                history.HasOne(h => h.User).WithMany()
                    .HasForeignKey(h => h.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Setting>(setting =>
            {
                setting.HasKey(s => s.Key);
                setting.Property(s => s.Key).HasMaxLength(100);
                setting.Property(s => s.Value).HasMaxLength(500);
                setting.HasData(new Setting { Key = Setting.FinanceThreshold, Value = "5000.00" });
            });
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            EnsureHistoryIsAppendOnly();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            EnsureHistoryIsAppendOnly();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // NFR-03: history rows can be added but never changed or removed.
        private void EnsureHistoryIsAppendOnly()
        {
            var changed = ChangeTracker.Entries<RequestHistory>()
                .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
            if (changed)
            {
                throw new InvalidOperationException("Request history is append-only and cannot be modified or deleted.");
            }
        }
    }
}
