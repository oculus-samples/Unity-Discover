using System;
using POpusCodec.Enums;
using System.Runtime.InteropServices;
using Photon.Voice;
using System.Collections.Generic;

namespace POpusCodec
{
    public class OpusDecoder<T> : IDisposable
    {
        private const bool UseInbandFEC = true;

        protected Action<FrameOut<T>> output;
        protected bool TisFloat;
        protected int sizeofT;
        protected FrameOut<T> frameOut = new FrameOut<T>(null, false);

        protected IntPtr handle = IntPtr.Zero;

        protected int channels;
        protected int frameSamples;

        protected static readonly T[] EmptyBuffer = new T[] { };

        public OpusDecoder(Action<FrameOut<T>> output, SamplingRate outputSamplingRateHz, Channels channels, int frameSamples)
        {
            this.output = output;
            TisFloat = default(T) is float;
            sizeofT = Marshal.SizeOf(default(T));

            if ((outputSamplingRateHz != SamplingRate.Sampling08000)
                && (outputSamplingRateHz != SamplingRate.Sampling12000)
                && (outputSamplingRateHz != SamplingRate.Sampling16000)
                && (outputSamplingRateHz != SamplingRate.Sampling24000)
                && (outputSamplingRateHz != SamplingRate.Sampling48000))
            {
                throw new ArgumentOutOfRangeException("outputSamplingRateHz", "Must use one of the pre-defined sampling rates (" + outputSamplingRateHz + ")");
            }
            if ((channels != Channels.Mono)
                && (channels != Channels.Stereo))
            {
                throw new ArgumentOutOfRangeException("numChannels", "Must be Mono or Stereo");
            }

            this.channels = (int)channels;
            this.frameSamples = frameSamples;
            if (!Wrapper.AsyncAPI)
            {
                this.buffer = new T[frameSamples * this.channels];
            }
            handle = Wrapper.opus_decoder_create(outputSamplingRateHz, channels);

            if (handle == IntPtr.Zero)
            {
                throw new OpusException(OpusStatusCode.AllocFail, "Memory was not allocated for the encoder");
            }
        }

        private T[] buffer; // allocated for exactly 1 frame size as first valid frame received
        bool prevPacketInvalid; // maybe false if prevPacket us null

        protected void decodePacket(FrameBuffer data, int decodeFEC, int channels, bool endOfStream)
        {
            if (Wrapper.AsyncAPI)
            {
                if (TisFloat)
                {
                    Wrapper.opus_decode_float_async(handle, data.Ptr, data.Length, decodeFEC, endOfStream);
                }
                else
                {
                    Wrapper.opus_decode_async(handle, data.Ptr, data.Length, decodeFEC, endOfStream);
                }
            }
            else
            {
                var numSamplesDecoded = TisFloat ?
                    Wrapper.opus_decode(handle, data, this.buffer as float[], this.frameSamples, decodeFEC) :
                    Wrapper.opus_decode(handle, data, this.buffer as short[], this.frameSamples, decodeFEC);

                //Negative already handled at this point.
                if (numSamplesDecoded == 0)
                    return;

                procOutput(this.buffer, endOfStream);
            }
        }

        protected void procOutput(T[] buffer, bool endOfStream)
        {
            output(frameOut.Set(buffer, endOfStream));
        }

        // pass null to indicate packet loss
        public void DecodePacket(ref FrameBuffer packetData, bool endOfStream)
        {
            bool packetInvalid;
            if (packetData.Array == null)
            {
                packetInvalid = true;
            }
            else
            {
                int bandwidth = Wrapper.opus_packet_get_bandwidth(packetData.Ptr);
                packetInvalid = bandwidth == (int)OpusStatusCode.InvalidPacket;
            }

            if (UseInbandFEC)
            {
                if (prevPacketInvalid)
                {
                    if (packetInvalid)
                    {
                        // no fec data, conceal previous frame
                        decodePacket( new FrameBuffer(),  0, channels, false);
                    }
                    else
                    {
                        // error correct previous frame with the help of the current
                        decodePacket(packetData, 1, channels, false);
                    }
                }

                if (!packetInvalid)
                {
                    decodePacket(packetData, 0, channels, endOfStream);
                }

                prevPacketInvalid = packetInvalid;
            }
            else
            {
#pragma warning disable 162
                // decode or conceal current frame
                decodePacket(packetData, 0, channels, endOfStream);
#pragma warning restore 162
            }
        }

        public virtual void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Wrapper.opus_decoder_destroy(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    public class OpusDecoderAsync<T> : OpusDecoder<T>
    {
        [AOT.MonoPInvokeCallbackAttribute(typeof(Action<IntPtr, IntPtr, int, bool>))]
        static public void DataCallbackStatic(IntPtr handle, IntPtr p, int count, bool endOfStream)
        {
            if (handles.TryGetValue(handle, out var obj))
            {
                obj.dataCallback(p, count, endOfStream);
            }
        }

        static protected Dictionary<IntPtr, OpusDecoderAsync<T>> handles = new Dictionary<IntPtr, OpusDecoderAsync<T>>();

        public OpusDecoderAsync(Action<FrameOut<T>> output, SamplingRate outputSamplingRateHz, Channels numChannels, int frameDurationSamples) 
            : base(output, outputSamplingRateHz, numChannels, frameDurationSamples)
        {
            handles[handle] = this;
        }

        private float[] bufOutFloat;
        private short[] bufOutShort;
        protected void dataCallback(IntPtr p, int count, bool endOfStream)
        {
            if (output != null)
            {
                if (TisFloat)
                {
                    if (bufOutFloat == null || bufOutFloat.Length < count)
                    {
                        bufOutFloat = new float[count];
                    }
                    Marshal.Copy(p, bufOutFloat, 0, count);
                    //UnityEngine.Debug.LogFormat("!!!!!!!!!!!! DECODE CALBBACK {0} {1} {2} {3} {4} {5} {6} {7}", p, count, endOfStream, bufOutFloat[0], bufOutFloat[1], bufOutFloat[2], bufOutFloat[count - 3], bufOutFloat[count - 2], bufOutFloat[count - 1]);
                    procOutput(bufOutFloat as T[], endOfStream);
                }
                else
                {
                    if (bufOutShort == null || bufOutShort.Length < count)
                    {
                        bufOutShort = new short[count];
                    }
                    Marshal.Copy(p, bufOutShort, 0, count);
                    procOutput(bufOutShort as T[], endOfStream);
                }
            }
        }

        public override void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                handles.Remove(handle);
            }
            base.Dispose();
        }
    }
}
