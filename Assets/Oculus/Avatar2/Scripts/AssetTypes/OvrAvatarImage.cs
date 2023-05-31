using System;
using System.Collections.Generic;

using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

/// @file OvrAvatarImage.cs

namespace Oculus.Avatar2
{
    ///
    /// Contains a 2D image used to texture a 3D mesh.
    /// The pixels of the texture come in a variety of formats.
    /// Mobile applications use ASTC compressed texture formats
    /// which are decompressed by hardware. PC applications
    /// need DXT compressed textures.
    ///
    /// The texture data begins loading asynchronously when
    /// the image is created.
    ///
    /// @see OvrAvatarPrimitive
    ///
    public class OvrAvatarImage : OvrAvatarAsset<CAPI.ovrAvatar2Image>
    {
        private const string avatarImageLogScope = "ovrAvatarImage";

        internal const FilterMode defaultFilterMode = FilterMode.Trilinear;
        internal const int defaultAnisoLevel = 1;

        public readonly TextureFormat format;
        public Texture2D texture { get; private set; } = null;

        private OvrTime.SliceHandle _textureLoadSliceHandle = default;

        public override string typeName => avatarImageLogScope;
        public override string assetName => texture != null ? texture.name : "disposed";

        public bool hasCopiedAllResourceData { get; private set; } = default;

        ///
        /// Create an image from the given image properties.
        /// The image begins asynchronously loading upon return.
        ///
        /// @param resourceId unique resource ID
        /// @param imageIndex index of image within material
        /// @param data       image properties (data format, height, width)
        /// @param srgb       true for images using SRGB color space,
        ///                   False for linear color space
        /// @see CAPI.ovrAvatar2Image
        ///
        public OvrAvatarImage(CAPI.ovrAvatar2Id resourceId, UInt32 imageIndex, CAPI.ovrAvatar2Image data, bool srgb) : base(data.id, data)
        {
            bool compressed = true;
            switch (data.format)
            {
                case CAPI.ovrAvatar2ImageFormat.RGBA32:
                    format = TextureFormat.RGBA32;
                    compressed = false;
                    break;
                case CAPI.ovrAvatar2ImageFormat.DXT1:
                    format = TextureFormat.DXT1;
                    break;
                case CAPI.ovrAvatar2ImageFormat.DXT5:
                    format = TextureFormat.DXT5;
                    break;
                case CAPI.ovrAvatar2ImageFormat.BC5S:
                    format = TextureFormat.BC5;
                    break;
                case CAPI.ovrAvatar2ImageFormat.BC7U:
                    format = TextureFormat.BC7;
                    break;
                case CAPI.ovrAvatar2ImageFormat.ASTC_RGBA_4x4:
                    format = TextureFormat.ASTC_4x4;
                    break;
                case CAPI.ovrAvatar2ImageFormat.ASTC_RGBA_6x6:
                    format = TextureFormat.ASTC_6x6;
                    break;
                case CAPI.ovrAvatar2ImageFormat.ASTC_RGBA_8x8:
                    format = TextureFormat.ASTC_8x8;
                    break;

//                 case CAPI.ovrAvatar2ImageFormat.ASTC_RGBA_10x10:
//                     format = TextureFormat.ASTC_10x10;
//                     break;

                case CAPI.ovrAvatar2ImageFormat.ASTC_RGBA_12x12:
                    format = TextureFormat.ASTC_12x12;
                    break;

                case CAPI.ovrAvatar2ImageFormat.Invalid:
                    OvrAvatarLog.LogError(
                        $"Invalid image format for image {assetId}",
                        avatarImageLogScope);
                    // Can't load invalid format, all valid data has been copied (unblock load)
                    hasCopiedAllResourceData = true;
                    return;

                case CAPI.ovrAvatar2ImageFormat.BC5U:
                    const string BC5UErrorString = "BC5U is currently unsupported in Unity";
                    // Appears to be unsupported
                    OvrAvatarLog.LogError(
                        BC5UErrorString,
                        avatarImageLogScope);
                    // Can't load format, proceed w/ other assets
                    hasCopiedAllResourceData = true;
                    throw new ArgumentException(BC5UErrorString);

                default:
                    // Exception will end loading sequence, no opportunity to copy data
                    hasCopiedAllResourceData = true;
                    throw new ArgumentOutOfRangeException($"Unrecognized format {data.format}");
            }
            Debug.Assert(SystemInfo.SupportsTextureFormat(format));

            bool hasMipMaps = data.mipCount > 1;
            var buildTexture = new Texture2D((int)data.sizeX, (int)data.sizeY, format, hasMipMaps, !srgb);

            var manager = OvrAvatarManager.Instance;
            var filterMode = defaultFilterMode;
            int anisoLevel = defaultAnisoLevel;
            if (manager != null)
            {
                filterMode = manager.TextureFilterMode;
                anisoLevel = manager.TextureAnisoLevel;
            }
            buildTexture.filterMode = filterMode;
            buildTexture.anisoLevel = anisoLevel;

            // Oh Unity...
            if (!buildTexture)
            {
                // TODO: Fall back to a lower mip level?
                OvrAvatarLog.LogError(
                    $"Unable to create texture with size ({data.sizeX}, {data.sizeY}) and formats ({data.format}, {format})",
                    avatarImageLogScope);
                // Failed to allocate texture, likely near-OOM condition - cease load and proceed
                hasCopiedAllResourceData = true;
                return;
            }

            buildTexture.name = $"{assetId}:{imageIndex}-{format}";
            texture = buildTexture;
            _textureLoadSliceHandle = OvrTime.Slice(LoadTextureAsync(buildTexture, resourceId, imageIndex, srgb, compressed));
        }

