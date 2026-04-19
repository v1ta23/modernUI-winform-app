using App.Core.Interfaces;
using App.Core.Models;
using WinFormsApp.Services;
using WinFormsApp.Exports;
using WinFormsApp.ViewModels;
using System.IO;
using System.Text.Json;

namespace WinFormsApp.Controllers;

internal sealed class InspectionController
{
    private readonly IInspectionRecordService _inspectionRecordService;
    private readonly IRiskAnalysisService _riskAnalysisService;
    private readonly IAiRiskAnalysisService _aiRiskAnalysisService;
    private readonly AiAnalysisHistoryStore _aiAnalysisHistoryStore;
    private readonly InspectionExcelExporter _excelExporter;

    public InspectionController(
        IInspectionRecordService inspectionRecordService,
        IRiskAnalysisService riskAnalysisService,
        IAiRiskAnalysisService aiRiskAnalysisService,
        AiAnalysisHistoryStore aiAnalysisHistoryStore,
        InspectionExcelExporter excelExporter)
    {
        _inspectionRecordService = inspectionRecordService;
        _riskAnalysisService = riskAnalysisService;
        _aiRiskAnalysisService = aiRiskAnalysisService;
        _aiAnalysisHistoryStore = aiAnalysisHistoryStore;
        _excelExporter = excelExporter;
    }

    public InspectionDashboardViewModel Load(InspectionFilterViewModel filter)
    {
        var result = _inspectionRecordService.Query(ToQuery(filter));
        return new InspectionDashboardViewModel
        {
            LineOptions = result.LineOptions,
            Templates = result.Templates
                .Select(template => new InspectionTemplateViewModel
                {
                    Id = template.Id,
                    DisplayText = $"{template.LineName} / {template.DeviceName} / {template.InspectionItem}",
                    LineName = template.LineName,
                    DeviceName = template.DeviceName,
                    InspectionItem = template.InspectionItem,
                    DefaultInspector = template.DefaultInspector,
                    DefaultRemark = template.DefaultRemark
                })
                .ToList(),
            Records = result.Records
                .Select(record => new InspectionRecordViewModel
                {
                    Id = record.Id,
                    Status = record.Status,
                    IsClosed = record.ClosedAt.HasValue,
                    IsRevoked = record.IsRevoked,
                    CheckedAtValue = record.CheckedAt,
                    CheckedAt = record.CheckedAt.ToString("yyyy-MM-dd HH:mm"),
                    LineName = record.LineName,
                    DeviceName = record.DeviceName,
                    InspectionItem = record.InspectionItem,
                    Inspector = record.Inspector,
                    StatusText = record.Status.ToDisplayText(),
                    MeasuredValue = record.MeasuredValue,
                    MeasuredValueText = record.MeasuredValue.ToString("0.##"),
                    Remark = record.Remark,
                    ClosureStateText = BuildClosureStateText(record),
                    ActionRemark = BuildActionRemark(record)
                })
                .ToList(),
            TrendPoints = result.Summary.TrendPoints
                .Select(point => new InspectionTrendPointViewModel
                {
                    Label = point.Label,
                    NormalCount = point.NormalCount,
                    WarningCount = point.WarningCount,
                    AbnormalCount = point.AbnormalCount
                })
                .ToList(),
            TotalCount = result.Summary.TotalCount,
            NormalCount = result.Summary.NormalCount,
            WarningCount = result.Summary.WarningCount,
            AbnormalCount = result.Summary.AbnormalCount,
            PassRateText = $"{result.Summary.PassRate:0.0}%",
            GeneratedAt = result.GeneratedAt,
            RiskAnalysis = _riskAnalysisService.Analyze(result)
        };
    }

    public async Task<RiskAnalysisResult> GenerateAiAnalysisAsync(
        InspectionFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        var result = _inspectionRecordService.Query(ToQuery(filter));
        var fallbackAnalysis = _riskAnalysisService.Analyze(result);
        return await _aiRiskAnalysisService
            .AnalyzeAsync(result, fallbackAnalysis, cancellationToken)
            .ConfigureAwait(false);
    }

    public AiRiskAnalysisSettings GetAiSettings()
    {
        return _aiRiskAnalysisService.GetSettings();
    }

    public void SaveAiSettings(AiRiskAnalysisSettings settings)
    {
        _aiRiskAnalysisService.SaveSettings(settings);
    }

    public Task TestAiSettingsAsync(
        AiRiskAnalysisSettings settings,
        CancellationToken cancellationToken = default)
    {
        return _aiRiskAnalysisService.TestConnectionAsync(settings, cancellationToken);
    }

