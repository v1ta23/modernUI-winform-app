using App.Core.Models;

namespace App.Core.Interfaces;

public interface IManagedDeviceService
{
    ManagedDeviceQueryResult Query(ManagedDeviceQuery query);

    ManagedDevice Save(ManagedDeviceDraft draft);

    void Delete(Guid id);
}
