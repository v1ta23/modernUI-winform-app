using App.Core.Interfaces;
using App.Core.Models;

namespace App.Core.Services;

public sealed class InspectionRecordService : IInspectionRecordService
{
    private readonly IInspectionRecordRepository _recordRepository;
    private readonly IInspectionTemplateRepository _templateRepository;

    public InspectionRecordService(
        IInspectionRecordRepository recordRepository,
        IInspectionTemplateRepository templateRepository)
    {
        _recordRepository = recordRepository;
        _templateRepository = templateRepository;
        EnsureSeedData();
        EnsureSeedTemplates();
    }

    public InspectionQueryResult Query(InspectionQuery query)
    {
        var allRecords = _recordRepository.GetAll();
        var templates = GetTemplates();
        var filteredRecords = ApplyQuery(allRecords, query)
            .OrderByDescending(record => record.CheckedAt)
            .ToList();

        var activeRecords = filteredRecords
            .Where(record => !record.IsRevoked)
            .ToList();

        var lineOptions = allRecords
            .Where(record => !record.IsRevoked)
            .Select(record => record.LineName)
            .Concat(templates.Select(template => template.LineName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new InspectionQueryResult(
            filteredRecords,
            lineOptions,
            templates,
            BuildSummary(activeRecords, query),
            DateTime.Now);
    }

    public InspectionRecord Add(InspectionRecordDraft draft)
    {
        var normalized = NormalizeDraft(draft);
        var record = new InspectionRecord(
            Guid.NewGuid(),
            normalized.LineName,
            normalized.DeviceName,
            normalized.InspectionItem,
            normalized.Inspector,
            normalized.Status,
            normalized.MeasuredValue,
            normalized.CheckedAt,
            normalized.Remark);

        var allRecords = _recordRepository.GetAll().ToList();
        allRecords.Add(record);
        _recordRepository.SaveAll(allRecords);
        return record;
    }

    public InspectionImportResult Import(IReadOnlyList<InspectionRecordDraft> drafts)
    {
        if (drafts.Count == 0)
        {
            throw new InvalidOperationException("没有可导入的巡检记录。");
        }

        var normalizedDrafts = drafts
            .Select(NormalizeDraft)
            .ToList();
        var importedAt = DateTime.Now;

        var importedRecords = normalizedDrafts
            .Select(draft => new InspectionRecord(
                Guid.NewGuid(),
                draft.LineName,
                draft.DeviceName,
                draft.InspectionItem,
                draft.Inspector,
                draft.Status,
                draft.MeasuredValue,
                draft.CheckedAt,
                draft.Remark))
            .ToList();

        var allRecords = _recordRepository.GetAll().ToList();
        allRecords.AddRange(importedRecords);
        _recordRepository.SaveAll(allRecords);

        var templates = _templateRepository.GetAll().ToList();
        var templateCreatedCount = 0;
        var templateUpdatedCount = 0;

        foreach (var draft in normalizedDrafts
                     .GroupBy(
                         item => $"{item.LineName}|{item.DeviceName}|{item.InspectionItem}",
                         StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.OrderByDescending(item => item.CheckedAt).First()))
        {
            var index = templates.FindIndex(template =>
                string.Equals(template.LineName, draft.LineName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(template.DeviceName, draft.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(template.InspectionItem, draft.InspectionItem, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                templates[index] = templates[index] with
                {
                    DefaultInspector = draft.Inspector,
                    DefaultRemark = draft.Remark
                };
                templateUpdatedCount++;
                continue;
            }

            templates.Add(new InspectionTemplate(
                Guid.NewGuid(),
                draft.LineName,
                draft.DeviceName,
                draft.InspectionItem,
                draft.Inspector,
                draft.Remark));
            templateCreatedCount++;
        }

        _templateRepository.SaveAll(templates);

        return new InspectionImportResult(
            importedRecords.Count,
            importedRecords.Count(record => record.Status == InspectionStatus.Normal),
            importedRecords.Count(record => record.Status == InspectionStatus.Warning),
            importedRecords.Count(record => record.Status == InspectionStatus.Abnormal),
            templateCreatedCount,
            templateUpdatedCount,
            importedAt);
    }

    public InspectionRecord Update(Guid id, InspectionRecordDraft draft)
    {
        var normalized = NormalizeDraft(draft);
        var allRecords = _recordRepository.GetAll().ToList();
        var index = allRecords.FindIndex(record => record.Id == id);
        if (index < 0)
        {
            throw new InvalidOperationException("未找到要编辑的点检记录。");
        }

        var existing = allRecords[index];
        if (existing.IsRevoked)
        {
            throw new InvalidOperationException("已撤回的记录不能再编辑。");
        }

        var updated = existing with
        {
            LineName = normalized.LineName,
            DeviceName = normalized.DeviceName,
            InspectionItem = normalized.InspectionItem,
            Inspector = normalized.Inspector,
            Status = normalized.Status,
            MeasuredValue = normalized.MeasuredValue,
            CheckedAt = normalized.CheckedAt,
            Remark = normalized.Remark,
            ClosedAt = normalized.Status == InspectionStatus.Normal ? null : existing.ClosedAt,
            ClosedBy = normalized.Status == InspectionStatus.Normal ? null : existing.ClosedBy,
            ClosureRemark = normalized.Status == InspectionStatus.Normal ? null : existing.ClosureRemark
        };

        allRecords[index] = updated;
        _recordRepository.SaveAll(allRecords);
        return updated;
    }

    public InspectionRecord Close(Guid id, string account, string closureRemark)
    {
        var actor = account.Trim();
        var remark = closureRemark.Trim();
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new InvalidOperationException("闭环人不能为空。");
        }

        if (string.IsNullOrWhiteSpace(remark))
        {
            throw new InvalidOperationException("请填写闭环说明。");
        }

        var allRecords = _recordRepository.GetAll().ToList();
        var index = allRecords.FindIndex(record => record.Id == id);
        if (index < 0)
        {
            throw new InvalidOperationException("未找到要闭环的点检记录。");
        }

        var existing = allRecords[index];
        if (existing.IsRevoked)
        {
            throw new InvalidOperationException("已撤回的记录不能闭环。");
        }

        if (existing.Status == InspectionStatus.Normal)
        {
            throw new InvalidOperationException("正常记录不需要闭环。");
        }

        if (existing.ClosedAt.HasValue)
        {
            throw new InvalidOperationException("这条记录已经闭环了。");
        }

        var updated = existing with
        {
            ClosedAt = DateTime.Now,
            ClosedBy = actor,
            ClosureRemark = remark
        };

        allRecords[index] = updated;
        _recordRepository.SaveAll(allRecords);
        return updated;
    }

    public InspectionRecord Revoke(Guid id, string account, string revokeReason)
    {
        var actor = account.Trim();
        var reason = revokeReason.Trim();
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new InvalidOperationException("撤回人不能为空。");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("请填写撤回原因。");
        }

        var allRecords = _recordRepository.GetAll().ToList();
        var index = allRecords.FindIndex(record => record.Id == id);
        if (index < 0)
        {
            throw new InvalidOperationException("未找到要撤回的点检记录。");
        }

        var existing = allRecords[index];
        if (existing.IsRevoked)
        {
            throw new InvalidOperationException("这条记录已经撤回了。");
        }

        var updated = existing with
        {
            IsRevoked = true,
            RevokedAt = DateTime.Now,
            RevokedBy = actor,
            RevokeReason = reason
        };

        allRecords[index] = updated;
        _recordRepository.SaveAll(allRecords);
        return updated;
    }

    public void Delete(Guid id)
    {
        var allRecords = _recordRepository.GetAll().ToList();
        var removed = allRecords.RemoveAll(record => record.Id == id);
        if (removed == 0)
        {
            throw new InvalidOperationException("未找到要删除的点检记录。");
        }

        _recordRepository.SaveAll(allRecords);
    }

    public IReadOnlyList<InspectionTemplate> GetTemplates()
    {
        return _templateRepository.GetAll()
            .OrderBy(template => template.LineName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.InspectionItem, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public InspectionTemplate SaveTemplate(InspectionTemplateDraft draft)
    {
        var normalized = NormalizeTemplateDraft(draft);
        var templates = _templateRepository.GetAll().ToList();
        var index = templates.FindIndex(template =>
            (normalized.Id.HasValue && template.Id == normalized.Id.Value) ||
            (string.Equals(template.LineName, normalized.LineName, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(template.DeviceName, normalized.DeviceName, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(template.InspectionItem, normalized.InspectionItem, StringComparison.OrdinalIgnoreCase)));

        var template = index >= 0
            ? templates[index] with
            {
                LineName = normalized.LineName,
                DeviceName = normalized.DeviceName,
                InspectionItem = normalized.InspectionItem,
                DefaultInspector = normalized.DefaultInspector,
                DefaultRemark = normalized.DefaultRemark
            }
            : new InspectionTemplate(
                Guid.NewGuid(),
                normalized.LineName,
                normalized.DeviceName,
                normalized.InspectionItem,
                normalized.DefaultInspector,
                normalized.DefaultRemark);

        if (index >= 0)
        {
            templates[index] = template;
        }
        else
        {
            templates.Add(template);
        }

        _templateRepository.SaveAll(templates);
        return template;
    }

    public void DeleteTemplate(Guid id)
    {
        var templates = _templateRepository.GetAll().ToList();
        var removed = templates.RemoveAll(template => template.Id == id);
        if (removed == 0)
        {
            throw new InvalidOperationException("未找到要删除的模板。");
        }

        _templateRepository.SaveAll(templates);
    }

    private void EnsureSeedData()
    {
        if (_recordRepository.GetAll().Count > 0)
        {
            return;
        }

        _recordRepository.SaveAll(CreateSeedRecords());
    }

    private void EnsureSeedTemplates()
    {
        if (_templateRepository.GetAll().Count > 0)
        {
            return;
        }

        _templateRepository.SaveAll(CreateTemplatesFromRecords(_recordRepository.GetAll()));
    }

    private static InspectionRecordDraft NormalizeDraft(InspectionRecordDraft draft)
    {
        var lineName = draft.LineName.Trim();
        var deviceName = draft.DeviceName.Trim();
        var inspectionItem = draft.InspectionItem.Trim();
        var inspector = draft.Inspector.Trim();

        if (string.IsNullOrWhiteSpace(lineName))
        {
            throw new InvalidOperationException("请输入产线名称。");
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new InvalidOperationException("请输入设备名称。");
        }

        if (string.IsNullOrWhiteSpace(inspectionItem))
        {
            throw new InvalidOperationException("请输入点检项目。");
        }

        if (string.IsNullOrWhiteSpace(inspector))
        {
            throw new InvalidOperationException("请输入点检人。");
        }

        return new InspectionRecordDraft(
            lineName,
            deviceName,
            inspectionItem,
            inspector,
            draft.Status,
            Math.Round(draft.MeasuredValue, 2),
            draft.CheckedAt,
            draft.Remark.Trim());
    }

    private static InspectionTemplateDraft NormalizeTemplateDraft(InspectionTemplateDraft draft)
    {
        var lineName = draft.LineName.Trim();
        var deviceName = draft.DeviceName.Trim();
        var inspectionItem = draft.InspectionItem.Trim();
        var inspector = draft.DefaultInspector.Trim();

        if (string.IsNullOrWhiteSpace(lineName))
        {
            throw new InvalidOperationException("模板产线不能为空。");
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new InvalidOperationException("模板设备不能为空。");
        }

        if (string.IsNullOrWhiteSpace(inspectionItem))
        {
            throw new InvalidOperationException("模板点检项目不能为空。");
        }

        if (string.IsNullOrWhiteSpace(inspector))
        {
            throw new InvalidOperationException("模板默认点检人不能为空。");
        }

        return new InspectionTemplateDraft(
            lineName,
            deviceName,
            inspectionItem,
            inspector,
            draft.DefaultRemark.Trim(),
            draft.Id);
    }

    private static IReadOnlyList<InspectionRecord> ApplyQuery(
        IReadOnlyList<InspectionRecord> records,
        InspectionQuery query)
    {
        var keyword = query.Keyword.Trim();
        var deviceName = query.DeviceName.Trim();
        IEnumerable<InspectionRecord> filtered = records;

        if (!query.IncludeRevoked)
        {
            filtered = filtered.Where(record => !record.IsRevoked);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(record =>
                record.LineName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.DeviceName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.InspectionItem.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.Inspector.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.Remark.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (record.ClosureRemark?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (record.RevokeReason?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(query.LineName))
        {
            filtered = filtered.Where(record => string.Equals(
                record.LineName,
                query.LineName,
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            filtered = filtered.Where(record => record.DeviceName.Contains(
                deviceName,
                StringComparison.OrdinalIgnoreCase));
        }

        if (query.Status.HasValue)
        {
            filtered = filtered.Where(record => record.Status == query.Status.Value);
        }

        if (query.PendingOnly)
        {
            filtered = filtered.Where(record =>
                record.Status != InspectionStatus.Normal &&
                !record.ClosedAt.HasValue);
        }

        if (query.StartTime.HasValue)
        {
            filtered = filtered.Where(record => record.CheckedAt >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            filtered = filtered.Where(record => record.CheckedAt <= query.EndTime.Value);
        }

        return filtered.ToList();
    }

    private static InspectionSummary BuildSummary(
        IReadOnlyList<InspectionRecord> records,
        InspectionQuery query)
    {
        var normalCount = records.Count(record => record.Status == InspectionStatus.Normal);
        var warningCount = records.Count(record => record.Status == InspectionStatus.Warning);
        var abnormalCount = records.Count(record => record.Status == InspectionStatus.Abnormal);
        var totalCount = records.Count;
        var passRate = totalCount == 0
            ? 0
            : Math.Round(normalCount * 100m / totalCount, 1);

        return new InspectionSummary(
            totalCount,
            normalCount,
            warningCount,
            abnormalCount,
            passRate,
            BuildTrendPoints(records, query));
    }

    private static IReadOnlyList<InspectionTrendPoint> BuildTrendPoints(
        IReadOnlyList<InspectionRecord> records,
        InspectionQuery query)
    {
        var hasExplicitTimeWindow = query.StartTime.HasValue || query.EndTime.HasValue;
        var latestRecordEnd = records.Count == 0
            ? DateTime.Now
            : records.Max(record => record.CheckedAt).AddMinutes(1);
        var end = query.EndTime?.AddMinutes(1) ?? (hasExplicitTimeWindow ? DateTime.Now : latestRecordEnd);
        var start = query.StartTime ?? end.AddHours(-7);
        var useDailyBuckets = (end - start).TotalDays > 2;

        if (useDailyBuckets)
        {
            var endDay = end.Date;
            return Enumerable.Range(0, 7)
                .Select(offset => endDay.AddDays(offset - 6))
                .Select(dayStart =>
                {
                    var dayEnd = dayStart.AddDays(1);
                    var dayRecords = records.Where(record =>
                            record.CheckedAt >= dayStart &&
                            record.CheckedAt < dayEnd)
                        .ToList();

                    return new InspectionTrendPoint(
                        dayStart.ToString("MM-dd"),
                        dayRecords.Count(record => record.Status == InspectionStatus.Normal),
                        dayRecords.Count(record => record.Status == InspectionStatus.Warning),
                        dayRecords.Count(record => record.Status == InspectionStatus.Abnormal));
                })
                .ToList();
        }

        var endHour = new DateTime(end.Year, end.Month, end.Day, end.Hour, 0, 0);
        return Enumerable.Range(0, 8)
            .Select(offset => endHour.AddHours(offset - 7))
            .Select(hourStart =>
            {
                var hourEnd = hourStart.AddHours(1);
                var hourRecords = records.Where(record =>
                        record.CheckedAt >= hourStart &&
                        record.CheckedAt < hourEnd)
                    .ToList();

                return new InspectionTrendPoint(
                    hourStart.ToString("HH:mm"),
                    hourRecords.Count(record => record.Status == InspectionStatus.Normal),
                    hourRecords.Count(record => record.Status == InspectionStatus.Warning),
                    hourRecords.Count(record => record.Status == InspectionStatus.Abnormal));
            })
            .ToList();
    }

    private static IReadOnlyList<InspectionRecord> CreateSeedRecords()
    {
        var now = DateTime.Now;
        return
        [
            new(Guid.NewGuid(), "一线", "冲压机-A01", "液压压力", "张磊", InspectionStatus.Normal, 78.5m, now.AddHours(-6), "参数稳定"),
            new(Guid.NewGuid(), "一线", "冲压机-A01", "油温", "张磊", InspectionStatus.Warning, 92.1m, now.AddHours(-5), "接近阈值"),
            new(Guid.NewGuid(), "二线", "装配机-B03", "夹具磨损", "李敏", InspectionStatus.Normal, 12.0m, now.AddHours(-4), "无异常"),
            new(Guid.NewGuid(), "二线", "装配机-B03", "振动值", "李敏", InspectionStatus.Abnormal, 14.6m, now.AddHours(-3), "需安排停机检查"),
            new(Guid.NewGuid(), "三线", "包装机-C02", "封口温度", "王娜", InspectionStatus.Normal, 186.3m, now.AddHours(-2), "波动正常"),
            new(Guid.NewGuid(), "三线", "包装机-C02", "传送速度", "王娜", InspectionStatus.Warning, 63.2m, now.AddHours(-1), "速度偏慢"),
            new(Guid.NewGuid(), "一线", "空压机-AUX", "排气压力", "陈博", InspectionStatus.Normal, 8.2m, now.AddMinutes(-35), "巡检通过"),
            new(Guid.NewGuid(), "二线", "焊接臂-B07", "焊缝质量", "周宁", InspectionStatus.Abnormal, 54.0m, now.AddMinutes(-10), "抽检不合格")
        ];
    }

    private static IReadOnlyList<InspectionTemplate> CreateTemplatesFromRecords(IReadOnlyList<InspectionRecord> records)
    {
        return records
            .GroupBy(
                record => $"{record.LineName}|{record.DeviceName}|{record.InspectionItem}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latest = group
                    .OrderByDescending(record => record.CheckedAt)
                    .First();

                return new InspectionTemplate(
                    Guid.NewGuid(),
                    latest.LineName,
                    latest.DeviceName,
                    latest.InspectionItem,
                    latest.Inspector,
                    latest.Remark);
            })
            .OrderBy(template => template.LineName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.InspectionItem, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