    public AiAnalysisHistoryEntry SaveAiAnalysisHistory(
        RiskAnalysisResult analysis,
        string reportText,
        string title = "AI 风险分析",
        string category = "AI 分析")
    {
        var settings = _aiRiskAnalysisService.GetSettings();
        var entry = new AiAnalysisHistoryEntry(
            Guid.NewGuid(),
            DateTime.Now,
            settings.Model,
            settings.BaseUrl,
            analysis,
            reportText,
            title,
            category);
        _aiAnalysisHistoryStore.Add(entry);
        return entry;
    }

    public IReadOnlyList<AiAnalysisHistoryEntry> GetAiAnalysisHistory()
    {
        return _aiAnalysisHistoryStore.GetRecent();
    }

    public async Task<string> GenerateAiCollaborationAdviceAsync(
        AiCollaborationRole role,
        InspectionFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        var result = _inspectionRecordService.Query(ToQuery(filter));
        var fallbackAnalysis = _riskAnalysisService.Analyze(result);
        var prompt = BuildCollaborationPrompt(role, result, fallbackAnalysis);
        return await _aiRiskAnalysisService
            .GenerateTextAsync(BuildCollaborationSystemPrompt(role), prompt, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(InspectionEntryViewModel entry)
    {
        _inspectionRecordService.Add(ToDraft(entry));
    }

    public InspectionImportResultViewModel Import(
        IReadOnlyList<InspectionEntryViewModel> entries,
        string sourceFileName)
    {
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("没有可导入的记录。");
        }

        var batchKeyword = $"CSV导入-{DateTime.Now:yyyyMMddHHmmss}";
        var sourceLabel = string.IsNullOrWhiteSpace(sourceFileName)
            ? "未命名文件"
            : Path.GetFileName(sourceFileName);
        var drafts = entries
            .Select(entry =>
            {
                var draft = ToDraft(entry);
                return draft with
                {
                    Remark = BuildImportedRemark(draft.Remark, batchKeyword, sourceLabel)
                };
            })
            .ToList();

        var result = _inspectionRecordService.Import(drafts);
        return new InspectionImportResultViewModel
        {
            BatchKeyword = batchKeyword,
            SourceFileName = sourceLabel,
            ImportedCount = result.ImportedCount,
            NormalCount = result.NormalCount,
            WarningCount = result.WarningCount,
            AbnormalCount = result.AbnormalCount,
            TemplateCreatedCount = result.TemplateCreatedCount,
            TemplateUpdatedCount = result.TemplateUpdatedCount,
            ImportedAt = result.ImportedAt
        };
    }

    public void Update(Guid id, InspectionEntryViewModel entry)
    {
        _inspectionRecordService.Update(id, ToDraft(entry));
    }

    public void Close(Guid id, string account, string closureRemark)
    {
        _inspectionRecordService.Close(id, account, closureRemark);
    }

    public void Revoke(Guid id, string account, string revokeReason)
    {
        _inspectionRecordService.Revoke(id, account, revokeReason);
    }

    public void Delete(Guid id)
    {
        _inspectionRecordService.Delete(id);
    }

    public IReadOnlyList<InspectionTemplateViewModel> GetTemplates()
    {
        return _inspectionRecordService.GetTemplates()
            .Select(template => new InspectionTemplateViewModel
            {
                Id = template.Id,
                DisplayText = $"{template.LineName} / {template.DeviceName} / {template.InspectionItem}",
                LineName = template.LineName,
                DeviceName = template.DeviceName,
                InspectionItem = template.InspectionItem,
                DefaultInspector = template.DefaultInspector,
                DefaultRemark = template.DefaultRemark
            })
            .ToList();
    }

    public void SaveTemplate(InspectionTemplateViewModel template)
    {
        _inspectionRecordService.SaveTemplate(new InspectionTemplateDraft(
            template.LineName,
            template.DeviceName,
            template.InspectionItem,
            template.DefaultInspector,
            template.DefaultRemark,
            template.Id == Guid.Empty ? null : template.Id));
    }

    public void DeleteTemplate(Guid id)
    {
        _inspectionRecordService.DeleteTemplate(id);
    }

    public void Export(string filePath, InspectionFilterViewModel filter)
    {
        var result = _inspectionRecordService.Query(ToQuery(filter));
        _excelExporter.Export(filePath, result);
    }

    private static InspectionQuery ToQuery(InspectionFilterViewModel filter)
    {
        return new InspectionQuery(
            filter.Keyword,
            filter.LineName,
            filter.DeviceName,
            filter.Status,
            filter.StartTime,
            filter.EndTime,
            filter.IncludeRevoked,
            filter.PendingOnly);
    }

    private static InspectionRecordDraft ToDraft(InspectionEntryViewModel entry)
    {
        return new InspectionRecordDraft(
            entry.LineName,
            entry.DeviceName,
            entry.InspectionItem,
            entry.Inspector,
            entry.Status,
            entry.MeasuredValue,
            entry.CheckedAt,
            entry.Remark);
    }

    private static string BuildCollaborationSystemPrompt(AiCollaborationRole role)
    {
        var roleName = role switch
        {
            AiCollaborationRole.Equipment => "设备部 AI 协同助手",
            AiCollaborationRole.Production => "生产部 AI 协同助手",
            AiCollaborationRole.Quality => "质量部 AI 协同助手",
            AiCollaborationRole.Management => "管理层 AI 协同助手",
            _ => "制造业 AI 协同助手"
        };

        var focus = role switch
        {
            AiCollaborationRole.Equipment => "优先检修设备、疑似原因、是否停机复检、备件和维修排程。",
            AiCollaborationRole.Production => "产线节拍影响、生产安排、现场协调、异常对排产的影响。",
            AiCollaborationRole.Quality => "复检建议、质量风险、批次追踪、是否需要隔离或加严检查。",
            AiCollaborationRole.Management => "总体风险、责任部门、处理优先级、跨部门协同事项。",
            _ => "风险判断和处理建议。"
        };

        return $"你是{roleName}。请基于巡检数据给出正式、简短、可执行的部门建议，重点关注：{focus}不要写代码，不要解释你在分析什么，直接输出报告。";
    }

    private static string BuildCollaborationPrompt(
        AiCollaborationRole role,
        InspectionQueryResult result,
        RiskAnalysisResult fallbackAnalysis)
    {
        var activeRecords = result.Records
            .Where(record => !record.IsRevoked)
            .ToList();
        var pendingRecords = activeRecords
            .Where(record => record.Status != InspectionStatus.Normal && !record.ClosedAt.HasValue)
            .OrderByDescending(record => record.CheckedAt)
            .Take(12)
            .Select(record => new
            {
                record.CheckedAt,
                record.LineName,
                record.DeviceName,
                Status = record.Status.ToDisplayText(),
                record.Remark,
                record.ClosureRemark
            })
            .ToList();
        var lineRisks = activeRecords
            .Where(record => record.Status != InspectionStatus.Normal)
            .GroupBy(record => record.LineName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                LineName = group.First().LineName,
                WarningCount = group.Count(record => record.Status == InspectionStatus.Warning),
                AbnormalCount = group.Count(record => record.Status == InspectionStatus.Abnormal)
            })
            .OrderByDescending(item => item.AbnormalCount)
            .ThenByDescending(item => item.WarningCount)
            .Take(8)
            .ToList();
        var deviceRisks = activeRecords
            .Where(record => record.Status != InspectionStatus.Normal)
            .GroupBy(record => new { record.LineName, record.DeviceName })
            .Select(group => new
            {
                group.Key.LineName,
                group.Key.DeviceName,
                WarningCount = group.Count(record => record.Status == InspectionStatus.Warning),
                AbnormalCount = group.Count(record => record.Status == InspectionStatus.Abnormal),
                LatestRemark = group.OrderByDescending(record => record.CheckedAt).First().Remark
            })
            .OrderByDescending(item => item.AbnormalCount)
            .ThenByDescending(item => item.WarningCount)
            .Take(8)
            .ToList();

        var context = new
        {
            Role = role.ToString(),
            Summary = new
            {
                result.Summary.TotalCount,
                result.Summary.NormalCount,
                result.Summary.WarningCount,
                result.Summary.AbnormalCount,
                result.Summary.PassRate
            },
            LocalRiskJudgement = fallbackAnalysis,
            LineRisks = lineRisks,
            DeviceRisks = deviceRisks,
            PendingRecords = pendingRecords
        };

        return "请输出以下结构：一、部门判断；二、重点事项；三、建议动作；四、需要协同的部门。每段控制在 2 到 3 行，语言像正式生产现场建议。\n" +
               JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildClosureStateText(InspectionRecord record)
    {
        if (record.IsRevoked)
        {
            return "已撤回";
        }

        if (record.Status == InspectionStatus.Normal)
        {
            return "无需闭环";
        }

        return record.ClosedAt.HasValue
            ? $"已闭环 {record.ClosedAt:MM-dd HH:mm}"
            : "待闭环";
    }

    private static string BuildActionRemark(InspectionRecord record)
    {
        if (record.IsRevoked)
        {
            return record.RevokeReason ?? string.Empty;
        }

        return record.ClosureRemark ?? string.Empty;
    }

    private static string BuildImportedRemark(string remark, string batchKeyword, string sourceLabel)
    {
        var importTag = $"[导入批次:{batchKeyword}]";
        if (remark.Contains(importTag, StringComparison.OrdinalIgnoreCase))
        {
            return remark;
        }

        var sourceTag = $"[来源:{sourceLabel}]";
        return string.IsNullOrWhiteSpace(remark)
            ? $"{importTag}{sourceTag}"
            : $"{remark} {importTag}{sourceTag}";
    }
}
