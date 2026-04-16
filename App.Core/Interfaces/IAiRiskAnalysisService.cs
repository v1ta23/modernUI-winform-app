using App.Core.Models;

namespace App.Core.Interfaces;

public interface IAiRiskAnalysisService
{
    bool IsConfigured { get; }

    AiRiskAnalysisSettings GetSettings();

    void SaveSettings(AiRiskAnalysisSettings settings);

    Task<RiskAnalysisResult> AnalyzeAsync(
        InspectionQueryResult inspectionResult,
        RiskAnalysisResult fallbackAnalysis,
        CancellationToken cancellationToken = default);
}
