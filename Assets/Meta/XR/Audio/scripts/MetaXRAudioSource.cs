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
 * Filename    :   MetaXRAudioSource.cs
 * Content     :   Interface into the Meta XR Audio Plugin
 ***********************************************************************************/

using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;

[RequireComponent(typeof(AudioSource))]
public class MetaXRAudioSource : MonoBehaviour
{
    private AudioSource source_;
    private bool wasPlaying_ = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnBeforeSceneLoadRuntimeMethod()
    {
        Debug.Log($"Setting spatial voice limit: {MetaXRAudioSettings.Instance.voiceLimit}");
        MetaXRAudio_SetGlobalVoiceLimit(MetaXRAudioSettings.Instance.voiceLimit);
    }

    [SerializeField]
    [Tooltip("Enables HRTF Spatialization. The audio source must be set to 3D")]
    private bool enableSpatialization = true;
    public  bool EnableSpatialization
    {
        get
        {
            return enableSpatialization;
        }
        set
        {
            enableSpatialization = value;
        }
    }

    [SerializeField]
    [Tooltip("Additional gain beyond 0dB")]
    [Range(0.0f, 20.0f)]
    private float gainBoostDb = 0.0f;
    public  float GainBoostDb
    {
        get
        {
            return gainBoostDb;
        }
        set
        {
            gainBoostDb = Mathf.Clamp(value, 0.0f, 20.0f);
        }
    }

    [SerializeField]
    [Tooltip("Enables room acoustics simulation (early reflections and reverberation) for this audio source only")]
    private bool enableAcoustics = false;
    public  bool EnableAcoustics
    {
        get
        {
            return enableAcoustics;
        }
        set
        {
            enableAcoustics = value;
        }
    }

    [SerializeField]
    [Tooltip("Additional gain applied to reverb send for this audio source only")]
    [Range(-60.0f, 20.0f)]
    private float reverbSendDb = 0.0f;
    public float ReverbSendDb
    {
        get
        {
            return reverbSendDb;
        }
        set
        {
            reverbSendDb = Mathf.Clamp(value, -60.0f, 20.0f);
        }
    }

    void Awake()
    {
        source_ = GetComponent<AudioSource>();
        UpdateParameters();
    }

    void Update()
    {
        if (source_ == null)
        {
            source_ = GetComponent<AudioSource>();
            if (source_ == null)
            {
                return;
            }
        }

        bool hasStopped = wasPlaying_ && !source_.isPlaying;

        // Check to see if we should disable spatializion
        if ((Application.isPlaying == false) ||
            (AudioListener.pause == true) ||
            hasStopped ||
            (source_.isActiveAndEnabled == false)
        )
        {
            source_.spatialize = false;
            return;
        }
        else
        {
            UpdateParameters();
        }

        wasPlaying_ = source_.isPlaying;
    }

    public enum NativeParameterIndex : int
    {
        P_GAIN,
        P_USEINVSQR,
        P_NEAR,
        P_FAR,
        P_RADIUS,
        P_DISABLE_RFL,
        P_AMBISTAT,
        P_READONLY_GLOBAL_RFL_ENABLED,
        P_READONLY_NUM_VOICES,
        P_HRTF_INTENSITY,
        P_REFLECTIONS_SEND,
        P_REVERB_SEND,
        P_DIRECTIVITY_ENABLED,
        P_AMBI_DIRECT_ENABLED,
        P_NUM
    };

    public void UpdateParameters()
    {
        source_.spatialize = enableSpatialization;
        source_.SetSpatializerFloat((int)NativeParameterIndex.P_GAIN, gainBoostDb);
        source_.SetSpatializerFloat((int)NativeParameterIndex.P_DISABLE_RFL, enableAcoustics ? 0.0f : 1.0f);
        source_.SetSpatializerFloat((int)NativeParameterIndex.P_REVERB_SEND, reverbSendDb);
    }

    [System.Runtime.InteropServices.DllImport(MetaXRAudioNativeInterface.UnityNativeInterface.binaryName)]
    private static extern int MetaXRAudio_SetGlobalVoiceLimit(int VoiceLimit);
}
