using POpusCodec.Enums;
using POpusCodec;
using System;

namespace Photon.Voice
{
    public class OpusCodec
    {
        static public string Version
        {
            get
            {
                return OpusLib.Version;
            }
        }

        public enum FrameDuration
        {
            Frame2dot5ms = 2500,
            Frame5ms = 5000,
            Frame10ms = 10000,
            Frame20ms = 20000,
            Frame40ms = 40000,
            Frame60ms = 60000
        }

        public static class Factory
        {
            static public IEncoder CreateEncoder<B>(VoiceInfo i, ILogger logger)
            {
                if (typeof(B) == typeof(float[]))
                    return new EncoderFloat(i, logger);
                else if (typeof(B) == typeof(short[]))
                    return new EncoderShort(i, logger);
                else
                    throw new UnsupportedCodecException("Factory.CreateEncoder<" + typeof(B) + ">", i.Codec);
            }
        }

        abstract public class Encoder<T> : IEncoderDirect<T[]>
        {
            protected OpusEncoder encoder;
            protected bool disposed;
            protected Encoder(VoiceInfo i, ILogger logger)
            {
                try
                {
                    encoder = new OpusEncoder((SamplingRate)i.SamplingRate, (Channels)i.Channels, i.Bitrate, OpusApplicationType.Voip, (Delay)(i.FrameDurationUs * 2 / 1000));
                    logger.LogInfo("[PV] OpusCodec.Encoder created. Opus version " + Version + ", " + i);
                }
                catch (Exception e)
                {
                    Error = e.ToString();
                    if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                    {
                        Error = "Exception in OpusCodec.Encoder constructor";
                    }
                    logger.LogError("[PV] OpusCodec.Encoder: " + Error);
                }
            }

            public string Error { get; private set; }

            Action<ArraySegment<byte>, FrameFlags> output;
            public Action<ArraySegment<byte>, FrameFlags> Output
            {
                set
                {
                    output = value;
                    encoder.Output = value;
                }
                get { return output; }
            }

            public void Input(T[] buf)
            {
                if (Error != null)
                {
                    return;
                }
                if (Output == null)
                {
                    Error = "OpusCodec.Encoder: Output action is not set";
                    return;
                }

                lock (this)
                {
                    if (disposed || Error != null) { }
                    else
                    {
                        encodeTyped(buf);
                    }
                }
            }

            public void EndOfStream()
            {
                lock (this)
                {
                    if (disposed || Error != null) { }
                    else
                    {
                        Output(EmptyBuffer, FrameFlags.EndOfStream);
                    }
                }
                return;
            }

            private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });

            public ArraySegment<byte> DequeueOutput(out FrameFlags flags) { flags = 0; return EmptyBuffer; }

            protected abstract void encodeTyped(T[] buf);

            public I GetPlatformAPI<I>() where I : class
            {
                return null;
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (encoder != null)
                    {
                        encoder.Dispose();
                    }
                    disposed = true;
                }
            }
        }

        public class EncoderFloat : Encoder<float>
        {
            internal EncoderFloat(VoiceInfo i, ILogger logger) : base(i, logger) { }

            override protected void encodeTyped(float[] buf)
            {
                encoder.Encode(buf);
            }
        }
        public class EncoderShort : Encoder<short>
        {
            internal EncoderShort(VoiceInfo i, ILogger logger) : base(i, logger) { }
            override protected void encodeTyped(short[] buf)
            {
                encoder.Encode(buf);
            }
        }

        public class Decoder<T> : IDecoder
        {
            protected OpusDecoder<T> decoder;
            ILogger logger;
            public Decoder(Action<FrameOut<T>> output, ILogger logger)
            {
                this.output = output;
                this.logger = logger;
            }

            public void Open(VoiceInfo i)
            {
                try
                {
                    if (Wrapper.AsyncAPI)
                    {
                        decoder = new OpusDecoderAsync<T>(output, (SamplingRate)i.SamplingRate, (Channels)i.Channels, i.FrameDurationSamples);
                    }
                    else
                    {
                        decoder = new OpusDecoder<T>(output, (SamplingRate)i.SamplingRate, (Channels)i.Channels, i.FrameDurationSamples);
                    }
                    logger.LogInfo("[PV] OpusCodec.Decoder created. Opus version " + Version + ", " + i);
                }
                catch (Exception e)
                {
                    Error = e.ToString();
                    if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                    {
                        Error = "Exception in OpusCodec.Decoder constructor";
                    }
                    logger.LogError("[PV] OpusCodec.Decoder: " + Error);
                }
            }

            public string Error { get; private set; }

            private Action<FrameOut<T>> output;

            public void Dispose()
            {
                if (decoder != null)
                {
                    decoder.Dispose();
                }
            }

            public void Input(ref FrameBuffer buf)
            {
                if (Error == null)
                {
                    bool endOfStream = (buf.Flags & FrameFlags.EndOfStream) != 0;
                    decoder.DecodePacket(ref buf, endOfStream);
                }
            }
        }

        public class Util
        {
            internal static int bestEncoderSampleRate(int f)
            {
                int diff = int.MaxValue;
                int res = (int)SamplingRate.Sampling48000;
                foreach (var x in Enum.GetValues(typeof(SamplingRate)))
                {
                    var d = Math.Abs((int)x - f);
                    if (d < diff)
                    {
                        diff = d;
                        res = (int)x;
                    }
                }
                return res;
            }
        }
    }
}