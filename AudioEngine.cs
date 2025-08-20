using System;
using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using AudioEffectsStudio.Effects;

namespace AudioEffectsStudio.Audio
{
    /// <summary>
    /// Core audio processing engine for device routing and effects
    /// </summary>
    public static class AudioEngine
    {
        #region Public Properties
        
        /// <summary>
        /// Currently active audio effects
        /// </summary>
        public static HashSet<string> ActiveEffects { get; set; } = new HashSet<string>();
        
        /// <summary>
        /// Global effects intensity (0.0 to 1.0)
        /// </summary>
        public static float EffectIntensity { get; set; } = 0.5f;
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Routes system default input to system default output with effects
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartSystemInputToSystemOutputRouting(CancellationToken cancellationToken = default)
        {
            // This would typically be handled by Windows audio system
            // For demonstration, we'll use loopback capture and default output
            using var waveIn = new WasapiLoopbackCapture();
            using var waveOut = new WaveOutEvent();
            
            var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            
            var sampleProvider = bufferedWaveProvider.ToSampleProvider();
            var effectsProvider = new EffectsProcessor(sampleProvider);
            
            waveOut.Init(effectsProvider);

            waveIn.DataAvailable += (sender, args) =>
            {
                bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
            };

            waveOut.Play();
            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveOut.Stop();
            }
        }

        /// <summary>
        /// Routes selected input device to system default output with effects
        /// </summary>
        /// <param name="inputDeviceIndex">Input device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartInputDeviceToSystemOutputRouting(int inputDeviceIndex, CancellationToken cancellationToken = default)
        {
            using var waveIn = new WaveInEvent 
            { 
                DeviceNumber = inputDeviceIndex, 
                WaveFormat = new WaveFormat(44100, 2),
                BufferMilliseconds = 50
            };
            
            using var waveOut = new WaveOutEvent(); // System default output
            
            var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            
            var sampleProvider = bufferedWaveProvider.ToSampleProvider();
            var effectsProvider = new EffectsProcessor(sampleProvider);
            
            waveOut.Init(effectsProvider);

            waveIn.DataAvailable += (sender, args) =>
            {
                bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
            };

            waveOut.Play();
            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveOut.Stop();
            }
        }

        /// <summary>
        /// Routes system default input to selected output device with effects
        /// </summary>
        /// <param name="outputDeviceIndex">Output device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartSystemInputToOutputDeviceRouting(int outputDeviceIndex, CancellationToken cancellationToken = default)
        {
            using var waveIn = new WasapiLoopbackCapture(); // System default input (what's playing)
            using var waveOut = new WaveOut 
            { 
                DeviceNumber = outputDeviceIndex,
                DesiredLatency = 100 
            };
            
            var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            
            var sampleProvider = bufferedWaveProvider.ToSampleProvider();
            var effectsProvider = new EffectsProcessor(sampleProvider);
            
            waveOut.Init(effectsProvider);

            waveIn.DataAvailable += (sender, args) =>
            {
                bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
            };

            waveOut.Play();
            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveOut.Stop();
            }
        }

        /// <summary>
        /// Routes microphone input to selected output device with effects
        /// </summary>
        /// <param name="micDeviceIndex">Microphone device index</param>
        /// <param name="outputDeviceIndex">Output device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartMicrophoneToDeviceRouting(int micDeviceIndex, int outputDeviceIndex, CancellationToken cancellationToken = default)
        {
            // Setup input capture from selected microphone device
            using var waveIn = new WaveInEvent 
            { 
                DeviceNumber = micDeviceIndex, 
                WaveFormat = new WaveFormat(44100, 2), // Stereo for better compatibility
                BufferMilliseconds = 50
            };
            
            // Setup output to selected device using WaveOut for device selection
            using var waveOut = new WaveOut 
            { 
                DeviceNumber = outputDeviceIndex,
                DesiredLatency = 100 
            };
            
            var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            
            var sampleProvider = bufferedWaveProvider.ToSampleProvider();
            var effectsProvider = new EffectsProcessor(sampleProvider);
            
            waveOut.Init(effectsProvider);

            waveIn.DataAvailable += (sender, args) =>
            {
                bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
            };

            waveOut.Play();
            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveOut.Stop();
            }
        }
        
        /// <summary>
        /// Routes system audio (default playback device) to selected output device with effects
        /// This captures whatever is playing on the system and routes it with effects
        /// </summary>
        /// <param name="outputDeviceIndex">Output device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartSystemAudioToDeviceRouting(int outputDeviceIndex, CancellationToken cancellationToken = default)
        {
            // Capture system audio using WASAPI loopback (captures default playback device)
            using var waveIn = new WasapiLoopbackCapture();
            
            // Setup output to selected device using WaveOut for device selection
            using var waveOut = new WaveOut 
            { 
                DeviceNumber = outputDeviceIndex,
                DesiredLatency = 100 
            };
            
            var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            
            var sampleProvider = bufferedWaveProvider.ToSampleProvider();
            var effectsProvider = new EffectsProcessor(sampleProvider);
            
            waveOut.Init(effectsProvider);

            waveIn.DataAvailable += (sender, args) =>
            {
                bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
            };

            waveOut.Play();
            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveOut.Stop();
            }
        }

        /// <summary>
        /// Routes system audio output with effects processing
        /// </summary>
        /// <param name="outputDeviceIndex">Output device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartSystemAudioRouting(int outputDeviceIndex = 0, CancellationToken cancellationToken = default)
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var outputDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            if (outputDeviceIndex < 0 || outputDeviceIndex >= outputDevices.Count)
                return;

            using var waveIn = new WasapiLoopbackCapture();
            using var waveOut = new WasapiOut(outputDevices[outputDeviceIndex], AudioClientShareMode.Shared, false, 200);
            
            var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            
            var sampleProvider = bufferedWaveProvider.ToSampleProvider();
            var effectsProvider = new EffectsProcessor(sampleProvider);
            
            waveOut.Init(effectsProvider);
            waveOut.Play();

            waveIn.DataAvailable += (sender, args) =>
            {
                bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
            };

            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveOut.Stop();
            }
        }

        /// <summary>
        /// Routes audio from selected input device to selected output device with effects
        /// This is the main method that handles device-to-device audio routing
        /// </summary>
        /// <param name="inputDeviceIndex">Input device index</param>
        /// <param name="outputDeviceIndex">Output device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartDeviceToDeviceRouting(int inputDeviceIndex, int outputDeviceIndex, CancellationToken cancellationToken = default)
        {
            // Setup input capture from selected device
            using var waveIn = new WaveInEvent 
            { 
                DeviceNumber = inputDeviceIndex, 
                WaveFormat = new WaveFormat(44100, 2), // Stereo for better compatibility
                BufferMilliseconds = 50
            };
            
            // Setup output to selected device using WaveOut for device selection
            using var waveOut = new WaveOut 
            { 
                DeviceNumber = outputDeviceIndex,
                DesiredLatency = 100 
            };
            
            var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            
            var sampleProvider = bufferedWaveProvider.ToSampleProvider();
            var effectsProvider = new EffectsProcessor(sampleProvider);
            
            waveOut.Init(effectsProvider);

            waveIn.DataAvailable += (sender, args) =>
            {
                bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
            };

            waveOut.Play();
            waveIn.StartRecording();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            finally
            {
                waveIn.StopRecording();
                waveOut.Stop();
            }
        }
        
        #endregion
    }
}
