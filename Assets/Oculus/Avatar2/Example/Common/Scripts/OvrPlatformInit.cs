#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

#if USING_XR_SDK

using System;
using Oculus.Avatar2;
using Oculus.Platform;
using Oculus.Platform.Models;

public enum OvrPlatformInitStatus
{
    NotStarted = 0,
    Initializing,
    Succeeded,
    Failed
}

public static class OvrPlatformInit
{
    private const string logScope = "examplePlatformInit";

    public static OvrPlatformInitStatus status { get; private set; } = OvrPlatformInitStatus.NotStarted;

    public static void InitializeOvrPlatform()
    {
        if (status == OvrPlatformInitStatus.Succeeded)
        {
            OvrAvatarLog.LogWarning("OvrPlatform is already initialized.", logScope);
            return;
        }

        try
        {
            status = OvrPlatformInitStatus.Initializing;
            Core.AsyncInitialize().OnComplete(InitializeComplete);

            void InitializeComplete(Message<PlatformInitialize> msg)
            {
                if (msg.Data.Result != PlatformInitializeResult.Success)
                {
                    status = OvrPlatformInitStatus.Failed;
                    OvrAvatarLog.LogError("Failed to initialize OvrPlatform", logScope);
                }
                else
                {
                    Entitlements.IsUserEntitledToApplication().OnComplete(CheckEntitlement);
                }
            }

            void CheckEntitlement(Message msg)
            {
                if (msg.IsError == false)
                {
                    Users.GetAccessToken().OnComplete(GetAccessTokenComplete);
                }
                else
                {
                    status = OvrPlatformInitStatus.Failed;
                    var e = msg.GetError();
                    OvrAvatarLog.LogError($"Failed entitlement check: {e.Code} - {e.Message}", logScope);
                }
            }

            void GetAccessTokenComplete(Message<string> msg)
            {
                if (String.IsNullOrEmpty(msg.Data))
                {
                    string output = "Token is null or empty.";
                    if (msg.IsError)
                    {
                        var e = msg.GetError();
                        output = $"{e.Code} - {e.Message}";
                    }

                    status = OvrPlatformInitStatus.Failed;
                    OvrAvatarLog.LogError($"Failed to retrieve access token: {output}", logScope);
                }
                else
                {
                    OvrAvatarLog.LogDebug($"Successfully retrieved access token.", logScope);
                    OvrAvatarEntitlement.SetAccessToken(msg.Data);
                    status = OvrPlatformInitStatus.Succeeded;
                }
            }
        }
        catch (Exception e)
        {
            status = OvrPlatformInitStatus.Failed;
            OvrAvatarLog.LogError($"{e.Message}\n{e.StackTrace}", logScope);
        }
    }
}

#endif
