using System;
using System.Collections.Generic;
using System.Linq;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Helper;
using RGB.NET.Core;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

public sealed class NanoleafRGBDevice : AbstractRGBDevice<NanoleafRGBDeviceInfo>
{
    /// <summary>
    /// Creates a panel-based Nanoleaf device (Shapes, Canvas, Elements, Lines, Skylight, etc.).
    /// </summary>
    internal NanoleafRGBDevice(NanoleafRGBDeviceInfo deviceInfo, string address, ushort port,
        IDeviceUpdateTrigger updateTrigger, Action<Exception> onError)
        : base(deviceInfo,
            new NanoleafDeviceUpdateQueue(updateTrigger, address, port,
                deviceInfo.StreamablePanels.Count,
                deviceInfo.ExtControlVersion,
                deviceInfo.LedIdToIndex,
                onError))
    {
        InitializeLayout();
    }

    /// <summary>
    /// Creates a Matter WiFi Essentials device with LED-indexed addressing.
    /// </summary>
    internal NanoleafRGBDevice(NanoleafRGBDeviceInfo deviceInfo, string address, ushort port,
        int ledCount, IDeviceUpdateTrigger updateTrigger, Action<Exception> onError)
        : base(deviceInfo,
            new NanoleafDeviceUpdateQueue(updateTrigger, address, port, ledCount,
                ExtControlVersion.v2,
                deviceInfo.LedIdToIndex,
                onError))
    {
        InitializeMatterLayout(ledCount);
    }

    private void InitializeLayout()
    {
        IReadOnlyList<NanoleafInfo.PositionDataInfo> panels = DeviceInfo.StreamablePanels;
        if (panels.Count == 0)
            return;

        // Nanoleaf positions are panel centroids with the Y axis pointing up, while RGB.NET expects the
        // top-left corner with the Y axis pointing down. Convert and shift so the layout starts at 0,0.
        int maxY = panels.Max(p => p.Y);
        float minLeft = panels.Min(p => p.X - SideLength(p) / 2);
        float minTop = panels.Min(p => maxY - p.Y - SideLength(p) / 2);

        int i = 0;
        foreach (NanoleafInfo.PositionDataInfo position in panels)
        {
            var ledId = position.ShapeType == NanoleafShapeType.Lightstrip4D
                ? LedId.LedStripe1 + i++
                : LedId.Custom1 + i++;
            DeviceInfo.LedIdToIndex.Add(ledId, position.PanelId);

            float sideLength = SideLength(position);
            var location = new Point(position.X - sideLength / 2 - minLeft, maxY - position.Y - sideLength / 2 - minTop);
            var led = AddLed(ledId, location, new Size(sideLength, sideLength));
            if (led != null)
            {
                led.Shape = position.ShapeType.GetShape() ?? Shape.Rectangle;
                led.Rotation = Rotation.FromDegrees(position.O);
            }
        }

        Rotation = Rotation.FromDegrees(DeviceInfo.Info.PanelLayout?.GlobalOrientation.Value ?? 0);
    }

    private static float SideLength(NanoleafInfo.PositionDataInfo position) => position.ShapeType.GetSideLength() ?? 0;

    /// <summary>
    /// Initializes a linear LED layout for Matter WiFi Essentials devices.
    /// LEDs are addressed sequentially by index (0 to N-1).
    /// </summary>
    private void InitializeMatterLayout(int ledCount)
    {
        const float ledSize = 10f;

        for (int i = 0; i < ledCount; i++)
        {
            var ledId = LedId.LedStripe1 + i;
            DeviceInfo.LedIdToIndex.Add(ledId, i);
            AddLed(ledId, new Point(i * ledSize, 0), new Size(ledSize, ledSize));
        }
    }
}
