using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

public static class DJEffects
{
    // Echo effect implementation
    public static ISampleProvider AddEcho(ISampleProvider input, float intensity, int delayMs = 300)
    {
        var echo = new EchoSampleProvider(input, delayMs, intensity);
        return echo;
    }

    // Simple chorus effect implementation
    public static ISampleProvider AddChorus(ISampleProvider input, float intensity)
    {
        // Simple chorus using basic delay
        var chorus = new ChorusSampleProvider(input, intensity);
        return chorus;
    }

    // Distortion effect
    public static ISampleProvider AddDistortion(ISampleProvider input, float intensity)
    {
        return new DistortionSampleProvider(input, intensity);
    }

    // Simple reverb simulation
    public static ISampleProvider AddReverb(ISampleProvider input, float intensity)
    {
        var reverb = new ReverbSampleProvider(input, intensity);
        return reverb;
    }

    // Pitch shift effect
    public static ISampleProvider AddPitchShift(ISampleProvider input, float pitchFactor)
    {
        // Simple pitch shifting by resampling
        var resampler = new WdlResamplingSampleProvider(input, (int)(input.WaveFormat.SampleRate * pitchFactor));
        return resampler;
    }

    // Flanger effect
    public static ISampleProvider AddFlanger(ISampleProvider input, float intensity)
    {
        return new FlangerSampleProvider(input, intensity);
    }
}

// Custom Chorus Sample Provider
public class ChorusSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly float[] delayBuffer;
    private readonly float intensity;
    private int writeIndex;
    private float lfoPhase;

    public ChorusSampleProvider(ISampleProvider source, float intensity)
    {
        this.source = source;
        this.intensity = intensity;
        this.delayBuffer = new float[source.WaveFormat.SampleRate / 50]; // 20ms delay buffer
        this.writeIndex = 0;
        this.lfoPhase = 0;
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            // LFO for chorus modulation
            float lfo = (float)Math.Sin(lfoPhase) * 0.5f + 0.5f;
            lfoPhase += 0.0005f; // LFO frequency
            
            // Variable delay based on LFO
            int delayOffset = (int)(lfo * delayBuffer.Length * 0.3f);
            int readIndex = (writeIndex - delayOffset + delayBuffer.Length) % delayBuffer.Length;
            
            float delayed = delayBuffer[readIndex];
            delayBuffer[writeIndex] = buffer[offset + i];
            
            // Mix original and delayed signal
            buffer[offset + i] = (buffer[offset + i] + delayed * intensity * 0.5f) * 0.8f;
            
            writeIndex = (writeIndex + 1) % delayBuffer.Length;
        }
        
        return samplesRead;
    }
}

// Custom Echo Sample Provider
public class EchoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int delaySamples;
    private readonly float echoLevel;
    private readonly float[] delayBuffer;
    private int delayIndex;

    public EchoSampleProvider(ISampleProvider source, int delayMs, float echoLevel)
    {
        this.source = source;
        this.echoLevel = echoLevel;
        this.delaySamples = (int)(source.WaveFormat.SampleRate * delayMs / 1000.0);
        this.delayBuffer = new float[delaySamples];
        this.delayIndex = 0;
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            float delayed = delayBuffer[delayIndex];
            delayBuffer[delayIndex] = buffer[offset + i] + (delayed * echoLevel * 0.5f);
            buffer[offset + i] = buffer[offset + i] + (delayed * echoLevel);
            
            delayIndex = (delayIndex + 1) % delaySamples;
        }
        
        return samplesRead;
    }
}

// Custom Distortion Sample Provider
public class DistortionSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly float gain;

    public DistortionSampleProvider(ISampleProvider source, float intensity)
    {
        this.source = source;
        this.gain = 1.0f + (intensity * 10.0f); // Scale intensity
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            float sample = buffer[offset + i] * gain;
            // Soft clipping distortion
            buffer[offset + i] = Math.Sign(sample) * (1 - (float)Math.Exp(-Math.Abs(sample)));
        }
        
        return samplesRead;
    }
}

// Custom Reverb Sample Provider
public class ReverbSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly float[] delayLine1, delayLine2, delayLine3, delayLine4;
    private readonly int[] delayIndices = new int[4];
    private readonly float reverbLevel;

    public ReverbSampleProvider(ISampleProvider source, float reverbLevel)
    {
        this.source = source;
        this.reverbLevel = reverbLevel;
        
        int sampleRate = source.WaveFormat.SampleRate;
        delayLine1 = new float[(int)(sampleRate * 0.03)]; // 30ms
        delayLine2 = new float[(int)(sampleRate * 0.05)]; // 50ms
        delayLine3 = new float[(int)(sampleRate * 0.07)]; // 70ms
        delayLine4 = new float[(int)(sampleRate * 0.09)]; // 90ms
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            float input = buffer[offset + i];
            
            // Get delayed samples from multiple delay lines
            float delayed1 = delayLine1[delayIndices[0]];
            float delayed2 = delayLine2[delayIndices[1]];
            float delayed3 = delayLine3[delayIndices[2]];
            float delayed4 = delayLine4[delayIndices[3]];
            
            // Mix delayed signals for reverb effect
            float reverb = (delayed1 + delayed2 + delayed3 + delayed4) * 0.25f * reverbLevel;
            
            // Store input in delay lines with feedback
            delayLine1[delayIndices[0]] = input + (delayed1 * 0.5f);
            delayLine2[delayIndices[1]] = input + (delayed2 * 0.4f);
            delayLine3[delayIndices[2]] = input + (delayed3 * 0.3f);
            delayLine4[delayIndices[3]] = input + (delayed4 * 0.2f);
            
            // Update delay indices
            delayIndices[0] = (delayIndices[0] + 1) % delayLine1.Length;
            delayIndices[1] = (delayIndices[1] + 1) % delayLine2.Length;
            delayIndices[2] = (delayIndices[2] + 1) % delayLine3.Length;
            delayIndices[3] = (delayIndices[3] + 1) % delayLine4.Length;
            
            buffer[offset + i] = input + reverb;
        }
        
        return samplesRead;
    }
}

// Custom Flanger Sample Provider
public class FlangerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly float[] delayBuffer;
    private readonly float intensity;
    private int writeIndex;
    private float lfoPhase;

    public FlangerSampleProvider(ISampleProvider source, float intensity)
    {
        this.source = source;
        this.intensity = intensity;
        this.delayBuffer = new float[source.WaveFormat.SampleRate / 100]; // 10ms max delay
        this.writeIndex = 0;
        this.lfoPhase = 0;
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            // LFO for varying delay
            float lfo = (float)Math.Sin(lfoPhase) * 0.5f + 0.5f;
            lfoPhase += 0.001f * intensity; // LFO frequency
            
            // Variable delay based on LFO
            int delayOffset = (int)(lfo * delayBuffer.Length * 0.5f);
            int readIndex = (writeIndex - delayOffset + delayBuffer.Length) % delayBuffer.Length;
            
            float delayed = delayBuffer[readIndex];
            delayBuffer[writeIndex] = buffer[offset + i];
            
            // Mix original and delayed signal
            buffer[offset + i] = (buffer[offset + i] + delayed * intensity) * 0.7f;
            
            writeIndex = (writeIndex + 1) % delayBuffer.Length;
        }
        
        return samplesRead;
    }
}
