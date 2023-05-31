using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{
    internal static class OvrPluginTracking
    {
#if UNITY_EDITOR || !UNITY_IOS
#if UNITY_EDITOR_OSX
        private const string LibFile = OvrAvatarPlugin.FullPluginFolderPath + "libovrplugintracking.framework/libovrplugintracking";
#else
        private const string LibFile = OvrAvatarManager.IsAndroidStandalone ? "ovrplugintracking" : "libovrplugintracking";
#endif  // UNITY_EDITOR_OSX
#else   // !UNITY_EDITOR && UNITY_IOS
        private const string LibFile = "__Internal";
#endif  // !UNITY_EDITOR && UNITY_IOS

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool ovrpTracking_Initialize(CAPI.LoggingDelegate loggingDelegate, IntPtr loggingContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ovrpTracking_Shutdown();


        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool ovrpTracking_CreateFaceTrackingContext(out CAPI.ovrAvatar2FacePoseProvider outContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrpTracking_CreateFaceTrackingContext")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool ovrpTracking_CreateFaceTrackingContextNative(out CAPI.ovrAvatar2FacePoseProviderNative outContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool ovrpTracking_CreateEyeTrackingContext(out CAPI.ovrAvatar2EyePoseProvider outContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrpTracking_CreateEyeTrackingContext")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool ovrpTracking_CreateEyeTrackingContextNative(out CAPI.ovrAvatar2EyePoseProviderNative outContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool ovrpTracking_CreateHandTrackingContext(
            out CAPI.ovrAvatar2HandTrackingDataContext outContext);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrpTracking_CreateHandTrackingContext")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool ovrpTracking_CreateHandTrackingContextNative(
            out CAPI.ovrAvatar2HandTrackingDataContextNative outContext);


        public static bool Initialize(CAPI.LoggingDelegate cb, IntPtr logContext)
        {
            try
            {
                return ovrpTracking_Initialize(cb, logContext);
            }
            catch (DllNotFoundException)
            {
                OvrAvatarLog.LogWarning($"Lib {LibFile} not found");
                return false;
            }
        }

        public static void Shutdown()
        {
            try
            {
                ovrpTracking_Shutdown();
            }
            catch (DllNotFoundException)
            {

            }
        }


        private static CAPI.ovrAvatar2FacePoseProvider? CreateInternalFaceTrackingContext()
        {
            if (ovrpTracking_CreateFaceTrackingContext(out var context))
            {
                return context;
            }

            return null;
        }

        private static CAPI.ovrAvatar2FacePoseProviderNative? CreateInternalFaceTrackingContextNative()
        {
            if (ovrpTracking_CreateFaceTrackingContextNative(out var context))
            {
                return context;
            }

            return null;
        }

        private static CAPI.ovrAvatar2EyePoseProvider? CreateInternalEyeTrackingContext()
        {
            if (ovrpTracking_CreateEyeTrackingContext(out var context))
            {
                return context;
            }

            return null;
        }

        private static CAPI.ovrAvatar2EyePoseProviderNative? CreateInternalEyeTrackingContextNative()
        {
            if (ovrpTracking_CreateEyeTrackingContextNative(out var context))
            {
                return context;
            }

            return null;
        }

        private static CAPI.ovrAvatar2HandTrackingDataContext? CreateHandTrackingContext()
        {
            if (ovrpTracking_CreateHandTrackingContext(out var context))
            {
                return context;
            }

            return null;
        }

        private static CAPI.ovrAvatar2HandTrackingDataContextNative? CreateHandTrackingContextNative()
        {
            if (ovrpTracking_CreateHandTrackingContextNative(out var context))
            {
                return context;
            }

            return null;
        }

        public static IOvrAvatarHandTrackingDelegate CreateHandTrackingDelegate()
        {
            var context = CreateHandTrackingContext();
            var native = CreateHandTrackingContextNative();
            return context.HasValue && native.HasValue ? new HandTrackingDelegate(context.Value, native.Value) : null;
        }


        public static OvrAvatarFacePoseProviderBase CreateFaceTrackingContext()
        {
            var context = CreateInternalFaceTrackingContext();
            var nativeContext = CreateInternalFaceTrackingContextNative();
            return context.HasValue && nativeContext.HasValue ? new OvrPluginFaceTrackingProvider(context.Value, nativeContext.Value) : null;
        }

        public static OvrAvatarEyePoseProviderBase CreateEyeTrackingContext()
        {
            var context = CreateInternalEyeTrackingContext();
            var nativeContext = CreateInternalEyeTrackingContextNative();
            return context.HasValue && nativeContext.HasValue ? new OvrPluginEyeTrackingProvider(context.Value, nativeContext.Value) : null;
        }

        private class HandTrackingDelegate : IOvrAvatarHandTrackingDelegate, IOvrAvatarNativeHandDelegate
        {
            private CAPI.ovrAvatar2HandTrackingDataContext _context;
            public CAPI.ovrAvatar2HandTrackingDataContextNative NativeContext { get; }

            public HandTrackingDelegate(CAPI.ovrAvatar2HandTrackingDataContext context, CAPI.ovrAvatar2HandTrackingDataContextNative native)
            {
                _context = context;
                NativeContext = native;
            }

            public bool GetHandData(OvrAvatarTrackingHandsState handData)
            {
                if (_context.handTrackingCallback(out var native, _context.context))
                {
                    handData.FromNative(ref native);
                    return true;
                }

                return false;
            }
        }


        private sealed class OvrPluginFaceTrackingProvider : OvrAvatarFacePoseProviderBase, IOvrAvatarNativeFacePose
        {
            private readonly CAPI.ovrAvatar2FacePoseProvider _context;
            private readonly CAPI.ovrAvatar2FacePoseProviderNative _nativeContext;

            CAPI.ovrAvatar2FacePoseProviderNative IOvrAvatarNativeFacePose.NativeProvider => _nativeContext;

            public OvrPluginFaceTrackingProvider(CAPI.ovrAvatar2FacePoseProvider context, CAPI.ovrAvatar2FacePoseProviderNative nativeContext)
            {
                _context = context;
                _nativeContext = nativeContext;
            }

            protected override bool GetFacePose(OvrAvatarFacePose faceState)
            {
                if (_context.facePoseCallback != null &&
                    _context.facePoseCallback(out var nativeFaceState, _context.provider))
                {
                    faceState.FromNative(in nativeFaceState);
                    return true;
                }
                return false;
            }
        }

        private sealed class OvrPluginEyeTrackingProvider : OvrAvatarEyePoseProviderBase, IOvrAvatarNativeEyePose
        {
            private readonly CAPI.ovrAvatar2EyePoseProvider _context;
            private readonly CAPI.ovrAvatar2EyePoseProviderNative _nativeContext;

            CAPI.ovrAvatar2EyePoseProviderNative IOvrAvatarNativeEyePose.NativeProvider => _nativeContext;

            public OvrPluginEyeTrackingProvider(CAPI.ovrAvatar2EyePoseProvider context, CAPI.ovrAvatar2EyePoseProviderNative nativeContext)
            {
                _context = context;
                _nativeContext = nativeContext;
            }

            protected override bool GetEyePose(OvrAvatarEyesPose eyeState)
            {
                if (_context.eyePoseCallback != null &&
                    _context.eyePoseCallback(out var nativeEyeState, _context.provider))
                {
                    eyeState.FromNative(in nativeEyeState);
                    return true;
                }
                return false;
            }
        }
    }
}
