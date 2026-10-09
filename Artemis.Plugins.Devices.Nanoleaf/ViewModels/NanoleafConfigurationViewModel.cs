using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Core.Services;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.Settings;
using Artemis.Plugins.Devices.Nanoleaf.ViewModels.Dialogs;
using Artemis.UI.Shared;
using Artemis.UI.Shared.Services;
using ReactiveUI;
using Serilog;

namespace Artemis.Plugins.Devices.Nanoleaf.ViewModels;

public class NanoleafConfigurationViewModel : PluginConfigurationViewModel
{
    private const string PANEL_PAIRING_INSTRUCTIONS =
        "Press and hold the power button on the device for 5 seconds until the lights flash to enter pairing mode.";

    private const string MATTER_PAIRING_INSTRUCTIONS =
        "Open the Nanoleaf app, go to the device settings and tap 'Connect to API'.";

    private readonly PluginSetting<List<DeviceDefinition>> _definitions;
    private readonly ILogger _logger;
    private readonly IPluginManagementService _pluginManagementService;
    private readonly IWindowService _windowService;

    public ObservableCollection<DeviceDefinition> DeviceDefinitions { get; }


    public ReactiveCommand<Unit, Unit> AddDevice { get; }
    public ReactiveCommand<DeviceDefinition, Unit> EditDevice { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }
    public ReactiveCommand<Unit, Unit> DiscoverDevices { get; }
    public ReactiveCommand<DeviceDefinition, Unit> AuthenticateDevice { get; }


    public NanoleafConfigurationViewModel(Plugin plugin, PluginSettings settings, IWindowService windowService,
        IPluginManagementService pluginManagementService, ILogger logger) : base(plugin)
    {
        _windowService = windowService;
        _pluginManagementService = pluginManagementService;
        _logger = logger;

        _definitions = settings.GetSetting(nameof(DeviceDefinitions), new List<DeviceDefinition>());

        DeviceDefinitions = new ObservableCollection<DeviceDefinition>(_definitions.Value ?? []);

        AddDevice = ReactiveCommand.CreateFromTask(ExecuteAddDevice);
        EditDevice = ReactiveCommand.CreateFromTask<DeviceDefinition>(ExecuteEditDevice);
        Save = ReactiveCommand.Create(ExecuteSave);
        Cancel = ReactiveCommand.CreateFromTask(ExecuteCancel);
        DiscoverDevices = ReactiveCommand.CreateFromTask(ExecuteDiscoverDevices);
        AuthenticateDevice = ReactiveCommand.CreateFromTask<DeviceDefinition>(ExecuteAuthenticateDevice);
    }

    private async Task ExecuteAuthenticateDevice(DeviceDefinition device)
    {
        if (!string.IsNullOrEmpty(device.AuthToken) &&
            await _windowService.ShowConfirmContentDialog("Authentication information",
                "You are already paired with this device. Do you want to pair it again?", "Keep pairing", "Pair again"))
            return;

        string instructions;
        if (NanoleafAPI.IsMatterEssentialsDevice(device.Model))
            instructions = $"{MATTER_PAIRING_INSTRUCTIONS}\r\nThen press the Pair button below within 30 seconds.";
        else if (string.IsNullOrEmpty(device.Model))
            instructions = $"Panel devices (Shapes, Canvas, Elements, Lines, ...): {PANEL_PAIRING_INSTRUCTIONS}\r\n" +
                           $"Matter WiFi Essentials devices (Lightstrips, Bulbs, ...): {MATTER_PAIRING_INSTRUCTIONS}\r\n" +
                           "Then press the Pair button below within 30 seconds.";
        else
            instructions = $"{PANEL_PAIRING_INSTRUCTIONS}\r\nThen press the Pair button below.";

        if (!await _windowService.ShowConfirmContentDialog("Authentication instructions", instructions, "Pair"))
            return;

        string hostname = device.Hostname.Trim();
        string? authToken = await NanoleafAPI.AuthenticateAsync(hostname);
        if (string.IsNullOrEmpty(authToken))
        {
            await _windowService.ShowConfirmContentDialog("Authentication failed",
                "Failed to authenticate with the device. Make sure it is in pairing mode and reachable, then try again.",
                "Close", null);
            return;
        }

        device.AuthToken = authToken;

        // Fill in the model so the plugin and the device list know what kind of device this is.
        NanoleafInfo? info = await NanoleafAPI.InfoAsync(hostname, authToken);
        if (!string.IsNullOrEmpty(info?.Model))
            device.Model = info.Model;

        // Save right away: a new token invalidates nothing on the device, but losing it means pairing again.
        _definitions.Save();

        await _windowService.ShowConfirmContentDialog("Authentication successful",
            "You have successfully authenticated with the device.", "Close", null);
    }

