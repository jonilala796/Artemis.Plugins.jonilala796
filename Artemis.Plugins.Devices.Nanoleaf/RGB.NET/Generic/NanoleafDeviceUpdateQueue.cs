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
    /// The full current frame containing every panel. Sent by the heartbeat and by devices without partial frames.
    /// </summary>
    private readonly byte[] _buffer;

    /// <summary>
    /// The buffer partial frames containing only the changed panels are written to.
    /// </summary>
    private readonly byte[] _partialBuffer;

    /// <summary>
    /// Whether only changed panels are sent. Panels that are not part of a packet keep their color, so this
    /// keeps the traffic on the device's panel chain low.
    /// </summary>
    private readonly bool _partialFrames;

    private readonly ExtControlVersion _version;
    private readonly Dictionary<LedId, int> _deviceInfoLedIdToIndex;
    private readonly Action<Exception> _onError;
    private volatile bool _disposed;
    private bool _frameInitialized;

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
    /// <param name="partialFrames">Whether to send only changed panels instead of the full frame on every update.</param>
    public NanoleafDeviceUpdateQueue(IDeviceUpdateTrigger updateTrigger, string address, int port, int ledCount,
        ExtControlVersion version, Dictionary<LedId, int> deviceInfoLedIdToIndex, Action<Exception> onError,
        bool partialFrames)
        : base(updateTrigger)
    {
        _version = version;
        _deviceInfoLedIdToIndex = deviceInfoLedIdToIndex;
        _onError = onError;
        _partialFrames = partialFrames;

        _buffer = new byte[GetPacketLength(version, ledCount)];
        _partialBuffer = partialFrames ? new byte[_buffer.Length] : [];
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
            // The LED mapping is filled after the queue is created, so address every panel on the first update.
            // Otherwise panels whose color never changes would not be part of the full frame.
            if (!_frameInitialized && _deviceInfoLedIdToIndex.Count > 0)
            {
                InitializeFrame(_buffer, _version, _deviceInfoLedIdToIndex);
                _frameInitialized = true;
            }

            // The heartbeat resends the full last frame so the device stays in external control mode
            // and panels recover from a lost packet.
            if (customData[CustomUpdateDataIndex.HEARTBEAT] as bool? ?? false)
                _socket.Send(_buffer);
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

            if (!_partialFrames)
            {
                _socket.Send(_buffer);
                return true;
            }

            int length = WritePartialPacket(_partialBuffer, _version, dataSet, _deviceInfoLedIdToIndex);
            if (length > 0)
                _socket.Send(_partialBuffer.AsSpan(0, length));
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

            WriteEntry(buffer.Slice(offset, entryLength), version, panelId, color);
        }
    }

    /// <summary>
    /// Writes every panel of the device into the full frame, black.
    /// </summary>
    internal static void InitializeFrame(Span<byte> buffer, ExtControlVersion version,
        IReadOnlyDictionary<LedId, int> ledIdToIndex)
    {
        (object key, Color color)[] dataSet = new (object key, Color color)[ledIdToIndex.Count];
        int i = 0;
        foreach (LedId ledId in ledIdToIndex.Keys)
            dataSet[i++] = (ledId, new Color(0, 0, 0));

        WriteEntries(buffer, version, dataSet, ledIdToIndex);
    }

    /// <summary>
    /// Writes a packet containing only the panels of <paramref name="dataSet"/>, one after another.
    /// </summary>
    /// <returns>The length of the packet, or 0 if no panel of the data set is part of the device.</returns>
    internal static int WritePartialPacket(Span<byte> buffer, ExtControlVersion version,
        ReadOnlySpan<(object key, Color color)> dataSet, IReadOnlyDictionary<LedId, int> ledIdToIndex)
    {
        int headerLength = version == ExtControlVersion.v1 ? 1 : 2;
        int entryLength = version == ExtControlVersion.v1 ? 7 : 8;

        int count = 0;
        foreach ((object key, Color color) in dataSet)
        {
            if (key is not LedId ledId || !ledIdToIndex.TryGetValue(ledId, out int panelId))
                continue;

            int offset = headerLength + count * entryLength;
            if (offset + entryLength > buffer.Length)
                break;

            WriteEntry(buffer.Slice(offset, entryLength), version, panelId, color);
            count++;
        }

        if (count == 0)
            return 0;

        WriteHeader(buffer, version, count);
        return headerLength + count * entryLength;
    }

    private static void WriteEntry(Span<byte> entry, ExtControlVersion version, int panelId, Color color)
    {
        if (version == ExtControlVersion.v1)
        {
            entry[0] = (byte)panelId;
            entry[1] = 1; // Number of frames, always 1
            entry[2] = color.GetR();
            entry[3] = color.GetG();
            entry[4] = color.GetB();
            entry[5] = 0; // White LED element, currently ignored
            entry[6] = 0; // transitionTime
        }
        else
        {
            entry[0] = (byte)(panelId >> 8); // panelId high byte
            entry[1] = (byte)(panelId & 0xFF); // panelId low byte
            entry[2] = color.GetR();
            entry[3] = color.GetG();
            entry[4] = color.GetB();
            entry[5] = 0; // White LED element, currently ignored
            entry[6] = 0; // transitionTime high byte
            entry[7] = 0; // transitionTime low byte
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
