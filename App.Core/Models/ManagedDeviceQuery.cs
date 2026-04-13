namespace App.Core.Models;

public sealed record ManagedDeviceQuery(
    string Keyword,
    string LineName,
    ManagedDeviceStatus? Status);
