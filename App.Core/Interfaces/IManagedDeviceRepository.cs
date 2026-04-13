using App.Core.Models;

namespace App.Core.Interfaces;

public interface IManagedDeviceRepository
{
    IReadOnlyList<ManagedDevice> GetAll();

    void SaveAll(IReadOnlyList<ManagedDevice> devices);
}
