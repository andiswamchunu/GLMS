using System.ComponentModel.DataAnnotations;

namespace GLMS.Web.Models;

public enum ContractStatus
{
    Draft,
    Active,
    [Display(Name = "On Hold")] OnHold,
    Expired
}

public enum ServiceLevel
{
    Standard,
    Premium,
    Express
}

public enum ServiceRequestStatus
{
    Pending,
    [Display(Name = "In Progress")] InProgress,
    Completed,
    Cancelled
}
