namespace GLMS.Web.Services;

public static class TimeProviderExtensions
{
    /// <summary>Today's date (UTC, time part stripped). Contracts are compared on whole dates.</summary>
    public static DateTime Today(this TimeProvider time) => time.GetUtcNow().UtcDateTime.Date;
}
