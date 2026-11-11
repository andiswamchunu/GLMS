using GLMS.Web.Models;
using GLMS.Web.Services;

namespace GLMS.Tests;

public class ContractRulesTests
{
    private static readonly DateTime Today = new(2026, 10, 15);

    private static Contract MakeContract(ContractStatus status, DateTime? end = null) => new()
    {
        Id = 1,
        ClientId = 1,
        Status = status,
        StartDate = new DateTime(2026, 1, 1),
        EndDate = end ?? new DateTime(2027, 1, 1)
    };

    [Fact]
    public void CanCreateServiceRequest_ActiveContractInDate_IsAllowed()
    {
        var result = ContractRules.CanCreateServiceRequest(MakeContract(ContractStatus.Active), Today);

        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void CanCreateServiceRequest_ExpiredContract_IsBlocked()
    {
        var result = ContractRules.CanCreateServiceRequest(MakeContract(ContractStatus.Expired), Today);

        Assert.False(result.Allowed);
        Assert.Contains("Expired", result.Reason);
    }

    [Fact]
    public void CanCreateServiceRequest_OnHoldContract_IsBlocked()
    {
        var result = ContractRules.CanCreateServiceRequest(MakeContract(ContractStatus.OnHold), Today);

        Assert.False(result.Allowed);
        Assert.Contains("On Hold", result.Reason);
    }

    [Fact]
    public void CanCreateServiceRequest_DraftContract_IsBlocked()
    {
        var result = ContractRules.CanCreateServiceRequest(MakeContract(ContractStatus.Draft), Today);

        Assert.False(result.Allowed);
        Assert.Contains("Draft", result.Reason);
    }

    [Fact]
    public void CanCreateServiceRequest_ActiveButPastEndDate_IsBlocked()
    {
        var result = ContractRules.CanCreateServiceRequest(MakeContract(ContractStatus.Active, Today.AddDays(-1)), Today);

        Assert.False(result.Allowed);
        Assert.Contains("ended", result.Reason);
    }

    [Fact]
    public void CanCreateServiceRequest_ActiveAndEndsToday_IsStillAllowed()
    {
        var result = ContractRules.CanCreateServiceRequest(MakeContract(ContractStatus.Active, Today), Today);

        Assert.True(result.Allowed);
    }

    [Theory]
    [InlineData(ContractStatus.Active, -1, ContractStatus.Expired)]
    [InlineData(ContractStatus.OnHold, -1, ContractStatus.Expired)]
    [InlineData(ContractStatus.Active, 0, ContractStatus.Active)]
    [InlineData(ContractStatus.Active, 30, ContractStatus.Active)]
    [InlineData(ContractStatus.Draft, -10, ContractStatus.Draft)]
    [InlineData(ContractStatus.Expired, 30, ContractStatus.Expired)]
    public void EffectiveStatus_ReflectsEndDate(ContractStatus stored, int endOffsetDays, ContractStatus expected)
    {
        var contract = MakeContract(stored, Today.AddDays(endOffsetDays));

        Assert.Equal(expected, ContractRules.EffectiveStatus(contract, Today));
    }

    [Theory]
    [InlineData(ContractStatus.Draft, ContractStatus.Active, true)]
    [InlineData(ContractStatus.Draft, ContractStatus.Expired, true)]
    [InlineData(ContractStatus.Draft, ContractStatus.OnHold, false)]
    [InlineData(ContractStatus.Active, ContractStatus.OnHold, true)]
    [InlineData(ContractStatus.Active, ContractStatus.Expired, true)]
    [InlineData(ContractStatus.Active, ContractStatus.Draft, false)]
    [InlineData(ContractStatus.OnHold, ContractStatus.Active, true)]
    [InlineData(ContractStatus.OnHold, ContractStatus.Expired, true)]
    [InlineData(ContractStatus.Expired, ContractStatus.Active, false)]
    [InlineData(ContractStatus.Expired, ContractStatus.Draft, false)]
    [InlineData(ContractStatus.Expired, ContractStatus.OnHold, false)]
    public void CanTransition_FollowsTheContractLifecycle(ContractStatus from, ContractStatus to, bool expected)
    {
        Assert.Equal(expected, ContractRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending, ServiceRequestStatus.InProgress, true)]
    [InlineData(ServiceRequestStatus.Pending, ServiceRequestStatus.Cancelled, true)]
    [InlineData(ServiceRequestStatus.Pending, ServiceRequestStatus.Completed, false)]
    [InlineData(ServiceRequestStatus.InProgress, ServiceRequestStatus.Completed, true)]
    [InlineData(ServiceRequestStatus.InProgress, ServiceRequestStatus.Cancelled, true)]
    [InlineData(ServiceRequestStatus.InProgress, ServiceRequestStatus.Pending, false)]
    [InlineData(ServiceRequestStatus.Completed, ServiceRequestStatus.InProgress, false)]
    [InlineData(ServiceRequestStatus.Cancelled, ServiceRequestStatus.Pending, false)]
    public void ServiceRequestRules_CanTransition_FollowsTheRequestLifecycle(
        ServiceRequestStatus from, ServiceRequestStatus to, bool expected)
    {
        Assert.Equal(expected, ServiceRequestRules.CanTransition(from, to));
    }
}
