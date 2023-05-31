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

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Meta.XR.Audio;

namespace Meta
{
    namespace XR
    {
        namespace Audio
        {
            /***********************************************************************************/
            // ENUMS and STRUCTS
            /***********************************************************************************/
            public enum FaceType : uint
            {
                TRIANGLES = 0,
                QUADS
            }

            public enum MaterialProperty : uint
            {
                ABSORPTION = 0,
                TRANSMISSION,
                SCATTERING
            }

            // Matches internal mesh layout
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct MeshGroup
            {
                public UIntPtr indexOffset;
                public UIntPtr faceCount;
                [MarshalAs(UnmanagedType.U4)]
                public FaceType faceType;
                public IntPtr material;
            }
        }
    }
}

public class MetaXRAudioNativeInterface
{
    static NativeInterface CachedInterface;
    public static NativeInterface Interface { get { if (CachedInterface == null) CachedInterface = FindInterface(); return CachedInterface; } }

    static NativeInterface FindInterface()
    {
        IntPtr temp;
        try
        {
            WwisePluginInterface.ovrAudio_GetPluginContext(out temp, ClientType.OVRA_CLIENT_TYPE_WWISE_UNKNOWN);
            Debug.Log("Meta XR Audio Native Interface initialized with Wwise plugin");
            return new WwisePluginInterface();
        }
        catch(System.DllNotFoundException)
        {
            // this is fine
        }
        try
        {
            FMODPluginInterface.ovrAudio_GetPluginContext(out temp, ClientType.OVRA_CLIENT_TYPE_FMOD);
            Debug.Log("Meta XR Audio Native Interface initialized with FMOD plugin");
            return new FMODPluginInterface();
        }
        catch (System.DllNotFoundException)
        {
            // this is fine
        }
        try
        {
            temp = MunroInterface.getOrCreateGlobalOvrAudioContext();
            Debug.Log("Meta XR Audio Native Interface initialized with Munro plugin");
            return new MunroInterface();
        }
        catch (System.DllNotFoundException)
        {
            // this is fine
        }

        Debug.Log("Meta XR Audio Native Interface initialized with Unity plugin");
        return new UnityNativeInterface();
    }

    public enum ovrAudioScalarType : uint
    {
        Int8,
        UInt8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float16,
        Float32,
        Float64
    }

    public class ClientType
    {
        // Copied from AudioSDK\OVRAudio\OVR_Audio_Internal.h
        public const uint OVRA_CLIENT_TYPE_NATIVE = 0;
        public const uint OVRA_CLIENT_TYPE_WWISE_2016 = 1;
        public const uint OVRA_CLIENT_TYPE_WWISE_2017_1 = 2;
        public const uint OVRA_CLIENT_TYPE_WWISE_2017_2 = 3;
        public const uint OVRA_CLIENT_TYPE_WWISE_2018_1 = 4;
        public const uint OVRA_CLIENT_TYPE_FMOD = 5;
        public const uint OVRA_CLIENT_TYPE_UNITY = 6;
        public const uint OVRA_CLIENT_TYPE_UE4 = 7;
        public const uint OVRA_CLIENT_TYPE_VST = 8;
        public const uint OVRA_CLIENT_TYPE_AAX = 9;
        public const uint OVRA_CLIENT_TYPE_TEST = 10;
        public const uint OVRA_CLIENT_TYPE_OTHER = 11;
        public const uint OVRA_CLIENT_TYPE_WWISE_UNKNOWN = 12;
    }

    public interface NativeInterface
    {
        /***********************************************************************************/
        // Settings API
        int SetPropagationQuality(float quality);
        int SetPropagationThreadAffinity(UInt64 cpuMask);

