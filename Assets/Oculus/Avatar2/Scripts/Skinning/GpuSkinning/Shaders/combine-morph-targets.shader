// CombineMorphTargets shader:
// - combines partial morphs into a blended shape based on the weights given

Shader "Avatar/CombineMorphTargets" {
  Properties {
    [NoScaleOffset] u_MorphTargetSourceTex("MorphTargets Source Texture", 2DArray) = "black" {}
  }
  SubShader {
    Tags{
        "RenderType" =
            "Transparent"
            "Queue" = "Transparent"} LOD 100

        Pass {
      Cull Off Blend One One ZTest Off ZWrite Off ZClip Off

          CGPROGRAM
#pragma vertex VertShader
#pragma fragment FragShader

#pragma target 4.5

#pragma multi_compile ___ OVR_MORPH_10_10_10_2
#pragma multi_compile ___ OVR_HAS_TANGENTS

#include "UnityCG.cginc"

      // TODO: Really this should be a constant buffer. But thats not working in Unity 2020
      uniform StructuredBuffer<float> u_Weights;
      uniform int u_WeightOffset;

      uniform float u_BlockEnabled;

      uniform float4 u_MorphTargetRanges[3];

      #define OVR_TEXTURE_PRECISION_FLOAT // hard coding this for now to test perf

      // NOTE: According to Unity documentation here https://docs.unity3d.com/Manual/SL-DataTypesAndPrecision.html
      // The standard declaration of Texture2DArray yields the following
      // "For mobile platforms, these translate into “low precision samplers”, i.e. the textures are expected to
      // have low precision data in them."
      // Upon shader inspection, the declarations become "uniform mediump sampler2DArray" which
      // is 16-bit precision. This is not desired as some of the data in the textures is
      // expected to have 32-bit precision. So, for mobile platforms, make an option for explicitly
      // setting 32-bit precision
      #if defined(SHADER_API_MOBILE) && defined(OVR_TEXTURE_PRECISION_FLOAT)
          #define OVR_DECLARE_TEX2DARRAY(tex) Texture2DArray_float tex; SamplerState sampler##tex
      #else
          #define OVR_DECLARE_TEX2DARRAY(tex) UNITY_DECLARE_TEX2DARRAY(tex)
      #endif

      OVR_DECLARE_TEX2DARRAY(u_MorphTargetSourceTex);

      struct appdata {
        float4 a_Position : POSITION;
        float4 a_Color : COLOR;
        float2 a_UV1 : TEXCOORD0;
      };

      struct v2F {
        float4 pos : SV_POSITION;
        float3 v_UVCoord1 : TEXCOORD0;

        nointerpolation float v_Weight : TEXCOORD1;
      };

      int OvrBitfieldExtract10(int value, int offset) {
        value = value >> offset;
        value &= 0x03ff;
        if ((value & 0x0200) != 0) {
          value |= 0xfffffc00;
        }
        return value;
      }

      float3 Unpack_10_10_10_2(int packedValue, float3 scale) {
        // bonus scale is still a unorm, if I convert it to an snorm, I loose one value.
        // that does mean I can't use the hardware to convert this format though, it has
        // to be unpacked by hand. If you do have hardware 10_10_10_2 conversion, it may
        // be better to just sample twice? once as unorm, once as snorm.
        uint bonusScaleIndex = uint(packedValue >> 30 & 0x03);

        const float bonusScaleLookup[4] = {1.0f, 0.5f, 0.25f, 0.125f};
        float bonusScale = bonusScaleLookup[bonusScaleIndex];

        int3 unpackedInt;
        unpackedInt.x = OvrBitfieldExtract10(packedValue, 0);
        unpackedInt.y = OvrBitfieldExtract10(packedValue, 10);
        unpackedInt.z = OvrBitfieldExtract10(packedValue, 20);

        float3 unpacked = float3(unpackedInt);
        // convert all to -1 to 1
        const float inv511 = 1.0 / 511.0;
        unpacked *= float3(inv511, inv511, inv511);

        unpacked = unpacked * scale * bonusScale;

        return unpacked;
      }

      v2F VertShader(appdata input) {
        v2F output;

        float4 pos = float4(input.a_Position.xyz, 1.0);
#if UNITY_UV_STARTS_AT_TOP
        // Unity is trying to be "helpful" and unify DX v OGL coordinates,
        // Unfortunately it does a very poor job and just makes things worse
        // Because our quad center is (0,0) this effectively flips them vertically
        pos.y = -input.a_Position.y;
#endif

        output.v_UVCoord1.xy = input.a_UV1;

        // Pull tex slice from color, store as z in UV
        output.v_UVCoord1.z = input.a_Color.w;

        // Grab blend weight array index and enabled array index
        uint weightIndex = uint(round(input.a_Color.y));

        float weight = u_Weights[u_WeightOffset + weightIndex];
        output.v_Weight = weight;

        float isWeightNonZero = float(weight > 0.0001);

        // Generate degnerate triangle if weight or enabled are 0
        output.pos = pos * (u_BlockEnabled * isWeightNonZero);

        return output;
      }

      float4 FragShader(v2F input) : SV_Target {
        float4 output;

#if defined(OVR_MORPH_10_10_10_2)
        uint2 texSize;
        uint elements;
        uint levels;
        u_MorphTargetSourceTex.GetDimensions(0, texSize.x, texSize.y, elements, levels);
        uint rangeSelection = uint(float(texSize.y) * input.v_UVCoord1.y);
#if defined(OVR_HAS_TANGENTS)
        rangeSelection %= 3;
#else
        rangeSelection %= 2;
#endif

        // Unity wont let you set the texture format to something more useful like R32_SInt, or
        // A2B10G10R10_UIntPack32. It will fail to create the texture array. Maybe would have
        // more luck with a regular 2d texture? At any rate currently have to take a lot more pain
        // in decoding the data. First taking R8G8B8A8_UNorm data, and converting it to a single 32
        // bit integer, then decoding the 10_10_10_2 format that it really is.
        float4 texSampleUNorms =
            UNITY_SAMPLE_TEX2DARRAY(u_MorphTargetSourceTex, input.v_UVCoord1.xyz);
        texSampleUNorms = round(texSampleUNorms * float4(255.0, 255.0, 255.0, 255.0));
        int4 texSampleBytes = texSampleUNorms;
        int texSample = texSampleBytes.x | (texSampleBytes.y << 8) | (texSampleBytes.z << 16) |
            (texSampleBytes.w << 24);
        float3 unpacked = Unpack_10_10_10_2(texSample, u_MorphTargetRanges[rangeSelection]);

        output = float4(unpacked * input.v_Weight, 1.0);
#else

        float4 texSample = UNITY_SAMPLE_TEX2DARRAY(u_MorphTargetSourceTex, input.v_UVCoord1.xyz);

        output.rgb = (texSample.rgb * input.v_Weight);
        output.a = 1.0;
#endif

        return output;
      }

      ENDCG
    } // Pass

  } // SubShader

} // Shader
