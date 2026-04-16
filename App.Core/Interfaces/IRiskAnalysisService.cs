using App.Core.Models;

namespace App.Core.Interfaces;

public interface IRiskAnalysisService
{
    RiskAnalysisResult Analyze(InspectionQueryResult inspectionResult);
}
