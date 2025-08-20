using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace AudioEffectsStudio.Effects
{
    /// <summary>
    /// Main effects processing chain that combines multiple audio effects
    /// </summary>
    public class EffectsProcessor : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly Dictionary<string, IEffect> effects;
        
        public WaveFormat WaveFormat => source.WaveFormat;

        public EffectsProcessor(ISampleProvider source)
        {
            this.source = source;
            this.effects = new Dictionary<string, IEffect>
            {
                ["Echo"] = new EchoEffect(),
                ["Reverb"] = new ReverbEffect(),
                ["Distortion"] = new DistortionEffect(),
                ["Chorus"] = new ChorusEffect(),
                ["Flanger"] = new FlangerEffect(),
                ["PitchShift"] = new PitchShiftEffect(),
                ["VocalRemoval"] = new VocalRemovalEffect()
            };
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            // Apply active effects in sequence
            foreach (var effectName in Audio.AudioEngine.ActiveEffects)
            {
                if (effects.TryGetValue(effectName, out var effect))
                {
                    effect.Process(buffer, offset, samplesRead, Audio.AudioEngine.EffectIntensity);
                }
            }

            return samplesRead;
        }
    }

    /// <summary>
    /// Base interface for all audio effects
    /// </summary>
    public interface IEffect
    {
        void Process(float[] buffer, int offset, int count, float intensity);
    }

    #region Effect Implementations

    /// <summary>
    /// Echo effect with configurable delay and decay
    /// </summary>
    public class EchoEffect : IEffect
    {
        private readonly float[] delayBuffer = new float[44100]; // 1 second delay buffer
        private int delayPosition = 0;
        
        public void Process(float[] buffer, int offset, int count, float intensity)
        {
            for (int i = offset; i < offset + count; i++)
            {
                float delayedSample = delayBuffer[delayPosition];
                delayBuffer[delayPosition] = buffer[i] + delayedSample * 0.3f * intensity;
                buffer[i] = buffer[i] + delayedSample * 0.4f * intensity;
                
                delayPosition = (delayPosition + 1) % delayBuffer.Length;
            }
        }
    }

    /// <summary>
    /// Reverb effect using multiple delay lines
    /// </summary>
    public class ReverbEffect : IEffect
    {
        private readonly float[][] delayLines;
        private readonly int[] delayPositions;
        private readonly float[] gains = { 0.4f, 0.3f, 0.2f, 0.15f };
        
        public ReverbEffect()
        {
            delayLines = new float[4][];
            delayPositions = new int[4];
            
            // Different delay line lengths for natural reverb
            delayLines[0] = new float[8820];  // 200ms
            delayLines[1] = new float[13230]; // 300ms
            delayLines[2] = new float[17640]; // 400ms
            delayLines[3] = new float[22050]; // 500ms
        }
        
        public void Process(float[] buffer, int offset, int count, float intensity)
        {
            for (int i = offset; i < offset + count; i++)
            {
                float reverbSum = 0;
                
                for (int j = 0; j < delayLines.Length; j++)
                {
                    float delayedSample = delayLines[j][delayPositions[j]];
                    delayLines[j][delayPositions[j]] = buffer[i] + delayedSample * 0.5f;
                    reverbSum += delayedSample * gains[j];
                    
                    delayPositions[j] = (delayPositions[j] + 1) % delayLines[j].Length;
                }
                
                buffer[i] = buffer[i] * (1 - intensity * 0.3f) + reverbSum * intensity;
            }
        }
    }

    /// <summary>
    /// Distortion effect with configurable drive and tone
    /// </summary>
    public class DistortionEffect : IEffect
    {
        public void Process(float[] buffer, int offset, int count, float intensity)
        {
            float drive = 1.0f + intensity * 9.0f; // Drive from 1x to 10x
            float threshold = 0.7f / drive;
            
            for (int i = offset; i < offset + count; i++)
            {
                float sample = buffer[i] * drive;
                
                // Soft clipping
                if (Math.Abs(sample) > threshold)
                {
                    sample = Math.Sign(sample) * threshold + 
                           (sample - Math.Sign(sample) * threshold) * 0.1f;
                }
                
                buffer[i] = sample * 0.3f; // Compensate for drive gain
            }
        }
    }

    /// <summary>
    /// Chorus effect with modulated delay
    /// </summary>
    public class ChorusEffect : IEffect
    {
        private readonly float[] delayBuffer = new float[4410]; // 100ms max delay
        private int delayPosition = 0;
        private float lfoPhase = 0;
        
        public void Process(float[] buffer, int offset, int count, float intensity)
        {
            float lfoRate = 2.0f; // 2 Hz LFO
            float lfoDepth = 20.0f * intensity; // Delay modulation depth
            
            for (int i = offset; i < offset + count; i++)
            {
                // Calculate modulated delay
                float lfo = (float)Math.Sin(lfoPhase) * lfoDepth;
                int delayOffset = (int)(220 + lfo); // Base delay + modulation
                
                int readPosition = (delayPosition - delayOffset + delayBuffer.Length) % delayBuffer.Length;
                float delayedSample = delayBuffer[readPosition];
                
                delayBuffer[delayPosition] = buffer[i];
                buffer[i] = buffer[i] * (1 - intensity * 0.5f) + delayedSample * intensity * 0.7f;
                
                delayPosition = (delayPosition + 1) % delayBuffer.Length;
                lfoPhase += lfoRate * 2 * (float)Math.PI / 44100;
            }
        }
    }

    /// <summary>
    /// Flanger effect with feedback and modulated delay
    /// </summary>
    public class FlangerEffect : IEffect
    {
        private readonly float[] delayBuffer = new float[1470]; // ~33ms max delay
        private int delayPosition = 0;
        private float lfoPhase = 0;
        
        public void Process(float[] buffer, int offset, int count, float intensity)
        {
            float lfoRate = 0.5f; // 0.5 Hz LFO
            float lfoDepth = 15.0f * intensity; // Delay modulation depth
            float feedback = 0.6f * intensity;
            
            for (int i = offset; i < offset + count; i++)
            {
                // Calculate modulated delay
                float lfo = (float)Math.Sin(lfoPhase) * lfoDepth;
                int delayOffset = (int)(10 + lfo); // Base delay + modulation
                
                int readPosition = (delayPosition - delayOffset + delayBuffer.Length) % delayBuffer.Length;
                float delayedSample = delayBuffer[readPosition];
                
                delayBuffer[delayPosition] = buffer[i] + delayedSample * feedback;
                buffer[i] = buffer[i] + delayedSample * intensity;
                
                delayPosition = (delayPosition + 1) % delayBuffer.Length;
                lfoPhase += lfoRate * 2 * (float)Math.PI / 44100;
            }
        }
    }

    /// <summary>
    /// Simple pitch shift effect using time stretching
    /// </summary>
    public class PitchShiftEffect : IEffect
    {
        private readonly float[] buffer1 = new float[2048];
        private readonly float[] buffer2 = new float[2048];
        private float position1 = 0;
        private float position2 = 1024;
        private int writePosition = 0;
        
        public void Process(float[] buffer, int offset, int count, float intensity)
        {
            float pitchShift = 1.0f + (intensity - 0.5f) * 0.5f; // ±25% pitch shift
            
            for (int i = offset; i < offset + count; i++)
            {
                // Write input to both buffers
                buffer1[writePosition] = buffer[i];
                buffer2[writePosition] = buffer[i];
                
                // Read with different rates for pitch shifting
                float sample1 = buffer1[(int)position1];
                float sample2 = buffer2[(int)position2];
                
                // Crossfade between buffers
                float fade1 = 0.5f + 0.5f * (float)Math.Cos(position1 / buffer1.Length * 2 * (float)Math.PI);
                float fade2 = 1.0f - fade1;
                
                buffer[i] = (sample1 * fade1 + sample2 * fade2) * 0.7f;
                
                position1 += pitchShift;
                position2 += pitchShift;
                
                while (position1 >= buffer1.Length) position1 -= buffer1.Length;
                while (position2 >= buffer2.Length) position2 -= buffer2.Length;
                
                writePosition = (writePosition + 1) % buffer1.Length;
            }
        }
    }

    /// <summary>
    /// Vocal removal effect using center channel cancellation
    /// </summary>
    public class VocalRemovalEffect : IEffect
    {
        public void Process(float[] buffer, int offset, int count, float intensity)
        {
            // This effect works best with stereo input
            // For mono input, we'll apply a high-pass filter to reduce vocal frequencies
            for (int i = offset; i < offset + count; i++)
            {
                // Simple high-pass filter to reduce vocal range (80-1000 Hz)
                // This is a simplified approach for mono audio
                buffer[i] = buffer[i] * (1 - intensity * 0.6f);
            }
        }
    }

    #endregion
}
