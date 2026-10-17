namespace Breb.App.Services;

public sealed class ApiException : Exception
{
    public int? StatusCode { get; }

    public ApiException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
