using System;

using Oculus.Avatar2;

using Unity.Collections;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Oculus.Skinning.GpuSkinning
{
    public class OvrExpandableTextureArray
    {
        private const string LOG_SCOPE = "OvrExpandableTextureArray";

        // TODO: Sender -> OvrExpandableTextureArray
        // Declare the delegate (if using non-generic pattern).
        public delegate void ArrayGrowthEventHandler(OvrExpandableTextureArray sender, Texture2DArray newArray);

        private event ArrayGrowthEventHandler _ArrayResized;
        // Declare the event.
        public event ArrayGrowthEventHandler ArrayResized
        {
            add
            {
                _ArrayResized += value;
                if (HasTexArray)
                {
                    value(this, GetTexArray());
                }
            }
            remove
            {
                _ArrayResized -= value;
            }
        }

        public string name => _texArray.name;
        // TODO: Remove, always true
        public bool HasTexArray => _texArray != null;

        public int Width => _texArray.width;
        public int Height => _texArray.height;
        public TextureFormat Format => _texArray.format;

        public bool HasMips =>
#if UNITY_2019_3_OR_NEWER
            _texArray.mipmapCount
#else
            MIP_COUNT
#endif
         > 1;

        private readonly GraphicsFormat _graphicsFormat;

        public bool IsLinear => IS_LINEAR;

        public bool CheckFit(in Vector2Int size)
        {
            return _texArray.CheckFit(size);
        }
        public OvrExpandableTextureArray(string name, Int32 width, Int32 height, GraphicsFormat texFormat)
            : this(name, _ConvertDimension(width), _ConvertDimension(height), texFormat) { }

        public OvrExpandableTextureArray(string name, UInt32 width, UInt32 height, GraphicsFormat texFormat)
        {
            Debug.Assert(width > 0 && width <= MAX_TEX_DIM);
            Debug.Assert(height > 0 && height <= MAX_TEX_DIM);

            _graphicsFormat = texFormat;

            try
            {
#if UNITY_2019_3_OR_NEWER
                _texArray = new Texture2DArray((Int32)width, (Int32)height, 1, texFormat, TextureCreationFlags.None, MIP_COUNT);
#else
                _texArray = new Texture2DArray((Int32)width, (Int32)height, 1, texFormat, MIP_COUNT > 1 ? TextureCreationFlags.MipChain : TextureCreationFlags.None);
#endif
                _texArray.name = name;

                ConfigureTexArray(_texArray);

                // We will only copy into this texture with GPU to GPU copies from temp textures. So just drop the cpu copy immediately.
                // Wish we could create the texture without the cpu backing copy to begin with. or at least drop it without paying for
                // a one time copy of garbage memory to GPU.
                _texArray.Apply(false, true);
            }
            catch (Exception e)
            {
                OvrAvatarLog.LogError($"Texture2DArray {name} ({width}, {height}) allocation failure - {e.Message}", logScope);
            }

            _atlasPackerId = CAPI.OvrGpuSkinning_AtlasPackerCreate(
                (uint)width,
                (uint)height,
                CAPI.AtlasPackerPackingAlgortihm.Runtime);
        }


        public void Destroy()
        {
            if (_atlasPackerId != CAPI.AtlasPackerId.Invalid)
            {
                CAPI.OvrGpuSkinning_AtlasPackerDestroy(_atlasPackerId);
            }
            if (_texArray != null)
            {
                Texture2DArray.Destroy(_texArray);
            }
        }

        public OvrSkinningTypes.Handle AddEmptyBlock(in CAPI.ovrAvatar2Vector2u dims)
        {
            return AddEmptyBlock(dims.x, dims.y);
        }
        public OvrSkinningTypes.Handle AddEmptyBlock(UInt32 width, UInt32 height)
        {
            // Call out to atlas packer to get packing rectangle
            if (_texArray == null || _atlasPackerId == CAPI.AtlasPackerId.Invalid
                || width > _texArray.width || height > _texArray.height)
            {
                return OvrSkinningTypes.Handle.kInvalidHandle;
            }

            OvrSkinningTypes.Handle packerHandle = CAPI.OvrGpuSkinning_AtlasPackerAddBlock(
                _atlasPackerId,
                width,
                height);

            //CAPI.ovrTextureLayoutResult packResult = CAPI.ovrTextureLayoutResult.INVALID_LAYOUT;
            var packResult = CAPI.OvrGpuSkinning_AtlasPackerResultsForBlock(
                _atlasPackerId,
                packerHandle);
            OvrAvatarLog.AssertConstMessage(packResult.IsValid, "Invalid Layout");

            // See if tex array needs to grow
            // Tex slice is a 0 based index, where num tex array slices isn't
            if (packResult.texSlice >= _texArray.depth)
            {
                GrowTexArray((int)(packResult.texSlice + 1));
            }

            return packerHandle;
        }

        public CAPI.ovrTextureLayoutResult GetLayout(OvrSkinningTypes.Handle handle)
        {
            //CAPI.ovrTextureLayoutResult layout = CAPI.ovrTextureLayoutResult.INVALID_LAYOUT;
            return CAPI.OvrGpuSkinning_AtlasPackerResultsForBlock(_atlasPackerId, handle);
        }

        public void CopyFromTexture(CAPI.ovrTextureLayoutResult layout, Texture2D tempTex)
        {
            if (QualitySettings.globalTextureMipmapLimit == 0) {
                Graphics.CopyTexture(
                    tempTex,  // src
                    0,    // srcElement
                    0,    // srcMip
                    0,    // srcX
                    0,    // srcY
                    layout.w, // srcWidth
                    layout.h, // srcHeight
                    _texArray,    // dst
                    (int)layout.texSlice, // dstElement
                    0,    // dstMip
                    layout.x, // dstX
                    layout.y  // dstY
                );
            } else {
                // the above call to CopyTexture fails during half-res or quarter-res Quality. See Unity bug: https://fogbugz.unity3d.com/default.asp?1312568_d9rfr8bdr8od1sj7
                OvrAvatarLog.AssertConstMessage(layout.x == 0 && layout.y == 0 && layout.w == tempTex.width && layout.h == tempTex.height,
                    "masterTextureLimit non-zero during GPU Skinning initialiation. This prohibits use of layout offsets. To use Avatar GPUSkinning, disable reduced texture resolution Project Quality Setttings.");

                Graphics.CopyTexture(
                    tempTex,    // src
                    0,  // srcElement
                    0,  // srcMip
                    _texArray, // dst
                    (int)layout.texSlice, // dstElement
                    0   // dstMip
                );
            }

            _texArray.IncrementUpdateCount();
        }

        public void RemoveBlock(OvrSkinningTypes.Handle handle)
        {
            CAPI.OvrGpuSkinning_AtlasPackerRemoveBlock(_atlasPackerId, handle);
        }

        public Texture2DArray GetTexArray()
        {
            return _texArray;
        }

        private void ConfigureTexArray(Texture2DArray newArray)
        {
            newArray.filterMode = FilterMode.Point;
            newArray.anisoLevel = 0;
            newArray.wrapMode = TextureWrapMode.Clamp;
        }

        private void GrowTexArray(int newDepth)
        {
            // Unfortunately the Unity API doesn't allow for expansion
            // of the same texture 2D array, so will need to create a new one
            // here and inform listeners of its creation

            // Copy all slices to a temporary texture array while the original one expands
            // TODO*: Have a pool/temporary allocation of texture arrays to not create new ones

            int oldDepth = _texArray.depth;

            // Create new expanded texture array
            var newArray = new Texture2DArray(
                _texArray.width,
                _texArray.height,
                newDepth,
                _graphicsFormat,
                MIP_COUNT > 1 ? TextureCreationFlags.MipChain : TextureCreationFlags.None
#if UNITY_2019_3_OR_NEWER
                , MIP_COUNT
#endif
                );

            ConfigureTexArray(newArray);

            var oldArray = _texArray;
            for (int texSlice = 0; texSlice < oldDepth; texSlice++)
            {
                Graphics.CopyTexture(oldArray, texSlice, newArray, texSlice);
            }

            newArray.name = oldArray.name;
            newArray.IncrementUpdateCount();
            _texArray = newArray;
            Texture2DArray.Destroy(oldArray);

            // Inform listeners
            _ArrayResized?.Invoke(this, _texArray);
        }

        private const string logScope = "OvrExpandableTextureArray";
        private static readonly UInt32 MAX_TEX_DIM = (UInt32)SystemInfo.maxTextureSize;
        private const int MIP_COUNT = 1;
        private const bool IS_LINEAR = true;

        private Texture2DArray _texArray;

        private readonly CAPI.AtlasPackerId _atlasPackerId;
        
        private static UInt32 _ConvertDimension(Int32 dim)
        {
            Debug.Assert(dim >= 0);
            return (UInt32)dim;
        }
    }
}
