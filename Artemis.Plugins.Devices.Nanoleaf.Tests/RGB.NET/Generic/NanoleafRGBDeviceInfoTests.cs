using System.Collections.Generic;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;
using RGB.NET.Core;
using Xunit;

namespace Artemis.Plugins.Devices.Nanoleaf.Tests.RGB.NET.Generic;

public class NanoleafRGBDeviceInfoTests
{
    private static NanoleafInfo CreateInfo(string model, params NanoleafShapeType[] shapes)
    {
        List<NanoleafInfo.PositionDataInfo> positions = [];
        for (int i = 0; i < shapes.Length; i++)
            positions.Add(new NanoleafInfo.PositionDataInfo { PanelId = (ushort)(i + 1), ShapeType = shapes[i] });

        return new NanoleafInfo
        {
            Name = "Test",
            Model = model,
            PanelLayout = new NanoleafInfo.PanelLayoutInfo
            {
                Layout = new NanoleafInfo.Layout { NumPanels = shapes.Length, PositionData = positions }
            }
        };
    }

    [Fact]
    public void ControllerFirst_UsesVersionOfFirstKnownPanel()
    {
        var info = new NanoleafRGBDeviceInfo(CreateInfo("NL42",
            NanoleafShapeType.ShapesController, NanoleafShapeType.ShapesHexagon, NanoleafShapeType.ShapesHexagon));

        Assert.False(info.IsMatterEssentials);
        Assert.Equal(ExtControlVersion.v2, info.ExtControlVersion);
    }

    [Fact]
    public void StreamablePanels_ExcludesPanelsWithoutLight()
    {
        var info = new NanoleafRGBDeviceInfo(CreateInfo("NL22",
            NanoleafShapeType.Rhythm, NanoleafShapeType.Triangle, NanoleafShapeType.Triangle));

        Assert.Equal(2, info.StreamablePanels.Count);
        Assert.Equal(ExtControlVersion.v1, info.ExtControlVersion);
    }

    [Theory]
    [InlineData("NL22", ExtControlVersion.v1)]
    [InlineData("NL99", ExtControlVersion.v2)]
    public void UnknownShapes_DeriveVersionFromModel(string model, ExtControlVersion expected)
    {
        Assert.Equal(expected, NanoleafRGBDeviceInfo.GetExtControlVersion(CreateInfo(model, (NanoleafShapeType)99)));
    }

    [Fact]
    public void KnownMatterModel_IsMatter()
    {
        var info = new NanoleafRGBDeviceInfo(CreateInfo("NL72K1"));

        Assert.True(info.IsMatterEssentials);
        Assert.Equal(RGBDeviceType.LedStripe, info.DeviceType);
        Assert.Equal(ExtControlVersion.v2, info.ExtControlVersion);
    }

    [Fact]
    public void DeviceWithoutLayout_IsMatter()
    {
        Assert.True(NanoleafRGBDeviceInfo.IsMatterDevice(new NanoleafInfo { Model = "NL99K9" }));
    }
}
