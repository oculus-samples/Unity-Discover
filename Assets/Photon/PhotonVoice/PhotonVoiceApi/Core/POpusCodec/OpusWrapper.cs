#if ((UNITY_IOS || UNITY_SWITCH) && !UNITY_EDITOR) || __IOS__
#define DLL_IMPORT_INTERNAL
#endif

#if NONE //UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
// opus.* lib built from original opus repo
#else
#define OPUS_EGPV // opus_egpv.* lib with interop helpers (we still may use such libs for the platforms where helpers are not required)
#endif

// Interop helpers required for iOS ARM64 IL2CPP (and maybe in other cases) because of variadic functions PInvoke calling issue:
// https://stackoverflow.com/questions/35536515/variable-argument-function-bad-access-with-va-arg-at-ios-arm64

// use statically linked interop helpers defined outside of opus.lib
#if (UNITY_IOS && !UNITY_EDITOR) || __IOS__
#define OPUS_EGPV_INTEROP_HELPER_EXTERNAL
#endif

// Interop helpers required also for Apple Silicon (ARM64)

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
// use interop helpers built into opus_egpv.* lib (works for any platform but requires opus lib compiled from customized sources)
#define OPUS_EGPV_INTEROP_HELPER_BUILTIN
#define OPUS_EGPV
#endif

#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER && !UNITY_EDITOR // opus lib requires Emscripten 2.0.19
#if PHOTON_VOICE_OPUS_JS_WORKER
#define OPUS_JS_WORKER
#endif
#define DLL_IMPORT_INTERNAL
#define OPUS_EGPV_INTEROP_HELPER_BUILTIN
#endif

using System;
using System.Runtime.InteropServices;
using POpusCodec.Enums;
using Photon.Voice;

namespace POpusCodec
{
    internal class Wrapper
    {
#if DLL_IMPORT_INTERNAL
        const string lib_name = "__Internal";
#else
#if OPUS_EGPV
        const string lib_name = "opus_egpv";
#else
        const string lib_name = "opus";
#endif
#endif

#if OPUS_JS_WORKER
        public const bool AsyncAPI = true;

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_JS_start_worker(Action<IntPtr, IntPtr, int> encoderDataCallbackStatic, Action<IntPtr, IntPtr, int, bool> decoderDataCallbackStatic);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "PhotonVoice_JS_opus_encoder_destroy")]
        public static extern void opus_encoder_destroy(IntPtr st);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "PhotonVoice_JS_opus_decode_async")]
        public static extern int opus_decode_async(IntPtr st, IntPtr data, int len, int decodeFEC, bool eos);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "PhotonVoice_JS_opus_decode_float_async")]
        public static extern int opus_decode_float_async(IntPtr st, IntPtr data, int len, int decodeFEC, bool eos);

        private static int opus_decode(IntPtr st, IntPtr data, int len, short[] pcm, int frame_size, int decode_fec)
        {
            return 0;
        }

        private static int opus_decode_float(IntPtr st, IntPtr data, int len, float[] pcm, int frame_size, int decode_fec)
        {
            return 0;
        }

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "PhotonVoice_JS_opus_decoder_destroy")]
        public static extern void opus_decoder_destroy(IntPtr st);

        static Wrapper()
        {
            PhotonVoice_JS_start_worker(OpusEncoder.DataCallbackStatic, OpusDecoderAsync<float>.DataCallbackStatic);
        }

        static int jsIDCount = 1; // avoid id=0 because 0 is checked for allocation in non-js
        const string jsProxyPref = "PhotonVoice_JS_";
#else
        public const bool AsyncAPI = false;

        public static void opus_encoder_destroy(IntPtr st)
        {
            Marshal.FreeHGlobal(st);
        }

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int opus_decode(IntPtr st, IntPtr data, int len, short[] pcm, int frame_size, int decode_fec);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int opus_decode_float(IntPtr st, IntPtr data, int len, float[] pcm, int frame_size, int decode_fec);

        public static int opus_decode_async(IntPtr st, IntPtr data, int len, int decodeFEC, bool eos)
        {
            return 0;
        }

        public static int opus_decode_float_async(IntPtr st, IntPtr data, int len, int decodeFEC, bool eos)
        {
            return 0;
        }

        public static void opus_decoder_destroy(IntPtr st)
        {
            Marshal.FreeHGlobal(st);
        }

        const string jsProxyPref = "";
