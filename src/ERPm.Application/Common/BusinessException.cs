namespace ERPm.Application.Common;

/// <summary>A business rule failure. <paramref name="args"/> fills {name} placeholders in the localized message.</summary>
public sealed class BusinessException(string code, int statusCode = 400, IReadOnlyDictionary<string, string>? args = null) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string>? Args { get; } = args;
}
