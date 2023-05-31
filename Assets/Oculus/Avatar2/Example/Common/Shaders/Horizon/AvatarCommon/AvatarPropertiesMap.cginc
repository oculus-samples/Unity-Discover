#ifndef AVATAR_PROPERTIES_MAP_CGINC
#define AVATAR_PROPERTIES_MAP_CGINC

#include "UnityCG.cginc"


#ifdef MATERIAL_MODE_TEXTURE
#if defined(AVATAR_SHADER_PROPERTIES_MAP_ARRAY)
  UNITY_DECLARE_TEX2DARRAY(_PropertiesMapArray);

  half4 SamplePropertiesMap(float3 coords, half4 vertORMT) {
    return UNITY_SAMPLE_TEX2DARRAY(_PropertiesMapArray, coords);
  }
#else
  sampler2D _PropertiesMap;

  half4 SamplePropertiesMap(float2 coords, half4 vertORMT) {
    return tex2D(_PropertiesMap, coords);
  }
#endif
#else
  half4 SamplePropertiesMap(float2 coords, half4 vertORMT) {
    return vertORMT;
  }
#endif

#endif
