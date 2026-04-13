using App.Core.Interfaces;
using App.Core.Models;

namespace App.Core.Services;

public sealed class ManagedDeviceService : IManagedDeviceService
{
    private readonly IManagedDeviceRepository _deviceRepository;
    private readonly IInspectionTemplateRepository _templateRepository;

    public ManagedDeviceService(
        IManagedDeviceRepository deviceRepository,
        IInspectionTemplateRepository templateRepository)
    {
        _deviceRepository = deviceRepository;
        _templateRepository = templateRepository;
        EnsureSeedDevices();
    }

    public ManagedDeviceQueryResult Query(ManagedDeviceQuery query)
    {
        var allDevices = _deviceRepository.GetAll();
        var filtered = ApplyQuery(allDevices, query)
            .OrderBy(device => device.LineName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lineOptions = allDevices
            .Select(device => device.LineName)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ManagedDeviceQueryResult(
            filtered,
            lineOptions,
            BuildOverview(allDevices),
            DateTime.Now);
    }

    public ManagedDevice Save(ManagedDeviceDraft draft)
    {
        var normalized = NormalizeDraft(draft);
        var allDevices = _deviceRepository.GetAll().ToList();
        var index = allDevices.FindIndex(device => normalized.Id.HasValue && device.Id == normalized.Id.Value);

        if (HasDuplicate(allDevices, normalized, index >= 0 ? allDevices[index].Id : null))
        {
            throw new InvalidOperationException("同一产线下已经有这个设备，别重复建啦。");
        }

        var now = DateTime.Now;
        var device = index >= 0
            ? allDevices[index] with
            {
                LineName = normalized.LineName,
                DeviceName = normalized.DeviceName,
                DeviceCode = normalized.DeviceCode,
                Location = normalized.Location,
                Owner = normalized.Owner,
                CommunicationAddress = normalized.CommunicationAddress,
                Status = normalized.Status,
                UpdatedAt = now,
                Remark = normalized.Remark
            }
            : new ManagedDevice(
                Guid.NewGuid(),
                normalized.LineName,
                normalized.DeviceName,
                normalized.DeviceCode,
                normalized.Location,
                normalized.Owner,
                normalized.CommunicationAddress,
                normalized.Status,
                now,
                normalized.Remark);

        if (index >= 0)
        {
            allDevices[index] = device;
        }
        else
        {
            allDevices.Add(device);
        }

        _deviceRepository.SaveAll(allDevices);
        return device;
    }

    public void Delete(Guid id)
    {
        var devices = _deviceRepository.GetAll().ToList();
        var removed = devices.RemoveAll(device => device.Id == id);
        if (removed == 0)
        {
            throw new InvalidOperationException("没找到要删除的设备。");
        }

        _deviceRepository.SaveAll(devices);
    }

    private void EnsureSeedDevices()
    {
        if (_deviceRepository.GetAll().Count > 0)
        {
            return;
        }

        var devices = CreateSeedDevicesFromTemplates(_templateRepository.GetAll());
        _deviceRepository.SaveAll(devices.Count > 0 ? devices : CreateFallbackDevices());
    }

    private static IReadOnlyList<ManagedDevice> ApplyQuery(
        IReadOnlyList<ManagedDevice> devices,
        ManagedDeviceQuery query)
    {
        IEnumerable<ManagedDevice> filtered = devices;
        var keyword = query.Keyword.Trim();
        var lineName = query.LineName.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(device =>
                device.DeviceName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                device.DeviceCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                device.LineName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                device.Location.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                device.Owner.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                device.CommunicationAddress.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                device.Remark.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(lineName))
        {
            filtered = filtered.Where(device => string.Equals(
                device.LineName,
                lineName,
                StringComparison.OrdinalIgnoreCase));
        }

        if (query.Status.HasValue)
        {
            filtered = filtered.Where(device => device.Status == query.Status.Value);
        }

        return filtered.ToList();
    }

    private static ManagedDeviceOverview BuildOverview(IReadOnlyList<ManagedDevice> devices)
    {
        return new ManagedDeviceOverview(
            devices.Count,
            devices.Count(device => device.Status == ManagedDeviceStatus.Active),
            devices.Count(device => device.Status == ManagedDeviceStatus.Maintenance),
            devices.Count(device => device.Status == ManagedDeviceStatus.Stopped),
            devices.Count(device => !string.IsNullOrWhiteSpace(device.CommunicationAddress)));
    }

    private static ManagedDeviceDraft NormalizeDraft(ManagedDeviceDraft draft)
    {
        var lineName = draft.LineName.Trim();
        var deviceName = draft.DeviceName.Trim();
        var deviceCode = draft.DeviceCode.Trim();

        if (string.IsNullOrWhiteSpace(lineName))
        {
            throw new InvalidOperationException("请填写产线。");
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new InvalidOperationException("请填写设备名称。");
        }

        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            deviceCode = BuildDeviceCode(lineName, deviceName);
        }

        return new ManagedDeviceDraft(
            lineName,
            deviceName,
            deviceCode,
            draft.Location.Trim(),
            draft.Owner.Trim(),
            draft.CommunicationAddress.Trim(),
            draft.Status,
            draft.Remark.Trim(),
            draft.Id);
    }

