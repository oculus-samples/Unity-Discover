using Oculus.Avatar2;

using System;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

using TexturePrecision = Oculus.Avatar2.GpuSkinningConfiguration.TexturePrecision;

namespace Oculus.Skinning
{
    internal static class OvrGpuSkinningExtensions
    {
        internal static GraphicsFormat GetGraphicsFormat(this TexturePrecision precision)
        {
            switch (precision)
            {
                case TexturePrecision.Float:
                    return GraphicsFormat.R32G32B32A32_SFloat;
                case TexturePrecision.Half:
                    return GraphicsFormat.R16G16B16A16_SFloat;
                case TexturePrecision.Unorm16:
                    return GraphicsFormat.R16G16B16A16_UNorm;
                case TexturePrecision.Snorm10:
                    return GraphicsFormat.R8G8B8A8_UNorm;
                case TexturePrecision.Byte:
                    return GraphicsFormat.R8G8B8A8_UNorm;
                case TexturePrecision.Nibble:
                    return GraphicsFormat.R4G4B4A4_UNormPack16;
                default:
                    throw new NotImplementedException($"Unhanded TexturePrecision {precision}.");
            }
        }

        internal static CAPI.ovrGpuSkinningEncodingPrecision GetOvrPrecision(this TexturePrecision precision)
        {
            // TODO: This should be flushed out further, but this is all we support for now
            switch (precision)
            {
                case TexturePrecision.Float:
                    return CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_FLOAT;
                case TexturePrecision.Half:
                    return CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_HALF;
                case TexturePrecision.Unorm16:
                    throw new NotImplementedException($"Unhanded TexturePrecision {precision}.");
                case TexturePrecision.Snorm10:
                    return CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_10_10_10_2;
                case TexturePrecision.Byte:
                    throw new NotImplementedException($"Unhanded TexturePrecision {precision}.");
                case TexturePrecision.Nibble:
                    throw new NotImplementedException($"Unhanded TexturePrecision {precision}.");
                default:
                    throw new NotImplementedException($"Unhanded TexturePrecision {precision}.");
            }
        }

        public static RenderTextureFormat GetRenderTextureFormat(this GraphicsFormat graphicsFormat)
        {
            switch (graphicsFormat)
            {
                case GraphicsFormat.R16G16B16A16_SFloat:
                    return RenderTextureFormat.ARGBHalf;
                case GraphicsFormat.R32G32B32A32_SFloat:
                    return RenderTextureFormat.ARGBFloat;
                default:
                    throw new NotImplementedException($"Unhanded GraphicsFormat {graphicsFormat}.");
            }
        }

        public static bool CheckFit(this Texture tex, in Vector2Int size)
        {
            return tex.width >= size.x && tex.height >= size.y;
        }

        public static RectInt ToRectInt(in this CAPI.ovrTextureLayoutResult layoutResult)
        {
            return new RectInt(layoutResult.x, layoutResult.y, layoutResult.w, layoutResult.h);
        }

        public static bool IsSuccess(this CAPI.ovrGpuSkinningResult result)
        {
            return result == CAPI.ovrGpuSkinningResult.Success;
        }
        public static bool IsFailure(this CAPI.ovrGpuSkinningResult result)
        {
            return result != CAPI.ovrGpuSkinningResult.Success;
        }

        private const string logScope = "OvrAvatarGpuSkinning";
        private const string DefaultContext = "no context";
        public static bool EnsureSuccess(this CAPI.ovrGpuSkinningResult result, string messageContext = DefaultContext, UnityEngine.Object unityContext = null)
        {
            bool success = result.IsSuccess();
            result.LogErrors(messageContext, unityContext);
            return success;
        }

        public static void LogErrors(this CAPI.ovrGpuSkinningResult result, string messageContext = DefaultContext, UnityEngine.Object unityContext = null)
        {
            if (result.IsFailure())
            {
                string errors = string.Empty;
                string separator = string.Empty;
                int remainingResults = (int)result;
                do
                {
                    var thisResult = remainingResults & -remainingResults;
                    remainingResults ^= thisResult;

                    errors += separator + ((CAPI.ovrGpuSkinningResult)thisResult).ToString();
                    separator = ", ";
                } while (remainingResults != 0);

                OvrAvatarLog.LogError($"Operation ({messageContext}) failed with errors ({errors})", logScope, unityContext);
            }
        }
    }
}
