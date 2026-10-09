using Artemis.Plugins.Devices.Nanoleaf.Helper;
using Xunit;

namespace Artemis.Plugins.Devices.Nanoleaf.Tests.Helper;

public class NanoleafDiscoveryHelperTests
{
    [Fact]
    public void TryParseSsdpDevice_ParsesShapesResponse()
    {
        const string response = "HTTP/1.1 200 OK\r\nCache-Control: max-age=60\r\nST: nanoleaf:nl42\r\n" +
                                "Location: http://192.168.1.20:16021\r\nnl-deviceid: 12:34\r\n\r\n";

        Assert.True(NanoleafDiscoveryHelper.TryParseSsdpDevice(response, out var device));
        Assert.Equal("192.168.1.20", device.address);
        Assert.Equal("NL42", device.model);
    }

    [Fact]
    public void TryParseSsdpDevice_MapsLightPanelsSearchTargetToModel()
    {
        const string response = "HTTP/1.1 200 OK\r\nst: nanoleaf_aurora:light\r\nlocation: http://10.0.0.5:16021\r\n\r\n";

        Assert.True(NanoleafDiscoveryHelper.TryParseSsdpDevice(response, out var device));
        Assert.Equal("10.0.0.5", device.address);
        Assert.Equal("NL22", device.model);
    }

    [Theory]
    [InlineData("HTTP/1.1 200 OK\r\nST: upnp:rootdevice\r\nLocation: http://10.0.0.1/desc.xml\r\n\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nST: nanoleaf\r\n\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nST: nanoleaf\r\nLocation: not a uri\r\n\r\n")]
    [InlineData("garbage")]
    [InlineData("")]
    public void TryParseSsdpDevice_RejectsUnrelatedOrMalformedResponses(string response)
    {
        Assert.False(NanoleafDiscoveryHelper.TryParseSsdpDevice(response, out _));
    }

    [Theory]
    [InlineData("nanoleaf:nl29", "NL29")]
    [InlineData("nanoleaf_aurora:light", "NL22")]
    [InlineData("nanoleaf", "")]
    [InlineData("nanoleaf:light", "")]
    public void GetModelFromSearchTarget_ReturnsModel(string searchTarget, string expected)
    {
        Assert.Equal(expected, NanoleafDiscoveryHelper.GetModelFromSearchTarget(searchTarget));
    }

    [Fact]
    public void BuildSearchRequest_UsesDiscoverMethod()
    {
        string request = NanoleafDiscoveryHelper.BuildSearchRequest("nanoleaf:nl42");

        Assert.StartsWith("M-SEARCH * HTTP/1.1\r\n", request);
        Assert.Contains("MAN: \"ssdp:discover\"\r\n", request);
        Assert.Contains("ST: nanoleaf:nl42\r\n", request);
        Assert.EndsWith("\r\n\r\n", request);
    }
}
