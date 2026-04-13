namespace App.Core.Models;

public enum ManagedDeviceStatus
{
    Active,
    Maintenance,
    Stopped
}

public static class ManagedDeviceStatusExtensions
{
    public static string ToDisplayText(this ManagedDeviceStatus status)
    {
        return status switch
        {
            ManagedDeviceStatus.Maintenance => "维护中",
            ManagedDeviceStatus.Stopped => "已停用",
            _ => "运行中"
        };
    }
}
