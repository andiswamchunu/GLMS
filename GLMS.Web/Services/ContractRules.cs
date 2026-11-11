using GLMS.Shared.Models;

namespace GLMS.Web.Services;

public readonly record struct RuleResult(bool Allowed, string? Reason)
{
    public static RuleResult Ok() => new(true, null);
    public static RuleResult Deny(string reason) => new(false, reason);
}

/// <summary>
/// Pure (database free) business rules for contracts, kept separate so they are trivial to unit test.
/// </summary>
public static class ContractRules
{
    /// <summary>
    /// A service request may only be created against a valid, Active contract that has not passed its end date
    /// (Part 1, Phase B). Expired and On Hold contracts are explicitly blocked; Draft contracts are not yet in force.
    /// </summary>
    public static RuleResult CanCreateServiceRequest(Contract contract, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(contract);

        switch (contract.Status)
        {
            case ContractStatus.Expired:
                return RuleResult.Deny("This contract has Expired, so no new service requests can be created for it.");
            case ContractStatus.OnHold:
                return RuleResult.Deny("This contract is On Hold, so no new service requests can be created for it.");
            case ContractStatus.Draft:
                return RuleResult.Deny("This contract is still a Draft. Activate it before creating service requests.");
        }

        if (contract.EndDate.Date < today.Date)
        {
            return RuleResult.Deny($"This contract ended on {contract.EndDate:yyyy-MM-dd}, so no new service requests can be created for it.");
        }

        return RuleResult.Ok();
    }

    /// <summary>The status the contract should be treated as today (an Active/On Hold contract past its end date is Expired).</summary>
    public static ContractStatus EffectiveStatus(Contract contract, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var inForceStatus = contract.Status is ContractStatus.Active or ContractStatus.OnHold;
        return inForceStatus && contract.EndDate.Date < today.Date ? ContractStatus.Expired : contract.Status;
    }

    /// <summary>Allowed lifecycle: Draft -> Active/Expired, Active -> On Hold/Expired, On Hold -> Active/Expired, Expired is final.</summary>
    public static bool CanTransition(ContractStatus from, ContractStatus to) => (from, to) switch
    {
        (ContractStatus.Draft, ContractStatus.Active) => true,
        (ContractStatus.Draft, ContractStatus.Expired) => true,
        (ContractStatus.Active, ContractStatus.OnHold) => true,
        (ContractStatus.Active, ContractStatus.Expired) => true,
        (ContractStatus.OnHold, ContractStatus.Active) => true,
        (ContractStatus.OnHold, ContractStatus.Expired) => true,
        _ => false
    };
}

public static class ServiceRequestRules
{
    /// <summary>Pending -> In Progress/Cancelled, In Progress -> Completed/Cancelled; Completed and Cancelled are final.</summary>
    public static bool CanTransition(ServiceRequestStatus from, ServiceRequestStatus to) => (from, to) switch
    {
        (ServiceRequestStatus.Pending, ServiceRequestStatus.InProgress) => true,
        (ServiceRequestStatus.Pending, ServiceRequestStatus.Cancelled) => true,
        (ServiceRequestStatus.InProgress, ServiceRequestStatus.Completed) => true,
        (ServiceRequestStatus.InProgress, ServiceRequestStatus.Cancelled) => true,
        _ => false
    };
}
