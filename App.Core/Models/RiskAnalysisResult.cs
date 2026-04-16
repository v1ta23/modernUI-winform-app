namespace App.Core.Models;

public sealed record RiskAnalysisResult(
    string DecisionTitle,
    string RiskLevel,
    string RiskLevelNote,
    string PrimaryLineName,
    string PrimaryLineNote,
    string ActionTitle,
    string ActionNote,
    string RiskReason,
    string PriorityAction,
    string ManagementAdvice)
{
    public static RiskAnalysisResult Empty { get; } = new(
        "结论：暂无巡检数据，建议先补充数据。",
        "暂无数据",
        "等待巡检数据。",
        "暂无重点产线",
        "补充巡检数据后再判断。",
        "补充数据",
        "先建立有效巡检记录。",
        "暂无巡检记录，建议先补充数据。",
        "先补充巡检数据。",
        "建立巡检记录后再进行风险判断。");
}
