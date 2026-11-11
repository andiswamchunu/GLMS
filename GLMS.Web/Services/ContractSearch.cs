using GLMS.Shared.Models;

namespace GLMS.Web.Services;

public class ContractSearchCriteria
{
    /// <summary>Only contracts whose period is still running on or after this date.</summary>
    public DateTime? From { get; set; }

    /// <summary>Only contracts that had already started on or before this date.</summary>
    public DateTime? To { get; set; }

    public ContractStatus? Status { get; set; }
    public int? ClientId { get; set; }

    /// <summary>Free text matched against the client name.</summary>
    public string? Query { get; set; }
}

public static class ContractQueryExtensions
{
    /// <summary>
    /// LINQ filter used by the Admin search screen. The date range matches contracts whose period
    /// (StartDate..EndDate) overlaps the requested range, so "01 Jan - 31 Mar" finds every contract
    /// that was running at any point in that window.
    /// </summary>
    public static IQueryable<Contract> ApplyFilter(this IQueryable<Contract> query, ContractSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        if (criteria.ClientId is { } clientId)
        {
            query = query.Where(c => c.ClientId == clientId);
        }

        if (criteria.From is { } fromValue)
        {
            var fromDate = fromValue.Date;
            query = query.Where(c => c.EndDate >= fromDate);
        }

        if (criteria.To is { } toValue)
        {
            var toDate = toValue.Date;
            query = query.Where(c => c.StartDate <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var term = criteria.Query.Trim();
            query = query.Where(c => c.Client!.Name.Contains(term));
        }

        return query.OrderBy(c => c.EndDate).ThenBy(c => c.Id);
    }
}
