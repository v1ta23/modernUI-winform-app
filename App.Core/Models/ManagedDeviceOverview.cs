namespace App.Core.Models;

public sealed record ManagedDeviceOverview(
    int TotalCount,
    int ActiveCount,
    int MaintenanceCount,
    int StoppedCount,
    int CommunicationLinkedCount);
