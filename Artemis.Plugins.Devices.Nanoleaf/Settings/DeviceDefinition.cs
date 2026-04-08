using Artemis.Core;

namespace Artemis.Plugins.Devices.Nanoleaf.Settings;

public class DeviceDefinition : CorePropertyChanged
{
    private string _hostname = "";

    public string Hostname
    {
        get => _hostname;
        set => SetAndNotify(ref _hostname, value);
    }

    private string _model = "";

    public string Model
    {
        get => _model;
        set => SetAndNotify(ref _model, value);
    }

    private string _authToken = "";

    public string AuthToken
    {
        get => _authToken;
        set => SetAndNotify(ref _authToken, value);
    }
    
    private byte _brightness;
    public byte Brightness
    {
        get => _brightness;
        set => SetAndNotify(ref _brightness, value);
    }
}