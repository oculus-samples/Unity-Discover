using System;

#if NETFX_CORE
using Windows.UI.Xaml;
using TimeObject = System.Object;
#else
using TimeObject = System.Timers.ElapsedEventArgs;
#endif

namespace Photon.Voice
{
    public static partial class AudioUtil
    {
        public static int ToneToBuf<T>(T[] buf, long timeSamples, int channels, double amp, double k, double phaseMod = 0)
        {
            return ToneToBuf(buf, 0, buf.Length, timeSamples, channels, amp, k, phaseMod);
        }

        public static int ToneToBuf<T>(T[] buf, int offset, int length, long timeSamples, int channels, double amp, double k, double phaseMod = 0)
        {
            int bufSamples = length / channels;

            if (buf is float[])
            {
                var b = buf as float[];
                for (int i = 0; i < bufSamples; i++)
                {
                    var x = timeSamples++ * k;
                    var v = (float)(Math.Sin(x + Math.Sin(x) * phaseMod) * amp);
                    for (int j = 0; j < channels; j++)
                        b[offset++] = v;
                }
            }
            else if (buf is short[])
            {
                var b = buf as short[];
                for (int i = 0; i < bufSamples; i++)
                {
                    var x = timeSamples++ * k;
                    var v = (short)(Math.Sin(x + Math.Sin(x) * phaseMod) * (amp * short.MaxValue));
                    for (int j = 0; j < channels; j++)
                        b[offset++] = v;
                }
            }

            return bufSamples;
        }

        public static int WaveformToBuf<T>(T[] buf, T[] waveform, long timePos)
        {
            int wfPos = (int)(timePos % waveform.Length); // pos in waveform array
            if (wfPos + buf.Length <= waveform.Length)
            {
                Array.Copy(waveform, wfPos, buf, 0, buf.Length);
            }
            else
            {
                int bufPos = 0;
                while (waveform.Length - wfPos <= buf.Length - bufPos)
                {
                    Array.Copy(waveform, wfPos, buf, bufPos, waveform.Length - wfPos);
                    bufPos += waveform.Length - wfPos;
                    wfPos = 0;
                }

                Array.Copy(waveform, wfPos, buf, bufPos, buf.Length - bufPos);
            }

            return buf.Length;
        }

        public abstract class GeneratorReader<T> : IAudioReader<T>
        {
            public GeneratorReader(Func<double> clockSec = null, int samplingRate = 48000, int channels = 1)
            {
                this.clockSec = clockSec == null ? () => DateTime.Now.Ticks / 10000000.0 : clockSec;
                SamplingRate = samplingRate;
                Channels = channels;
            }

            public int Channels { get; }
            public int SamplingRate { get; }
            public string Error { get; private set; }

            public void Dispose()
            {
            }

            long timeSamples;
            Func<double> clockSec;

            public bool Read(T[] buf)
            {
                var bufSamples = buf.Length / Channels;
                var t = (long)(clockSec() * SamplingRate);

                var deltaTimeSamples = t - timeSamples;
                if (Math.Abs(deltaTimeSamples) > SamplingRate / 4) // when started or Read has not been called for a while
                {
                    deltaTimeSamples = bufSamples;
                    timeSamples = t - bufSamples;
                }

                if (deltaTimeSamples < bufSamples)
                {
                    return false;
                }
                else
                {
                    var writensamples = Gen(buf, timeSamples);
                    timeSamples += writensamples;
                    return writensamples == bufSamples;
                }
            }

            abstract protected int Gen(T[] buf, long timeSamples);
        }

        /// <summary>IAudioPusher that provides a constant tone signal.</summary>
        public abstract class GeneratorPusher<T> : IAudioPusher<T>
        {
            public GeneratorPusher(int bufSizeMs = 100, int samplingRate = 48000, int channels = 1)
            {
                SamplingRate = samplingRate;
                Channels = channels;
                bufSamples = bufSizeMs * SamplingRate / 1000;
            }

#if NETFX_CORE
            DispatcherTimer timer;
#else
            System.Timers.Timer timer;
#endif
            Action<T[]> callback;
            ObjectFactory<T[], int> bufferFactory;

