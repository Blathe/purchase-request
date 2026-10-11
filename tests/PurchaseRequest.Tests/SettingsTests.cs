using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    public class SettingsTests
    {
        private static readonly CurrentUser Admin = TestDb.UserWithRoles(TestDb.Morgan, AppRoles.Admin);

        private readonly TestDb testDb = new();
        private readonly RequestService requests;
        private readonly ApprovalService approvals;
        private readonly SettingsService settings;

        public SettingsTests()
        {
            requests = testDb.CreateRequestService();
            approvals = testDb.CreateApprovalService();
            settings = testDb.CreateSettingsService();
        }

        // FR-14, BR-02, US-10, UAT-17
        [Fact]
        public async Task ThresholdChange_DoesNotAffectSubmitted()
        {
            var before = await SubmitAsync(TestDb.OfficeSupplies, 3000m);

            await settings.SetFinanceThresholdAsync(Admin, 2500m);
            var after = await SubmitAsync(TestDb.OfficeSupplies, 3000m);

            Assert.Equal(2500m, await settings.GetFinanceThresholdAsync());
            var old = await LoadAsync(before);
            Assert.Equal(5000m, old.ThresholdAtSubmit);
            Assert.DoesNotContain(old.ApprovalSteps, s => s.StepType == StepType.Finance);
            var changed = await LoadAsync(after);
            Assert.Equal(2500m, changed.ThresholdAtSubmit);
            Assert.Contains(changed.ApprovalSteps, s => s.StepType == StepType.Finance);
        }

        // FR-14, BR-03
        [Fact]
        public async Task ItCategoryChange_AppliesToNewSubmissionsOnly()
        {
            var before = await SubmitAsync(TestDb.OfficeSupplies, 50m);

            await settings.UpdateCategoryAsync(Admin, TestDb.OfficeSupplies, requiresItApproval: true, isActive: true);
            var after = await SubmitAsync(TestDb.OfficeSupplies, 50m);

            Assert.DoesNotContain((await LoadAsync(before)).ApprovalSteps, s => s.StepType == StepType.IT);
            Assert.Contains((await LoadAsync(after)).ApprovalSteps, s => s.StepType == StepType.IT);
        }

        [Fact]
        public async Task InactiveCategory_HiddenFromRequestForm()
        {
            await settings.UpdateCategoryAsync(Admin, TestDb.OfficeSupplies, requiresItApproval: false, isActive: false);

            Assert.DoesNotContain(await requests.GetActiveCategoriesAsync(), c => c.Id == TestDb.OfficeSupplies);
            Assert.Contains(await settings.GetCategoriesAsync(), c => c.Id == TestDb.OfficeSupplies);
        }

        [Fact]
        public async Task AddCategory_DuplicateName_IsRejected()
        {
            var added = await settings.AddCategoryAsync(Admin, "  Lab Equipment ", requiresItApproval: true);

            Assert.Equal("Lab Equipment", added.Name);
            await Assert.ThrowsAsync<ValidationException>(() => settings.AddCategoryAsync(Admin, "Lab Equipment", false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(10.005)]
        public async Task Threshold_InvalidValue_IsRejected(decimal threshold)
        {
            await Assert.ThrowsAsync<ValidationException>(() => settings.SetFinanceThresholdAsync(Admin, threshold));
        }

        [Fact]
        public async Task NonAdmin_CannotChangeSettings()
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => settings.SetFinanceThresholdAsync(TestDb.User(TestDb.Casey), 1m));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => settings.UpdateCategoryAsync(TestDb.User(TestDb.Casey), TestDb.OfficeSupplies, true, true));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => settings.AddCategoryAsync(TestDb.User(TestDb.Casey), "New", false));
        }

        private async Task<int> SubmitAsync(int categoryId, decimal unitPrice)
        {
            var model = TestDb.ValidModel(categoryId, unitPrice);
            model.LineItems[0].Quantity = 1;
            var draft = await requests.CreateDraftAsync(TestDb.Riley, model);
            await approvals.SubmitAsync(draft.Id, TestDb.Riley);
            return draft.Id;
        }

        private async Task<Request> LoadAsync(int id)
        {
            await using var db = testDb.CreateDbContext();
            return await db.Requests.Include(r => r.ApprovalSteps).SingleAsync(r => r.Id == id);
        }
    }
}
