using System;
using NAudio.Wave;

class Program
{
    static void Main(string[] args)
    {
        var micThread = new System.Threading.Thread(LoopMicrophone);
        var dupThread = new System.Threading.Thread(DuplicateAudio);
        micThread.Start();
        dupThread.Start();
        micThread.Join();
        dupThread.Join();
    }

    static void LoopMicrophone()
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
    
    static void DuplicateAudio()
    { 
        
 var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active);
            Console.WriteLine("Available output devices:");
            for (int i = 0; i < devices.Count; i++)
                Console.WriteLine($"{i}: {devices[i].FriendlyName}");
            Console.Write("Enter the device number to duplicate output to: ");
            int deviceNumber = -1;
            int.TryParse(Console.ReadLine(), out deviceNumber);

            using var waveIn = new WasapiLoopbackCapture();
            NAudio.Wave.BufferedWaveProvider buffer = null;
            NAudio.Wave.WasapiOut waveOut = null;
            if (deviceNumber >= 0 && deviceNumber < devices.Count)
            {
                buffer = new NAudio.Wave.BufferedWaveProvider(waveIn.WaveFormat);
                waveOut = new NAudio.Wave.WasapiOut(devices[deviceNumber], NAudio.CoreAudioApi.AudioClientShareMode.Shared, false, 200);
                waveOut.Init(buffer);
                waveOut.Play();
            }

            waveIn.DataAvailable += (s, e) =>
            {
                buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };

            waveIn.StartRecording();
            Console.WriteLine("Duplicating system audio output. Press Enter to stop.");
            Console.ReadLine();
            waveIn.StopRecording();
            waveOut?.Stop();

    }
}
