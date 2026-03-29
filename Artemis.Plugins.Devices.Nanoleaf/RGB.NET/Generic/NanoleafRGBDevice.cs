using System.Collections.Generic;
using System.Linq;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Helper;
using RGB.NET.Core;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

public sealed class NanoleafRGBDevice : AbstractRGBDevice<NanoleafRGBDeviceInfo>
{
    internal NanoleafRGBDevice(NanoleafRGBDeviceInfo deviceInfo, string address, ushort port,
        IDeviceUpdateTrigger updateTrigger)
        : base(deviceInfo,
            new NanoleafDeviceUpdateQueue(updateTrigger, address, port, deviceInfo.Info.PanelLayout.Layout.NumPanels,
                GetExtControlVersion(deviceInfo),
                deviceInfo.LedIdToIndex))
    {
        InitializeLayout();
    }

    private static ExtControlVersion? GetExtControlVersion(NanoleafRGBDeviceInfo deviceInfo)
    {
        var positionData = deviceInfo.Info.PanelLayout.Layout.PositionData;
        return positionData.Count > 0 ? positionData[0].ShapeType.GetExtControlVersion() : null;
    }

    private void InitializeLayout()
    {
        List<NanoleafInfo.PositionDataInfo> positionData = DeviceInfo.Info.PanelLayout.Layout.PositionData;
        if (positionData.Count == 0)
            return;

        int maxY = positionData.Max(p => p.Y);
        int i = 0;
        foreach (var position in positionData)
        {
            if (position.ShapeType.GetSideLength() != null && position.ShapeType.GetSideLength() > 0)
            {
                var ledId = position.ShapeType == NanoleafShapeType.Lightstrip4D
                    ? LedId.LedStripe1 + i++
                    : LedId.Custom1 + i++;
                DeviceInfo.LedIdToIndex.Add(ledId, position.PanelId);
                float sideLength = position.ShapeType.GetSideLength() ?? 0;
                var led = AddLed(ledId, new Point(position.X, maxY - position.Y), new Size(sideLength, sideLength));
                if (led != null)
                {
                    led.Shape = position.ShapeType.GetShape() ?? Shape.Rectangle;
                    led.Rotation = Rotation.FromDegrees(position.O);
                }
            }
        }

        Rotation = Rotation.FromDegrees(DeviceInfo.Info.PanelLayout.GlobalOrientation.Value);
    }
}