using System.Collections.Generic;
using System.Linq;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Helper;
using RGB.NET.Core;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

// ReSharper disable once InconsistentNaming
public class NanoleafRGBDeviceInfo : IRGBDeviceInfo
{
    /// <summary>
    /// Model number of the original Light Panels (Aurora), the only device family using ext-control v1.
    /// </summary>
    private const string LIGHT_PANELS_MODEL = "NL22";

    /// <summary>
    /// Gets the type of the RGB device.
    /// </summary>
    public RGBDeviceType DeviceType { get; }

    public string DeviceName { get; }
    public string Manufacturer { get; }
    public string Model { get; }
    public object? LayoutMetadata { get; set; }
    public NanoleafInfo Info { get; }

    /// <summary>
    /// Gets whether this device is a Matter WiFi Essentials device (LED-indexed, no panel layout).
    /// </summary>
    public bool IsMatterEssentials { get; }

    /// <summary>
    /// Gets the external control protocol version used to stream to this device.
    /// </summary>
    public ExtControlVersion ExtControlVersion { get; }

    /// <summary>
    /// Gets the panels that have a light and are therefore created as LEDs and streamed to.
    /// Controllers, the Rhythm module and unknown shape types are excluded.
    /// </summary>
    public IReadOnlyList<NanoleafInfo.PositionDataInfo> StreamablePanels { get; }

    /// <summary>
    /// Gets the mapping of LED id to the panel id (panel devices) or LED index (Matter devices) used in the stream.
    /// </summary>
    public Dictionary<LedId, int> LedIdToIndex { get; } = new();

    public NanoleafRGBDeviceInfo(NanoleafInfo info)
    {
        Info = info;
        DeviceName = info.Name;
        Manufacturer = "Nanoleaf";
        Model = info.Model;

        List<NanoleafInfo.PositionDataInfo>? positionData = info.PanelLayout?.Layout.PositionData;
        IsMatterEssentials = IsMatterDevice(info);

        if (IsMatterEssentials)
        {
            DeviceType = RGBDeviceType.LedStripe;
            ExtControlVersion = ExtControlVersion.v2;
            StreamablePanels = [];
        }
        else
        {
            StreamablePanels = positionData!.Where(p => p.ShapeType.GetSideLength() > 0).ToList();
            ExtControlVersion = GetExtControlVersion(info);
            DeviceType = positionData![0].ShapeType == NanoleafShapeType.Lightstrip4D
                ? RGBDeviceType.LedStripe
                : RGBDeviceType.Unknown;
        }
    }

    /// <summary>
    /// Determines whether the device described by <paramref name="info"/> is a Matter WiFi Essentials device:
    /// either a known model, or a device that reports no panel layout.
    /// </summary>
    public static bool IsMatterDevice(NanoleafInfo info) =>
        NanoleafAPI.IsMatterEssentialsDevice(info.Model)
        || info.PanelLayout?.Layout.PositionData is null or { Count: 0 };

    /// <summary>
    /// Determines the external control version of a panel device. The first panel with a known version wins,
    /// because controllers and unknown shape types carry no version. If no panel is known, the version is
    /// derived from the model.
    /// </summary>
    public static ExtControlVersion GetExtControlVersion(NanoleafInfo info)
    {
        List<NanoleafInfo.PositionDataInfo> positionData = info.PanelLayout?.Layout.PositionData ?? [];
        foreach (NanoleafInfo.PositionDataInfo position in positionData)
        {
            ExtControlVersion? version = position.ShapeType.GetExtControlVersion();
            if (version != null)
                return version.Value;
        }

        return string.Equals(info.Model, LIGHT_PANELS_MODEL, System.StringComparison.OrdinalIgnoreCase)
            ? ExtControlVersion.v1
            : ExtControlVersion.v2;
    }
}
