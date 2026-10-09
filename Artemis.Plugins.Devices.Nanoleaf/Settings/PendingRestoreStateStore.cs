using System;
using System.Collections.Generic;
using System.Threading;
using Artemis.Core;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

namespace Artemis.Plugins.Devices.Nanoleaf.Settings;

/// <summary>
/// Stores <see cref="PendingRestoreState"/> entries in the plugin settings.
/// </summary>
public class PendingRestoreStateStore(PluginSettings settings) : IPendingRestoreStateStore
{
    private readonly Lock _lock = new();

    private readonly PluginSetting<List<PendingRestoreState>> _setting =
        settings.GetSetting("PendingRestoreStates", new List<PendingRestoreState>());

    public PendingRestoreState? Get(string hostname)
    {
        lock (_lock)
            return (_setting.Value ?? []).Find(s => Matches(s, hostname));
    }

    public void Set(PendingRestoreState state)
    {
        lock (_lock)
        {
            List<PendingRestoreState> states = _setting.Value ?? [];
            states.RemoveAll(s => Matches(s, state.Hostname));
            states.Add(state);
            _setting.Value = states;
            _setting.Save();
        }
    }

    public void Remove(string hostname)
    {
        lock (_lock)
        {
            List<PendingRestoreState> states = _setting.Value ?? [];
            if (states.RemoveAll(s => Matches(s, hostname)) == 0)
                return;

            _setting.Value = states;
            _setting.Save();
        }
    }

    private static bool Matches(PendingRestoreState state, string hostname) =>
        string.Equals(state.Hostname, hostname, StringComparison.OrdinalIgnoreCase);
}
