using NAudio.Wave;

namespace EatsVoiceCompanion.App.Services;

public sealed record AudioInputDevice(
    int DeviceNumber,
    string Name)
{
    public string DisplayName => $"{DeviceNumber}: {Name}";
}

public sealed class MicrophoneService
{
    public IReadOnlyList<AudioInputDevice> GetDevices()
    {
        var devices = new List<AudioInputDevice>();

        for (int deviceNumber = 0;
             deviceNumber < WaveIn.DeviceCount;
             deviceNumber++)
        {
            WaveInCapabilities capabilities =
                WaveIn.GetCapabilities(deviceNumber);

            devices.Add(
                new AudioInputDevice(
                    deviceNumber,
                    capabilities.ProductName));
        }

        return devices;
    }
}