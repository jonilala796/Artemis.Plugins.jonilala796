using System;
using System.Collections.Generic;
using System.Threading;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Helper;
using RGB.NET.Core;
using Serilog;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET;

/// <inheritdoc />
/// <summary>
/// Represents a device provider responsible for Nanoleaf devices.
/// </summary>
// ReSharper disable once InconsistentNaming
public class NanoleafRGBDeviceProvider : AbstractRGBDeviceProvider
{
    #region Constants

    private const int HEARTBEAT_TIMER = 100;

    #endregion

    #region Properties & Fields

    // ReSharper disable once InconsistentNaming
    private static readonly Lock _lock = new();

    private static NanoleafRGBDeviceProvider? _instance;

    /// <summary>
    /// Gets the singleton <see cref="NanoleafRGBDeviceProvider"/> instance.
    /// </summary>
    public static NanoleafRGBDeviceProvider Instance
    {
        get
        {
            lock (_lock)
                return _instance ?? new NanoleafRGBDeviceProvider();
        }
    }

    /// <summary>
    /// Gets a list of all defined device-definitions.
    /// </summary>
    public List<INanoleafDeviceDefinition> DeviceDefinitions { get; } = [];

    /// <summary>
    /// Logger supplied by the Artemis device provider. Optional — logging is skipped when null.
    /// </summary>
    internal ILogger? Logger { get; set; }

    private static readonly Dictionary<INanoleafDeviceDefinition, NanoleafInfo> OldStates = new();

    #endregion

    #region Cleanup

