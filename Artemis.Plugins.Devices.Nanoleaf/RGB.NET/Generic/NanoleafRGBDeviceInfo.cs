using System.Collections.Generic;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using RGB.NET.Core;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

// ReSharper disable once InconsistentNaming
public class NanoleafRGBDeviceInfo : IRGBDeviceInfo
{
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

    public Dictionary<LedId, int> LedIdToIndex = new();

    public NanoleafRGBDeviceInfo(NanoleafInfo info)
    {
        Info = info;
        DeviceName = info.Name;
        Manufacturer = "Nanoleaf";
        Model = info.Model;

        var positionData = info.PanelLayout?.Layout.PositionData;
        IsMatterEssentials = NanoleafAPI.IsMatterEssentialsDevice(info.Model)
                             || positionData is null or { Count: 0 };

        if (IsMatterEssentials)
        {
            DeviceType = RGBDeviceType.LedStripe;
        }
        else
        {
            DeviceType = positionData![0].ShapeType == NanoleafShapeType.Lightstrip4D
                ? RGBDeviceType.LedStripe
                : RGBDeviceType.Unknown;
        }
    }
}