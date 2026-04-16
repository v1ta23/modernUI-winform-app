using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using App.Core.Interfaces;
using App.Core.Models;

namespace App.Infrastructure.Services;

public sealed class OpenAiCompatibleRiskAnalysisService : IAiRiskAnalysisService, IDisposable
{
    private const string ApiKeyEnvironmentName = "AI_API_KEY";
    private const string BaseUrlEnvironmentName = "AI_API_BASE_URL";
    private const string ModelEnvironmentName = "AI_MODEL";
    private static readonly Uri DefaultEndpoint = BuildEndpoint(AiRiskAnalysisSettings.DefaultBaseUrl);
    private readonly object _settingsLock = new();
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private string _apiKey = string.Empty;
    private string _baseUrl = AiRiskAnalysisSettings.DefaultBaseUrl;
    private string _model = AiRiskAnalysisSettings.DefaultModel;
    private Uri _endpoint = DefaultEndpoint;

    public OpenAiCompatibleRiskAnalysisService(
        string apiKey,
        string baseUrl,
        string model,
        HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _ownsHttpClient = httpClient is null;
        ApplySettings(new AiRiskAnalysisSettings(apiKey, baseUrl, model));
    }

    public bool IsConfigured
    {
        get
        {
            lock (_settingsLock)
            {
                return !string.IsNullOrWhiteSpace(_apiKey);
            }
        }
    }

    public static OpenAiCompatibleRiskAnalysisService FromEnvironment()
    {
        var apiKey = ReadEnvironment(ApiKeyEnvironmentName)
            ?? ReadEnvironment("OPENAI_API_KEY")
            ?? string.Empty;
        var baseUrl = ReadEnvironment(BaseUrlEnvironmentName)
            ?? ReadEnvironment("OPENAI_BASE_URL")
            ?? AiRiskAnalysisSettings.DefaultBaseUrl;
        var model = ReadEnvironment(ModelEnvironmentName)
            ?? AiRiskAnalysisSettings.DefaultModel;

        return new OpenAiCompatibleRiskAnalysisService(apiKey, baseUrl, model);
    }

    public AiRiskAnalysisSettings GetSettings()
    {
        lock (_settingsLock)
        {
            return new AiRiskAnalysisSettings(_apiKey, _baseUrl, _model);
        }
    }

    public void SaveSettings(AiRiskAnalysisSettings settings)
    {
        var normalizedSettings = NormalizeSettings(settings);
        var endpoint = BuildEndpoint(normalizedSettings.BaseUrl);

        lock (_settingsLock)
        {
            _apiKey = normalizedSettings.ApiKey;
            _baseUrl = normalizedSettings.BaseUrl;
            _model = normalizedSettings.Model;
            _endpoint = endpoint;
        }

        WriteEnvironment(ApiKeyEnvironmentName, normalizedSettings.ApiKey);
        WriteEnvironment(BaseUrlEnvironmentName, normalizedSettings.BaseUrl);
        WriteEnvironment(ModelEnvironmentName, normalizedSettings.Model);
    }

