using System;
using NAudio.Wave;

public static class MicLoopback
{
    public static void LoopMicrophone(int defaultInputDevice = 0, System.Threading.CancellationToken? cancelToken = null)
    {
        Console.WriteLine("Microphone Loopback: Capture from one mic and play to another.");
        Console.WriteLine("Available input devices:");
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var deviceInfo = WaveInEvent.GetCapabilities(i);
            Console.WriteLine($"{i}: {deviceInfo.ProductName}");
        }

        int inputDevice = defaultInputDevice;
        if (!string.IsNullOrWhiteSpace(inputDevice.ToString()))
            int.TryParse(inputDevice.ToString(), out inputDevice);

        using var waveIn = new WaveInEvent { DeviceNumber = inputDevice, WaveFormat = new WaveFormat(44100, 1) };
        using var waveOut = new WaveOutEvent();
        var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
        waveOut.Init(bufferedWaveProvider);

        waveIn.DataAvailable += (s, e) =>
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        waveOut.Play();
        waveIn.StartRecording();

        Console.WriteLine("Loopback started. Press Enter to exit or stop from UI.");
        try
        {
            while (cancelToken == null || !cancelToken.Value.IsCancellationRequested)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                    break;
                System.Threading.Thread.Sleep(100);
            }
        }
        catch { }
        finally
        {
            waveIn.StopRecording();
            waveOut.Stop();
        }
    }

    public static void DuplicateAudio(int defaultOutputDevice = 0, System.Threading.CancellationToken? cancelToken = null)
    {
        var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active);
        Console.WriteLine("Available output devices:");
        for (int i = 0; i < devices.Count; i++)
            Console.WriteLine($"{i}: {devices[i].FriendlyName}");

        int deviceNumber = defaultOutputDevice;
        if (!string.IsNullOrWhiteSpace(deviceNumber.ToString()))
            int.TryParse(deviceNumber.ToString(), out deviceNumber);

        using var waveIn = new WasapiLoopbackCapture();
        BufferedWaveProvider? buffer = null;
        WasapiOut? waveOut = null;
        if (deviceNumber >= 0 && deviceNumber < devices.Count)
        {
            buffer = new BufferedWaveProvider(waveIn.WaveFormat);
            waveOut = new WasapiOut(devices[deviceNumber], NAudio.CoreAudioApi.AudioClientShareMode.Shared, false, 200);
            waveOut.Init(buffer);
            waveOut.Play();
        }

        waveIn.DataAvailable += (s, e) =>
        {
            buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        waveIn.StartRecording();
        Console.WriteLine("Duplicating system audio output. Press Enter to stop or stop from UI.");
        try
        {
            while (cancelToken == null || !cancelToken.Value.IsCancellationRequested)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                    break;
                System.Threading.Thread.Sleep(100);
            }
        }
        catch { }
        finally
        {
            waveIn.StopRecording();
            waveOut?.Stop();
        }
    }
}