#endif
        // methods w/o jsProxyPref in EntryPoint are not called in WebGL
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int opus_encoder_get_size(Channels channels);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_encoder_init")]
        private static extern OpusStatusCode opus_encoder_init(IntPtr st, SamplingRate Fs, Channels channels, OpusApplicationType application);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_get_version_string")]
        public static extern IntPtr opus_get_version_string();

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_encode")]
        private static extern int opus_encode(IntPtr st, short[] pcm, int frame_size, byte[] data, int max_data_bytes);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_encode_float")]
        private static extern int opus_encode_float(IntPtr st, float[] pcm, int frame_size, byte[] data, int max_data_bytes);

#if OPUS_EGPV_INTEROP_HELPER_BUILTIN
        const string ctl_entry_point_set = "_set";
        const string ctl_entry_point_get = "_get";
#elif OPUS_EGPV_INTEROP_HELPER_EXTERNAL
        const string ctl_entry_point_set = "_set_ext";
        const string ctl_entry_point_get = "_get_ext";
#else
        const string ctl_entry_point_set = "";
        const string ctl_entry_point_get = "";
#endif
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_encoder_ctl" + ctl_entry_point_set)]
        private static extern int opus_encoder_ctl_set(IntPtr st, OpusCtlSetRequest request, int value);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_encoder_ctl" + ctl_entry_point_get)]
        private static extern int opus_encoder_ctl_get(IntPtr st, OpusCtlGetRequest request, ref int value);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_decoder_ctl" + ctl_entry_point_set)]
        private static extern int opus_decoder_ctl_set(IntPtr st, OpusCtlSetRequest request, int value);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_decoder_ctl" + ctl_entry_point_get)]
        private static extern int opus_decoder_ctl_get(IntPtr st, OpusCtlGetRequest request, ref int value);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int opus_decoder_get_size(Channels channels);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = jsProxyPref + "opus_decoder_init")]
        private static extern OpusStatusCode opus_decoder_init(IntPtr st, SamplingRate Fs, Channels channels);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int opus_packet_get_bandwidth(IntPtr data);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int opus_packet_get_nb_channels(byte[] data);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr opus_strerror(OpusStatusCode error);

        public static IntPtr opus_encoder_create(SamplingRate Fs, Channels channels, OpusApplicationType application)
        {
#if OPUS_JS_WORKER
            var jsID = new IntPtr(jsIDCount++); // use IntPtr instead of int for compatibility with method signatures used in both js and non-js.
            OpusStatusCode statusCode = Wrapper.opus_encoder_init(jsID, Fs, channels, application);
            return jsID;
#else
            int size = Wrapper.opus_encoder_get_size(channels);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            OpusStatusCode statusCode = Wrapper.opus_encoder_init(ptr, Fs, channels, application);
            try
            {
                HandleStatusCode(statusCode, "opus_encoder_create/opus_encoder_init", Fs, channels, application);
            }
            catch (Exception ex)
            {
                if (ptr != IntPtr.Zero)
                {
                    Wrapper.opus_encoder_destroy(ptr);
                    ptr = IntPtr.Zero;
                }

                throw ex;
            }
            return ptr;
#endif
        }

        public static int opus_encode(IntPtr st, short[] pcm, int frame_size, byte[] data)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusEncoder");

            int payloadLength = opus_encode(st, pcm, frame_size, data, data.Length);

            if (payloadLength < 0)
            {
                HandleStatusCode((OpusStatusCode)payloadLength, "opus_encode/short", frame_size, data.Length);
            }

            return payloadLength;
        }

        public static int opus_encode(IntPtr st, float[] pcm, int frame_size, byte[] data)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusEncoder");
            int payloadLength = opus_encode_float(st, pcm, frame_size, data, data.Length);

            if (payloadLength < 0)
            {
                HandleStatusCode((OpusStatusCode)payloadLength, "opus_encode/float", frame_size, data.Length);
            }

            return payloadLength;
        }

        public static int get_opus_encoder_ctl(IntPtr st, OpusCtlGetRequest request)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusEncoder");

            int value = 0;
            OpusStatusCode statusCode = (OpusStatusCode)opus_encoder_ctl_get(st, request, ref value);

            HandleStatusCode(statusCode, "opus_encoder_ctl_get", request);

            return value;
        }

        public static void set_opus_encoder_ctl(IntPtr st, OpusCtlSetRequest request, int value)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusEncoder");

            OpusStatusCode statusCode = (OpusStatusCode)opus_encoder_ctl_set(st, request, value);

            HandleStatusCode(statusCode, "opus_encoder_ctl_set", request, value);
        }

        public static int get_opus_decoder_ctl(IntPtr st, OpusCtlGetRequest request)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusDcoder");

            int value = 0;
            OpusStatusCode statusCode = (OpusStatusCode)opus_decoder_ctl_get(st, request, ref value);

            HandleStatusCode(statusCode, "get_opus_decoder_ctl", request, value);

            return value;
        }

        public static void set_opus_decoder_ctl(IntPtr st, OpusCtlSetRequest request, int value)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusDecoder");

            OpusStatusCode statusCode = (OpusStatusCode)opus_decoder_ctl_set(st, request, value);

            HandleStatusCode(statusCode, "set_opus_decoder_ctl", request, value);
        }
        public static IntPtr opus_decoder_create(SamplingRate Fs, Channels channels, Action<IntPtr, bool> dataCallbackStatic = null)
        {
#if OPUS_JS_WORKER
            var jsID = new IntPtr(jsIDCount++); // use IntPtr instead of int for compatibility with method signatures used in both js and non-js.
            OpusStatusCode statusCode = Wrapper.opus_decoder_init(jsID, Fs, channels);
            return jsID;
#else
            int size = Wrapper.opus_decoder_get_size(channels);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            OpusStatusCode statusCode = Wrapper.opus_decoder_init(ptr, Fs, channels);

            try
            {
                HandleStatusCode(statusCode, "opus_decoder_create", Fs, channels);
            }
            catch (Exception ex)
            {
                if (ptr != IntPtr.Zero)
                {
                    Wrapper.opus_decoder_destroy(ptr);
                    ptr = IntPtr.Zero;
                }

                throw ex;
            }

            return ptr;
#endif
        }

        public static int opus_decode(IntPtr st, FrameBuffer data, short[] pcm, int frame_size, int decode_fec)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusDecoder");

            int numSamplesDecoded = opus_decode(st, data.Ptr, data.Length, pcm, frame_size, decode_fec);

            if (numSamplesDecoded == (int)OpusStatusCode.InvalidPacket)
                return 0;

            if (numSamplesDecoded < 0)
            {
                HandleStatusCode((OpusStatusCode)numSamplesDecoded, "opus_decode/short", data.Length, pcm.Length, frame_size, decode_fec);
            }

            return numSamplesDecoded;
        }

        public static int opus_decode(IntPtr st, FrameBuffer data, float[] pcm, int frame_size, int decode_fec)
        {
            if (st == IntPtr.Zero)
                throw new ObjectDisposedException("OpusDecoder");

            int numSamplesDecoded = opus_decode_float(st, data.Ptr, data.Length, pcm, frame_size, decode_fec);

            if (numSamplesDecoded == (int)OpusStatusCode.InvalidPacket)
                return 0;

            if (numSamplesDecoded < 0)
            {
                HandleStatusCode((OpusStatusCode)numSamplesDecoded, "opus_decode/float", data.Length, pcm.Length, frame_size, decode_fec);
            }

            return numSamplesDecoded;
        }

        private static void HandleStatusCode(OpusStatusCode statusCode, params object[] info)
        {
            if (statusCode != OpusStatusCode.OK)
            {
                var infoMsg = "";
                foreach (var i in info) infoMsg += i.ToString() + ":";
                throw new OpusException(statusCode, infoMsg + Marshal.PtrToStringAnsi(opus_strerror(statusCode)));
            }
        }
    }
}
