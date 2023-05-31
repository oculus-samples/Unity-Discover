#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenAvatarEditor : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
#if USING_XR_SDK
        // Button Press
        if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch | OVRInput.Controller.LHand))
        {
            AvatarEditorDeeplink.LaunchAvatarEditor();
        }
#endif
    }
}
