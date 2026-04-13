namespace App.Core.Models;

public sealed record ManagedDevice(
    Guid Id,
    string LineName,
    string DeviceName,
    string DeviceCode,
    string Location,
    string Owner,
    string CommunicationAddress,
    ManagedDeviceStatus Status,
    DateTime UpdatedAt,
    string Remark);
