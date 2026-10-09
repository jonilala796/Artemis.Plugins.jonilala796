using Artemis.Plugins.Devices.Nanoleaf.Settings;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

/// <summary>
/// Persists the state devices had before Artemis took control of them.
/// </summary>
public interface IPendingRestoreStateStore
{
    PendingRestoreState? Get(string hostname);
    void Set(PendingRestoreState state);
    void Remove(string hostname);
}
