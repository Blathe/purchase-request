using PurchaseRequest.Data;
using PurchaseRequest.Services;

namespace PurchaseRequest.Tests
{
    public class ApprovalChainTests
    {
        private static readonly AppUser Manager = new() { Id = TestDb.Morgan, DisplayName = "Morgan" };
        private static readonly Category OfficeSupplies = new() { Id = 1, Name = "Office Supplies" };
        private static readonly Category Hardware = new() { Id = 2, Name = "Hardware", RequiresItApproval = true };

        // BR-01
        [Fact]
        public void Build_FirstStepIsManager()
        {
            var steps = ApprovalChainBuilder.Build(1, Manager, OfficeSupplies, 60m, 5000m);

            var step = Assert.Single(steps);
            Assert.Equal(StepType.Manager, step.StepType);
            Assert.Same(Manager, step.Approver);
            Assert.Equal(1, step.Sequence);
        }

        // BR-01 edge case
        [Fact]
        public void Build_NoManager_GoesToAdmin()
        {
            var step = Assert.Single(ApprovalChainBuilder.Build(1, null, OfficeSupplies, 60m, 5000m));

            Assert.Null(step.Approver);
            Assert.Equal(AppRoles.Admin, step.ApproverRole);
        }

        // BR-02, UAT-06
        [Fact]
        public void Build_OverThreshold_AddsFinance()
        {
            var steps = ApprovalChainBuilder.Build(1, Manager, OfficeSupplies, 5000.01m, 5000m);

            Assert.Equal([StepType.Manager, StepType.Finance], steps.Select(s => s.StepType));
            Assert.Equal(AppRoles.FinanceApprover, steps[1].ApproverRole);
        }

        // BR-02, UAT-05: equal to the threshold is not over it.
        [Fact]
        public void Build_AtThreshold_NoFinance()
        {
            var steps = ApprovalChainBuilder.Build(1, Manager, OfficeSupplies, 5000.00m, 5000m);

            Assert.Equal([StepType.Manager], steps.Select(s => s.StepType));
        }

        // BR-03, UAT-04, UAT-06
        [Fact]
        public void Build_ItCategory_AddsItStep()
        {
            var steps = ApprovalChainBuilder.Build(2, Manager, Hardware, 6000m, 5000m);

            Assert.Equal([StepType.Manager, StepType.IT, StepType.Finance], steps.Select(s => s.StepType));
            Assert.Equal([1, 2, 3], steps.Select(s => s.Sequence));
            Assert.All(steps, s => Assert.Equal(2, s.Round));
        }
    }
}
