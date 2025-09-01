namespace VladiCore.App.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public object? Details { get; }

    public AppException(int statusCode, string code, string message, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }
}
