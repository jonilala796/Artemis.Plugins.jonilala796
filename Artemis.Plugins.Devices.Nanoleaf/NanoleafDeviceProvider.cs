using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Core.DeviceProviders;
using Artemis.Core.Services;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Generic;
using Artemis.Plugins.Devices.Nanoleaf.Settings;
using Artemis.Plugins.Devices.Nanoleaf.ViewModels;
using RGB.NET.Core;
using Serilog;


namespace Artemis.Plugins.Devices.Nanoleaf;

[PluginFeature(Name = "Nanoleaf Device Provider")]
public class NanoleafDeviceProvider(ILogger logger, IDeviceService deviceService, PluginSettings settings)
    : DeviceProvider
{
    /// <summary>
    /// How long to wait for a device to accept a connection on its API port or answer a ping.
    /// Devices in Wi-Fi power save mode can take several hundred milliseconds to respond.
    /// </summary>
    private static readonly TimeSpan ReachabilityTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Minimum time between two logged stream errors, so a device that went offline does not flood the log.
    /// </summary>
    private static readonly TimeSpan ExceptionLogInterval = TimeSpan.FromSeconds(30);

    private readonly PendingRestoreStateStore _restoreStateStore = new(settings);
    private NanoleafRGBDeviceProvider? _rgbDeviceProvider;
    private DateTime _lastExceptionLog = DateTime.MinValue;

    public override void Enable()
    {
        NanoleafAPI.Logger = logger;

        _rgbDeviceProvider = RgbDeviceProvider;
        _rgbDeviceProvider.Exception += Provider_OnException;
        _rgbDeviceProvider.Logger = logger;
        _rgbDeviceProvider.RestoreStateStore = _restoreStateStore;
        _rgbDeviceProvider.DeviceDefinitions.Clear();

        PluginSetting<List<DeviceDefinition>> definitions =
            settings.GetSetting(nameof(NanoleafConfigurationViewModel.DeviceDefinitions),
                new List<DeviceDefinition>());

        List<DeviceDefinition> devices = definitions.Value ?? [];
        logger.Information("Enabling Nanoleaf plugin with {count} configured device(s)", devices.Count);

        List<DeviceDefinition> candidates = [];
        foreach (DeviceDefinition device in devices)
        {
            if (string.IsNullOrWhiteSpace(device.Hostname))
            {
                logger.Warning("Skipping a configured device without hostname");
                continue;
            }

            if (string.IsNullOrWhiteSpace(device.AuthToken))
            {
                logger.Warning("Device at {hostname} is not paired yet, skipping. Pair it in the plugin settings", device.Hostname);
                continue;
            }

            candidates.Add(device);
        }

        // Check all devices at the same time so unreachable devices do not add up their timeouts.
        // Run on the thread pool so blocking here cannot deadlock when called from a synchronization context.
        bool[] reachable = Task.Run(() => Task.WhenAll(candidates.Select(d => IsReachableAsync(d.Hostname.Trim()))))
            .GetAwaiter().GetResult();

        int added = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            DeviceDefinition device = candidates[i];
            if (!reachable[i])
            {
                logger.Warning("Device at {hostname} is unreachable, skipping", device.Hostname);
                continue;
            }

            _rgbDeviceProvider.DeviceDefinitions.Add(new NanoleafDeviceDefinition(device.Hostname.Trim(), device.AuthToken,
                device.Brightness, device.TransitionTime, device.MaxUpdateRate));
            added++;
        }

        logger.Debug("Added {count} reachable device(s) to the provider", added);
        deviceService.AddDeviceProvider(this);
    }

    public override void Disable()
    {
        logger.Information("Disabling Nanoleaf plugin");
        deviceService.RemoveDeviceProvider(this);

        if (_rgbDeviceProvider != null)
        {
            _rgbDeviceProvider.Exception -= Provider_OnException;
            _rgbDeviceProvider = null;
        }

        NanoleafRGBDeviceProvider.ResetInstance();
    }

    public override NanoleafRGBDeviceProvider RgbDeviceProvider => _rgbDeviceProvider ?? NanoleafRGBDeviceProvider.Instance;

    /// <summary>
    /// Checks whether the device accepts connections on its API port. Falls back to a ping, because the API
    /// port is what matters but some setups only let ICMP through until the device wakes up.
    /// </summary>
    private async Task<bool> IsReachableAsync(string hostname)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(hostname, NanoleafAPI.API_PORT).WaitAsync(ReachabilityTimeout);
            return true;
        }
        catch (Exception e)
        {
            logger.Debug("Connection to {hostname}:{port} failed: {reason}", hostname, NanoleafAPI.API_PORT, e.Message);
        }

        try
        {
            using var ping = new Ping();
            PingReply reply = await ping.SendPingAsync(hostname, ReachabilityTimeout);
            return reply.Status == IPStatus.Success;
        }
        catch (Exception e)
        {
            logger.Debug(e, "Ping to {hostname} failed with exception", hostname);
            return false;
        }
    }

    private void Provider_OnException(object? sender, ExceptionEventArgs args)
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastExceptionLog < ExceptionLogInterval)
        {
            logger.Debug(args.Exception, "Nanoleaf Exception: {message}", args.Exception.Message);
            return;
        }

        _lastExceptionLog = now;
        logger.Warning(args.Exception, "Nanoleaf Exception: {message}", args.Exception.Message);
    }
}
