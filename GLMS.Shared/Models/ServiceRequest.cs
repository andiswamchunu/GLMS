using System.ComponentModel.DataAnnotations;

namespace GLMS.Shared.Models;

public class ServiceRequest
{
    public int Id { get; set; }

    [Display(Name = "Contract")]
    public int ContractId { get; set; }

    public Contract? Contract { get; set; }

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Amount as entered by the user, in <see cref="CurrencyCode"/>.</summary>
    [Display(Name = "Original amount")]
    public decimal OriginalAmount { get; set; }

    [Required, StringLength(3), Display(Name = "Currency")]
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>The rate used (1 unit of <see cref="CurrencyCode"/> = N ZAR) at the time of creation.</summary>
    [Display(Name = "Exchange rate")]
    public decimal ExchangeRate { get; set; }

    [StringLength(60), Display(Name = "Rate source")]
    public string RateSource { get; set; } = string.Empty;

    /// <summary>The local (ZAR) cost - this is the "Cost" TechMove reports on.</summary>
    [Display(Name = "Cost (ZAR)")]
    public decimal CostZar { get; set; }

    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }
}
