using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;
using Artemis.Plugins.Devices.Nanoleaf.Settings;
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

    private const string EXT_CONTROL_EFFECT = "*ExtControl*";

    /// <summary>
    /// Matter devices ignore on/off commands while they leave external control mode. This is how long to wait
    /// after closing the stream before turning them off.
    /// </summary>
    private const int MATTER_EXT_CONTROL_EXIT_DELAY = 1500;

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

    /// <summary>
    /// Store used to persist the original device states while Artemis controls the devices. Optional.
    /// </summary>
    internal IPendingRestoreStateStore? RestoreStateStore { get; set; }

    /// <summary>
    /// The state of every device Artemis currently controls, captured before taking control.
    /// </summary>
    private readonly Dictionary<INanoleafDeviceDefinition, PendingRestoreState> _oldStates = new();

    private bool _disposed;

    #endregion

    #region Cleanup

    /// <summary>
    /// Disposes the current provider instance and clears the singleton.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        lock (_lock)
        {
            if (_instance == this)
                _instance = null;
        }

        if (_disposed)
            return;
        _disposed = true;

        List<KeyValuePair<INanoleafDeviceDefinition, PendingRestoreState>> oldStates;
        lock (_oldStates)
        {
            oldStates = _oldStates.ToList();
            _oldStates.Clear();
        }

        // Phase 1 (before UDP close): restore effect/color and turn on devices that
        // were already on. These HTTP commands are processed reliably while ext control
        // is still active on Matter devices.
        Dictionary<INanoleafDeviceDefinition, bool> restored = oldStates.ToDictionary(
            kvp => kvp.Key,
            kvp => RestoreOldNanoleafState(kvp.Key, kvp.Value));

        // Phase 2: close UDP — devices begin exiting ext-control mode.
        base.Dispose(disposing);

        // Phase 3: turn off devices that were originally off. Matter devices ignore HTTP
        // on/off commands sent during the ext-control transition, so wait for the
        // ext-control timeout to expire first, but only if a Matter device needs it.
        var offDevices = oldStates.Where(kvp => !kvp.Value.On).ToList();
        if (offDevices.Any(kvp => kvp.Value.IsMatter))
            Thread.Sleep(MATTER_EXT_CONTROL_EXIT_DELAY);

        foreach (var (deviceDefinition, _) in offDevices)
        {
            if (!NanoleafAPI.SetOnOff(deviceDefinition.Address, deviceDefinition.AuthToken, false))
                restored[deviceDefinition] = false;
        }

        // Keep the persisted state of devices that could not be restored so the next start can try again.
        foreach (var (deviceDefinition, success) in restored)
        {
            if (success)
                RestoreStateStore?.Remove(deviceDefinition.Address);
            else
                Logger?.Warning("Could not fully restore the previous state of device at {address}, will retry on next start",
                    deviceDefinition.Address);
        }

        DeviceDefinitions.Clear();
    }

    /// <summary>
    /// Resets the singleton instance if it exists.
    /// </summary>
    public static void ResetInstance()
    {
        NanoleafRGBDeviceProvider? instance;
        lock (_lock)
        {
            instance = _instance;
            _instance = null;
        }

        // Dispose outside the lock: restoring the devices performs network requests and may wait for
        // Matter devices to leave external control mode.
        instance?.Dispose();
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
        string address = deviceDefinition.Address;
        string authToken = deviceDefinition.AuthToken;

        var nanoleafInfo = NanoleafAPI.Info(address, authToken);
        if (nanoleafInfo == null)
        {
            Logger?.Warning("Could not retrieve device info for {address}, skipping device", address);
            return null;
        }

        var deviceInfo = new NanoleafRGBDeviceInfo(nanoleafInfo);
        bool isMatter = deviceInfo.IsMatterEssentials;

        // Check everything that can make the device unusable before changing anything on it.
        int ledCount = 0;
        if (isMatter)
        {
            ledCount = NanoleafAPI.GetLedCount(address, authToken);
            if (ledCount <= 0)
            {
                Logger?.Warning("Could not determine LED count for Matter device '{name}' at {address}, skipping",
                    nanoleafInfo.Name, address);
                return null;
            }
        }
        else if (deviceInfo.StreamablePanels.Count == 0)
        {
            Logger?.Warning("Panel device '{name}' at {address} reports no panels with lights, skipping",
                nanoleafInfo.Name, address);
            return null;
        }

        // For Matter devices the root info endpoint omits the effects section.
        // Fetch the current effect separately so it can be restored on shutdown.
        string? currentEffect = nanoleafInfo.Effects?.Select;
        if (isMatter && nanoleafInfo.Effects == null)
            currentEffect = NanoleafAPI.GetCurrentEffect(address, authToken);

        PendingRestoreState oldState = CaptureOldState(deviceDefinition, nanoleafInfo, currentEffect, isMatter);
        lock (_oldStates)
            _oldStates[deviceDefinition] = oldState;

        // Ensure brightness is at least 1 (default 0 means invisible)
        byte brightness = deviceDefinition.Brightness > 0 ? deviceDefinition.Brightness : (byte)100;

        // Matter WiFi Essentials devices must be turned on before ext control is activated
        if (isMatter)
            NanoleafAPI.SetOnOff(address, authToken, true);
        NanoleafAPI.SetBrightness(address, authToken, brightness);

        var stream = NanoleafAPI.StartExternalControl(address, authToken, deviceInfo.ExtControlVersion);
        if (stream == null)
        {
            Logger?.Warning("Failed to start external control for device '{name}' at {address}, skipping",
                nanoleafInfo.Name, address);
            RestoreFailedDevice(deviceDefinition);
            return null;
        }

        var (streamAddress, streamPort) = stream.Value;
        try
        {
            if (isMatter)
            {
                Logger?.Information("Connected to Nanoleaf device '{name}' ({model}) at {address} [{ledCount} LED(s)]",
                    nanoleafInfo.Name, nanoleafInfo.Model, address, ledCount);
                return new NanoleafRGBDevice(deviceInfo, streamAddress, streamPort, ledCount, updateTrigger, OnStreamError);
            }

            Logger?.Information("Connected to Nanoleaf device '{name}' ({model}) at {address} [{panelCount} panel(s)]",
                nanoleafInfo.Name, nanoleafInfo.Model, address, deviceInfo.StreamablePanels.Count);
            return new NanoleafRGBDevice(deviceInfo, streamAddress, streamPort, updateTrigger, OnStreamError);
        }
        catch (Exception e)
        {
            Logger?.Warning(e, "Failed to open the stream to device '{name}' at {address}, skipping",
                nanoleafInfo.Name, address);
            RestoreFailedDevice(deviceDefinition);
            return null;
        }
    }

    /// <summary>
    /// Captures the state to restore when Artemis releases the device. A state persisted by an earlier session
    /// takes precedence: it means Artemis did not shut down cleanly and the device still shows Artemis' colors.
    /// </summary>
    private PendingRestoreState CaptureOldState(INanoleafDeviceDefinition deviceDefinition, NanoleafInfo nanoleafInfo,
        string? currentEffect, bool isMatter)
    {
        // Only trust the persisted state while the device still appears to be under external control. If the
        // user changed the device since, its current state is the one to restore.
        PendingRestoreState? persisted = RestoreStateStore?.Get(deviceDefinition.Address);
        if (persisted != null && (string.IsNullOrEmpty(currentEffect) || currentEffect == EXT_CONTROL_EFFECT))
        {
            Logger?.Information("Found the state of device at {address} saved before Artemis last stopped unexpectedly, " +
                                "it will be restored when Artemis releases the device", deviceDefinition.Address);
            return persisted;
        }

        if (nanoleafInfo.State.On.Value && currentEffect == EXT_CONTROL_EFFECT)
        {
            Logger?.Warning("Device '{name}' at {address} is already in external control mode, taking over. " +
                            "Its previous effect is unknown and will not be restored", nanoleafInfo.Name, deviceDefinition.Address);
        }

        var state = PendingRestoreState.FromInfo(deviceDefinition.Address, nanoleafInfo, currentEffect, isMatter);
        RestoreStateStore?.Set(state);
        return state;
    }

    /// <summary>
    /// Restores a device that was changed but could not be taken over.
    /// </summary>
    private void RestoreFailedDevice(INanoleafDeviceDefinition deviceDefinition)
    {
        PendingRestoreState? oldState;
        lock (_oldStates)
        {
            if (!_oldStates.Remove(deviceDefinition, out oldState))
                return;
        }

        bool restored = RestoreOldNanoleafState(deviceDefinition, oldState);
        if (!oldState.On)
            restored &= NanoleafAPI.SetOnOff(deviceDefinition.Address, deviceDefinition.AuthToken, false);

        if (restored)
            RestoreStateStore?.Remove(deviceDefinition.Address);
    }

    /// <summary>
    /// Restores the effect or color, the brightness and, for devices that were on, the on state.
    /// Devices that were off are turned off by the caller.
    /// </summary>
    /// <returns><c>true</c> if every request succeeded; otherwise, <c>false</c>.</returns>
    private bool RestoreOldNanoleafState(INanoleafDeviceDefinition deviceDefinition, PendingRestoreState oldState)
    {
        string address = deviceDefinition.Address;
        string authToken = deviceDefinition.AuthToken;
        Logger?.Debug("Restoring previous state for device at {address}", address);

        string? oldEffect = oldState.Effect;
        bool hadEffect = !string.IsNullOrEmpty(oldEffect) && !oldEffect.Contains('*');

        bool success;
        if (hadEffect)
        {
            success = NanoleafAPI.SetEffect(address, authToken, oldEffect!);
            // Selecting an effect does not restore the brightness that was changed when taking control.
            if (oldState.Brightness > 0)
                success &= NanoleafAPI.SetBrightness(address, authToken, oldState.Brightness);
        }
        else
        {
            // Restore color mode (ct or hs) and brightness. on/off is handled below (was on)
            // or by the caller after ext-control expires (was off).
            success = NanoleafAPI.SetState(address, authToken, oldState.ToStateInfo());
        }

        // For devices that were on, commit the on state now while ext control is still
        // active — this is reliably processed.
        if (oldState.On)
            success &= NanoleafAPI.SetOnOff(address, authToken, true);

        return success;
    }

    private void OnStreamError(Exception exception) => Throw(exception);

    protected override IDeviceUpdateTrigger CreateUpdateTrigger(int id, double updateRateHardLimit) =>
        new DeviceUpdateTrigger(updateRateHardLimit) { HeartbeatTimer = HEARTBEAT_TIMER };

    #endregion
}
