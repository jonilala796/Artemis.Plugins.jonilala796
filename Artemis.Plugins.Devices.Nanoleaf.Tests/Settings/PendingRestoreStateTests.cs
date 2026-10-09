using System.Text.Json;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.Settings;
using Xunit;

namespace Artemis.Plugins.Devices.Nanoleaf.Tests.Settings;

public class PendingRestoreStateTests
{
    [Fact]
    public void RoundTripsDeviceState()
    {
        var info = new NanoleafInfo
        {
            State = new NanoleafInfo.StateInfo
            {
                On = new NanoleafInfo.On { Value = true },
                ColorMode = "ct",
                Brightness = new NanoleafInfo.Brightness { Value = 40 },
                Ct = new NanoleafInfo.Ct { Value = 2700 },
                Hue = new NanoleafInfo.Hue { Value = 120 },
                Sat = new NanoleafInfo.Sat { Value = 50 }
            }
        };

        var state = PendingRestoreState.FromInfo("10.0.0.5", info, "Northern Lights", false);
        NanoleafInfo.StateInfo restored = state.ToStateInfo();

        Assert.Equal("10.0.0.5", state.Hostname);
        Assert.Equal("Northern Lights", state.Effect);
        Assert.True(restored.On.Value);
        Assert.Equal("ct", restored.ColorMode);
        Assert.Equal(40, restored.Brightness.Value);
        Assert.Equal(2700, restored.Ct.Value);
        Assert.Equal(120, restored.Hue.Value);
        Assert.Equal(50, restored.Sat.Value);
    }

    [Theory]
    [InlineData("ct", "{\"brightness\":{\"value\":40},\"ct\":{\"value\":2700},\"on\":{\"value\":true}}")]
    [InlineData("hs", "{\"brightness\":{\"value\":40},\"hue\":{\"value\":120},\"sat\":{\"value\":50},\"on\":{\"value\":true}}")]
    public void StateBody_OnlyContainsParametersOfColorMode(string colorMode, string expected)
    {
        var state = new PendingRestoreState
        {
            On = true, ColorMode = colorMode, Brightness = 40, Ct = 2700, Hue = 120, Sat = 50
        };

        Assert.Equal(expected, JsonSerializer.Serialize(NanoleafAPI.BuildStateBody(state.ToStateInfo())));
    }
}
