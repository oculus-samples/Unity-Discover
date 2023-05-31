using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Oculus.Avatar2
{
    public readonly struct OvrAvatarMaterialExtension
    {
        //////////////////////////////////////////////////
        // ExtenstionEntry<T>
        //////////////////////////////////////////////////
        private struct ExtensionEntry<T>
        {
            private string _name;
            private T _payload;

            public ExtensionEntry(string name, T payload)
            {
                _name = name;
                _payload = payload;
            }

            public string Name => _name;
            public T Payload => _payload;
        }

        //////////////////////////////////////////////////
        // ExtenstionEntries
        //////////////////////////////////////////////////
        private class ExtensionEntries
        {
            private const string extensionLogScope = "OvrAvatarMaterialExtension_ExtensionEntries";

            private readonly List<ExtensionEntry<Vector3>> _vector3Entries = new List<ExtensionEntry<Vector3>>();
            private readonly List<ExtensionEntry<Vector4>> _vector4Entries = new List<ExtensionEntry<Vector4>>();
            private readonly List<ExtensionEntry<float>> _floatEntries = new List<ExtensionEntry<float>>();
            private readonly List<ExtensionEntry<int>> _intEntries = new List<ExtensionEntry<int>>();
            private readonly List<ExtensionEntry<Texture2D>> _textureEntries = new List<ExtensionEntry<Texture2D>>();

            public void ApplyToMaterial(Material mat, string extensionName,
                OvrAvatarMaterialExtensionConfig extensionConfig)
            {
                Debug.Assert(extensionConfig != null);

                string nameInShader;
                foreach (var entry in _vector3Entries)
                {
                    if (extensionConfig.TryGetNameInShader(extensionName, entry.Name, out nameInShader))
                    {
                        mat.SetVector(nameInShader, entry.Payload);
                    }
                }

                foreach (var entry in _vector4Entries)
                {
                    if (extensionConfig.TryGetNameInShader(extensionName, entry.Name, out nameInShader))
                    {
                        mat.SetVector(nameInShader, entry.Payload);
                    }
                }

                foreach (var entry in _floatEntries)
                {
                    if (extensionConfig.TryGetNameInShader(extensionName, entry.Name, out nameInShader))
                    {
                        mat.SetFloat(nameInShader, entry.Payload);
                    }
                }

                foreach (var entry in _intEntries)
                {
                    if (extensionConfig.TryGetNameInShader(extensionName, entry.Name, out nameInShader))
                    {
                        mat.SetInt(nameInShader, entry.Payload);
                    }
                }

                foreach (var entry in _textureEntries)
                {
                    if (extensionConfig.TryGetNameInShader(extensionName, entry.Name, out nameInShader))
                    {
                        mat.SetTexture(nameInShader, entry.Payload);
                    }
                }
            }

            public bool LoadEntry(CAPI.ovrAvatar2Id primitiveId, UInt32 extensionIndex, UInt32 entryIndex)
            {
                bool success = GetEntryMetaData(primitiveId, extensionIndex, entryIndex, out var metaData);

                if (!success) { return false; }

                // Now grab the name and the data of the entry
                switch (metaData.entryType)
                {
                    case CAPI.ovrAvatar2MaterialExtensionEntryType.Float:
                        success = StoreNameAndPayloadForEntry(
                            primitiveId,
                            extensionIndex,
                            entryIndex,
                            metaData,
                            _floatEntries);
                        break;
                    case CAPI.ovrAvatar2MaterialExtensionEntryType.Int:
                        success = StoreNameAndPayloadForEntry(
                            primitiveId,
                            extensionIndex,
                            entryIndex,
                            metaData,
                            _intEntries);
                        break;
                    case CAPI.ovrAvatar2MaterialExtensionEntryType.Vector3f:
                        success = StoreNameAndPayloadForEntry(
                            primitiveId,
                            extensionIndex,
                            entryIndex,
                            metaData,
                            _vector3Entries);
                        break;
                    case CAPI.ovrAvatar2MaterialExtensionEntryType.Vector4f:
                        success = StoreNameAndPayloadForEntry(
                            primitiveId,
                            extensionIndex,
                            entryIndex,
                            metaData,
                            _vector4Entries);
                        break;
                    case CAPI.ovrAvatar2MaterialExtensionEntryType.ImageId:
                        string entryName;
                        var payload = CAPI.ovrAvatar2Id.Invalid;
                        unsafe
                        {
                            success = GetNameAndPayloadForEntry(
                                primitiveId,
                                extensionIndex,
                                entryIndex,
                                metaData,
                                out entryName,
                                &payload);
                        }

                        if (success)
                        {
                            OvrAvatarLog.Assert(payload != CAPI.ovrAvatar2Id.Invalid, extensionLogScope);
                            // Convert image ID to texture
                            success = OvrAvatarManager.GetOvrAvatarAsset(payload, out OvrAvatarImage image);
                            if (success)
                            {
                                _textureEntries.Add(new ExtensionEntry<Texture2D>(entryName, image.texture));
                            }
                            else
                            {
                                OvrAvatarLog.LogError(
                                    $"Could not find image entryName:{entryName} assetId:{payload}"
                                    , extensionLogScope
                                    , image?.texture);
                            }
                        }

                        break;

                    case CAPI.ovrAvatar2MaterialExtensionEntryType.Invalid:
                        OvrAvatarLog.LogError(
                            $"Invalid extension type for primitiveId:{primitiveId} extensionIndex:{extensionIndex} entryIndex:{entryIndex}"
                            , extensionLogScope);

                        // Invalid signals an internal error in `libovravatar2` - should not have returned success
                        success = false;
                        break;

                    default:
                        OvrAvatarLog.LogWarning(
                            $"Unrecognized extension type ({metaData.entryType}) for primitiveId:{primitiveId} extensionIndex:{extensionIndex} entryIndex:{entryIndex}"
                            , extensionLogScope);
                        break;
                }

                return success;
            }

            private static bool GetEntryMetaData(
                CAPI.ovrAvatar2Id primitiveId,
                UInt32 extensionIdx,
                UInt32 entryIdx,
                out CAPI.ovrAvatar2MaterialExtensionEntry metaData)
            {
                var success = CAPI.OvrAvatar2Primitive_MaterialExtensionEntryMetaDataByIndex(
                    primitiveId,
                    extensionIdx,
                    entryIdx,
                    out metaData);

                if (!success)
                {
                    OvrAvatarLog.LogError(
                        $"MaterialExtensionEntryMetaDataByIndex ({extensionIdx}, {entryIdx}) bufferSize:{metaData.dataBufferSize}"
                        , LOG_SCOPE);
                }

                return success;
            }

            private static unsafe bool GetNameAndPayloadForEntry<T>(
                CAPI.ovrAvatar2Id primitiveId,
                UInt32 extensionIndex,
                UInt32 entryIndex,
                in CAPI.ovrAvatar2MaterialExtensionEntry metaData,
                out string entryName,
                T* outPayload)
                where T : unmanaged
            {
                OvrAvatarLog.Assert(metaData.nameBufferSize > 0);
                OvrAvatarLog.Assert(metaData.dataBufferSize > 0);

                uint nameBufferSize = metaData.nameBufferSize;
                var nameBuffer = stackalloc byte[(int)nameBufferSize];

                bool success;

                uint managedSize = (uint)UnsafeUtility.SizeOf<T>();
                uint dataBufferSize = metaData.dataBufferSize;
                bool noMarshal = managedSize == dataBufferSize;
                if (noMarshal)
                {
                    success = CAPI.OvrAvatar2Primitive_MaterialExtensionEntryDataByIndex(
                        primitiveId,
                        extensionIndex,
                        entryIndex,
                        nameBuffer,
                        nameBufferSize,
                        (byte*)outPayload,
                        managedSize);
                }
                else
                {
                    var dataBuffer = stackalloc byte[(int)dataBufferSize];

                    success = CAPI.OvrAvatar2Primitive_MaterialExtensionEntryDataByIndex(
                        primitiveId,
                        extensionIndex,
                        entryIndex,
                        nameBuffer,
                        nameBufferSize,
                        dataBuffer,
                        dataBufferSize);

                    if (success) { *outPayload = Marshal.PtrToStructure<T>((IntPtr)dataBuffer); }
                }

                if (!success)
                {
                    OvrAvatarLog.LogWarning(
                        @$"MaterialExtensionEntryDataByIndex (extensionIdx:{extensionIndex}, entryIdx:{entryIndex})"
                        + $"nameSize:{nameBufferSize} managedSize:{managedSize} bufferSize:{dataBufferSize}"
                        , LOG_SCOPE);

                    entryName = string.Empty;
                    *outPayload = default;
                    return false;
                }

                entryName = Marshal.PtrToStringAnsi((IntPtr)nameBuffer);
                return true;
            }

            private static bool StoreNameAndPayloadForEntry<T>(
                CAPI.ovrAvatar2Id primitiveId,
                UInt32 extensionIdx,
                UInt32 entryIdx,
                in CAPI.ovrAvatar2MaterialExtensionEntry metaData,
                in List<ExtensionEntry<T>> listToStoreInto)
                where T : unmanaged
            {
                bool success;
                string entryName;
                T payload;
                unsafe
                {
                    success = GetNameAndPayloadForEntry(
                        primitiveId,
                        extensionIdx,
                        entryIdx,
                        metaData,
                        out entryName,
                        &payload);
                }

                if (success) { listToStoreInto.Add(new ExtensionEntry<T>(entryName, payload)); }

                return success;
            }
        }

        //////////////////////////////////////////////////
        // OvrAvatarMaterialExtension
        //////////////////////////////////////////////////
        private readonly ExtensionEntries _entries;
        private readonly string _name;

        private const string LOG_SCOPE = "OvrAvatarMaterialExtension";

        private OvrAvatarMaterialExtension(string extensionName, ExtensionEntries entries)
        {
            _name = extensionName;
            _entries = entries;
        }

        public string Name => _name;

        public void ApplyEntriesToMaterial(Material material, OvrAvatarMaterialExtensionConfig extensionConfig)
        {
            if (_entries == null || material == null || extensionConfig == null) { return; }

            _entries.ApplyToMaterial(material, _name, extensionConfig);
        }

        public static bool LoadExtension(CAPI.ovrAvatar2Id primitiveId, UInt32 extensionIndex,
            out OvrAvatarMaterialExtension materialExtension)
        {
            materialExtension = default;

            // Get extension name
            if (!GetMaterialExtensionName(primitiveId, extensionIndex, out string extensionName)) { return false; }

            // Get entries for the extension
            ExtensionEntries entries = new ExtensionEntries();
            if (!GetNumEntries(primitiveId, extensionIndex, out uint numEntries)) { return false; }

            // Loop over all entries
            for (UInt32 entryIdx = 0; entryIdx < numEntries; entryIdx++)
            {
                if (!entries.LoadEntry(primitiveId, extensionIndex, entryIdx)) { return false; }
            }

            materialExtension = new OvrAvatarMaterialExtension(extensionName, entries);

            return true;
        }

        private static bool GetMaterialExtensionName(CAPI.ovrAvatar2Id primitiveId, UInt32 extensionIdx,
            out string extensionName)
        {
            unsafe
            {
                extensionName = String.Empty;

                // Get extension name
                uint nameSize = 0;
                var result = CAPI.ovrAvatar2Primitive_GetMaterialExtensionName(
                    primitiveId,
                    extensionIdx,
                    null,
                    &nameSize);

                if (!result.EnsureSuccess($"GetMaterialExtensionName ({extensionIdx}) {result}", LOG_SCOPE))
                {
                    return false;
                }

                var nameBuffer = stackalloc byte[(int)nameSize];
                result = CAPI.ovrAvatar2Primitive_GetMaterialExtensionName(
                    primitiveId,
                    extensionIdx,
                    nameBuffer,
                    &nameSize);
                if (!result.EnsureSuccess($"GetMaterialExtensionName ({extensionIdx}) {result}", LOG_SCOPE))
                {
                    return false;
                }

                extensionName = Marshal.PtrToStringAnsi((IntPtr)nameBuffer);
            }

            return true;
        }

        private static bool GetNumEntries(CAPI.ovrAvatar2Id primitiveId, UInt32 extensionIndex, out UInt32 count)
        {
            count = 0;
            var result =
                CAPI.ovrAvatar2Primitive_GetNumEntriesInMaterialExtensionByIndex(
                    primitiveId, extensionIndex
                    , out count);

            if (!result.IsSuccess())
            {
                OvrAvatarLog.LogError($"GetNumEntriesInMaterialExtensionByIndex ({extensionIndex}) {result}"
                    , LOG_SCOPE);
                return false;
            }

            return true;
        }
    }
}
