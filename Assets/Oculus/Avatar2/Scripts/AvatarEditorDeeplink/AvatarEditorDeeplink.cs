#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Oculus.Avatar2;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using Daybreak.IPC;
#endif

public static class AvatarEditorDeeplink
{
    private const string logscope = "deeplink";

    public static void LaunchAvatarEditor()
    {
        OvrAvatarLog.LogInfo("Launching Avatar Editor", logscope);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (!OvrAvatarEntitlement.AccessTokenIsValid())
        {
            OvrAvatarLog.LogError(
                "Launching the Avatar Editor requires app entitlement. Set a valid access token in OvrAvatarEntitlement");
            return;
        }

        if (LoadLibrary() == LoadLibraryResult.Success)
        {
            // Unity 2020 seems to have issues with multiple requests using the same connector,
            // so we must recreate the connector every time
            if (!IpcOafConnector.Recreate().IsConnected())
            {
                OvrAvatarLog.LogError("LaunchAvatarEditor failed: Couldn't connect IpcOafConnector", logscope);
            }

            IpcOafConnector.RequestWork(
                "/social/avatar/start",
                new Dictionary<string, object>()
                {
                    {
                        "source",
                        System.Convert.ToBase64String(
                            System.Text.Encoding.UTF8.GetBytes(
                                "\"avatar_2_sdk\""))
                    }
                },
                (dataSent, dataReceived, error) =>
                {
                    if (error != null)
                    {
                        error.PopulateFromErrorCode("Unknown error");
                        if (dataReceived.payloadType == "NOTIFICATION")
                        {
                            RecvNotificationInnerData payload = (RecvNotificationInnerData) dataReceived.payload;
                            OvrAvatarLog.LogError(
                                $"Failed to launch avatar editor: {payload.notificationType} ({payload.errorCode}) - {payload.title},\n{payload.description}\n{payload.stackTrace}");
                        }
                        else
                        {
                            OvrAvatarLog.LogError(
                                $"Failed to launch avatar editor: {(Code)error.ErrorCode} - {error.Title}\n{error.Description}\n{error.StackTrace}",
                                logscope);
                        }
                    }
                }
            );
        }
#else
        string deeplinkUri = $"/?version=V2&returnUrl=apk://{Application.identifier}";

        AndroidJavaObject activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
        var intent = new AndroidJavaObject("android.content.Intent");

        intent.Call<AndroidJavaObject>("setPackage", "com.oculus.vrshell");
        intent.Call<AndroidJavaObject>("setAction", "com.oculus.vrshell.intent.action.LAUNCH");
        intent.Call<AndroidJavaObject>("putExtra", "intent_data", "systemux://avatareditor");
        intent.Call<AndroidJavaObject>("putExtra", "uri", deeplinkUri);

        // Broadcast instead of starting activity, so that it goes to overlay
        currentActivity.Call("sendBroadcast", intent);
#endif
    }

    // TODO: We've had to copy this code for a number of dlls now. Make a generic class to handle this case?
    private enum LoadLibraryResult : Int32
    {
        Success = 0,
        Failure = 1,
        Unknown = 2
    }

    private static LoadLibraryResult LoadLibrary()
    {
        LoadLibraryResult loadResult;
        try
        {
            ovrAvatar2_forceLibraryLoad();
            // This call should have failed
            loadResult = LoadLibraryResult.Unknown;
        }
        catch (Exception e)
        {
            loadResult = !(e is DllNotFoundException) ? LoadLibraryResult.Success : LoadLibraryResult.Failure;
            if (!(e is EntryPointNotFoundException))
            {
                OvrAvatarLog.LogError($"Unexpected exception, {e.ToString()}", logscope);
            }
        }
        if (loadResult != LoadLibraryResult.Success)
        {
            OvrAvatarLog.LogError("Unable to find OafIpc.dll!", logscope);
        }
        return loadResult;
    }

    private const string OafIpcLibFile = "OafIpc";

    // This method *should* not exist -
    // we are using it to trigger an expected exception and force DLL load in older Unity versions
    [DllImport(OafIpcLibFile, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ovrAvatar2_forceLibraryLoad();
}
