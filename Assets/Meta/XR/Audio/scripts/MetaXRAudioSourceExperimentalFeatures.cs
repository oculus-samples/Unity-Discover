/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/************************************************************************************
 * Filename    :   MetaXRAudioSourceExperimentalFeatures.cs
 * Content     :   Extended Interface into the Meta XR Audio Plugin
 ***********************************************************************************/


using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;

[RequireComponent(typeof(MetaXRAudioSource))]
public class MetaXRAudioSourceExperimentalFeatures : MonoBehaviour
{
    private AudioSource source_;

    // Public
    [SerializeField]
    [Tooltip("How much of the HRTF EQ is applied to the sound. Interaural time delay (ITD) and interaural level differences (ILD) are kept the same.")]
    [Range(0.0f, 1.0f)]
    private float hrtfIntensity = 1.0f;
    public  float HrtfIntensity
    {
        get
        {
            return hrtfIntensity;
        }
        set
        {
            hrtfIntensity = Mathf.Clamp(value, 0.0f, 1.0f);
        }
    }

    [SerializeField]
    [Tooltip("Use to increase the spatial audio emitter radius. Useful for sounds that come from a large area rather than a precise point. If increased too large, users may end up inside the radius if the sound source is too close.")]
    private float volumetricRadius = 0.0f;
    public  float VolumetricRadius
    {
        get
        {
            return volumetricRadius;
        }
        set
        {
            volumetricRadius = Mathf.Max(value, 0.0f);
        }
    }

    [SerializeField]
    [Tooltip("Additional gain applied to early reflections for this audio source only")]
    [Range(-60.0f, 20.0f)]
    private float earlyReflectionsSendDb = 0.0f;
    public float EarlyReflectionsSendDb
    {
        get
        {
            return earlyReflectionsSendDb;
        }
        set
        {
            earlyReflectionsSendDb = Mathf.Clamp(value, -60.0f, 20.0f);
        }
    }

    public enum DirectivityPatternType
    {
        None,
        HumanVoice,
    }

    [SerializeField]
    [Tooltip("Option for human voice directivity pattern that makes this sound more muffled when the source is facing away from listener")]
    private DirectivityPatternType directivityPattern = DirectivityPatternType.None;
    public  DirectivityPatternType DirectivityPattern
    {
        get
        {
            return directivityPattern;
        }
        set
        {
            directivityPattern = value;
        }
    }

    private void OnValidate()
    {
        volumetricRadius = Mathf.Max(volumetricRadius, 0.0f);
    }

    void Awake()
    {
        // We might iterate through multiple sources / game object
        source_ = GetComponent<AudioSource>();
        UpdateParameters();
    }

    void Update()
    {
        // We might iterate through multiple sources / game object
        if (source_ == null)
        {
            source_ = GetComponent<AudioSource>();
            if (source_ == null)
            {
                return;
            }
        }

        UpdateParameters();
    }

    /// <summary>
    /// Sets the parameters.
    /// </summary>
    /// <param name="source">Source.</param>
    public void UpdateParameters()
    {
        source_.SetSpatializerFloat((int)MetaXRAudioSource.NativeParameterIndex.P_HRTF_INTENSITY, hrtfIntensity);
        source_.SetSpatializerFloat((int)MetaXRAudioSource.NativeParameterIndex.P_RADIUS, volumetricRadius);
        source_.SetSpatializerFloat((int)MetaXRAudioSource.NativeParameterIndex.P_REFLECTIONS_SEND, earlyReflectionsSendDb);
        source_.SetSpatializerFloat((int)MetaXRAudioSource.NativeParameterIndex.P_DIRECTIVITY_ENABLED, directivityPattern == DirectivityPatternType.None ? 0.0f : 1.0f);
    }

    // Import functions
    [DllImport(MetaXRAudioNativeInterface.UnityNativeInterface.binaryName)]
    private static extern void MetaXRAudio_GetGlobalRoomReflectionValues(ref bool reflOn, ref bool reverbOn,
        ref float width, ref float height, ref float length);
}
