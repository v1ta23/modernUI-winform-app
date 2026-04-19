using App.Core.Models;

namespace App.Core.Interfaces;

public interface IAiRiskAnalysisService
{
    bool IsConfigured { get; }

    AiRiskAnalysisSettings GetSettings();

    void SaveSettings(AiRiskAnalysisSettings settings);

    Task TestConnectionAsync(
        AiRiskAnalysisSettings settings,
        CancellationToken cancellationToken = default);

    Task<RiskAnalysisResult> AnalyzeAsync(
        InspectionQueryResult inspectionResult,
        RiskAnalysisResult fallbackAnalysis,
        CancellationToken cancellationToken = default);

    Task<string> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
