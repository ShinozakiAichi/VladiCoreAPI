using System.Net;

namespace VladiCore.Api.Infrastructure;

public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public object? Details { get; }

    public ApiException(int statusCode, string code, string message, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public static ApiException NotFound(string message) =>
        new((int)HttpStatusCode.NotFound, "not_found", message);

    public static ApiException Conflict(string message) =>
        new((int)HttpStatusCode.Conflict, "conflict", message);
}
