namespace App.Core.Models;

public sealed record ManagedDeviceQueryResult(
    IReadOnlyList<ManagedDevice> Devices,
    IReadOnlyList<string> LineOptions,
    ManagedDeviceOverview Overview,
    DateTime GeneratedAt);
