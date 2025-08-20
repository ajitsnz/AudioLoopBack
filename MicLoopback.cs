using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

public static class MicLoopback
{
    public static string CurrentEffect = "None";
    public static HashSet<string> ActiveEffects = new HashSet<string>();
    public static float EffectIntensity = 0.5f;

    public static void LoopMicrophone(int defaultInputDevice = 0, System.Threading.CancellationToken? cancelToken = null)
    {
        int inputDevice = defaultInputDevice;

        using var waveIn = new WaveInEvent { DeviceNumber = inputDevice, WaveFormat = new WaveFormat(44100, 1) };
        using var waveOut = new WaveOutEvent();
        
        // Create a buffered wave provider
        var bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
        
        // Convert to sample provider for effects processing
        var sampleProvider = bufferedWaveProvider.ToSampleProvider();
        
        // Apply effects based on current selection
        var effectsProvider = new EffectsProvider(sampleProvider);
        
        waveOut.Init(effectsProvider);

        waveIn.DataAvailable += (s, e) =>
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        waveOut.Play();
        waveIn.StartRecording();

        try
        {
            while (cancelToken == null || !cancelToken.Value.IsCancellationRequested)
            {
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

        int deviceNumber = defaultOutputDevice;

        using var waveIn = new WasapiLoopbackCapture();
        BufferedWaveProvider? buffer = null;
        WasapiOut? waveOut = null;
        
        if (deviceNumber >= 0 && deviceNumber < devices.Count)
        {
            buffer = new BufferedWaveProvider(waveIn.WaveFormat);
            
            // Convert to sample provider for effects processing
            var sampleProvider = buffer.ToSampleProvider();
            var effectsProvider = new EffectsProvider(sampleProvider);
            
            waveOut = new WasapiOut(devices[deviceNumber], NAudio.CoreAudioApi.AudioClientShareMode.Shared, false, 200);
            waveOut.Init(effectsProvider);
            waveOut.Play();
        }

        waveIn.DataAvailable += (s, e) =>
        {
            buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        waveIn.StartRecording();
        try
        {
            while (cancelToken == null || !cancelToken.Value.IsCancellationRequested)
            {
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

// Effects Provider that applies real-time audio effects
public class EffectsProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly EchoEffect echoEffect;
    private readonly ReverbEffect reverbEffect;
    private readonly DistortionEffect distortionEffect;
    private readonly ChorusEffect chorusEffect;
    private readonly FlangerEffect flangerEffect;
    private readonly VocalRemovalEffect vocalRemovalEffect;

    public EffectsProvider(ISampleProvider source)
    {
        this.source = source;
        this.echoEffect = new EchoEffect(source.WaveFormat);
        this.reverbEffect = new ReverbEffect(source.WaveFormat);
        this.distortionEffect = new DistortionEffect();
        this.chorusEffect = new ChorusEffect(source.WaveFormat);
        this.flangerEffect = new FlangerEffect(source.WaveFormat);
        this.vocalRemovalEffect = new VocalRemovalEffect();
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        // Apply all active effects in sequence
        foreach (var effectName in MicLoopback.ActiveEffects)
        {
            switch (effectName)
            {
                case "Echo":
                    echoEffect.ProcessSamples(buffer, offset, samplesRead, MicLoopback.EffectIntensity);
                    break;
                case "Reverb":
                    reverbEffect.ProcessSamples(buffer, offset, samplesRead, MicLoopback.EffectIntensity);
                    break;
                case "Distortion":
                    distortionEffect.ProcessSamples(buffer, offset, samplesRead, MicLoopback.EffectIntensity);
                    break;
                case "Chorus":
                    chorusEffect.ProcessSamples(buffer, offset, samplesRead, MicLoopback.EffectIntensity);
                    break;
                case "Flanger":
                    flangerEffect.ProcessSamples(buffer, offset, samplesRead, MicLoopback.EffectIntensity);
                    break;
                case "Pitch Shift":
                    // Simple pitch shift by amplitude modulation
                    for (int i = 0; i < samplesRead; i++)
                    {
                        buffer[offset + i] *= (1.0f + (MicLoopback.EffectIntensity - 0.5f) * 0.5f);
                    }
                    break;
                case "Vocal Remover":
                    vocalRemovalEffect.ProcessSamples(buffer, offset, samplesRead, MicLoopback.EffectIntensity);
                    break;
            }
        }
        
        return samplesRead;
    }
}

// Simple effect classes for real-time processing
public class EchoEffect
{
    private readonly float[] delayBuffer;
    private int delayIndex;

    public EchoEffect(WaveFormat format)
    {
        delayBuffer = new float[format.SampleRate / 2]; // 500ms delay
    }

    public void ProcessSamples(float[] buffer, int offset, int count, float intensity)
    {
        for (int i = 0; i < count; i++)
        {
            float delayed = delayBuffer[delayIndex];
            delayBuffer[delayIndex] = buffer[offset + i] + (delayed * intensity * 0.3f);
            buffer[offset + i] = buffer[offset + i] + (delayed * intensity);
            delayIndex = (delayIndex + 1) % delayBuffer.Length;
        }
    }
}

public class ReverbEffect
{
    private readonly float[][] delayLines;
    private readonly int[] delayIndices;

    public ReverbEffect(WaveFormat format)
    {
        delayLines = new float[4][];
        delayIndices = new int[4];
        
        delayLines[0] = new float[(int)(format.SampleRate * 0.03)]; // 30ms
        delayLines[1] = new float[(int)(format.SampleRate * 0.05)]; // 50ms
        delayLines[2] = new float[(int)(format.SampleRate * 0.07)]; // 70ms
        delayLines[3] = new float[(int)(format.SampleRate * 0.09)]; // 90ms
    }

    public void ProcessSamples(float[] buffer, int offset, int count, float intensity)
    {
        for (int i = 0; i < count; i++)
        {
            float input = buffer[offset + i];
            float reverb = 0;
            
            for (int d = 0; d < 4; d++)
            {
                float delayed = delayLines[d][delayIndices[d]];
                reverb += delayed * 0.25f;
                delayLines[d][delayIndices[d]] = input + (delayed * 0.5f);
                delayIndices[d] = (delayIndices[d] + 1) % delayLines[d].Length;
            }
            
            buffer[offset + i] = input + (reverb * intensity);
        }
    }
}

public class DistortionEffect
{
    public void ProcessSamples(float[] buffer, int offset, int count, float intensity)
    {
        float gain = 1.0f + (intensity * 5.0f);
        for (int i = 0; i < count; i++)
        {
            float sample = buffer[offset + i] * gain;
            buffer[offset + i] = Math.Sign(sample) * (1 - (float)Math.Exp(-Math.Abs(sample)));
        }
    }
}

public class ChorusEffect
{
    private readonly float[] delayBuffer;
    private int writeIndex;
    private float lfoPhase;

    public ChorusEffect(WaveFormat format)
    {
        delayBuffer = new float[format.SampleRate / 50]; // 20ms
    }

    public void ProcessSamples(float[] buffer, int offset, int count, float intensity)
    {
        for (int i = 0; i < count; i++)
        {
            float lfo = (float)Math.Sin(lfoPhase) * 0.5f + 0.5f;
            lfoPhase += 0.0005f;
            
            int delayOffset = (int)(lfo * delayBuffer.Length * 0.3f);
            int readIndex = (writeIndex - delayOffset + delayBuffer.Length) % delayBuffer.Length;
            
            float delayed = delayBuffer[readIndex];
            delayBuffer[writeIndex] = buffer[offset + i];
            
            buffer[offset + i] = (buffer[offset + i] + delayed * intensity * 0.5f) * 0.8f;
            writeIndex = (writeIndex + 1) % delayBuffer.Length;
        }
    }
}

public class FlangerEffect
{
    private readonly float[] delayBuffer;
    private int writeIndex;
    private float lfoPhase;

    public FlangerEffect(WaveFormat format)
    {
        delayBuffer = new float[format.SampleRate / 100]; // 10ms
    }

    public void ProcessSamples(float[] buffer, int offset, int count, float intensity)
    {
        for (int i = 0; i < count; i++)
        {
            float lfo = (float)Math.Sin(lfoPhase) * 0.5f + 0.5f;
            lfoPhase += 0.001f * intensity;
            
            int delayOffset = (int)(lfo * delayBuffer.Length * 0.5f);
            int readIndex = (writeIndex - delayOffset + delayBuffer.Length) % delayBuffer.Length;
            
            float delayed = delayBuffer[readIndex];
            delayBuffer[writeIndex] = buffer[offset + i];
            
            buffer[offset + i] = (buffer[offset + i] + delayed * intensity) * 0.7f;
            writeIndex = (writeIndex + 1) % delayBuffer.Length;
        }
    }
}

public class VocalRemovalEffect
{
    private float[] previousSamples = new float[0];
    private bool initialized = false;

    public void ProcessSamples(float[] buffer, int offset, int count, float intensity)
    {
        // Initialize previous samples buffer if needed
        if (!initialized)
        {
            previousSamples = new float[count];
            initialized = true;
        }
        
        // Ensure buffer is large enough
        if (previousSamples.Length < count)
        {
            Array.Resize(ref previousSamples, count);
        }

        // For stereo audio, we can do center channel extraction
        // For mono audio, we'll apply a high-pass filter to reduce vocal frequencies
        
        if (count % 2 == 0) // Assume stereo if even sample count
        {
            // Stereo vocal removal - subtract center channel
            for (int i = 0; i < count; i += 2)
            {
                float left = buffer[offset + i];
                float right = buffer[offset + i + 1];
                
                // Center channel extraction: subtract the common (center) signal
                float center = (left + right) * 0.5f;
                float leftMinusCenter = left - center;
                float rightMinusCenter = right - center;
                
                // Apply intensity - blend between original and processed
                buffer[offset + i] = left * (1.0f - intensity) + leftMinusCenter * intensity;
                buffer[offset + i + 1] = right * (1.0f - intensity) + rightMinusCenter * intensity;
            }
        }
        else
        {
            // Mono vocal removal - apply high-pass filter to reduce vocal frequencies
            float cutoffFreq = 0.1f + (intensity * 0.3f); // Adjustable cutoff based on intensity
            
            for (int i = 0; i < count; i++)
            {
                float input = buffer[offset + i];
                
                // Simple high-pass filter
                float filtered = input - (previousSamples[i] * cutoffFreq);
                previousSamples[i] = input;
                
                // Blend between original and filtered
                buffer[offset + i] = input * (1.0f - intensity) + filtered * intensity;
            }
        }
    }
}
