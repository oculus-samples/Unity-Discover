using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Oculus.Avatar2;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Oculus.Skinning.GpuSkinning
{
    internal static class OvrComputeUtils
    {
        private const string LOG_SCOPE = nameof(OvrComputeUtils);
        // These are pulled from the shader
        private const int FORMAT_FLOAT_32 = 0;
        private const int FORMAT_HALF_16  = 1;
        private const int FORMAT_UNORM_16 = 2;
        private const int FORMAT_UINT_16 = 3;
        private const int FORMAT_SNORM_10_10_10_2 = 4;
        // private const int FORMAT_UNORM_8  = 5;
        private const int FORMAT_UINT_8 = 6;

        public static int GetEncodingPrecisionShaderValue(CAPI.ovrGpuSkinningEncodingPrecision precision)
        {
            switch (precision)
            {
                case CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_FLOAT:
                    return FORMAT_FLOAT_32;
                case CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_HALF:
                    return FORMAT_HALF_16;
                case CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_UINT16:
                    return FORMAT_UINT_16;
                case CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_10_10_10_2:
                    return FORMAT_SNORM_10_10_10_2;
                case CAPI.ovrGpuSkinningEncodingPrecision.ENCODING_PRECISION_UINT8:
                    return FORMAT_UINT_8;
            }

            OvrAvatarLog.LogError($"Unsupported format in compute shader {precision}.", LOG_SCOPE);
            return FORMAT_FLOAT_32;
        }

        public static int GetEncodingPrecisionShaderValue(GpuSkinningConfiguration.TexturePrecision precision)
        {
            switch (precision)
            {
                case GpuSkinningConfiguration.TexturePrecision.Float:
                    return FORMAT_FLOAT_32;
                case GpuSkinningConfiguration.TexturePrecision.Half:
                    return FORMAT_HALF_16;
                case GpuSkinningConfiguration.TexturePrecision.Snorm10:
                    return FORMAT_SNORM_10_10_10_2;
                case GpuSkinningConfiguration.TexturePrecision.Unorm16:
                    return FORMAT_UNORM_16;
                case GpuSkinningConfiguration.TexturePrecision.Byte:
                    return FORMAT_UINT_8;
            }

            OvrAvatarLog.LogError($"Unsupported format in compute shader {precision}.", LOG_SCOPE);
            return FORMAT_FLOAT_32;
        }
    }
}