        /***********************************************************************************/
        // Geometry API
        int CreateAudioGeometry(out IntPtr geometry);
        int DestroyAudioGeometry(IntPtr geometry);
        int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount);
        int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
        int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        int AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);

        /***********************************************************************************/
        // Material API
        int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        int CreateAudioMaterial(out IntPtr material);
        int DestroyAudioMaterial(IntPtr material);
        int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        int AudioMaterialReset(IntPtr material, MaterialProperty property);

        /***********************************************************************************/
        // Shoebox Reflections API
        int SetAdvancedBoxRoomParameters(float width, float height, float depth,
            bool lockToListenerPosition, Vector3 position, float[] wallMaterials);

        int SetRoomClutterFactor(float[] clutterFactor);

        int SetReflectionModel(int reflectionModel);
        int SetEnabled(int feature, bool enabled);

        int SetDynamicRoomRaysPerSecond(int RaysPerSecond);
        int SetDynamicRoomInterpSpeed(float InterpSpeed);
        int SetDynamicRoomMaxWallDistance(float MaxWallDistance);
        int SetDynamicRoomRaysRayCacheSize(int RayCacheSize);
        int GetRoomDimensions(float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position);
        int GetRaycastHits(Vector3[] points, Vector3[] normals, int length);
    }

    /***********************************************************************************/
    // UNITY NATIVE
    /***********************************************************************************/
    public class UnityNativeInterface : NativeInterface
    {
        // The name used for the plugin DLL.
        public const string binaryName = "MetaXRAudioUnity";

        /***********************************************************************************/
        // Context API: Required to create internal context if it does not exist yet
        IntPtr context_ = IntPtr.Zero;
        IntPtr context { get { if (context_ == IntPtr.Zero) { ovrAudio_GetPluginContext(out context_, ClientType.OVRA_CLIENT_TYPE_UNITY); } return context_; } }

        [DllImport(binaryName)]
        public static extern int ovrAudio_GetPluginContext(out IntPtr context, uint clientType);

        /***********************************************************************************/
        // Settings API
        [DllImport(binaryName)]
        private static extern int ovrAudio_SetPropagationQuality(IntPtr context, float quality);
        public int SetPropagationQuality(float quality)
        {
            return ovrAudio_SetPropagationQuality(context, quality);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_SetPropagationThreadAffinity(IntPtr context, UInt64 cpuMask);
        public int SetPropagationThreadAffinity(UInt64 cpuMask)
        {
            return ovrAudio_SetPropagationThreadAffinity(context, cpuMask);
        }

        /***********************************************************************************/
        // Geometry API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
        public int CreateAudioGeometry(out IntPtr geometry)
        {
            return ovrAudio_CreateAudioGeometry(context, out geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
        public int DestroyAudioGeometry(IntPtr geometry)
        {
            return ovrAudio_DestroyAudioGeometry(geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount);

        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount)
        {
            return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
        public int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4)
        {
            return ovrAudio_AudioGeometrySetTransform(geometry, matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
        {
            return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFileObj(geometry, filePath);
        }

        /***********************************************************************************/
        // Material API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
        public int CreateAudioMaterial(out IntPtr material)
        {
            return ovrAudio_CreateAudioMaterial(context, out material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
        public int DestroyAudioMaterial(IntPtr material)
        {
            return ovrAudio_DestroyAudioMaterial(material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
        {
            return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
        {
            return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
        public int AudioMaterialReset(IntPtr material, MaterialProperty property)
        {
            return ovrAudio_AudioMaterialReset(material, property);
        }

        /***********************************************************************************/
        // Shoebox Reflections API
        [DllImport(binaryName)]
        private static extern int ovrAudio_SetAdvancedBoxRoomParametersUnity(IntPtr context, float width, float height, float depth,
            bool lockToListenerPosition, float positionX, float positionY, float positionZ,
            float[] wallMaterials);
        public int SetAdvancedBoxRoomParameters(float width, float height, float depth,
            bool lockToListenerPosition, Vector3 position, float[] wallMaterials)
        {
            return ovrAudio_SetAdvancedBoxRoomParametersUnity(context, width, height, depth,
                lockToListenerPosition, position.x, position.y, -position.z, wallMaterials);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_SetRoomClutterFactor(IntPtr context, float[] clutterFactor);
        public int SetRoomClutterFactor(float[] clutterFactor)
        {
            return ovrAudio_SetRoomClutterFactor(context, clutterFactor);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_SetReflectionModel(IntPtr context, int model);
        public int SetReflectionModel(int model)
        {
            return ovrAudio_SetReflectionModel(context, model);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_Enable(IntPtr context, int what, int enable);
        public int SetEnabled(int feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_SetDynamicRoomRaysPerSecond(IntPtr context, int RaysPerSecond);
        public int SetDynamicRoomRaysPerSecond(int RaysPerSecond)
        {
            return ovrAudio_SetDynamicRoomRaysPerSecond(context, RaysPerSecond);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_SetDynamicRoomInterpSpeed(IntPtr context, float InterpSpeed);
        public int SetDynamicRoomInterpSpeed(float InterpSpeed)
        {
            return ovrAudio_SetDynamicRoomInterpSpeed(context, InterpSpeed);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_SetDynamicRoomMaxWallDistance(IntPtr context, float MaxWallDistance);
        public int SetDynamicRoomMaxWallDistance(float MaxWallDistance)
        {
            return ovrAudio_SetDynamicRoomMaxWallDistance(context, MaxWallDistance);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_SetDynamicRoomRaysRayCacheSize(IntPtr context, int RayCacheSize);
        public int SetDynamicRoomRaysRayCacheSize(int RayCacheSize)
        {
            return ovrAudio_SetDynamicRoomRaysRayCacheSize(context, RayCacheSize);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_GetRoomDimensions(IntPtr context, float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position);
        public int GetRoomDimensions(float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position)
        {
            return ovrAudio_GetRoomDimensions(context, roomDimensions, reflectionsCoefs, out position);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_GetRaycastHits(IntPtr context, Vector3[] points, Vector3[] normals, int length);
        public int GetRaycastHits(Vector3[] points, Vector3[] normals, int length)
        {
            return ovrAudio_GetRaycastHits(context, points, normals, length);
        }
    }

    /***********************************************************************************/
    // WWISE
    /***********************************************************************************/
    public class WwisePluginInterface : NativeInterface
    {
        // The name used for the plugin DLL.
        public const string strOSPS = "OculusSpatializerWwise";

        /***********************************************************************************/
        // Context API: Required to create internal context if it does not exist yet
        IntPtr context_ = IntPtr.Zero;
        IntPtr context { get { if (context_ == IntPtr.Zero) { ovrAudio_GetPluginContext(out context_, ClientType.OVRA_CLIENT_TYPE_WWISE_UNKNOWN); } return context_; } }

        [DllImport(strOSPS)]
        public static extern int ovrAudio_GetPluginContext(out IntPtr context, uint clientType);

        /***********************************************************************************/
        // Settings API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetPropagationQuality(IntPtr context, float quality);
        public int SetPropagationQuality(float quality)
        {
            return ovrAudio_SetPropagationQuality(context, quality);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetPropagationThreadAffinity(IntPtr context, UInt64 cpuMask);
        public int SetPropagationThreadAffinity(UInt64 cpuMask)
        {
            return ovrAudio_SetPropagationThreadAffinity(context, cpuMask);
        }

        /***********************************************************************************/
        // Geometry API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
        public int CreateAudioGeometry(out IntPtr geometry)
        {
            return ovrAudio_CreateAudioGeometry(context, out geometry);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
        public int DestroyAudioGeometry(IntPtr geometry)
        {
            return ovrAudio_DestroyAudioGeometry(geometry);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount);

        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount)
        {
            return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
        public int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4)
        {
            return ovrAudio_AudioGeometrySetTransform(geometry, matrix4x4);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
        {
            return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFileObj(geometry, filePath);
        }

        /***********************************************************************************/
        // Material API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
        public int CreateAudioMaterial(out IntPtr material)
        {
            return ovrAudio_CreateAudioMaterial(context, out material);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
        public int DestroyAudioMaterial(IntPtr material)
        {
            return ovrAudio_DestroyAudioMaterial(material);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
        {
            return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
        {
            return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
        public int AudioMaterialReset(IntPtr material, MaterialProperty property)
        {
            return ovrAudio_AudioMaterialReset(material, property);
        }

        /***********************************************************************************/
        // Shoebox Reflections API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetAdvancedBoxRoomParametersUnity(IntPtr context, float width, float height, float depth,
            bool lockToListenerPosition, float positionX, float positionY, float positionZ,
            float[] wallMaterials);
        public int SetAdvancedBoxRoomParameters(float width, float height, float depth,
            bool lockToListenerPosition, Vector3 position, float[] wallMaterials)
        {
            return ovrAudio_SetAdvancedBoxRoomParametersUnity(context, width, height, depth,
                lockToListenerPosition, position.x, position.y, -position.z, wallMaterials);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetRoomClutterFactor(IntPtr context, float[] clutterFactor);
        public int SetRoomClutterFactor(float[] clutterFactor)
        {
            return ovrAudio_SetRoomClutterFactor(context, clutterFactor);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetReflectionModel(IntPtr context, int model);

        public int SetReflectionModel(int model)
        {
            return ovrAudio_SetReflectionModel(context, model);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_Enable(IntPtr context, int what, int enable);
        public int SetEnabled(int feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomRaysPerSecond(IntPtr context, int RaysPerSecond);
        public int SetDynamicRoomRaysPerSecond(int RaysPerSecond)
        {
            return ovrAudio_SetDynamicRoomRaysPerSecond(context, RaysPerSecond);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomInterpSpeed(IntPtr context, float InterpSpeed);
        public int SetDynamicRoomInterpSpeed(float InterpSpeed)
        {
            return ovrAudio_SetDynamicRoomInterpSpeed(context, InterpSpeed);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomMaxWallDistance(IntPtr context, float MaxWallDistance);
        public int SetDynamicRoomMaxWallDistance(float MaxWallDistance)
        {
            return ovrAudio_SetDynamicRoomMaxWallDistance(context, MaxWallDistance);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomRaysRayCacheSize(IntPtr context, int RayCacheSize);
        public int SetDynamicRoomRaysRayCacheSize(int RayCacheSize)
        {
            return ovrAudio_SetDynamicRoomRaysRayCacheSize(context, RayCacheSize);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_GetRoomDimensions(IntPtr context, float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position);
        public int GetRoomDimensions(float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position)
        {
            return ovrAudio_GetRoomDimensions(context, roomDimensions, reflectionsCoefs, out position);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_GetRaycastHits(IntPtr context, Vector3[] points, Vector3[] normals, int length);
        public int GetRaycastHits(Vector3[] points, Vector3[] normals, int length)
        {
            return ovrAudio_GetRaycastHits(context, points, normals, length);
        }
    }

    /***********************************************************************************/
    // FMOD
    /***********************************************************************************/
    public class FMODPluginInterface : NativeInterface
    {
        // The name used for the plugin DLL.
        public const string strOSPS = "OculusSpatializerFMOD";

        /***********************************************************************************/
        // Context API: Required to create internal context if it does not exist yet
        IntPtr context_ = IntPtr.Zero;
        IntPtr context { get { if (context_ == IntPtr.Zero) { ovrAudio_GetPluginContext(out context_, ClientType.OVRA_CLIENT_TYPE_FMOD); } return context_; } }

        [DllImport(strOSPS)]
        public static extern int ovrAudio_GetPluginContext(out IntPtr context, uint clientType);

        /***********************************************************************************/
        // Settings API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetPropagationQuality(IntPtr context, float quality);
        public int SetPropagationQuality(float quality)
        {
            return ovrAudio_SetPropagationQuality(context, quality);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetPropagationThreadAffinity(IntPtr context, UInt64 cpuMask);
        public int SetPropagationThreadAffinity(UInt64 cpuMask)
        {
            return ovrAudio_SetPropagationThreadAffinity(context, cpuMask);
        }

        /***********************************************************************************/
        // Geometry API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
        public int CreateAudioGeometry(out IntPtr geometry)
        {
            return ovrAudio_CreateAudioGeometry(context, out geometry);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
        public int DestroyAudioGeometry(IntPtr geometry)
        {
            return ovrAudio_DestroyAudioGeometry(geometry);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount);

        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount)
        {
            return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
        public int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4)
        {
            return ovrAudio_AudioGeometrySetTransform(geometry, matrix4x4);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
        {
            return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFileObj(geometry, filePath);
        }

        /***********************************************************************************/
        // Material API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
        public int CreateAudioMaterial(out IntPtr material)
        {
            return ovrAudio_CreateAudioMaterial(context, out material);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
        public int DestroyAudioMaterial(IntPtr material)
        {
            return ovrAudio_DestroyAudioMaterial(material);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
        {
            return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
        {
            return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
        public int AudioMaterialReset(IntPtr material, MaterialProperty property)
        {
            return ovrAudio_AudioMaterialReset(material, property);
        }

        /***********************************************************************************/
        // Shoebox Reflections API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetAdvancedBoxRoomParametersUnity(IntPtr context, float width, float height, float depth,
            bool lockToListenerPosition, float positionX, float positionY, float positionZ,
            float[] wallMaterials);
        public int SetAdvancedBoxRoomParameters(float width, float height, float depth,
            bool lockToListenerPosition, Vector3 position, float[] wallMaterials)
        {
            return ovrAudio_SetAdvancedBoxRoomParametersUnity(context, width, height, depth,
                lockToListenerPosition, position.x, position.y, -position.z, wallMaterials);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetRoomClutterFactor(IntPtr context, float[] clutterFactor);
        public int SetRoomClutterFactor(float[] clutterFactor)
        {
            return ovrAudio_SetRoomClutterFactor(context, clutterFactor);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetReflectionModel(IntPtr context, int model);

        public int SetReflectionModel(int model)
        {
            return ovrAudio_SetReflectionModel(context, model);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_Enable(IntPtr context, int what, int enable);
        public int SetEnabled(int feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomRaysPerSecond(IntPtr context, int RaysPerSecond);
        public int SetDynamicRoomRaysPerSecond(int RaysPerSecond)
        {
            return ovrAudio_SetDynamicRoomRaysPerSecond(context, RaysPerSecond);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomInterpSpeed(IntPtr context, float InterpSpeed);
        public int SetDynamicRoomInterpSpeed(float InterpSpeed)
        {
            return ovrAudio_SetDynamicRoomInterpSpeed(context, InterpSpeed);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomMaxWallDistance(IntPtr context, float MaxWallDistance);
        public int SetDynamicRoomMaxWallDistance(float MaxWallDistance)
        {
            return ovrAudio_SetDynamicRoomMaxWallDistance(context, MaxWallDistance);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomRaysRayCacheSize(IntPtr context, int RayCacheSize);
        public int SetDynamicRoomRaysRayCacheSize(int RayCacheSize)
        {
            return ovrAudio_SetDynamicRoomRaysRayCacheSize(context, RayCacheSize);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_GetRoomDimensions(IntPtr context, float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position);
        public int GetRoomDimensions(float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position)
        {
            return ovrAudio_GetRoomDimensions(context, roomDimensions, reflectionsCoefs, out position);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_GetRaycastHits(IntPtr context, Vector3[] points, Vector3[] normals, int length);
        public int GetRaycastHits(Vector3[] points, Vector3[] normals, int length)
        {
            return ovrAudio_GetRaycastHits(context, points, normals, length);
        }
    }


    /***********************************************************************************/
    // Munro
    /***********************************************************************************/
    public class MunroInterface : NativeInterface
    {
        // The name used for the plugin DLL.
        public const string strOSPS = "Audio360CSharp";

        /***********************************************************************************/
        // Context API: Required to create internal context if it does not exist yet
        static IntPtr context_ = IntPtr.Zero;

        static void OnDestroyContext(IntPtr context)
        {
            // Note: any geometry will not be carried over to a new context!
            if (context != context_)
            {
                Debug.LogError("Context mismatch, current context=" + context_ + ", destroyed context=" + context);
            }

            setOnDestroyContextCallback(IntPtr.Zero);
            context_ = IntPtr.Zero;
        }

        ~MunroInterface()
        {
            setOnDestroyContextCallback(IntPtr.Zero);
        }

        public IntPtr context
        {
            get
            {
                if (context_ == IntPtr.Zero)
                {
                    context_ = getOrCreateGlobalOvrAudioContext();
                    setOnDestroyContextCallback(OnDestroyContext);
                }

                return context_;
            }
        }

        [DllImport(strOSPS)]
        public static extern IntPtr getOrCreateGlobalOvrAudioContext();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void OnDestroyContextCallback(IntPtr context);

        [DllImport(strOSPS)]
        private static extern void setOnDestroyContextCallback(OnDestroyContextCallback callback);

        [DllImport(strOSPS)]
        private static extern void setOnDestroyContextCallback(IntPtr callback);

        /***********************************************************************************/
        // Settings API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetPropagationQuality(IntPtr context, float quality);
        public int SetPropagationQuality(float quality)
        {
            return ovrAudio_SetPropagationQuality(context, quality);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetPropagationThreadAffinity(IntPtr context, UInt64 cpuMask);
        public int SetPropagationThreadAffinity(UInt64 cpuMask)
        {
            return ovrAudio_SetPropagationThreadAffinity(context, cpuMask);
        }

        /***********************************************************************************/
        // Geometry API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
        public int CreateAudioGeometry(out IntPtr geometry)
        {
            return ovrAudio_CreateAudioGeometry(context, out geometry);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
        public int DestroyAudioGeometry(IntPtr geometry)
        {
            return ovrAudio_DestroyAudioGeometry(geometry);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount);

        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount)
        {
            return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
        public int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4)
        {
            return ovrAudio_AudioGeometrySetTransform(geometry, matrix4x4);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
        {
            return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFileObj(geometry, filePath);
        }

        /***********************************************************************************/
        // Material API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
        public int CreateAudioMaterial(out IntPtr material)
        {
            return ovrAudio_CreateAudioMaterial(context, out material);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
        public int DestroyAudioMaterial(IntPtr material)
        {
            return ovrAudio_DestroyAudioMaterial(material);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
        {
            return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
        {
            return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
        public int AudioMaterialReset(IntPtr material, MaterialProperty property)
        {
            return ovrAudio_AudioMaterialReset(material, property);
        }

        /***********************************************************************************/
        // Shoebox Reflections API
        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetAdvancedBoxRoomParametersUnity(IntPtr context, float width, float height, float depth,
            bool lockToListenerPosition, float positionX, float positionY, float positionZ,
            float[] wallMaterials);
        public int SetAdvancedBoxRoomParameters(float width, float height, float depth,
            bool lockToListenerPosition, Vector3 position, float[] wallMaterials)
        {
            return ovrAudio_SetAdvancedBoxRoomParametersUnity(context, width, height, depth,
                lockToListenerPosition, position.x, position.y, -position.z, wallMaterials);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetRoomClutterFactor(IntPtr context, float[] clutterFactor);
        public int SetRoomClutterFactor(float[] clutterFactor)
        {
            return ovrAudio_SetRoomClutterFactor(context, clutterFactor);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetReflectionModel(IntPtr context, int model);

        public int SetReflectionModel(int model)
        {
            return ovrAudio_SetReflectionModel(context, model);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_Enable(IntPtr context, int what, int enable);
        public int SetEnabled(int feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomRaysPerSecond(IntPtr context, int RaysPerSecond);
        public int SetDynamicRoomRaysPerSecond(int RaysPerSecond)
        {
            return ovrAudio_SetDynamicRoomRaysPerSecond(context, RaysPerSecond);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomInterpSpeed(IntPtr context, float InterpSpeed);
        public int SetDynamicRoomInterpSpeed(float InterpSpeed)
        {
            return ovrAudio_SetDynamicRoomInterpSpeed(context, InterpSpeed);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomMaxWallDistance(IntPtr context, float MaxWallDistance);
        public int SetDynamicRoomMaxWallDistance(float MaxWallDistance)
        {
            return ovrAudio_SetDynamicRoomMaxWallDistance(context, MaxWallDistance);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_SetDynamicRoomRaysRayCacheSize(IntPtr context, int RayCacheSize);
        public int SetDynamicRoomRaysRayCacheSize(int RayCacheSize)
        {
            return ovrAudio_SetDynamicRoomRaysRayCacheSize(context, RayCacheSize);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_GetRoomDimensions(IntPtr context, float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position);
        public int GetRoomDimensions(float[] roomDimensions, float[] reflectionsCoefs, out Vector3 position)
        {
            return ovrAudio_GetRoomDimensions(context, roomDimensions, reflectionsCoefs, out position);
        }

        [DllImport(strOSPS)]
        private static extern int ovrAudio_GetRaycastHits(IntPtr context, Vector3[] points, Vector3[] normals, int length);
        public int GetRaycastHits(Vector3[] points, Vector3[] normals, int length)
        {
            return ovrAudio_GetRaycastHits(context, points, normals, length);
        }
    }
}