        private IEnumerator<OvrTime.SliceStep> LoadTextureAsync(Texture2D buildTexture, CAPI.ovrAvatar2Id resourceId, UInt32 imageIndex, bool srgb, bool compressed)
        {
            var result = LoadTextureData(buildTexture, resourceId, imageIndex);

            if (result.IsSuccess())
            {
                bool generateMips = AllowMipGeneration && !compressed && data.mipCount == 1;

                // TODO: Should perhaps `Stall` instead?
                if (OvrTime.ShouldHold) { yield return OvrTime.SliceStep.Hold; }
                buildTexture.Apply(generateMips, true);

                texture = buildTexture;
                isLoaded = true;
            }
            else
            {
                OvrAvatarLog.LogError($"MeshPrimitive Error: GetImageDataByIndex ({imageIndex}) {result}", avatarImageLogScope, buildTexture);
                texture = null;
                Texture2D.Destroy(buildTexture);
            }

            _textureLoadSliceHandle.Clear();
        }

        private CAPI.ovrAvatar2Result LoadTextureData(Texture2D buildTexture, CAPI.ovrAvatar2Id resourceId, UInt32 imageIndex)
        {
            var textureData = buildTexture.GetRawTextureData<byte>();
            // AvatarSDK will catch this at runtime, this just provides a more useful error when developing in Unity
            Debug.Assert(textureData.Length == data.imageDataSize,
                $"Texture data arrays are different sizes! Texture is {textureData.Length} but image is {data.imageDataSize}");


            IntPtr textureDataPtr;
            unsafe
            {
                textureDataPtr = (IntPtr)textureData.GetUnsafePtr();
            }

            var result = CAPI.ovrAvatar2Asset_GetImageDataByIndex(resourceId, imageIndex, textureDataPtr, (UInt32)textureData.Length);
            hasCopiedAllResourceData = result.IsSuccess();
            return result;
        }

        protected override void _ExecuteCancel()
        {
            if (_textureLoadSliceHandle.IsValid)
            {
                OvrAvatarLog.LogVerbose("Cancelled image during load", avatarImageLogScope);

                _textureLoadSliceHandle.Cancel();
            }

            // We will not load any more resource data, so we have effectively loaded all of it
            hasCopiedAllResourceData = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (_textureLoadSliceHandle.IsValid)
            {
                if (disposing)
                {
                    _textureLoadSliceHandle.Cancel();
                }
                else
                {
                    OvrAvatarLog.LogWarning("Finalized image w/ in progress loading slice", avatarImageLogScope);

                    var cpyHandle = _textureLoadSliceHandle;
                    OvrTime.PostCleanupToUnityMainThread(() => cpyHandle.Cancel());
                }
            }

            if (!(texture is null))
            {
                if (disposing)
                {
                    Texture2D.Destroy(texture);
                }
                else
                {
                    OvrAvatarLog.LogError(
                        $"Texture2D asset was not destroyed before OvrAvatarImage ({assetId}) was finalized"
                        , avatarImageLogScope);

                    var holdTex = texture;
                    OvrTime.PostCleanupToUnityMainThread(() => Texture2D.Destroy(holdTex));
                }
                texture = null;
            }
        }

        private const bool AllowMipGeneration = false;
    }
}