    private async Task ExecuteDiscoverDevices()
    {
        var devicesToAdd = await _windowService.ShowDialogAsync<DiscoverDevicesDialogViewModel, List<DeviceDefinition>>();

        if (devicesToAdd == null || devicesToAdd.Count == 0)
            return;

        foreach (var device in devicesToAdd)
        {
            if (FindByHostname(device.Hostname, null) != null)
                continue;

            _definitions.Value?.Add(device);
            DeviceDefinitions.Add(device);
        }
    }


    private async Task ExecuteAddDevice()
    {
        DeviceDefinition device = new();
        if (await _windowService.ShowDialogAsync<DeviceConfigurationDialogViewModel, DeviceDialogResult>(device) !=
            DeviceDialogResult.Save)
            return;

        if (FindByHostname(device.Hostname, null) != null)
        {
            await ShowDuplicateHostnameDialog(device.Hostname);
            return;
        }

        _definitions.Value?.Add(device);
        DeviceDefinitions.Add(device);
    }

    private async Task ExecuteEditDevice(DeviceDefinition device)
    {
        string oldHostname = device.Hostname;
        DeviceDialogResult result =
            await _windowService.ShowDialogAsync<DeviceConfigurationDialogViewModel, DeviceDialogResult>(device);

        if (result == DeviceDialogResult.Remove)
        {
            RemoveDevice(device);
        }
        else if (result == DeviceDialogResult.Save && FindByHostname(device.Hostname, device) != null)
        {
            await ShowDuplicateHostnameDialog(device.Hostname);
            device.Hostname = oldHostname;
        }
    }

    private async Task ExecuteCancel()
    {
        if (_definitions.HasChanged)
        {
            if (!await _windowService.ShowConfirmContentDialog("Discard changes",
                    "Do you want to discard any changes you made?"))
                return;
        }

        _definitions.RejectChanges();

        Close();
    }

    private void RemoveDevice(DeviceDefinition device)
    {
        _definitions.Value?.Remove(device);
        DeviceDefinitions.Remove(device);
    }

    private DeviceDefinition? FindByHostname(string hostname, DeviceDefinition? except) =>
        DeviceDefinitions.FirstOrDefault(d => d != except &&
                                              string.Equals(d.Hostname.Trim(), hostname.Trim(), StringComparison.OrdinalIgnoreCase));

    private Task ShowDuplicateHostnameDialog(string hostname) =>
        _windowService.ShowConfirmContentDialog("Device already added",
            $"A device with the hostname '{hostname}' is already configured.", "Close", null);

    private void ExecuteSave()
    {
        _definitions.Save();

        Task.Run(() =>
        {
            try
            {
                var deviceProvider = Plugin.GetFeature<NanoleafDeviceProvider>();
                if (deviceProvider == null || !deviceProvider.IsEnabled) return;
                _pluginManagementService.DisablePluginFeature(deviceProvider, false);
                _pluginManagementService.EnablePluginFeature(deviceProvider, false);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to restart the Nanoleaf device provider after saving the settings");
            }
        });
    }
}
