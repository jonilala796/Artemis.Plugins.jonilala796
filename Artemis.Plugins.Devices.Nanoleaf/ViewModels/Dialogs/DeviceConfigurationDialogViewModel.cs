using System;
using System.Reactive;
using System.Threading.Tasks;
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
    private readonly IWindowService _windowService;

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

    private byte _transitionTime;

    /// <summary>
    /// Fade time between colors in steps of 100 ms.
    /// </summary>
    public byte TransitionTime
    {
        get => _transitionTime;
        set => RaiseAndSetIfChanged(ref _transitionTime, value);
    }

    private int _maxUpdateRate;

    /// <summary>
    /// Maximum number of updates per second. 0 means unlimited.
    /// </summary>
    public int MaxUpdateRate
    {
        get => _maxUpdateRate;
        set => RaiseAndSetIfChanged(ref _maxUpdateRate, value);
    }

    public ReactiveCommand<Unit, Unit> Save { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }
    public ReactiveCommand<Unit, Unit> RemoveDevice { get; }

    public DeviceConfigurationDialogViewModel(DeviceDefinition device, IWindowService windowService)
    {
        _device = device;
        Device = device;
        _windowService = windowService;

        _hostname = device.Hostname;
        _model = device.Model;
        _authToken = device.AuthToken;
        // 0 is the default of devices added before brightness could be configured and means full brightness
        _brightness = device.Brightness > 0 ? device.Brightness : (byte)100;
        _transitionTime = device.TransitionTime;
        _maxUpdateRate = device.MaxUpdateRate;

        this.ValidationRule(vm => vm.Hostname, IsValidHostname, "A valid IP address or hostname is required");

        Save = ReactiveCommand.Create(ExecuteSave, ValidationContext.Valid);
        Cancel = ReactiveCommand.CreateFromTask(ExecuteCancel);
        RemoveDevice = ReactiveCommand.CreateFromTask(ExecuteRemoveDevice);
    }

    private bool HasChanges => Hostname != _device.Hostname ||
                               Model != _device.Model ||
                               AuthToken != _device.AuthToken ||
                               Brightness != (_device.Brightness > 0 ? _device.Brightness : 100) ||
                               TransitionTime != _device.TransitionTime ||
                               MaxUpdateRate != _device.MaxUpdateRate;

    private static bool IsValidHostname(string? hostname) =>
        !string.IsNullOrWhiteSpace(hostname) && Uri.CheckHostName(hostname.Trim()) != UriHostNameType.Unknown;

    private void ExecuteSave()
    {
        if (HasErrors)
            return;

        _device.Hostname = Hostname.Trim();
        _device.Model = Model;
        _device.AuthToken = AuthToken.Trim();
        _device.Brightness = Brightness;
        _device.TransitionTime = TransitionTime;
        _device.MaxUpdateRate = MaxUpdateRate;

        Close(DeviceDialogResult.Save);
    }

    private async Task ExecuteCancel()
    {
        if (HasChanges)
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

        Close(DeviceDialogResult.Remove);
    }

}
public enum DeviceDialogResult
{
    Save,
    Cancel,
    Remove
}
