// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;

public class NanoleafInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("serialNo")]
    public string SerialNo { get; set; } = "";

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = "";

    [JsonPropertyName("firmwareVersion")]
    public string FirmwareVersion { get; set; } = "";

    [JsonPropertyName("hardwareVersion")]
    public string HardwareVersion { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("discovery")]
    public DiscoveryInfo? Discovery { get; set; }

    [JsonPropertyName("effects")]
    public EffectsInfo? Effects { get; set; }

    [JsonPropertyName("firmwareUpgrade")]
    public FirmwareUpgradeInfo? FirmwareUpgrade { get; set; }

    [JsonPropertyName("panelLayout")]
    public PanelLayoutInfo? PanelLayout { get; set; }

    [JsonPropertyName("state")]
    public StateInfo State { get; set; } = new();

    [JsonPropertyName("rhythm")]
    public RhythmInfo? Rhythm { get; set; }


    public class DiscoveryInfo
    {
    }

    public class RhythmInfo
    {
        [JsonPropertyName("rhythmConnected")]
        public bool RhythmConnected { get; set; }

        [JsonPropertyName("rhythmActive")]
        public bool RhythmActive { get; set; }

        [JsonPropertyName("rhythmId")]
        public int RhythmId { get; set; }

        [JsonPropertyName("rhythmPos")]
        public RhytmPos RhythmPos { get; set; } = new();

        [JsonPropertyName("rhythmMode")]
        public byte RhythmMode { get; set; }

        [JsonPropertyName("hardwareVersion")]
        public string HardwareVersion { get; set; } = "";

        [JsonPropertyName("firmwareVersion")]
        public string FirmwareVersion { get; set; } = "";

        [JsonPropertyName("auxAvailable")]
        public bool AuxAvailable { get; set; }
    }

    public class RhytmPos
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("o")]
        public int O { get; set; }
    }

    public class EffectsInfo
    {
        [JsonPropertyName("effectsList")]
        public List<string> EffectsList { get; set; } = [];

        [JsonPropertyName("select")]
        public string Select { get; set; } = "";
    }

    public class FirmwareUpgradeInfo
    {
    }

    public class PanelLayoutInfo
    {
        [JsonPropertyName("globalOrientation")]
        public GlobalOrientation GlobalOrientation { get; set; } = new();

        [JsonPropertyName("layout")]
        public Layout Layout { get; set; } = new();
    }

    public class GlobalOrientation
    {
        [JsonPropertyName("value")]
        public ushort Value { get; set; }

        [JsonPropertyName("max")]
        public ushort Max { get; set; }

        [JsonPropertyName("min")]
        public ushort Min { get; set; }
    }

    public class Layout
    {
        [JsonPropertyName("numPanels")]
        public int NumPanels { get; set; }

        [JsonPropertyName("sideLength")]
        public float SideLength { get; set; }

        [JsonPropertyName("positionData")]
        public List<PositionDataInfo> PositionData { get; set; } = [];
    }

    public class PositionDataInfo
    {
        [JsonPropertyName("panelId")]
        public ushort PanelId { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("o")]
        public int O { get; set; }

        [JsonPropertyName("shapeType")]
        public NanoleafShapeType ShapeType { get; set; }
    }

    public class StateInfo
    {
        [JsonPropertyName("brightness")]
        public Brightness Brightness { get; set; } = new();

        [JsonPropertyName("colorMode")]
        public string ColorMode { get; set; } = "";

        [JsonPropertyName("ct")]
        public Ct Ct { get; set; } = new();

        [JsonPropertyName("hue")]
        public Hue Hue { get; set; } = new();

        [JsonPropertyName("on")]
        public On On { get; set; } = new();

        [JsonPropertyName("sat")]
        public Sat Sat { get; set; } = new();
    }

    public class Brightness
    {
        [JsonPropertyName("value")]
        public byte Value { get; set; }

        [JsonPropertyName("max")]
        public byte Max { get; set; }

        [JsonPropertyName("min")]
        public byte Min { get; set; }
    }

    public class Ct
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("max")]
        public int Max { get; set; }

        [JsonPropertyName("min")]
        public int Min { get; set; }
    }

    public class Hue
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("max")]
        public int Max { get; set; }

        [JsonPropertyName("min")]
        public int Min { get; set; }
    }

    public class On
    {
        [JsonPropertyName("value")]
        public bool Value { get; set; }
    }

    public class Sat
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("max")]
        public int Max { get; set; }

        [JsonPropertyName("min")]
        public int Min { get; set; }
    }
}