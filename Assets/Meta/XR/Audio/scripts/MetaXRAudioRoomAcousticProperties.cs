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
 * Filename    :   MetaXRAudioRoomAcousticProperties.cs
 * Content     :   Interface into the Meta XR Audio shoebox reflections system
 ***********************************************************************************/

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Playables;

public sealed class MetaXRAudioRoomAcousticProperties : MonoBehaviour
{
    [Tooltip("Center the room model on the listener. When disabled, center the room model on the GameObject this script is attached to.")]
    public bool lockPositionToListener = true;

    [Tooltip("Width of the room model in meters")]
    public float width = 8.0f;
    [Tooltip("Height of the room model in meters")]
    public float height = 3.0f;
    [Tooltip("Depth of the room model in meters")]
    public float depth = 5.0f;

    [Tooltip("Material of the left wall of the room model")]
    public MaterialPreset leftMaterial = MaterialPreset.GypsumBoard;
    [Tooltip("Material of the right wall of the room model")]
    public MaterialPreset rightMaterial = MaterialPreset.GypsumBoard;
    [Tooltip("Material of the ceiling of the room model")]
    public MaterialPreset ceilingMaterial = MaterialPreset.AcousticTile;
    [Tooltip("Material of the floor of the room model")]
    public MaterialPreset floorMaterial = MaterialPreset.Carpet;
    [Tooltip("Material of the front wall of the room model")]
    public MaterialPreset frontMaterial = MaterialPreset.GypsumBoard;
    [Tooltip("Material of the back wall of the room model")]
    public MaterialPreset backMaterial = MaterialPreset.GypsumBoard;

    [Tooltip("Diffuses the reflections and reverberation to simulate objects inside the room. Zero represents a completely empty room.")]
    [Range(0.0f, 1.0f)]
    public float clutterFactor = 0.5f;

    private AudioListener listener;

    private const int kAudioBandCount = 4;
    private float[] clutterFactorBands = new float[kAudioBandCount];


    float[] wallMaterials = new float[6 * kAudioBandCount];
    public enum MaterialPreset
    {
        AcousticTile,
        Brick,
        BrickPainted,
        Carpet,
        CarpetHeavy,
        CarpetHeavyPadded,
        CeramicTile,
        Concrete,
        ConcreteRough,
        ConcreteBlock,
        ConcreteBlockPainted,
        Curtain,
        Foliage,
        Glass,
        GlassHeavy,
        Grass,
        Gravel,
        GypsumBoard,
        PlasterOnBrick,
        PlasterOnConcreteBlock,
        Soil,
        SoundProof,
        Snow,
        Steel,
        Water,
        WoodThin,
        WoodThick,
        WoodFloor,
        WoodOnConcrete
    }
    
    [RuntimeInitializeOnLoadMethod]
    static void CheckSceneHasRoom()
    {
        MetaXRAudioRoomAcousticProperties[] rooms = FindObjectsOfType<MetaXRAudioRoomAcousticProperties>();
        if (rooms.Length == 0)
        {
            Debug.Log("No Meta XR Audio Room found, setting default room");
            GameObject temp = new GameObject("Temporary Room");
            MetaXRAudioRoomAcousticProperties tempRoom = temp.AddComponent<MetaXRAudioRoomAcousticProperties>();
            tempRoom.Update();
            DestroyImmediate(temp);
        }

        if (rooms.Length > 1)
        {
            Debug.LogError("Multiple Meta XR Audio Rooms found, only one is allowed!");
        }
    }

    void Update()
    {
        SetWallMaterialPreset(0, rightMaterial);
        SetWallMaterialPreset(1, leftMaterial);
        SetWallMaterialPreset(2, ceilingMaterial);
        SetWallMaterialPreset(3, floorMaterial);
        SetWallMaterialPreset(4, frontMaterial);
        SetWallMaterialPreset(5, backMaterial);

        MetaXRAudioNativeInterface.Interface.SetAdvancedBoxRoomParameters(width, height, depth, lockPositionToListener,
            transform.position, wallMaterials);
        float factor = clutterFactor;
        for (int band = kAudioBandCount - 1; band >= 0; --band)
        {
            clutterFactorBands[band] = factor;
            factor *= 0.5f; // clutter has less impact on low frequencies
        }
        MetaXRAudioNativeInterface.Interface.SetRoomClutterFactor(clutterFactorBands);
    }

