using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GLMS.Web.Models;

public class Contract : IValidatableObject
{
    public int Id { get; set; }

    [Display(Name = "Client"), Range(1, int.MaxValue, ErrorMessage = "Please choose a client.")]
    public int ClientId { get; set; }

    public Client? Client { get; set; }

    [DataType(DataType.Date), Display(Name = "Start date")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date), Display(Name = "End date")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    [Display(Name = "Service level")]
    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Standard;

    /// <summary>Server-side (GUID based) file name of the signed agreement PDF.</summary>
    [StringLength(100)]
    public string? SignedAgreementStoredName { get; set; }

    /// <summary>The file name the user originally uploaded; used as the download name.</summary>
    [StringLength(260)]
    public string? SignedAgreementOriginalName { get; set; }

    public DateTime? SignedAgreementUploadedAtUtc { get; set; }

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();

    [NotMapped]
    public bool HasSignedAgreement => !string.IsNullOrEmpty(SignedAgreementStoredName);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate.Date <= StartDate.Date)
        {
            yield return new ValidationResult(
                "The end date must be after the start date.",
                new[] { nameof(EndDate) });
        }
    }
}
