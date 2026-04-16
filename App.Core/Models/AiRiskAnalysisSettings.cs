namespace App.Core.Models;

public sealed record AiRiskAnalysisSettings(string ApiKey, string BaseUrl, string Model)
{
    public const string DefaultBaseUrl = "https://codeapi.icu";
    public const string DefaultModel = "gpt-5.4-mini";

    public static AiRiskAnalysisSettings Empty { get; } = new(
        string.Empty,
        DefaultBaseUrl,
        DefaultModel);
}