    private static bool HasDuplicate(
        IReadOnlyList<ManagedDevice> devices,
        ManagedDeviceDraft draft,
        Guid? currentId)
    {
        return devices.Any(device =>
            (!currentId.HasValue || device.Id != currentId.Value) &&
            (string.Equals(device.DeviceCode, draft.DeviceCode, StringComparison.OrdinalIgnoreCase) ||
             (string.Equals(device.LineName, draft.LineName, StringComparison.OrdinalIgnoreCase) &&
              string.Equals(device.DeviceName, draft.DeviceName, StringComparison.OrdinalIgnoreCase))));
    }

    private static IReadOnlyList<ManagedDevice> CreateSeedDevicesFromTemplates(IReadOnlyList<InspectionTemplate> templates)
    {
        var now = DateTime.Now;
        return templates
            .GroupBy(template => $"{template.LineName}|{template.DeviceName}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return new ManagedDevice(
                    Guid.NewGuid(),
                    first.LineName,
                    first.DeviceName,
                    BuildDeviceCode(first.LineName, first.DeviceName),
                    first.LineName,
                    first.DefaultInspector,
                    string.Empty,
                    ManagedDeviceStatus.Active,
                    now,
                    $"从巡检模板自动生成，关联 {group.Count()} 个巡检项");
            })
            .OrderBy(device => device.LineName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ManagedDevice> CreateFallbackDevices()
    {
        var now = DateTime.Now;
        return
        [
            new ManagedDevice(Guid.NewGuid(), "一线", "冲压机 A01", "DEV-A01", "一线前段", "张磊", "tcp://192.168.10.21:9001", ManagedDeviceStatus.Active, now, "核心设备，优先接通信"),
            new ManagedDevice(Guid.NewGuid(), "二线", "装配机 B03", "DEV-B03", "二线装配区", "李敏", string.Empty, ManagedDeviceStatus.Maintenance, now, "先补维护计划"),
            new ManagedDevice(Guid.NewGuid(), "三线", "包装机 C02", "DEV-C02", "三线包装区", "王婷", "tcp://192.168.10.33:9001", ManagedDeviceStatus.Active, now, "已预留网关地址")
        ];
    }

    private static string BuildDeviceCode(string lineName, string deviceName)
    {
        var raw = $"{lineName}-{deviceName}";
        var safe = new string(raw
            .Where(character => char.IsLetterOrDigit(character))
            .Take(10)
            .ToArray());
        return string.IsNullOrWhiteSpace(safe)
            ? $"DEV-{Guid.NewGuid():N}"[..12]
            : $"DEV-{safe.ToUpperInvariant()}";
    }
}
