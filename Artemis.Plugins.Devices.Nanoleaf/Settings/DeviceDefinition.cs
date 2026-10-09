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
    
    private byte _brightness = 100;
    public byte Brightness
    {
        get => _brightness;
        set => SetAndNotify(ref _brightness, value);
    }

    private byte _transitionTime;

    /// <summary>
    /// Fade time between colors in steps of 100 ms. 0 changes colors instantly.
    /// </summary>
    public byte TransitionTime
    {
        get => _transitionTime;
        set => SetAndNotify(ref _transitionTime, value);
    }

    private int _maxUpdateRate;

    /// <summary>
    /// Maximum number of updates per second sent to the device. 0 means unlimited.
    /// </summary>
    public int MaxUpdateRate
    {
        get => _maxUpdateRate;
        set => SetAndNotify(ref _maxUpdateRate, value);
    }
}