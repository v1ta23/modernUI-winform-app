using App.Core.Interfaces;
using App.Core.Models;

namespace App.Core.Services;

public sealed class LocalRiskAnalysisService : IRiskAnalysisService
{
    public RiskAnalysisResult Analyze(InspectionQueryResult inspectionResult)
    {
        var records = inspectionResult.Records
            .Where(record => !record.IsRevoked)
            .ToList();

        if (records.Count == 0)
        {
            return RiskAnalysisResult.Empty;
        }

        var pendingCount = records.Count(record =>
            record.Status != InspectionStatus.Normal &&
            !record.ClosedAt.HasValue);
        var affectedDeviceCount = records
            .Where(record => record.Status != InspectionStatus.Normal)
            .Select(record => $"{record.LineName}|{record.DeviceName}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var lineSummaries = BuildLineSummaries(records);
        var highRiskLine = lineSummaries
            .FirstOrDefault(row => row.AbnormalCount > 0)
            ?? lineSummaries.FirstOrDefault(row => row.WarningCount > 0);

        return new RiskAnalysisResult(
            BuildDecisionTitle(inspectionResult.Summary, pendingCount, highRiskLine),
            BuildRiskLevel(inspectionResult.Summary, pendingCount),
            BuildRiskLevelNote(pendingCount),
            highRiskLine?.LineName ?? "暂无重点产线",
            BuildPrimaryLineNote(highRiskLine),
            BuildActionTitle(pendingCount, inspectionResult.Summary, highRiskLine),
            BuildActionNote(pendingCount, inspectionResult.Summary),
            BuildRiskReason(inspectionResult.Summary, affectedDeviceCount, highRiskLine),
            BuildPriorityAction(inspectionResult.Summary, pendingCount, highRiskLine),
            BuildManagementAdvice(inspectionResult.Summary, pendingCount, highRiskLine));
    }

    private static IReadOnlyList<LineRiskSummary> BuildLineSummaries(IReadOnlyList<InspectionRecord> records)
    {
        return records
            .GroupBy(record => record.LineName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LineRiskSummary(
                group.First().LineName,
                group.Count(),
                group.Count(record => record.Status == InspectionStatus.Warning),
                group.Count(record => record.Status == InspectionStatus.Abnormal)))
            .OrderByDescending(row => row.AbnormalCount)
            .ThenByDescending(row => row.WarningCount)
            .ThenByDescending(row => row.TotalCount)
            .ThenBy(row => row.LineName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildDecisionTitle(
        InspectionSummary summary,
        int pendingCount,
        LineRiskSummary? highRiskLine)
    {
        if (summary.AbnormalCount > 0)
        {
            return $"结论：{highRiskLine?.LineName ?? "当前产线"} 风险偏高，建议优先安排异常处理。";
        }

        if (pendingCount > 0)
        {
            return "结论：当前存在待闭环问题，建议优先完成闭环确认。";
        }

        if (summary.WarningCount > 0)
        {
            return "结论：当前以预警为主，建议复核重点设备和产线波动。";
        }

        return "结论：当前巡检状态平稳，可按既定节奏复盘。";
    }

    private static string BuildRiskLevel(InspectionSummary summary, int pendingCount)
    {
        if (summary.AbnormalCount > 0)
        {
            return "高风险";
        }

        if (pendingCount > 0)
        {
            return "中风险";
        }

        if (summary.WarningCount > 0)
        {
            return "轻度波动";
        }

        return "整体平稳";
    }

    private static string BuildRiskLevelNote(int pendingCount)
    {
        return pendingCount == 0
            ? "当前没有待闭环问题。"
            : $"待闭环 {pendingCount} 条，优先确认。";
    }

    private static string BuildPrimaryLineNote(LineRiskSummary? highRiskLine)
    {
        return highRiskLine is null
            ? "关注整体趋势和待关注记录。"
            : $"异常 {highRiskLine.AbnormalCount} 条 / 预警 {highRiskLine.WarningCount} 条。";
    }

    private static string BuildActionTitle(
        int pendingCount,
        InspectionSummary summary,
        LineRiskSummary? highRiskLine)
    {
        if (pendingCount > 0)
        {
            return highRiskLine is null ? "优先闭环" : $"优先处理 {highRiskLine.LineName}";
        }

        if (summary.WarningCount > 0 || summary.AbnormalCount > 0)
        {
            return "复核记录";
        }

        return "趋势复盘";
    }

    private static string BuildActionNote(int pendingCount, InspectionSummary summary)
    {
        if (summary.AbnormalCount > 0)
        {
            return "异常记录优先处理。";
        }

        if (pendingCount > 0)
        {
            return "避免问题跨班次滞留。";
        }

        return summary.WarningCount > 0
            ? "复核近期波动记录。"
            : "持续观察趋势稳定性。";
    }

    private static string BuildRiskReason(
        InspectionSummary summary,
        int affectedDeviceCount,
        LineRiskSummary? highRiskLine)
    {
        if (summary.AbnormalCount > 0)
        {
            return highRiskLine is null
                ? $"异常 {summary.AbnormalCount} 条，涉及 {affectedDeviceCount} 台设备。"
                : $"{highRiskLine.LineName} 异常 {highRiskLine.AbnormalCount} 条，预警 {highRiskLine.WarningCount} 条，是当前主要风险来源。";
        }

        if (summary.WarningCount > 0)
        {
            return highRiskLine is null
                ? $"当前有 {summary.WarningCount} 条预警，暂未形成异常集中。"
                : $"{highRiskLine.LineName} 出现预警波动，建议复核设备状态。";
        }

        return "巡检状态保持稳定，未发现明显集中风险。";
    }

    private static string BuildPriorityAction(
        InspectionSummary summary,
        int pendingCount,
        LineRiskSummary? highRiskLine)
    {
        if (summary.AbnormalCount > 0)
        {
            return highRiskLine is null
                ? "先处理异常记录，再复核预警设备。"
                : $"先处理 {highRiskLine.LineName} 异常记录，再复核同产线预警设备。";
        }

        if (pendingCount > 0)
        {
            return "先完成待闭环确认，再复查未关闭记录。";
        }

        if (summary.WarningCount > 0)
        {
            return "先复核预警设备，再观察后续趋势。";
        }

        return "按当前巡检节奏继续复盘。";
    }

    private static string BuildManagementAdvice(
        InspectionSummary summary,
        int pendingCount,
        LineRiskSummary? highRiskLine)
    {
        if (summary.AbnormalCount > 0)
        {
            return highRiskLine is null
                ? "本班次结束前完成异常确认，并记录处理结果。"
                : $"本班次结束前完成 {highRiskLine.LineName} 复核，并跟踪后续趋势。";
        }

        if (pendingCount > 0)
        {
            return "明确责任人和处理时限，避免待闭环事项跨班次滞留。";
        }

        if (summary.WarningCount > 0)
        {
            return "保持巡检频次，关注预警是否转为异常。";
        }

        return "保持当前巡检节奏，定期复盘产线稳定性。";
    }

    private sealed record LineRiskSummary(
        string LineName,
        int TotalCount,
        int WarningCount,
        int AbnormalCount);
}
