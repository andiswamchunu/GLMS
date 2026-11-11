using System.ComponentModel.DataAnnotations;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GLMS.Web.ViewModels;

public class ContractListViewModel
{
    [DataType(DataType.Date), Display(Name = "Active from")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date), Display(Name = "Active until")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime? To { get; set; }

    public ContractStatus? Status { get; set; }

    [Display(Name = "Client")]
    public int? ClientId { get; set; }

    [Display(Name = "Client name")]
    public string? Query { get; set; }

    [BindNever]
    public IReadOnlyList<Contract> Results { get; set; } = Array.Empty<Contract>();

    [BindNever]
    public IEnumerable<SelectListItem> Clients { get; set; } = Array.Empty<SelectListItem>();

    public bool HasFilter => From.HasValue || To.HasValue || Status.HasValue || ClientId.HasValue || !string.IsNullOrWhiteSpace(Query);

    public ContractSearchCriteria ToCriteria() => new()
    {
        From = From,
        To = To,
        Status = Status,
        ClientId = ClientId,
        Query = Query
    };
}

public class ContractDetailsViewModel
{
    public Contract Contract { get; set; } = new();
    public RuleResult CanCreateRequest { get; set; }
    public ContractStatus EffectiveStatus { get; set; }
    public IReadOnlyList<ContractStatus> AllowedTransitions { get; set; } = Array.Empty<ContractStatus>();
}

public class ContractOption
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Eligible { get; set; }
    public string? Reason { get; set; }
}

public class ServiceRequestCreateViewModel
{
    [Required(ErrorMessage = "Please choose a contract."), Display(Name = "Contract")]
    public int? ContractId { get; set; }

    [Required(ErrorMessage = "Please describe the request."), StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter an amount."), Range(0.01, 1_000_000_000, ErrorMessage = "The amount must be greater than zero.")]
    [Display(Name = "Amount")]
    public decimal? Amount { get; set; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Use a 3-letter currency code."), Display(Name = "Currency")]
    public string CurrencyCode { get; set; } = "USD";

    [BindNever]
    public IReadOnlyList<ContractOption> Contracts { get; set; } = Array.Empty<ContractOption>();

    public static readonly string[] SupportedCurrencies = { "USD", "EUR", "GBP" };
}

public class DashboardViewModel
{
    public int ClientCount { get; set; }
    public int ActiveContracts { get; set; }
    public int OnHoldContracts { get; set; }
    public int ExpiredContracts { get; set; }
    public int DraftContracts { get; set; }
    public int OpenRequests { get; set; }
    public decimal TotalCostZar { get; set; }
    public IReadOnlyList<Contract> ExpiringSoon { get; set; } = Array.Empty<Contract>();
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
