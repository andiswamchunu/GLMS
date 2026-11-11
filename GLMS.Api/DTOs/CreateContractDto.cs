
using GLMS.Shared.Models;
using System.ComponentModel.DataAnnotations;

namespace GLMS.Api.DTOs;

public class CreateContractDto : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int ClientId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public ServiceLevel ServiceLevel { get; set; }
        = ServiceLevel.Standard;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (EndDate.Date <= StartDate.Date)
        {
            yield return new ValidationResult(
                "End date must be after start date.",
                new[] { nameof(EndDate) });
        }

        if (!Enum.IsDefined(ServiceLevel))
        {
            yield return new ValidationResult(
                "Invalid service level.",
                new[] { nameof(ServiceLevel) });
        }
    }
}

