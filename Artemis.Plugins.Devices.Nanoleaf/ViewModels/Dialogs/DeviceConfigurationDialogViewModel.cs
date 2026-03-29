using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Plugins.Devices.Nanoleaf.Settings;
using Artemis.UI.Shared;
using Artemis.UI.Shared.Services;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;

namespace Artemis.Plugins.Devices.Nanoleaf.ViewModels.Dialogs;

public class
    DeviceConfigurationDialogViewModel : DialogViewModelBase<DeviceDialogResult>
{
    private readonly DeviceDefinition _device;
    private readonly PluginSettings _settings;
    private readonly IWindowService _windowService;
    private bool _hasChanges;

    public DeviceDefinition Device { get; }

    private string _hostname;

    public string Hostname
    {
        get => _hostname;
        set => RaiseAndSetIfChanged(ref _hostname, value);
    }

    private string _model;

    public string Model
    {
        get => _model;
        set => RaiseAndSetIfChanged(ref _model, value);
    }

    private string _authToken;

    public string AuthToken
    {
        get => _authToken;
        set => RaiseAndSetIfChanged(ref _authToken, value);
    }
    
    private byte _brightness;
    public byte Brightness
    {
        get => _brightness;
        set => RaiseAndSetIfChanged(ref _brightness, value);
    }

    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }
    public ReactiveCommand<Unit, Unit> RemoveDevice { get; }

    public DeviceConfigurationDialogViewModel(DeviceDefinition device, PluginSettings settings,
        IWindowService windowService)
    {
        _device = device;
        Device = device;
        _settings = settings;
        _windowService = windowService;

        _hostname = device.Hostname;
        _model = device.Model;
        _authToken = device.AuthToken;
        _brightness = device.Brightness;

        this.ValidationRule(vm => vm.Hostname, s => !string.IsNullOrWhiteSpace(s), "A Hostname is required");

        Save = ReactiveCommand.Create(ExecuteSave, ValidationContext.Valid);
        Cancel = ReactiveCommand.CreateFromTask(ExecuteCancel);
        RemoveDevice = ReactiveCommand.CreateFromTask(ExecuteRemoveDevice);

        PropertyChanged += (_, _) => _hasChanges = true;
    }

    private void ExecuteSave()
    {
        if (HasErrors)
            return;

        _device.Hostname = Hostname;
        _device.Model = Model;
        _device.AuthToken = AuthToken;
        _device.Brightness = Brightness;

        Close(DeviceDialogResult.Save);
    }

    private async Task ExecuteCancel()
    {
        if (_hasChanges)
        {
            bool confirmed = await _windowService.ShowConfirmContentDialog("Discard changes?",
                "Are you sure you want to discard your changes?");
            if (confirmed)
                Close(DeviceDialogResult.Cancel);
        }
        else
            Close(DeviceDialogResult.Cancel);
    }

    private async Task ExecuteRemoveDevice()
    {
        bool confirmed = await _windowService.ShowConfirmContentDialog("Remove device?",
            "Are you sure you want to remove this device?");
        if (!confirmed)
            return;

        PluginSetting<List<DeviceDefinition>> definitions =
            _settings.GetSetting("DeviceDefinitions", new List<DeviceDefinition>());
        definitions.Value?.Remove(_device);
        Close(DeviceDialogResult.Remove);
    }

}
public enum DeviceDialogResult
{
    Save,
    Cancel,
    Remove
}
