using Buff_App.Models;

namespace Buff_App.Services;

public interface INetworkAdapterService
{
    Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default);
}
