using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    // FR-14: admins change the finance threshold (BR-02) and which categories need IT approval (BR-03).
    // Both are read at submit time, so changes only affect requests submitted afterward.
    public class SettingsService(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<SettingsService> logger)
    {
        public async Task<decimal> GetFinanceThresholdAsync(CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var value = await db.Settings
                .Where(s => s.Key == Setting.FinanceThreshold)
                .Select(s => s.Value)
                .SingleOrDefaultAsync(cancellationToken);
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var threshold)
                ? threshold
                : ApprovalService.DefaultFinanceThreshold;
        }

        public async Task SetFinanceThresholdAsync(CurrentUser user, decimal threshold, CancellationToken cancellationToken = default)
        {
            RequireAdmin(user);
            if (threshold <= 0 || decimal.Round(threshold, 2) != threshold)
            {
                throw new ValidationException("The threshold must be more than $0.00, in whole cents.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var setting = await db.Settings.SingleOrDefaultAsync(s => s.Key == Setting.FinanceThreshold, cancellationToken);
            if (setting is null)
            {
                setting = new Setting { Key = Setting.FinanceThreshold };
                db.Settings.Add(setting);
            }
            var old = setting.Value;
            setting.Value = threshold.ToString("0.00", CultureInfo.InvariantCulture);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("User {UserId} changed the finance threshold from {Old} to {New}.", user.Id, old, setting.Value);
        }

        public async Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        }

        public async Task UpdateCategoryAsync(CurrentUser user, int categoryId, bool requiresItApproval, bool isActive, CancellationToken cancellationToken = default)
        {
            RequireAdmin(user);
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
                ?? throw new InvalidOperationException("That category no longer exists.");

            category.RequiresItApproval = requiresItApproval;
            category.IsActive = isActive;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("User {UserId} set category {Category}: requires IT approval {RequiresIt}, active {Active}.",
                user.Id, category.Name, requiresItApproval, isActive);
        }

        public async Task<Category> AddCategoryAsync(CurrentUser user, string name, bool requiresItApproval, CancellationToken cancellationToken = default)
        {
            RequireAdmin(user);
            name = name?.Trim() ?? "";
            if (name.Length is 0 or > 100)
            {
                throw new ValidationException("Enter a category name of up to 100 characters.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (await db.Categories.AnyAsync(c => c.Name == name, cancellationToken))
            {
                throw new ValidationException($"A category named \"{name}\" already exists.");
            }

            var category = new Category { Name = name, RequiresItApproval = requiresItApproval };
            db.Categories.Add(category);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("User {UserId} added category {Category}.", user.Id, name);
            return category;
        }

        private static void RequireAdmin(CurrentUser user)
        {
            if (!user.IsInRole(AppRoles.Admin))
            {
                throw new UnauthorizedAccessException("Only admins can change settings.");
            }
        }
    }
}
