using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Core.Services;
using Artemis.Plugins.Devices.Nanoleaf.Helper;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.API;
using Artemis.Plugins.Devices.Nanoleaf.Settings;
using Artemis.Plugins.Devices.Nanoleaf.ViewModels.Dialogs;
using Artemis.UI.Shared;
using Artemis.UI.Shared.Services;
using ReactiveUI;

namespace Artemis.Plugins.Devices.Nanoleaf.ViewModels;

public class NanoleafConfigurationViewModel : PluginConfigurationViewModel
{
    private readonly PluginSetting<List<DeviceDefinition>> _definitions;
    private readonly IPluginManagementService _pluginManagementService;
    private readonly PluginSettings _settings;
    private readonly IWindowService _windowService;

    public ObservableCollection<DeviceDefinition> DeviceDefinitions { get; }


    public ReactiveCommand<Unit, Unit> AddDevice { get; }
    public ReactiveCommand<DeviceDefinition, Unit> EditDevice { get; }
    public ReactiveCommand<DeviceDefinition, Unit> RemoveDevice { get; }
    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }
    public ReactiveCommand<Unit, Unit> DiscoverDevices { get; }
    public ReactiveCommand<DeviceDefinition, Unit> AuthenticateDevice { get; }


    public NanoleafConfigurationViewModel(Plugin plugin, PluginSettings settings, IWindowService windowService,
        IPluginManagementService pluginManagementService) : base(plugin)
    {
        _settings = settings;
        _windowService = windowService;
        _pluginManagementService = pluginManagementService;

        _definitions = settings.GetSetting(nameof(DeviceDefinitions), new List<DeviceDefinition>());

        DeviceDefinitions = new ObservableCollection<DeviceDefinition>(_definitions.Value ?? []);

        AddDevice = ReactiveCommand.CreateFromTask(ExecuteAddDevice);
        EditDevice = ReactiveCommand.CreateFromTask<DeviceDefinition>(ExecuteEditDevice);
        RemoveDevice = ReactiveCommand.Create<DeviceDefinition>(ExecuteRemoveDevice);
        Save = ReactiveCommand.Create(ExecuteSave);
        Cancel = ReactiveCommand.CreateFromTask(ExecuteCancel);
        DiscoverDevices = ReactiveCommand.CreateFromTask(ExecuteDiscoverDevices);
        AuthenticateDevice = ReactiveCommand.CreateFromTask<DeviceDefinition>(ExecuteAuthenticateDevice);
    }

    private async Task ExecuteAuthenticateDevice(DeviceDefinition device)
    {
        if (!string.IsNullOrEmpty(device.AuthToken) &&
            await _windowService.ShowConfirmContentDialog("Authentication information",
                "You are already paired with this device."))
            return;

        if (!await _windowService.ShowConfirmContentDialog("Authentication instructions",
                "Please press the power button on the device for 5 seconds to enter pairing mode.\r\nThen press the Pair button below.",
                "Pair"))
            return;

        string? authToken = NanoleafAPI.Authenticate(device.Hostname);
        if (string.IsNullOrEmpty(authToken))
        {
            await _windowService.ShowConfirmContentDialog("Authentication failed",
                "Failed to authenticate with the device");
        }
        else
        {
            PluginSetting<List<DeviceDefinition>> definitions =
                _settings.GetSetting(nameof(DeviceDefinitions), new List<DeviceDefinition>());
            var matchedDevice = definitions.Value?.FirstOrDefault(d => d.Hostname == device.Hostname);
            if (matchedDevice != null)
            {
                matchedDevice.AuthToken = authToken;
            }
            else
            {
                // Fallback: update the token on the passed instance.
                device.AuthToken = authToken;
                definitions.Value?.Add(device);
            }
            await _windowService.ShowConfirmContentDialog("Authentication successful",
                "You have successfully authenticated with the device");
        }
    }

    private async Task ExecuteDiscoverDevices()
    {
        List<(string address, string model)> discoverDevices = NanoleafDiscoveryHelper.DiscoverDevices();
        string message = discoverDevices.Count switch
        {
            0 => "No devices found",
            1 => "1 device found",
            _ => $"{discoverDevices.Count} devices found"
        };

        if (!await _windowService.ShowConfirmContentDialog("Discover devices", message, "Add devices"))
            return;


        //check if devices with the ip address already exist
        foreach ((var ipAddress, string model) in discoverDevices)
        {
            if ((_definitions.Value ?? []).Any(d => ipAddress.Equals(d.Hostname)))
                continue;

            _definitions.Value?.Add(new DeviceDefinition
            {
                Hostname = ipAddress,
                Model = model,
                Brightness = 100
            });

            DeviceDefinitions.Add(new DeviceDefinition
            {
                Hostname = ipAddress,
                Model = model,
                Brightness = 100
            });
        }
    }


    private async Task ExecuteAddDevice()
    {
        DeviceDefinition device = new();
        if (await _windowService.ShowDialogAsync<DeviceConfigurationDialogViewModel, DeviceDialogResult>(device) !=
            DeviceDialogResult.Save)
            return;

        _definitions.Value?.Add(device);
        DeviceDefinitions.Add(device);
    }

    private async Task ExecuteEditDevice(DeviceDefinition device)
    {
        if (await _windowService.ShowDialogAsync<DeviceConfigurationDialogViewModel, DeviceDialogResult>(device) ==
            DeviceDialogResult.Remove)
            DeviceDefinitions.Remove(device);
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

    private void ExecuteRemoveDevice(DeviceDefinition device)
    {
        _definitions.Value?.Remove(device);
        DeviceDefinitions.Remove(device);
    }

    private void ExecuteSave()
    {
        _definitions.Save();

        Task.Run(() =>
        {
            var deviceProvider = Plugin.GetFeature<NanoleafDeviceProvider>();
            if ((deviceProvider == null ||  !deviceProvider.IsEnabled)) return;
            _pluginManagementService.DisablePluginFeature(deviceProvider, false);
            _pluginManagementService.EnablePluginFeature(deviceProvider, false);
        });
    }
}