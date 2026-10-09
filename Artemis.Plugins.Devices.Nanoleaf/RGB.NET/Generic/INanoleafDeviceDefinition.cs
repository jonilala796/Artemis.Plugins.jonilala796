namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

/// <summary>
/// Represents the data used to create a Nanoleaf-device.
/// </summary>
public interface INanoleafDeviceDefinition
{
    string Address { get; }
    string AuthToken { get; }
    byte Brightness { get; }

    /// <summary>
    /// Fade time between colors in steps of 100 ms. 0 changes colors instantly.
    /// </summary>
    byte TransitionTime { get; }

    /// <summary>
    /// Maximum number of updates per second sent to the device. 0 means unlimited.
    /// </summary>
    int MaxUpdateRate { get; }
}