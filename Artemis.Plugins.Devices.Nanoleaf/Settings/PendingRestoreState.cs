using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;

namespace Artemis.Plugins.Devices.Nanoleaf.Settings;

/// <summary>
/// The state a device had before Artemis took control of it. It is persisted while Artemis controls the device,
/// so the original state can still be restored after Artemis crashed or was killed.
/// </summary>
public class PendingRestoreState
{
    public string Hostname { get; set; } = "";
    public bool IsMatter { get; set; }
    public string? Effect { get; set; }
    public bool On { get; set; }
    public string ColorMode { get; set; } = "";
    public byte Brightness { get; set; }
    public int Ct { get; set; }
    public int Hue { get; set; }
    public int Sat { get; set; }

    /// <summary>
    /// Creates a snapshot of the current state of a device.
    /// </summary>
    public static PendingRestoreState FromInfo(string hostname, NanoleafInfo info, string? effect, bool isMatter) => new()
    {
        Hostname = hostname,
        IsMatter = isMatter,
        Effect = effect,
        On = info.State.On.Value,
        ColorMode = info.State.ColorMode,
        Brightness = info.State.Brightness.Value,
        Ct = info.State.Ct.Value,
        Hue = info.State.Hue.Value,
        Sat = info.State.Sat.Value
    };

    /// <summary>
    /// Converts the snapshot into a state that can be sent to the device.
    /// </summary>
    public NanoleafInfo.StateInfo ToStateInfo() => new()
    {
        On = new NanoleafInfo.On { Value = On },
        ColorMode = ColorMode,
        Brightness = new NanoleafInfo.Brightness { Value = Brightness },
        Ct = new NanoleafInfo.Ct { Value = Ct },
        Hue = new NanoleafInfo.Hue { Value = Hue },
        Sat = new NanoleafInfo.Sat { Value = Sat }
    };
}
