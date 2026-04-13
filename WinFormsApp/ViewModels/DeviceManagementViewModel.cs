using App.Core.Models;

namespace WinFormsApp.ViewModels;

internal sealed class DeviceManagementDashboardViewModel
{
    public IReadOnlyList<DeviceRowViewModel> Devices { get; init; } = Array.Empty<DeviceRowViewModel>();

    public IReadOnlyList<string> LineOptions { get; init; } = Array.Empty<string>();

    public int TotalCount { get; init; }

    public int ActiveCount { get; init; }

    public int MaintenanceCount { get; init; }

    public int StoppedCount { get; init; }

    public int CommunicationLinkedCount { get; init; }

    public DateTime GeneratedAt { get; init; }
}

internal sealed class DeviceRowViewModel
{
    public Guid Id { get; init; }

    public string DeviceCode { get; init; } = string.Empty;

    public string LineName { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string Owner { get; init; } = string.Empty;

    public string CommunicationAddress { get; init; } = string.Empty;

    public ManagedDeviceStatus Status { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public string UpdatedAtText { get; init; } = string.Empty;

    public string Remark { get; init; } = string.Empty;
}

internal sealed class DeviceEditorViewModel
{
    public Guid? Id { get; init; }

    public string DeviceCode { get; init; } = string.Empty;

    public string LineName { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string Owner { get; init; } = string.Empty;

    public string CommunicationAddress { get; init; } = string.Empty;

    public ManagedDeviceStatus Status { get; init; } = ManagedDeviceStatus.Active;

    public string Remark { get; init; } = string.Empty;
}

internal sealed class DeviceFilterViewModel
{
    public string Keyword { get; init; } = string.Empty;

    public string LineName { get; init; } = string.Empty;

    public ManagedDeviceStatus? Status { get; init; }
}
