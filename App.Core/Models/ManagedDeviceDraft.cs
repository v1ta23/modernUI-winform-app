namespace App.Core.Models;

public sealed record ManagedDeviceDraft(
    string LineName,
    string DeviceName,
    string DeviceCode,
    string Location,
    string Owner,
    string CommunicationAddress,
    ManagedDeviceStatus Status,
    string Remark,
    Guid? Id = null);
