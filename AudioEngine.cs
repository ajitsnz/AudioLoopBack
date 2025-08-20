using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
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
        /// Enhanced for Bluetooth device support using WASAPI
        /// </summary>
        /// <param name="inputDeviceIndex">Input device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartInputDeviceToSystemOutputRouting(int inputDeviceIndex, CancellationToken cancellationToken = default)
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var captureDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            // Validate device index
            if (inputDeviceIndex < 0 || inputDeviceIndex >= captureDevices.Count)
            {
                // Fallback to legacy method if WASAPI index is invalid
                using var fallbackWaveIn = new WaveInEvent 
                { 
                    DeviceNumber = Math.Max(0, Math.Min(inputDeviceIndex, WaveInEvent.DeviceCount - 1)), 
                    WaveFormat = new WaveFormat(44100, 2),
                    BufferMilliseconds = 50
                };
                
                using var fallbackWaveOut = new WaveOutEvent(); // System default output
                
                var fallbackBufferedProvider = new BufferedWaveProvider(fallbackWaveIn.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(5),
                    DiscardOnBufferOverflow = true
                };
                
                var fallbackSampleProvider = fallbackBufferedProvider.ToSampleProvider();
                var fallbackEffectsProvider = new EffectsProcessor(fallbackSampleProvider);
                
                fallbackWaveOut.Init(fallbackEffectsProvider);

                fallbackWaveIn.DataAvailable += (sender, args) =>
                {
                    fallbackBufferedProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
                };

                fallbackWaveOut.Play();
                fallbackWaveIn.StartRecording();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    fallbackWaveIn.StopRecording();
                    fallbackWaveOut.Stop();
                }
                return;
            }

            var inputDevice = captureDevices[inputDeviceIndex];

            try
            {
                // Check if device is still available before starting
                if (inputDevice.State != DeviceState.Active)
                {
                    throw new InvalidOperationException($"Audio input device '{inputDevice.FriendlyName}' is not active or has been disconnected.");
                }

                // Use WASAPI for better Bluetooth support
                using var wasapiIn = new WasapiCapture(inputDevice);
                using var waveOut = new WaveOutEvent(); // System default output
                
                var bufferedWaveProvider = new BufferedWaveProvider(wasapiIn.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(3), // Shorter buffer for Bluetooth
                    DiscardOnBufferOverflow = true
                };
                
                var sampleProvider = bufferedWaveProvider.ToSampleProvider();
                var effectsProvider = new EffectsProcessor(sampleProvider);
                
                waveOut.Init(effectsProvider);

                wasapiIn.DataAvailable += (sender, args) =>
                {
                    try
                    {
                        bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
                    }
                    catch
                    {
                        // Ignore buffer errors during device disconnect
                    }
                };

                waveOut.Play();
                wasapiIn.StartRecording();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Check device state periodically for Bluetooth devices
                        if (inputDevice.State != DeviceState.Active)
                        {
                            throw new InvalidOperationException("Audio input device disconnected during recording");
                        }
                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    try
                    {
                        wasapiIn.StopRecording();
                        waveOut.Stop();
                    }
                    catch
                    {
                        // Ignore errors during cleanup if device is already gone
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x8889000F))
            {
                // AUDCLNT_E_DEVICE_INVALIDATED - Device was disconnected
                throw new InvalidOperationException($"Audio input device '{inputDevice.FriendlyName}' was disconnected. Please reconnect the device and try again.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x88890008))
            {
                // AUDCLNT_E_DEVICE_IN_USE - Device is being used by another application
                throw new InvalidOperationException($"Audio input device '{inputDevice.FriendlyName}' is currently in use by another application.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                // Other WASAPI errors - provide helpful message
                throw new InvalidOperationException($"Failed to access audio input device '{inputDevice.FriendlyName}'. Error code: 0x{ex.HResult:X}. The device may be disconnected or in use by another application.", ex);
            }
        }

        /// <summary>
        /// Routes system default input to selected output device with effects
        /// Enhanced for Bluetooth device support using WASAPI
        /// Uses system audio capture approach for better Bluetooth compatibility
        /// </summary>
        /// <param name="outputDeviceIndex">Output device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartSystemInputToOutputDeviceRouting(int outputDeviceIndex, CancellationToken cancellationToken = default)
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var renderDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            // Validate device index
            if (outputDeviceIndex < 0 || outputDeviceIndex >= renderDevices.Count)
            {
                // Fallback to legacy method if WASAPI index is invalid
                using var fallbackWaveIn = new WasapiLoopbackCapture(); // System default input (what's playing)
                using var fallbackWaveOut = new WaveOut 
                { 
                    DeviceNumber = Math.Max(0, Math.Min(outputDeviceIndex, WaveOut.DeviceCount - 1)),
                    DesiredLatency = 100 
                };
                
                var fallbackBufferedProvider = new BufferedWaveProvider(fallbackWaveIn.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(5),
                    DiscardOnBufferOverflow = true
                };
                
                var fallbackSampleProvider = fallbackBufferedProvider.ToSampleProvider();
                var fallbackEffectsProvider = new EffectsProcessor(fallbackSampleProvider);
                
                fallbackWaveOut.Init(fallbackEffectsProvider);

                fallbackWaveIn.DataAvailable += (sender, args) =>
                {
                    fallbackBufferedProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
                };

                fallbackWaveOut.Play();
                fallbackWaveIn.StartRecording();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    fallbackWaveIn.StopRecording();
                    fallbackWaveOut.Stop();
                }
                return;
            }

            var outputDevice = renderDevices[outputDeviceIndex];
            
            // Check if this is a Bluetooth device
            bool isBluetooth = outputDevice.FriendlyName.ToLower().Contains("bluetooth") || 
                              outputDevice.DeviceFriendlyName.ToLower().Contains("bluetooth") ||
                              outputDevice.FriendlyName.ToLower().Contains("wireless") ||
                              outputDevice.FriendlyName.ToLower().Contains("headphones") ||
                              outputDevice.FriendlyName.ToLower().Contains("earbuds");
            
            if (isBluetooth)
            {
                // Special handling for Bluetooth devices
                StartSystemToBluetoothRouting(outputDevice, cancellationToken);
            }
            else
            {
                // Standard routing for non-Bluetooth devices
                StartStandardSystemToDeviceRouting(outputDevice, cancellationToken);
            }
        }

        /// <summary>
        /// Special routing method for Bluetooth output devices
        /// Uses optimized settings for Bluetooth compatibility
        /// </summary>
        private static void StartSystemToBluetoothRouting(MMDevice bluetoothDevice, CancellationToken cancellationToken)
        {
            try
            {
                // Use system loopback capture (captures what Windows is playing)
                using var loopbackCapture = new WasapiLoopbackCapture();
                
                // For Bluetooth, we need to be very careful with formats and buffers
                // Use a format that Bluetooth devices commonly support
                var bluetoothFormat = new WaveFormat(44100, 16, 2); // 44.1kHz, 16-bit, stereo
                
                var bufferedProvider = new BufferedWaveProvider(bluetoothFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(5), // Large buffer for Bluetooth stability
                    DiscardOnBufferOverflow = true
                };
                
                // Try WasapiOut first, then fallback to WaveOut for Bluetooth
                IWavePlayer? waveOut = null;
                try
                {
                    // Try WASAPI first for modern Bluetooth devices
                    waveOut = new WasapiOut(bluetoothDevice, AudioClientShareMode.Shared, false, 300);
                }
                catch
                {
                    // Fallback to standard WaveOut for problematic Bluetooth devices
                    var deviceIndex = GetWaveOutDeviceIndex(bluetoothDevice.FriendlyName);
                    waveOut = new WaveOut 
                    { 
                        DeviceNumber = deviceIndex,
                        DesiredLatency = 300 // High latency for Bluetooth stability
                    };
                }
                
                var sampleProvider = bufferedProvider.ToSampleProvider();
                var effectsProvider = new EffectsProcessor(sampleProvider);
                
                waveOut.Init(effectsProvider);

                loopbackCapture.DataAvailable += (sender, args) =>
                {
                    try
                    {
                        // Convert captured system audio to Bluetooth-compatible format
                        var convertedData = ConvertToCompatibleFormat(args.Buffer, args.BytesRecorded, loopbackCapture.WaveFormat, bluetoothFormat);
                        bufferedProvider.AddSamples(convertedData, 0, convertedData.Length);
                    }
                    catch
                    {
                        // Ignore conversion errors for stability
                    }
                };

                waveOut.Play();
                loopbackCapture.StartRecording();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Check Bluetooth device state less frequently to avoid issues
                        try
                        {
                            if (bluetoothDevice.State != DeviceState.Active)
                            {
                                throw new InvalidOperationException($"Bluetooth device '{bluetoothDevice.FriendlyName}' disconnected");
                            }
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            // Bluetooth device state check failed - likely disconnected
                            throw new InvalidOperationException("Bluetooth device became unavailable");
                        }
                        Thread.Sleep(1000); // Check less frequently for Bluetooth stability
                    }
                }
                finally
                {
                    try
                    {
                        loopbackCapture.StopRecording();
                        waveOut.Stop();
                        waveOut.Dispose();
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x8889000F))
            {
                throw new InvalidOperationException($"Bluetooth device '{bluetoothDevice.FriendlyName}' connection failed. Try: 1) Reconnecting the device, 2) Restarting Bluetooth service, 3) Using the device as Windows default audio output first.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x88890008))
            {
                throw new InvalidOperationException($"Bluetooth device '{bluetoothDevice.FriendlyName}' is in use by another application. Close other audio apps and try again.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                throw new InvalidOperationException($"Bluetooth routing failed (Error: 0x{ex.HResult:X}). Try: 1) Making the Bluetooth device your default Windows audio output, 2) Restarting the application, 3) Re-pairing the Bluetooth device.", ex);
            }
        }

        /// <summary>
        /// Standard routing for non-Bluetooth output devices
        /// </summary>
        private static void StartStandardSystemToDeviceRouting(MMDevice outputDevice, CancellationToken cancellationToken)
        {
            try
            {
                // Check if device is still available before starting
                if (outputDevice.State != DeviceState.Active)
                {
                    throw new InvalidOperationException($"Audio device '{outputDevice.FriendlyName}' is not active or has been disconnected.");
                }

                // Use WASAPI for better Bluetooth support with retry mechanism
                using var waveIn = new WasapiLoopbackCapture(); // System default input (what's playing)
                using var wasapiOut = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, 100);
                
                var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(3), // Shorter buffer for non-Bluetooth
                    DiscardOnBufferOverflow = true
                };
                
                var sampleProvider = bufferedWaveProvider.ToSampleProvider();
                var effectsProvider = new EffectsProcessor(sampleProvider);
                
                wasapiOut.Init(effectsProvider);

                waveIn.DataAvailable += (sender, args) =>
                {
                    try
                    {
                        bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
                    }
                    catch
                    {
                        // Ignore buffer errors during device disconnect
                    }
                };

                wasapiOut.Play();
                waveIn.StartRecording();

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Check device state periodically
                        if (outputDevice.State != DeviceState.Active)
                        {
                            throw new InvalidOperationException("Audio device disconnected during playback");
                        }
                        Thread.Sleep(100);
                    }
                }
                finally
                {
                    try
                    {
                        waveIn.StopRecording();
                        wasapiOut.Stop();
                    }
                    catch
                    {
                        // Ignore errors during cleanup if device is already gone
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x8889000F))
            {
                throw new InvalidOperationException($"Audio device '{outputDevice.FriendlyName}' was disconnected. Please reconnect the device and try again.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x88890008))
            {
                throw new InvalidOperationException($"Audio device '{outputDevice.FriendlyName}' is currently in use by another application.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                throw new InvalidOperationException($"Failed to access audio device '{outputDevice.FriendlyName}'. Error code: 0x{ex.HResult:X}. The device may be disconnected or in use by another application.", ex);
            }
        }

        /// <summary>
        /// Get WaveOut device index by friendly name for Bluetooth fallback
        /// </summary>
        private static int GetWaveOutDeviceIndex(string friendlyName)
        {
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(i);
                    if (caps.ProductName.Contains(friendlyName.Split(' ')[0]) || 
                        friendlyName.Contains(caps.ProductName))
                    {
                        return i;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return 0; // Default device
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
        /// Enhanced for multiple Bluetooth devices with compatibility workarounds
        /// </summary>
        /// <param name="inputDeviceIndex">Input device index</param>
        /// <param name="outputDeviceIndex">Output device index</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static void StartDeviceToDeviceRouting(int inputDeviceIndex, int outputDeviceIndex, CancellationToken cancellationToken = default)
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            var captureDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            var renderDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            // Validate device indices
            if (inputDeviceIndex < 0 || inputDeviceIndex >= captureDevices.Count ||
                outputDeviceIndex < 0 || outputDeviceIndex >= renderDevices.Count)
            {
                throw new ArgumentException("Invalid device index");
            }

            var inputDevice = captureDevices[inputDeviceIndex];
            var outputDevice = renderDevices[outputDeviceIndex];

            // Check if both devices are Bluetooth - this requires special handling
            bool inputIsBluetooth = inputDevice.FriendlyName.ToLower().Contains("bluetooth") || 
                                   inputDevice.DeviceFriendlyName.ToLower().Contains("bluetooth") ||
                                   inputDevice.FriendlyName.ToLower().Contains("wireless");
            bool outputIsBluetooth = outputDevice.FriendlyName.ToLower().Contains("bluetooth") || 
                                     outputDevice.DeviceFriendlyName.ToLower().Contains("bluetooth") ||
                                     outputDevice.FriendlyName.ToLower().Contains("wireless");

            if (inputIsBluetooth && outputIsBluetooth)
            {
                // Special routing for Bluetooth-to-Bluetooth
                StartBluetoothToBluetoothRouting(inputDevice, outputDevice, cancellationToken);
            }
            else
            {
                // Standard routing for non-Bluetooth or mixed scenarios
                StartStandardDeviceRouting(inputDevice, outputDevice, cancellationToken);
            }
        }

        /// <summary>
        /// Special routing method for Bluetooth-to-Bluetooth scenarios
        /// Uses a different approach to work around Windows Bluetooth limitations
        /// </summary>
        private static void StartBluetoothToBluetoothRouting(MMDevice inputDevice, MMDevice outputDevice, CancellationToken cancellationToken)
        {
            try
            {
                // For Bluetooth-to-Bluetooth routing, we need to be more conservative
                // Use event-driven mode with longer buffers
                using var waveIn = new WasapiCapture(inputDevice, false, 300); // 300ms buffer
                
                // Initialize and get format
                var tempBuffer = new List<byte>();
                var formatDetected = false;
                WaveFormat? inputFormat = null;

                waveIn.DataAvailable += (sender, args) =>
                {
                    if (!formatDetected)
                    {
                        inputFormat = waveIn.WaveFormat;
                        formatDetected = true;
                    }
                    
                    for (int i = 0; i < args.BytesRecorded; i++)
                    {
                        tempBuffer.Add(args.Buffer[i]);
                    }
                };

                waveIn.StartRecording();
                
                // Wait for format detection
                var timeout = DateTime.Now.AddSeconds(2);
                while (!formatDetected && DateTime.Now < timeout && !cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(50);
                }
                
                waveIn.StopRecording();
                
                if (!formatDetected || inputFormat == null)
                {
                    throw new InvalidOperationException("Could not detect input device format. The Bluetooth device may not be properly configured for recording.");
                }

                // Now set up output with detected format
                using var wasapiOut = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, 300);
                
                // Use compatible format for Bluetooth devices
                var bluetoothFormat = new WaveFormat(44100, 16, 2);
                
                var bufferedWaveProvider = new BufferedWaveProvider(bluetoothFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(4), // Larger buffer for Bluetooth stability
                    DiscardOnBufferOverflow = true
                };
                
                var sampleProvider = bufferedWaveProvider.ToSampleProvider();
                var effectsProvider = new EffectsProcessor(sampleProvider);
                
                wasapiOut.Init(effectsProvider);

                // Clear temp buffer and restart with proper routing
                tempBuffer.Clear();
                
                waveIn.DataAvailable += (sender, args) =>
                {
                    try
                    {
                        // Convert to Bluetooth-friendly format
                        var convertedData = ConvertToCompatibleFormat(args.Buffer, args.BytesRecorded, inputFormat, bluetoothFormat);
                        bufferedWaveProvider.AddSamples(convertedData, 0, convertedData.Length);
                    }
                    catch
                    {
                        // Silently ignore conversion errors for Bluetooth stability
                    }
                };

                wasapiOut.Play();
                waveIn.StartRecording();

                // Monitor both devices more frequently for Bluetooth
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (inputDevice.State != DeviceState.Active || outputDevice.State != DeviceState.Active)
                        {
                            throw new InvalidOperationException($"Bluetooth device connection lost: Input='{inputDevice.FriendlyName}' Output='{outputDevice.FriendlyName}'");
                        }
                        Thread.Sleep(50); // More frequent checks for Bluetooth
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        // Device state check failed - device likely disconnected
                        throw new InvalidOperationException("Bluetooth device became unavailable during audio routing");
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x8889000F))
            {
                throw new InvalidOperationException($"Bluetooth device '{inputDevice.FriendlyName}' or '{outputDevice.FriendlyName}' connection failed. This can happen when both devices are Bluetooth. Try: 1) Using one wired device, 2) Restarting Bluetooth service, 3) Re-pairing devices.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x88890008))
            {
                throw new InvalidOperationException($"One of the Bluetooth devices is in use. Only one application can use a Bluetooth audio device at a time.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                throw new InvalidOperationException($"Bluetooth routing failed. Error: 0x{ex.HResult:X}. Try using devices one at a time or restart the Bluetooth service.", ex);
            }
        }

        /// <summary>
        /// Standard routing for non-Bluetooth devices or mixed scenarios
        /// </summary>
        private static void StartStandardDeviceRouting(MMDevice inputDevice, MMDevice outputDevice, CancellationToken cancellationToken)
        {
            try
            {
                // Check device states
                if (inputDevice.State != DeviceState.Active || outputDevice.State != DeviceState.Active)
                {
                    throw new InvalidOperationException("One or both devices are not active or have been disconnected.");
                }

                // Standard routing with reasonable buffer sizes
                using var waveIn = new WasapiCapture(inputDevice, false, 100);
                using var wasapiOut = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, 100);
                
                var commonFormat = new WaveFormat(44100, 16, 2);
                
                var bufferedWaveProvider = new BufferedWaveProvider(commonFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(2),
                    DiscardOnBufferOverflow = true
                };
                
                var sampleProvider = bufferedWaveProvider.ToSampleProvider();
                var effectsProvider = new EffectsProcessor(sampleProvider);
                
                wasapiOut.Init(effectsProvider);

                waveIn.DataAvailable += (sender, args) =>
                {
                    try
                    {
                        var convertedData = ConvertToCompatibleFormat(args.Buffer, args.BytesRecorded, waveIn.WaveFormat, commonFormat);
                        bufferedWaveProvider.AddSamples(convertedData, 0, convertedData.Length);
                    }
                    catch
                    {
                        // Ignore errors during device disconnect
                    }
                };

                wasapiOut.Play();
                waveIn.StartRecording();

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (inputDevice.State != DeviceState.Active || outputDevice.State != DeviceState.Active)
                    {
                        throw new InvalidOperationException("Device disconnected during audio routing");
                    }
                    Thread.Sleep(100);
                }
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x8889000F))
            {
                throw new InvalidOperationException($"Audio device '{inputDevice.FriendlyName}' or '{outputDevice.FriendlyName}' was disconnected.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x88890008))
            {
                throw new InvalidOperationException($"One of the devices is currently in use by another application.", ex);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                throw new InvalidOperationException($"Failed to establish audio routing. Error: 0x{ex.HResult:X}.", ex);
            }
        }

        /// <summary>
        /// Convert audio buffer to compatible format for Bluetooth devices
        /// Simplified conversion for better stability
        /// </summary>
        private static byte[] ConvertToCompatibleFormat(byte[] buffer, int bytesRecorded, WaveFormat sourceFormat, WaveFormat targetFormat)
        {
            if (sourceFormat.Equals(targetFormat))
                return buffer.Take(bytesRecorded).ToArray();
            
            // Simple format conversion for Bluetooth compatibility
            try
            {
                using var sourceStream = new MemoryStream(buffer, 0, bytesRecorded);
                using var rawSource = new RawSourceWaveStream(sourceStream, sourceFormat);
                
                // Convert to target format using simple conversion
                var convertedProvider = rawSource.ToSampleProvider().ToWaveProvider16();
                
                // If channel count differs, convert
                if (sourceFormat.Channels != targetFormat.Channels)
                {
                    if (sourceFormat.Channels == 1 && targetFormat.Channels == 2)
                    {
                        convertedProvider = new MonoToStereoProvider16(convertedProvider);
                    }
                    else if (sourceFormat.Channels == 2 && targetFormat.Channels == 1)
                    {
                        convertedProvider = new StereoToMonoProvider16(convertedProvider);
                    }
                }
                
                using var memoryStream = new MemoryStream();
                var tempBuffer = new byte[4096];
                int bytesRead;
                
                while ((bytesRead = convertedProvider.Read(tempBuffer, 0, tempBuffer.Length)) > 0)
                {
                    memoryStream.Write(tempBuffer, 0, bytesRead);
                }
                
                return memoryStream.ToArray();
            }
            catch
            {
                // Fallback: return original buffer (best effort)
                return buffer.Take(bytesRecorded).ToArray();
            }
        }
        
        #endregion
    }
}
