using App.Core.Models;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Services;

internal static class AiReportFormatter
{
    public static string BuildAnalysisReport(RiskAnalysisResult analysis)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, [
            $"【综合结论】{analysis.DecisionTitle}",
            $"【风险等级】{analysis.RiskLevel}。{analysis.RiskLevelNote}",
            $"【重点产线】{analysis.PrimaryLineName}。{analysis.PrimaryLineNote}",
            $"【疑似原因】{analysis.SuspectedCause}",
            $"【责任建议】{analysis.SuggestedOwner}",
            $"【处理时限】{analysis.SuggestedDeadline}",
            $"【生产影响】{analysis.ProductionImpact}",
            $"【停机复检】{analysis.StopInspectionAdvice}",
            $"【优先处理】{analysis.PriorityAction}",
            $"【管理建议】{analysis.ManagementAdvice}"
        ]);
    }

    public static string BuildDailyReport(
        InspectionDashboardViewModel dashboard,
        RiskAnalysisResult analysis,
        InspectionFilterViewModel filter)
    {
        var pendingCount = dashboard.Records.Count(record =>
            record.Status != InspectionStatus.Normal &&
            !record.IsClosed &&
            !record.IsRevoked);
        var filterText = BuildFilterText(filter);
        var topLine = dashboard.Records
            .Where(record => record.Status != InspectionStatus.Normal)
            .GroupBy(record => record.LineName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                LineName = group.First().LineName,
                AbnormalCount = group.Count(record => record.Status == InspectionStatus.Abnormal),
                WarningCount = group.Count(record => record.Status == InspectionStatus.Warning)
            })
            .OrderByDescending(item => item.AbnormalCount)
            .ThenByDescending(item => item.WarningCount)
            .FirstOrDefault();

        var topLineText = topLine is null
            ? "暂无突出产线风险。"
            : $"{topLine.LineName}：异常 {topLine.AbnormalCount} 条，预警 {topLine.WarningCount} 条。";

        return string.Join(Environment.NewLine + Environment.NewLine, [
            $"巡检日报  {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"筛选范围：{filterText}",
            $"一、巡检概况：共 {dashboard.TotalCount} 条记录，合格率 {dashboard.PassRateText}。正常 {dashboard.NormalCount} 条，预警 {dashboard.WarningCount} 条，异常 {dashboard.AbnormalCount} 条。",
            $"二、闭环状态：待闭环 {pendingCount} 条，建议优先处理异常项，再复核预警项。",
            $"三、重点产线：{topLineText}",
            $"四、AI 结论：{analysis.DecisionTitle}",
            $"五、现场处理：{analysis.PriorityAction}",
            $"六、责任建议：{analysis.SuggestedOwner}，处理时限：{analysis.SuggestedDeadline}",
            $"七、管理建议：{analysis.ManagementAdvice}"
        ]);
    }

    private static string BuildFilterText(InspectionFilterViewModel filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.LineName))
        {
            parts.Add($"产线={filter.LineName}");
        }

        if (filter.Status.HasValue)
        {
            parts.Add($"状态={filter.Status.Value.ToDisplayText()}");
        }

        if (filter.PendingOnly)
        {
            parts.Add("仅待闭环");
        }

        if (filter.StartTime.HasValue || filter.EndTime.HasValue)
        {
            var start = filter.StartTime?.ToString("yyyy-MM-dd") ?? "不限";
            var end = filter.EndTime?.ToString("yyyy-MM-dd") ?? "不限";
            parts.Add($"日期={start} 至 {end}");
        }

        return parts.Count == 0 ? "全部数据" : string.Join("，", parts);
    }
}
