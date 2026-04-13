using App.Core.Interfaces;
using App.Core.Models;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Controllers;

internal sealed class DeviceManagementController
{
    private readonly IManagedDeviceService _deviceService;

    public DeviceManagementController(IManagedDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    public DeviceManagementDashboardViewModel Load(DeviceFilterViewModel filter)
    {
        var result = _deviceService.Query(new ManagedDeviceQuery(
            filter.Keyword,
            filter.LineName,
            filter.Status));

        return new DeviceManagementDashboardViewModel
        {
            Devices = result.Devices.Select(ToRow).ToList(),
            LineOptions = result.LineOptions,
            TotalCount = result.Overview.TotalCount,
            ActiveCount = result.Overview.ActiveCount,
            MaintenanceCount = result.Overview.MaintenanceCount,
            StoppedCount = result.Overview.StoppedCount,
            CommunicationLinkedCount = result.Overview.CommunicationLinkedCount,
            GeneratedAt = result.GeneratedAt
        };
    }

    public DeviceRowViewModel Save(DeviceEditorViewModel editor)
    {
        var device = _deviceService.Save(new ManagedDeviceDraft(
            editor.LineName,
            editor.DeviceName,
            editor.DeviceCode,
            editor.Location,
            editor.Owner,
            editor.CommunicationAddress,
            editor.Status,
            editor.Remark,
            editor.Id));

        return ToRow(device);
    }

    public void Delete(Guid id)
    {
        _deviceService.Delete(id);
    }

    private static DeviceRowViewModel ToRow(ManagedDevice device)
    {
        return new DeviceRowViewModel
        {
            Id = device.Id,
            DeviceCode = device.DeviceCode,
            LineName = device.LineName,
            DeviceName = device.DeviceName,
            Location = device.Location,
            Owner = device.Owner,
            CommunicationAddress = device.CommunicationAddress,
            Status = device.Status,
            StatusText = device.Status.ToDisplayText(),
            UpdatedAtText = device.UpdatedAt.ToString("MM-dd HH:mm"),
            Remark = device.Remark
        };
    }
}
