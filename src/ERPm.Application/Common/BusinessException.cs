namespace ERPm.Application.Common;

public sealed class BusinessException(string code, int statusCode = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
