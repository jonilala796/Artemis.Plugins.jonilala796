using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Plugins.Devices.Nanoleaf.Helper;
using Artemis.Plugins.Devices.Nanoleaf.Settings;
using Artemis.UI.Shared;
using Avalonia.Threading;
using ReactiveUI;

namespace Artemis.Plugins.Devices.Nanoleaf.ViewModels.Dialogs;

public class DiscoveredDeviceEntry : ReactiveObject
{
    private bool _isSelected = true;

    public required string Address { get; init; }
    public required string Model { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

public class DiscoverDevicesDialogViewModel : DialogViewModelBase<List<DeviceDefinition>>
{
    // Cancelled when the dialog closes — stops all scanning permanently.
    private readonly CancellationTokenSource _dialogCts = new();

    // Cancelled to abort the current scan cycle and restart immediately (Retry).
    private CancellationTokenSource _cycleCts = new();

    // Tracks addresses seen across all cycles so entries are never duplicated.
    private readonly HashSet<string> _seenAddresses = new(StringComparer.OrdinalIgnoreCase);

    private bool _isDiscovering;
    private string _statusMessage = "Starting discovery...";

    public ObservableCollection<DiscoveredDeviceEntry> DiscoveredDevices { get; } = [];

    public bool IsDiscovering
    {
        get => _isDiscovering;
        set => this.RaiseAndSetIfChanged(ref _isDiscovering, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> AddSelected { get; }
    public ReactiveCommand<Unit, Unit> Retry { get; }
    public ReactiveCommand<Unit, Unit> Cancel { get; }

    public DiscoverDevicesDialogViewModel()
    {
        AddSelected = ReactiveCommand.Create(ExecuteAddSelected);
        Retry = ReactiveCommand.Create(ExecuteRetry);
        Cancel = ReactiveCommand.Create(ExecuteCancel);
        _ = RunScanLoop();
    }

    private async Task RunScanLoop()
    {
        while (!_dialogCts.IsCancellationRequested)
        {
            _cycleCts.Dispose();
            _cycleCts = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_dialogCts.Token, _cycleCts.Token);

            IsDiscovering = true;
            int countAtStart = DiscoveredDevices.Count;
            StatusMessage = countAtStart == 0
                ? "Searching for devices..."
                : $"Found {countAtStart} device(s). Scanning for more...";

            string? error = null;
            try
            {
                await NanoleafDiscoveryHelper.DiscoverAllDevicesAsync(OnDeviceFound, linked.Token);
            }
            catch (OperationCanceledException)
            {
                // Either the dialog is closing or Retry was pressed — handled below.
            }
            catch (Exception e)
            {
                // Keep scanning: the network may become available again, e.g. after a Wi-Fi reconnect.
                error = e.Message;
            }

            if (_dialogCts.IsCancellationRequested)
                break;

            // Pause between cycles; Retry cancels the delay so the next cycle starts immediately.
            IsDiscovering = false;
            if (error != null)
                StatusMessage = $"Discovery failed: {error}. Retrying shortly...";
            else
                StatusMessage = DiscoveredDevices.Count == 0
                    ? "No devices found. Scanning again shortly..."
                    : $"Found {DiscoveredDevices.Count} device(s). Scanning again shortly...";

            try
            {
                await Task.Delay(3000, linked.Token);
            }
            catch (OperationCanceledException)
            {
                if (_dialogCts.IsCancellationRequested)
                    break;
                // Retry was pressed — loop immediately.
            }
        }

        IsDiscovering = false;
    }

    private void OnDeviceFound((string address, string model) device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_seenAddresses.Add(device.address))
                return;

            DiscoveredDevices.Add(new DiscoveredDeviceEntry { Address = device.address, Model = device.model });
            StatusMessage = $"Found {DiscoveredDevices.Count} device(s)...";
        });
    }

    private void ExecuteRetry()
    {
        _cycleCts.Cancel();
    }

    /// <summary>
    /// Stops discovery permanently. Called when the dialog closes, including when the window itself is closed.
    /// </summary>
    public void StopDiscovery()
    {
        if (!_dialogCts.IsCancellationRequested)
            _dialogCts.Cancel();
    }

    private void ExecuteAddSelected()
    {
        StopDiscovery();
        var result = DiscoveredDevices
            .Where(d => d.IsSelected)
            .Select(d => new DeviceDefinition { Hostname = d.Address, Model = d.Model, Brightness = 100 })
            .ToList();
        Close(result);
    }

    private void ExecuteCancel()
    {
        StopDiscovery();
        Close([]);
    }
}

