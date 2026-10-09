using System;
using System.Collections.Generic;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;
using RGB.NET.Core;
using Xunit;

namespace Artemis.Plugins.Devices.Nanoleaf.Tests.RGB.NET.Generic;

public class NanoleafDeviceUpdateQueueTests
{
    [Theory]
    [InlineData(ExtControlVersion.v1, 3, 22)]
    [InlineData(ExtControlVersion.v2, 3, 26)]
    public void GetPacketLength_MatchesProtocol(ExtControlVersion version, int ledCount, int expected)
    {
        Assert.Equal(expected, NanoleafDeviceUpdateQueue.GetPacketLength(version, ledCount));
    }

    [Fact]
    public void WriteEntries_V2_WritesPanelIdAndColorIntoLedSlot()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.Custom1] = 0x1234, [LedId.Custom2] = 0x0042 };
        byte[] buffer = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v2, 2)];
        NanoleafDeviceUpdateQueue.WriteHeader(buffer, ExtControlVersion.v2, 2);

        (object key, Color color)[] data = [(LedId.Custom2, new Color(255, 10, 20, 30))];
        NanoleafDeviceUpdateQueue.WriteEntries(buffer, ExtControlVersion.v2, data, mapping);

        Assert.Equal(new byte[] { 0, 2 }, buffer[..2]);
        // Custom1's slot is untouched
        Assert.Equal(new byte[8], buffer[2..10]);
        Assert.Equal(new byte[] { 0x00, 0x42, 10, 20, 30, 0, 0, 0 }, buffer[10..18]);
    }

    [Fact]
    public void WriteEntries_V1_WritesSingleFrameEntry()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.Custom1] = 7 };
        byte[] buffer = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v1, 1)];
        NanoleafDeviceUpdateQueue.WriteHeader(buffer, ExtControlVersion.v1, 1);

        (object key, Color color)[] data = [(LedId.Custom1, new Color(255, 1, 2, 3))];
        NanoleafDeviceUpdateQueue.WriteEntries(buffer, ExtControlVersion.v1, data, mapping);

        Assert.Equal(new byte[] { 1, 7, 1, 1, 2, 3, 0, 0 }, buffer);
    }

    [Fact]
    public void WriteEntries_IgnoresUnknownAndOutOfRangeLeds()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.Custom1] = 1, [LedId.Custom5] = 5 };
        byte[] buffer = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v2, 1)];

        (object key, Color color)[] data =
        [
            (LedId.Custom3, new Color(255, 1, 1, 1)), // not mapped
            (LedId.Custom5, new Color(255, 1, 1, 1)), // mapped but outside the packet
            ("not a led id", new Color(255, 1, 1, 1))
        ];

        Exception? exception = Record.Exception(() =>
            NanoleafDeviceUpdateQueue.WriteEntries(buffer, ExtControlVersion.v2, data, mapping));

        Assert.Null(exception);
        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    [Fact]
    public void WriteEntries_MatterStripUsesLedIndex()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.LedStripe1] = 0, [LedId.LedStripe1 + 299] = 299 };
        byte[] buffer = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v2, 300)];
        NanoleafDeviceUpdateQueue.WriteHeader(buffer, ExtControlVersion.v2, 300);

        (object key, Color color)[] data = [(LedId.LedStripe1 + 299, new Color(255, 9, 8, 7))];
        NanoleafDeviceUpdateQueue.WriteEntries(buffer, ExtControlVersion.v2, data, mapping);

        Assert.Equal(new byte[] { 0x01, 0x2C }, buffer[..2]);
        int offset = 2 + 299 * 8;
        Assert.Equal(new byte[] { 0x01, 0x2B, 9, 8, 7, 0, 0, 0 }, buffer[offset..(offset + 8)]);
    }
    [Fact]
    public void WritePartialPacket_V2_ContainsOnlyChangedPanelsInOrder()
    {
        var mapping = new Dictionary<LedId, int>
        {
            [LedId.Custom1] = 0x0101, [LedId.Custom2] = 0x0202, [LedId.Custom3] = 0x0303
        };
        byte[] buffer = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v2, 3)];

        (object key, Color color)[] data =
        [
            (LedId.Custom3, new Color(255, 1, 2, 3)),
            (LedId.Custom9, new Color(255, 9, 9, 9)), // not part of the device
            (LedId.Custom1, new Color(255, 4, 5, 6))
        ];
        int length = NanoleafDeviceUpdateQueue.WritePartialPacket(buffer, ExtControlVersion.v2, data, mapping);

        Assert.Equal(2 + 2 * 8, length);
        Assert.Equal(new byte[] { 0, 2 }, buffer[..2]);
        Assert.Equal(new byte[] { 0x03, 0x03, 1, 2, 3, 0, 0, 0 }, buffer[2..10]);
        Assert.Equal(new byte[] { 0x01, 0x01, 4, 5, 6, 0, 0, 0 }, buffer[10..18]);
    }

    [Fact]
    public void WritePartialPacket_V1_UsesSevenByteEntries()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.Custom1] = 5, [LedId.Custom2] = 6 };
        byte[] buffer = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v1, 2)];

        (object key, Color color)[] data = [(LedId.Custom2, new Color(255, 7, 8, 9))];
        int length = NanoleafDeviceUpdateQueue.WritePartialPacket(buffer, ExtControlVersion.v1, data, mapping);

        Assert.Equal(1 + 7, length);
        Assert.Equal(new byte[] { 1, 6, 1, 7, 8, 9, 0, 0 }, buffer[..8]);
    }

    [Fact]
    public void WritePartialPacket_WithoutKnownPanels_ReturnsZero()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.Custom1] = 1 };
        byte[] buffer = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v2, 1)];

        Assert.Equal(0, NanoleafDeviceUpdateQueue.WritePartialPacket(buffer, ExtControlVersion.v2, [], mapping));
    }

    [Fact]
    public void FullFrame_KeepsEarlierColorsAfterPartialUpdate()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.Custom1] = 1, [LedId.Custom2] = 2 };
        byte[] frame = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v2, 2)];
        NanoleafDeviceUpdateQueue.WriteHeader(frame, ExtControlVersion.v2, 2);
        NanoleafDeviceUpdateQueue.InitializeFrame(frame, ExtControlVersion.v2, mapping);

        NanoleafDeviceUpdateQueue.WriteEntries(frame, ExtControlVersion.v2, [(LedId.Custom1, new Color(255, 10, 10, 10))], mapping);
        NanoleafDeviceUpdateQueue.WriteEntries(frame, ExtControlVersion.v2, [(LedId.Custom2, new Color(255, 20, 20, 20))], mapping);

        Assert.Equal(new byte[] { 0, 2, 0, 1, 10, 10, 10, 0, 0, 0, 0, 2, 20, 20, 20, 0, 0, 0 }, frame);
    }

    [Fact]
    public void InitializeFrame_AddressesEveryPanel()
    {
        var mapping = new Dictionary<LedId, int> { [LedId.Custom1] = 0x0A0B, [LedId.Custom2] = 0x0C0D };
        byte[] frame = new byte[NanoleafDeviceUpdateQueue.GetPacketLength(ExtControlVersion.v2, 2)];

        NanoleafDeviceUpdateQueue.InitializeFrame(frame, ExtControlVersion.v2, mapping);

        Assert.Equal(new byte[] { 0x0A, 0x0B }, frame[2..4]);
        Assert.Equal(new byte[] { 0x0C, 0x0D }, frame[10..12]);
    }
}
