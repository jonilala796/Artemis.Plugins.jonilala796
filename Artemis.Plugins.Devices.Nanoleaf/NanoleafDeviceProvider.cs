using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using Artemis.Core;
using Artemis.Core.DeviceProviders;
using Artemis.Core.Services;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET;
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
    public override void Enable()
    {
        RgbDeviceProvider.Exception += Provider_OnException;
        RgbDeviceProvider.Logger = logger;
        RgbDeviceProvider.DeviceDefinitions.Clear();

        PluginSetting<List<DeviceDefinition>> definitions =
            settings.GetSetting(nameof(NanoleafConfigurationViewModel.DeviceDefinitions),
                new List<DeviceDefinition>());

        List<(string Hostname, string Model, string AuthToken, byte Brightness)> devices = (definitions.Value ?? []).Select(deviceDefinition =>
            (deviceDefinition.Hostname, deviceDefinition.Model, deviceDefinition.AuthToken, deviceDefinition.Brightness)).ToList();

        logger.Information("Enabling Nanoleaf plugin with {count} configured device(s)", devices.Count);

        int added = 0;
        foreach ((string hostname, string _, string authToken, byte brightness) in devices)
        {
            try
            {
                if (!string.IsNullOrEmpty(hostname))
                {
                    var pingSender = new Ping();
                    var reply = pingSender.Send(hostname, 100);
                    if (reply.Status != IPStatus.Success)
                    {
                        logger.Warning("Device at {hostname} is unreachable (ping status: {status}), skipping", hostname, reply.Status);
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                logger.Debug(e, "Ping to {hostname} failed with exception", hostname);
                continue;
            }
            RgbDeviceProvider.DeviceDefinitions.Add(new NanoleafDeviceDefinition(hostname, authToken, brightness));
            added++;
        }

        logger.Debug("Added {count} reachable device(s) to the provider", added);
        deviceService.AddDeviceProvider(this);
    }

    public override void Disable()
    {
        logger.Information("Disabling Nanoleaf plugin");
        deviceService.RemoveDeviceProvider(this);

        RgbDeviceProvider.Exception -= Provider_OnException;
        
        NanoleafRGBDeviceProvider.ResetInstance();
    }

    public override NanoleafRGBDeviceProvider RgbDeviceProvider => NanoleafRGBDeviceProvider.Instance;

    private void Provider_OnException(object? sender, ExceptionEventArgs args)
    {
        logger.Debug(args.Exception, "Nanoleaf Exception: {message}", args.Exception.Message);
    }
}