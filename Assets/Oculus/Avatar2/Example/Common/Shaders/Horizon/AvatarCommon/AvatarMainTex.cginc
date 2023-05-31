#ifndef AVATAR_MAIN_TEX_CGINC
#define AVATAR_MAIN_TEX_CGINC

#include "UnityCG.cginc"

#if defined(AVATAR_SHADER_MAIN_TEX_ARRAY)
  UNITY_DECLARE_TEX2DARRAY(_MainTexArray);

  half4 SampleMainTex(float3 coords) {    
    return UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, coords);
  }

#else
  sampler2D _MainTex;

  half4 SampleMainTex(float2 coords) {
    return tex2D(_MainTex, coords);
  }
#endif

#endif
