using App.Core.Interfaces;
using App.Core.Models;
using WinFormsApp.Exports;
using WinFormsApp.ViewModels;
using System.IO;

namespace WinFormsApp.Controllers;

internal sealed class InspectionController
{
    private readonly IInspectionRecordService _inspectionRecordService;
    private readonly IRiskAnalysisService _riskAnalysisService;
    private readonly IAiRiskAnalysisService _aiRiskAnalysisService;
    private readonly InspectionExcelExporter _excelExporter;

    public InspectionController(
        IInspectionRecordService inspectionRecordService,
        IRiskAnalysisService riskAnalysisService,
        IAiRiskAnalysisService aiRiskAnalysisService,
        InspectionExcelExporter excelExporter)
    {
        _inspectionRecordService = inspectionRecordService;
        _riskAnalysisService = riskAnalysisService;
        _aiRiskAnalysisService = aiRiskAnalysisService;
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