    void SetWallMaterialPreset(int wallIndex, MaterialPreset materialPreset)
    {
        switch (materialPreset) {
            case MaterialPreset.AcousticTile:           SetWallMaterialProperties(wallIndex, 0.488168418f, 0.361475229f, 0.339595377f, 0.498946249f); break;
            case MaterialPreset.Brick:                  SetWallMaterialProperties(wallIndex, 0.975468814f, 0.972064495f, 0.949180186f, 0.930105388f); break;
            case MaterialPreset.BrickPainted:           SetWallMaterialProperties(wallIndex, 0.975710571f, 0.983324170f, 0.978116691f, 0.970052719f); break;
            case MaterialPreset.Carpet:                 SetWallMaterialProperties(wallIndex, 0.987633705f, 0.905486643f, 0.583110571f, 0.351053834f); break;
            case MaterialPreset.CarpetHeavy:            SetWallMaterialProperties(wallIndex, 0.977633715f, 0.859082878f, 0.526479602f, 0.370790422f); break;
            case MaterialPreset.CarpetHeavyPadded:      SetWallMaterialProperties(wallIndex, 0.910534739f, 0.530433178f, 0.294055820f, 0.270105422f); break;
            case MaterialPreset.CeramicTile:            SetWallMaterialProperties(wallIndex, 0.990000010f, 0.990000010f, 0.982753932f, 0.980000019f); break;
            case MaterialPreset.Concrete:               SetWallMaterialProperties(wallIndex, 0.990000010f, 0.983324170f, 0.980000019f, 0.980000019f); break;
            case MaterialPreset.ConcreteRough:          SetWallMaterialProperties(wallIndex, 0.989408433f, 0.964494646f, 0.922127008f, 0.900105357f); break;
            case MaterialPreset.ConcreteBlock:          SetWallMaterialProperties(wallIndex, 0.635267377f, 0.652230680f, 0.671053469f, 0.789051592f); break;
            case MaterialPreset.ConcreteBlockPainted:   SetWallMaterialProperties(wallIndex, 0.902957916f, 0.940235913f, 0.917584062f, 0.919947326f); break;
            case MaterialPreset.Curtain:                SetWallMaterialProperties(wallIndex, 0.686494231f, 0.545859993f, 0.310078561f, 0.399473131f); break;
            case MaterialPreset.Foliage:                SetWallMaterialProperties(wallIndex, 0.518259346f, 0.503568292f, 0.578688800f, 0.690210819f); break;
            case MaterialPreset.Glass:                  SetWallMaterialProperties(wallIndex, 0.655915797f, 0.800631821f, 0.918839693f, 0.923488140f); break;
            case MaterialPreset.GlassHeavy:             SetWallMaterialProperties(wallIndex, 0.827098966f, 0.950222731f, 0.974604130f, 0.980000019f); break;
            case MaterialPreset.Grass:                  SetWallMaterialProperties(wallIndex, 0.881126285f, 0.507170796f, 0.131893098f, 0.0103688836f); break;
            case MaterialPreset.Gravel:                 SetWallMaterialProperties(wallIndex, 0.729294717f, 0.373122454f, 0.255317450f, 0.200263441f); break;
            case MaterialPreset.GypsumBoard:            SetWallMaterialProperties(wallIndex, 0.721240044f, 0.927690148f, 0.934302270f, 0.910105407f); break;
            case MaterialPreset.PlasterOnBrick:         SetWallMaterialProperties(wallIndex, 0.975696504f, 0.979106009f, 0.961063504f, 0.950052679f); break;
            case MaterialPreset.PlasterOnConcreteBlock: SetWallMaterialProperties(wallIndex, 0.881774724f, 0.924773932f, 0.951497555f, 0.959947288f); break;
            case MaterialPreset.Soil:                   SetWallMaterialProperties(wallIndex, 0.844084203f, 0.634624243f, 0.416662872f, 0.400000036f); break;
            case MaterialPreset.SoundProof:             SetWallMaterialProperties(wallIndex, 0.000000000f, 0.000000000f, 0.000000000f, 0.000000000f); break;
            case MaterialPreset.Snow:                   SetWallMaterialProperties(wallIndex, 0.532252669f, 0.154535770f, 0.0509644151f, 0.0500000119f); break;
            case MaterialPreset.Steel:                  SetWallMaterialProperties(wallIndex, 0.793111682f, 0.840140402f, 0.925591767f, 0.979736567f); break;
            case MaterialPreset.Water:                  SetWallMaterialProperties(wallIndex, 0.970588267f, 0.971753478f, 0.978309572f, 0.970052719f); break;
            case MaterialPreset.WoodThin:               SetWallMaterialProperties(wallIndex, 0.592423141f, 0.858273327f, 0.917242289f, 0.939999998f); break;
            case MaterialPreset.WoodThick:              SetWallMaterialProperties(wallIndex, 0.812957883f, 0.895329595f, 0.941304684f, 0.949947298f); break;
            case MaterialPreset.WoodFloor:              SetWallMaterialProperties(wallIndex, 0.852366328f, 0.898992121f, 0.934784114f, 0.930052698f); break;
            case MaterialPreset.WoodOnConcrete:         SetWallMaterialProperties(wallIndex, 0.959999979f, 0.941232264f, 0.937923789f, 0.930052698f); break;
            //default:
            //    break;
        }
    }
    void SetWallMaterialProperties(int wallIndex, float band0, float band1, float band2, float band3)
    {
        wallMaterials[wallIndex * 4 + 0] = band0;
        wallMaterials[wallIndex * 4 + 1] = band1;
        wallMaterials[wallIndex * 4 + 2] = band2;
        wallMaterials[wallIndex * 4 + 3] = band3;
    }

    void OnDrawGizmosSelected()
    {
        if (!listener)
        {
            listener = Camera.main.GetComponent<AudioListener>();
        }
        Vector3 center = lockPositionToListener ? listener.transform.position : transform.position;
        Gizmos.color = new Color(0.8f, 0.8f, 0.8f, 0.25f);
        Gizmos.DrawCube(center, new Vector3(width, height, depth));
    }
}