    private static string? ReadEnvironment(string name)
    {
        var processValue = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return processValue;
        }

        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    }

    public async Task<RiskAnalysisResult> AnalyzeAsync(
        InspectionQueryResult inspectionResult,
        RiskAnalysisResult fallbackAnalysis,
        CancellationToken cancellationToken = default)
    {
        var requestSettings = CreateRequestSettings();
        if (string.IsNullOrWhiteSpace(requestSettings.ApiKey))
        {
            throw new InvalidOperationException("未配置 AI_API_KEY，无法生成 AI 分析。");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, requestSettings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestSettings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = requestSettings.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "你是制造业设备巡检风险分析助手。只返回 JSON，不要 Markdown。字段为 decisionTitle、riskLevel、riskLevelNote、primaryLineName、primaryLineNote、actionTitle、actionNote、riskReason、priorityAction、managementAdvice。"
                },
                new
                {
                    role = "user",
                    content = BuildPrompt(inspectionResult, fallbackAnalysis)
                }
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI 分析失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}。{TrimForMessage(responseBody)}");
        }

        var content = ExtractMessageContent(responseBody);
        var payload = ParsePayload(content);
        return new RiskAnalysisResult(
            Use(payload.DecisionTitle, fallbackAnalysis.DecisionTitle),
            Use(payload.RiskLevel, fallbackAnalysis.RiskLevel),
            Use(payload.RiskLevelNote, fallbackAnalysis.RiskLevelNote),
            Use(payload.PrimaryLineName, fallbackAnalysis.PrimaryLineName),
            Use(payload.PrimaryLineNote, fallbackAnalysis.PrimaryLineNote),
            Use(payload.ActionTitle, fallbackAnalysis.ActionTitle),
            Use(payload.ActionNote, fallbackAnalysis.ActionNote),
            Use(payload.RiskReason, fallbackAnalysis.RiskReason),
            Use(payload.PriorityAction, fallbackAnalysis.PriorityAction),
            Use(payload.ManagementAdvice, fallbackAnalysis.ManagementAdvice));
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private void ApplySettings(AiRiskAnalysisSettings settings)
    {
        var normalizedSettings = NormalizeSettings(settings);
        _apiKey = normalizedSettings.ApiKey;
        _baseUrl = normalizedSettings.BaseUrl;
        _model = normalizedSettings.Model;
        _endpoint = BuildEndpoint(normalizedSettings.BaseUrl);
    }

    private RequestSettings CreateRequestSettings()
    {
        lock (_settingsLock)
        {
            return new RequestSettings(_apiKey, _model, _endpoint);
        }
    }

    private static AiRiskAnalysisSettings NormalizeSettings(AiRiskAnalysisSettings settings)
    {
        var apiKey = settings.ApiKey.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? AiRiskAnalysisSettings.DefaultBaseUrl
            : settings.BaseUrl.Trim();
        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? AiRiskAnalysisSettings.DefaultModel
            : settings.Model.Trim();
        return new AiRiskAnalysisSettings(apiKey, baseUrl, model);
    }

    private static void WriteEnvironment(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
    }

    private static string BuildPrompt(InspectionQueryResult result, RiskAnalysisResult fallbackAnalysis)
    {
        var records = result.Records
            .Where(record => !record.IsRevoked)
            .OrderByDescending(record => record.CheckedAt)
            .Take(20)
            .Select(record => new
            {
                record.CheckedAt,
                record.LineName,
                record.DeviceName,
                Status = record.Status.ToString(),
                IsClosed = record.ClosedAt.HasValue,
                record.Remark,
                record.ClosureRemark
            })
            .ToList();

        var context = new
        {
            Summary = new
            {
                result.Summary.TotalCount,
                result.Summary.NormalCount,
                result.Summary.WarningCount,
                result.Summary.AbnormalCount,
                result.Summary.PassRate
            },
            LocalAnalysis = fallbackAnalysis,
            RecentRecords = records
        };

        return "请基于以下巡检数据生成简短、正式、可执行的风险分析。每个字段控制在 40 个汉字以内。\n" +
               JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Uri BuildEndpoint(string baseUrl)
    {
        var raw = string.IsNullOrWhiteSpace(baseUrl) ? AiRiskAnalysisSettings.DefaultBaseUrl : baseUrl.Trim();
        var uri = new Uri(raw, UriKind.Absolute);
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var root = new Uri(raw.EndsWith("/") ? raw : $"{raw}/", UriKind.Absolute);
        var relativePath = path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? "chat/completions"
            : uri.Host.Contains("deepseek", StringComparison.OrdinalIgnoreCase)
                ? "chat/completions"
                : "v1/chat/completions";
        return new Uri(root, relativePath);
    }

    private static string ExtractMessageContent(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var choices = document.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("AI 分析失败：响应中没有 choices。");
        }

        return choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static AiRiskAnalysisPayload ParsePayload(string content)
    {
        var json = ExtractJsonObject(content);
        var payload = JsonSerializer.Deserialize<AiRiskAnalysisPayload>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return payload ?? new AiRiskAnalysisPayload();
    }

    private static string ExtractJsonObject(string content)
    {
        var text = content.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd >= 0 && lastFence > firstLineEnd)
            {
                text = text[(firstLineEnd + 1)..lastFence].Trim();
            }
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start
            ? text[start..(end + 1)]
            : text;
    }

    private static string Use(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string TrimForMessage(string value)
    {
        var text = value.Trim();
        return text.Length <= 240 ? text : $"{text[..240]}...";
    }

    private sealed class AiRiskAnalysisPayload
    {
        public string? DecisionTitle { get; init; }

        public string? RiskLevel { get; init; }

        public string? RiskLevelNote { get; init; }

        public string? PrimaryLineName { get; init; }

        public string? PrimaryLineNote { get; init; }

        public string? ActionTitle { get; init; }

        public string? ActionNote { get; init; }

        public string? RiskReason { get; init; }

        public string? PriorityAction { get; init; }

        public string? ManagementAdvice { get; init; }
    }

    private readonly record struct RequestSettings(string ApiKey, string Model, Uri Endpoint);
}
