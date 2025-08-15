using System;
using NAudio.Wave;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Microphone Loopback: Capture from one mic and play to another.");
        // List input devices
        Console.WriteLine("Available input devices:");
        for (int i = 0; i < NAudio.Wave.WaveInEvent.DeviceCount; i++)
        {
            var deviceInfo = NAudio.Wave.WaveInEvent.GetCapabilities(i);
            Console.WriteLine($"{i}: {deviceInfo.ProductName}");
        }
        // Prompt user for input device selection
        Console.Write("Select input device (mic1) index: ");
        int inputDevice = int.Parse(Console.ReadLine());

        // Set up audio capture and playback (default output device)
        var waveIn = new NAudio.Wave.WaveInEvent { DeviceNumber = inputDevice, WaveFormat = new NAudio.Wave.WaveFormat(44100, 1) };
        var waveOut = new NAudio.Wave.WaveOutEvent(); // Uses default output device
        var bufferedWaveProvider = new NAudio.Wave.BufferedWaveProvider(waveIn.WaveFormat);
        waveOut.Init(bufferedWaveProvider);

        waveIn.DataAvailable += (s, e) =>
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        waveOut.Play();
        waveIn.StartRecording();

        Console.WriteLine("Loopback started. Press Enter to exit.");
        Console.ReadLine();
        waveIn.StopRecording();
        waveOut.Stop();
    }
}
