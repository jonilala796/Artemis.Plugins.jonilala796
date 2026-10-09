using System;
using Artemis.Plugins.Devices.Nanoleaf.ViewModels.Dialogs;
using Artemis.UI.Shared;

namespace Artemis.Plugins.Devices.Nanoleaf.Views.Dialogs;

public partial class DiscoverDevicesDialogView : ReactiveAppWindow<DiscoverDevicesDialogViewModel>
{
    public DiscoverDevicesDialogView()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Closing the window with the title bar button bypasses the dialog commands, stop discovery here too.
        ViewModel?.StopDiscovery();
        base.OnClosed(e);
    }
}
