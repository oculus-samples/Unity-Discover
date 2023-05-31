#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleAvatarLocomotion : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Controls the speed of movement")]
    public float movementSpeed = 1.0f;

    // (1, 0, -1)
    private Vector3 mirrorVector = Vector3.right + Vector3.back;

    void Update()
    {
#if USING_XR_SDK
        // Moves the avatar forward/back and left/right based on primary input
        var primaryThumbstickVector = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        var translationVector = new Vector3(primaryThumbstickVector.x, 0.0f, primaryThumbstickVector.y);
        transform.Translate(translationVector * Time.deltaTime * movementSpeed);
#endif
    }
}
