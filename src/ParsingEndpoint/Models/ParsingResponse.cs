using System.Text.Json.Serialization;

namespace ParsingEndpoint.Models;

public sealed class ParsingResponse
{
    public int IsError { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public int ElementsCount { get; init; }
    public int EmailsCount { get; init; }
    public string Url { get; init; } = string.Empty;
    public string DecryptedPlainText { get; init; } = string.Empty;
    public List<string> ElementsAttrList { get; init; } = [];
    public List<string> EmailsList { get; init; } = [];

    [JsonIgnore]
    public int StatusCode { get; init; } = 200;

    public static ParsingResponse Failure(string code, string message, int statusCode = 400) =>
        new() { IsError = 1, ErrorCode = code, ErrorMessage = message, StatusCode = statusCode };
}

