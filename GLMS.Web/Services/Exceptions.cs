namespace GLMS.Web.Services;

/// <summary>A business rule was violated (e.g. creating a request on an expired contract).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

/// <summary>An uploaded file was rejected (wrong type, empty, too large or not really a PDF).</summary>
public class InvalidFileException : Exception
{
    public InvalidFileException(string message) : base(message)
    {
    }
}

/// <summary>No exchange rate could be obtained from the live API, the cache or the configured fallback.</summary>
public class CurrencyUnavailableException : BusinessRuleException
{
    public CurrencyUnavailableException(string message) : base(message)
    {
    }
}
