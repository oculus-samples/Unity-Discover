#ifndef AVATAR_EFFECTS_MAP_CGINC
#define AVATAR_EFFECTS_MAP_CGINC

#include "UnityCG.cginc"

#if defined(AVATAR_SHADER_EFFECTS_MAP_ARRAY)
  UNITY_DECLARE_TEX2DARRAY(_EffectsMapArray);

  half4 SampleEffectsMap(float3 coords) {
    return UNITY_SAMPLE_TEX2DARRAY(_EffectsMapArray, coords);
  }

#else
  sampler2D _EffectsMap;

  half4 SampleEffectsMap(float2 coords) {
    return tex2D(_EffectsMap, coords);
  }
#endif

#endif
