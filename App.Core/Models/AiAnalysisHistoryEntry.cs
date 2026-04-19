namespace App.Core.Models;

public sealed record AiAnalysisHistoryEntry(
    Guid Id,
    DateTime CreatedAt,
    string Model,
    string BaseUrl,
    RiskAnalysisResult Analysis,
    string ReportText,
    string Title = "AI 风险分析",
    string Category = "AI 分析");
