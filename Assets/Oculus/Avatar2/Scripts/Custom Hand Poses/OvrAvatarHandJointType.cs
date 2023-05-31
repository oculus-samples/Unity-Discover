using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Avatar2;
using UnityEngine;

public class OvrAvatarHandJointType : MonoBehaviour
{
    public enum HandJointType : Int32 {
        Invalid = -1,
            
        Wrist = 0,
        ThumbTrapezium,
        ThumbMeta,
        ThumbProximal,
        ThumbDistal,
        IndexMeta,
        IndexProximal,
        IndexIntermediate,
        IndexDistal,
        MiddleMeta,
        MiddleProximal,
        MiddleIntermediate,
        MiddleDistal,
        RingMeta,
        RingProximal,
        RingIntermediate,
        RingDistal,
        PinkyMeta,
        PinkyProximal,
        PinkyIntermediate,
        PinkyDistal,

        Count,
    }
    
    public HandJointType jointType;
}
