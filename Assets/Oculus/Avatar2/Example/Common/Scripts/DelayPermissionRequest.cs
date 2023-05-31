#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

#if USING_XR_SDK
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Avatar2
{
    public class DelayPermissionRequest : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            OvrAvatarManager.Instance.automaticallyRequestPermissions = false;
        }

        // Update is called once per frame
        void Update()
        {

            if (OVRInput.Get(OVRInput.Button.Two))
            {
                OvrAvatarManager.Instance.EnablePermissionRequests();
            }
        }
    }
}
#endif