    /// <summary>
    /// Disposes the current provider instance and clears the singleton.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        lock (_lock)
        {
            base.Dispose(disposing);

            foreach (var deviceDefinition in DeviceDefinitions)
            {
                RestoreOldNanoleafState(deviceDefinition);
            }

            DeviceDefinitions.Clear();
            OldStates.Clear();

            _instance = null;
        }
    }

    /// <summary>
    /// Resets the singleton instance if it exists.
    /// </summary>
    public static void ResetInstance()
    {
        lock (_lock)
        {
            if (_instance == null)
                return;

            _instance.Dispose();
            _instance = null;
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoleafRGBDeviceProvider"/> class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this constructor is called even if there is already an instance of this class.</exception>
    public NanoleafRGBDeviceProvider()
    {
        lock (_lock)
        {
            if (_instance != null)
                throw new InvalidOperationException(
                    $"There can be only one instance of type {nameof(NanoleafRGBDeviceProvider)}");
            _instance = this;
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    protected override void InitializeSDK()
    {
    }

    /// <inheritdoc />
    protected override IEnumerable<IRGBDevice> LoadDevices()
    {
        int i = 0;
        foreach (INanoleafDeviceDefinition deviceDefinition in DeviceDefinitions)
        {
            IDeviceUpdateTrigger updateTrigger = GetUpdateTrigger(i++);
            var device = CreateNanoleafDevice(deviceDefinition, updateTrigger);
            if (device != null)
                yield return device;
        }
    }


    private NanoleafRGBDevice? CreateNanoleafDevice(INanoleafDeviceDefinition deviceDefinition,
        IDeviceUpdateTrigger updateTrigger)
    {
        var nanoleafInfo = NanoleafAPI.Info(deviceDefinition.Address, deviceDefinition.AuthToken);
        if (nanoleafInfo == null)
        {
            Logger?.Warning("Could not retrieve device info for {address}, skipping device", deviceDefinition.Address);
            return null;
        }

        bool isMatter = NanoleafAPI.IsMatterEssentialsDevice(nanoleafInfo.Model)
                        || nanoleafInfo.PanelLayout?.Layout.PositionData is null or { Count: 0 };

        if (!isMatter)
        {
            // Panel-based device: check if already in ext-control mode
            if (nanoleafInfo.State.On.Value && nanoleafInfo.Effects?.Select == "*ExtControl*")
            {
                Logger?.Debug("Panel device '{name}' at {address} is already in ext-control mode, skipping",
                    nanoleafInfo.Name, deviceDefinition.Address);
                return null;
            }
        }

        // Store the initial state info for restoring later
        OldStates[deviceDefinition] = nanoleafInfo;

        // Ensure brightness is at least 1 (default 0 means invisible)
        byte brightness = deviceDefinition.Brightness > 0 ? deviceDefinition.Brightness : (byte)100;

        if (isMatter)
        {
            // Matter WiFi Essentials: turn on the device, set brightness, then activate ext control
            NanoleafAPI.SetOnOff(deviceDefinition.Address, deviceDefinition.AuthToken, true);
            NanoleafAPI.SetBrightness(deviceDefinition.Address, deviceDefinition.AuthToken, brightness);

            int ledCount = NanoleafAPI.GetLedCount(deviceDefinition.Address, deviceDefinition.AuthToken);
            if (ledCount <= 0)
            {
                Logger?.Warning("Could not determine LED count for Matter device '{name}' at {address}, skipping",
                    nanoleafInfo.Name, deviceDefinition.Address);
                return null;
            }

            var startExtControl = NanoleafAPI.StartExternalControl(deviceDefinition.Address,
                deviceDefinition.AuthToken, ExtControlVersion.v2);

            // Matter devices may return empty address; fall back to device address + port 60222
            string streamAddress = string.IsNullOrEmpty(startExtControl.address)
                ? deviceDefinition.Address
                : startExtControl.address;
            ushort streamPort = startExtControl.port > 0 ? startExtControl.port : (ushort)60222;

            Logger?.Information("Connected to Nanoleaf device '{name}' ({model}) at {address} [{ledCount} LED(s)]",
                nanoleafInfo.Name, nanoleafInfo.Model, deviceDefinition.Address, ledCount);

            return new NanoleafRGBDevice(new NanoleafRGBDeviceInfo(nanoleafInfo), streamAddress,
                streamPort, ledCount, updateTrigger);
        }
        else
        {
            NanoleafAPI.SetBrightness(deviceDefinition.Address, deviceDefinition.AuthToken, brightness);

            // Panel-based device
            var startExtControl = NanoleafAPI.StartExternalControl(deviceDefinition.Address,
                deviceDefinition.AuthToken,
                nanoleafInfo.PanelLayout!.Layout.PositionData[0].ShapeType.GetExtControlVersion());

            if (string.IsNullOrEmpty(startExtControl.address) || startExtControl.port == 0)
            {
                Logger?.Warning("Failed to start external control for panel device '{name}' at {address}, skipping",
                    nanoleafInfo.Name, deviceDefinition.Address);
                return null;
            }

            int panelCount = nanoleafInfo.PanelLayout?.Layout.NumPanels ?? 0;
            Logger?.Information("Connected to Nanoleaf device '{name}' ({model}) at {address} [{panelCount} panel(s)]",
                nanoleafInfo.Name, nanoleafInfo.Model, deviceDefinition.Address, panelCount);

            return new NanoleafRGBDevice(new NanoleafRGBDeviceInfo(nanoleafInfo), startExtControl.address,
                startExtControl.port, updateTrigger);
        }
    }

    private void RestoreOldNanoleafState(INanoleafDeviceDefinition deviceDefinition)
    {
        if (!OldStates.Remove(deviceDefinition, out var oldStateInfo))
            return;

        Logger?.Debug("Restoring previous state for device at {address}", deviceDefinition.Address);

        string? oldEffect = oldStateInfo.Effects?.Select;

        if (string.IsNullOrEmpty(oldEffect) || oldEffect.Contains('*'))
        {
            // Matter devices have no effects, or effect was internal — restore state directly
            NanoleafAPI.SetState(deviceDefinition.Address, deviceDefinition.AuthToken, oldStateInfo.State);
        }
        else
        {
            NanoleafAPI.SetEffect(deviceDefinition.Address, deviceDefinition.AuthToken, oldEffect);
            NanoleafAPI.SetBrightness(deviceDefinition.Address, deviceDefinition.AuthToken,
                oldStateInfo.State.Brightness.Value);
            NanoleafAPI.SetOnOff(deviceDefinition.Address, deviceDefinition.AuthToken, oldStateInfo.State.On.Value);
        }
    }

    protected override IDeviceUpdateTrigger CreateUpdateTrigger(int id, double updateRateHardLimit) =>
        new DeviceUpdateTrigger(updateRateHardLimit) { HeartbeatTimer = HEARTBEAT_TIMER };

    #endregion
}