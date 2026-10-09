using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using RGB.NET.Core;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;

/// <inheritdoc />
/// <summary>
/// Represents the update-queue performing updates for Nanoleaf devices using the external control streaming API.
/// </summary>
internal sealed class NanoleafDeviceUpdateQueue : UpdateQueue
{
    #region Properties & Fields

    /// <summary>
    /// The UDP-Connection used to send data.
    /// </summary>
    private readonly UdpClient _socket;

    /// <summary>
    /// The buffer the UDP-data is stored in.
    /// </summary>
    private readonly byte[] _buffer;

    private readonly ExtControlVersion _version;
    private readonly Dictionary<LedId, int> _deviceInfoLedIdToIndex;
    private readonly Action<Exception> _onError;
    private volatile bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoleafDeviceUpdateQueue"/> class.
    /// </summary>
    /// <param name="updateTrigger">The update trigger used by this queue.</param>
    /// <param name="address">The address to stream to.</param>
    /// <param name="port">The port to stream to.</param>
    /// <param name="ledCount">The number of panels or LEDs contained in every packet.</param>
    /// <param name="version">The external control protocol version.</param>
    /// <param name="deviceInfoLedIdToIndex">The mapping of LED id to panel id or LED index.</param>
    /// <param name="onError">Callback invoked when sending fails.</param>
    public NanoleafDeviceUpdateQueue(IDeviceUpdateTrigger updateTrigger, string address, int port, int ledCount,
        ExtControlVersion version, Dictionary<LedId, int> deviceInfoLedIdToIndex, Action<Exception> onError)
        : base(updateTrigger)
    {
        _version = version;
        _deviceInfoLedIdToIndex = deviceInfoLedIdToIndex;
        _onError = onError;

        _buffer = new byte[GetPacketLength(version, ledCount)];
        WriteHeader(_buffer, version, ledCount);

        try
        {
            IPAddress ipAddress = ResolveAddress(address);
            _socket = new UdpClient(ipAddress.AddressFamily);
            _socket.Connect(new IPEndPoint(ipAddress, port));
        }
        catch
        {
            // The base constructor already subscribed to the update trigger, unsubscribe again
            _disposed = true;
            base.Dispose();
            throw;
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    protected override void OnUpdate(object? sender, CustomUpdateData customData)
    {
        if (_disposed)
            return;

        try
        {
            // The heartbeat resends the last frame so the device stays in external control mode.
            if (customData[CustomUpdateDataIndex.HEARTBEAT] as bool? ?? false)
                Update(Array.Empty<(object key, Color color)>());
            else
                base.OnUpdate(sender, customData);
        }
        catch (Exception ex)
        {
            _onError(ex);
        }
    }

    /// <inheritdoc />
    protected override bool Update(ReadOnlySpan<(object key, Color color)> dataSet)
    {
        if (_disposed)
            return false;

        try
        {
            WriteEntries(_buffer, _version, dataSet, _deviceInfoLedIdToIndex);
            _socket.Send(_buffer);
            return true;
        }
        catch (Exception ex)
        {
            if (!_disposed)
                _onError(ex);
        }

        return false;
    }

    /// <summary>
    /// Gets the length of a packet containing <paramref name="ledCount"/> entries.
    /// </summary>
    internal static int GetPacketLength(ExtControlVersion version, int ledCount) => version == ExtControlVersion.v1
        ? 1 + ledCount * 7
        : 2 + ledCount * 8;

    /// <summary>
    /// Writes the number of panels into the packet header.
    /// </summary>
    internal static void WriteHeader(Span<byte> buffer, ExtControlVersion version, int ledCount)
    {
        if (version == ExtControlVersion.v1)
        {
            buffer[0] = (byte)ledCount;
        }
        else
        {
            buffer[0] = (byte)(ledCount >> 8); // Number of panels high byte
            buffer[1] = (byte)(ledCount & 0xFF); // Number of panels low byte
        }
    }

    /// <summary>
    /// Writes the colors of <paramref name="dataSet"/> into the packet. Each LED owns a fixed slot derived from
    /// its <see cref="LedId"/>, so entries that are not part of the data set keep their last color.
    /// </summary>
    internal static void WriteEntries(Span<byte> buffer, ExtControlVersion version,
        ReadOnlySpan<(object key, Color color)> dataSet, IReadOnlyDictionary<LedId, int> ledIdToIndex)
    {
        int headerLength = version == ExtControlVersion.v1 ? 1 : 2;
        int entryLength = version == ExtControlVersion.v1 ? 7 : 8;

        foreach ((object key, Color color) in dataSet)
        {
            if (key is not LedId ledId || !ledIdToIndex.TryGetValue(ledId, out int panelId))
                continue;

            int slot = ((int)ledId & 0x000FFFFF) - 1;
            int offset = slot * entryLength + headerLength;
            if (slot < 0 || offset + entryLength > buffer.Length)
                continue;

            if (version == ExtControlVersion.v1)
            {
                buffer[offset] = (byte)panelId;
                buffer[offset + 1] = 1; // Number of frames, always 1
                buffer[offset + 2] = color.GetR();
                buffer[offset + 3] = color.GetG();
                buffer[offset + 4] = color.GetB();
                buffer[offset + 5] = 0; // White LED element, currently ignored
                buffer[offset + 6] = 0; // transitionTime
            }
            else
            {
                buffer[offset] = (byte)(panelId >> 8); // panelId high byte
                buffer[offset + 1] = (byte)(panelId & 0xFF); // panelId low byte
                buffer[offset + 2] = color.GetR();
                buffer[offset + 3] = color.GetG();
                buffer[offset + 4] = color.GetB();
                buffer[offset + 5] = 0; // White LED element, currently ignored
                buffer[offset + 6] = 0; // transitionTime high byte
                buffer[offset + 7] = 0; // transitionTime low byte
            }
        }
    }

    /// <summary>
    /// Resolves the stream address, preferring IPv4 so hosts with IPv6 disabled can still stream.
    /// </summary>
    private static IPAddress ResolveAddress(string address)
    {
        if (IPAddress.TryParse(address, out IPAddress? ipAddress))
            return ipAddress;

        IPAddress[] addresses = Dns.GetHostAddresses(address);
        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
               ?? addresses.FirstOrDefault()
               ?? throw new SocketException((int)SocketError.HostNotFound);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _disposed = true;
        base.Dispose();

        _socket.Dispose();
    }

    #endregion
}