            /// <summary>Set the callback function used for pushing data</summary>
            /// <param name="callback">Callback function to use</param>
            /// <param name="bufferFactory">Buffer factory used to create the buffer that is pushed to the callback</param>
            public void SetCallback(Action<T[]> callback, ObjectFactory<T[], int> bufferFactory)
            {
                if (timer != null)
                {
                    Dispose();
                }

                this.callback = callback;
                this.bufferFactory = bufferFactory;
                // Hook up the Elapsed event for the timer.
#if NETFX_CORE
                timer = new DispatcherTimer();
                timer.Tick += OnTimedEvent;
                timer.Interval = new TimeSpan(10000000 * bufSizeSamples / SamplingRate); // ticks (10 000 000 per sec) in single buffer
#else
                timer = new System.Timers.Timer(1000.0 * bufSamples / SamplingRate);
                timer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
                timer.Enabled = true;
#endif
            }

            private void OnTimedEvent(object source, TimeObject e)
            {
                var buf = bufferFactory.New(bufSamples * Channels);
                timeSamples += Gen(buf, timeSamples);
                callback(buf);
            }

            abstract protected int Gen(T[] buf, long timeSamples);

            protected long timeSamples;
            int bufSamples;

            public int Channels { get; }
            public int SamplingRate { get; }
            public string Error { get; private set; }

            public void Dispose()
            {
                if (timer != null)
                {
#if NETFX_CORE
                    timer.Stop();
#else
                    timer.Close();
#endif
                }
            }
        }

        /// <summary>IAudioReader that provides a constant tone signal.</summary>
        /// Because of current resampling algorithm, the tone is distorted if SamplingRate does not equal encoder sampling rate.
        public class ToneAudioReader<T> : GeneratorReader<T>
        {
            /// <summary>Create a new ToneAudioReader instance</summary>
            /// <param name="clockSec">Function to get current time in seconds. In Unity, pass in '() => AudioSettings.dspTime' for better results.</param>
            /// <param name="frequency">Frequency of the generated tone (in Hz).</param>
            /// <param name="samplingRate">Sampling rate of the audio signal (in Hz).</param>
            /// <param name="channels">Number of channels in the audio signal.</param>
            public ToneAudioReader(Func<double> clockSec = null, double frequency = 440, int samplingRate = 48000, int channels = 1)
                : base(clockSec, samplingRate, channels)
            {
                k = 2 * Math.PI * frequency / SamplingRate;
            }

            double k;

            protected override int Gen(T[] buf, long timeSamples)
            {
                return ToneToBuf(buf, timeSamples, Channels, 0.2, k);
            }
        }

        /// <summary>IAudioPusher that provides a constant tone signal.</summary>
        public class ToneAudioPusher<T> : GeneratorPusher<T>
        {
            /// <summary>Create a new ToneAudioReader instance</summary>
            /// <param name="frequency">Frequency of the generated tone (in Hz).</param>
            /// <param name="bufSizeMs">Size of buffers to push (in milliseconds).</param>
            /// <param name="samplingRate">Sampling rate of the audio signal (in Hz).</param>
            /// <param name="channels">Number of channels in the audio signal.</param>
            public ToneAudioPusher(int frequency = 440, int bufSizeMs = 100, int samplingRate = 48000, int channels = 1)
                : base(bufSizeMs, samplingRate, channels)
            {
                k = 2 * Math.PI * frequency / SamplingRate;
            }

            double k;
            protected override int Gen(T[] buf, long timeSamples)
            {
                return ToneToBuf(buf, timeSamples, Channels, 0.2, k);
            }

        }

        /// <summary>IAudioReader that provides the given waveform.</summary>
        public class WaveformAudioReader<T> : GeneratorReader<T>
        {
            public WaveformAudioReader(Func<double> clockSec = null, int samplingRate = 48000, int channels = 1)
                : base(clockSec, samplingRate, channels)
            {
            }

            protected override int Gen(T[] buf, long timeSamples)
            {
                var wf = Waveform;
                return (wf != null && wf.Length > 0) ? WaveformToBuf<T>(buf, wf, timeSamples * Channels) / Channels : 0;
            }

            public T[] Waveform { private get; set; }

        }

        /// <summary>IAudioPusher that provides the given waveform.</summary>
        public class WaveformAudioPusher<T> : GeneratorPusher<T>
        {
            public WaveformAudioPusher(int bufSizeMs = 100, int samplingRate = 48000, int channels = 1)
                : base(bufSizeMs, samplingRate, channels)
            {
            }

            public T[] Waveform { private get; set; }

            protected override int Gen(T[] buf, long timeSamples)
            {
                var wf = Waveform;
                return (wf != null && wf.Length > 0) ? WaveformToBuf<T>(buf, wf, timeSamples * Channels) / Channels : 0;
            }
        }
    }
}