using Buff_App.Models;

namespace Buff_App.Services;

public interface INetworkPriorityService
{
    Task SetPrimaryAsync(NetworkAdapterInfo selectedAdapter, IReadOnlyList<NetworkAdapterInfo> adapters, CancellationToken cancellationToken = default);
}
