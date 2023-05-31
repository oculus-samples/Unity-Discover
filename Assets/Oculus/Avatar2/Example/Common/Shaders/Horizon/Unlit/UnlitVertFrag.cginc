#ifndef AVATAR_UNLIT_VERT_FRAG_CGINC
#define AVATAR_UNLIT_VERT_FRAG_CGINC

#include "UnlitTypes.cginc"

///////////////////
// vertex shader //
///////////////////

v2f AvatarShaderUnlitVertProgramInit(appdata v) {
  UNITY_SETUP_INSTANCE_ID(v);
  v2f o;
  UNITY_INITIALIZE_OUTPUT(v2f,o);
  UNITY_TRANSFER_INSTANCE_ID(v,o);
  UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

  return o;
}

void AvatarShaderUnlitVertProgramTransform(appdata v, inout v2f o) {
  o.pos = UnityObjectToClipPos(v.vertex);
  o.color = v.color;
  o.uv = v.uv;

  o.propertiesMapUV.xy = v.uv.xy;
  o.effectsMapUV.xy = v.uv.xy;
  o.ormt = v.ormt;
}

#define GENERATE_AVATAR_SHADER_UNLIT_DEFAULT_VERT_PROGRAM(VertProgName) \
  v2f VertProgName(appdata v) { \
    v2f o = AvatarShaderUnlitVertProgramInit(v); \
    AvatarShaderUnlitVertProgramTransform(v, o); \
    \
    return o; \
  }

#define GENERATE_AVATAR_SHADER_UNLIT_VERT_PROGRAM(VertProgName, CustomVertFunc) \
  v2f VertProgName(appdata v) { \
    v2f o = AvatarShaderUnlitVertProgramInit(v); \
    \
    AvatarShaderUnlitVertProgramTransform(v, o); \
    \
    CustomVertFunc(v, o); \
    \
    return o; \
  }

/////////////////////
// fragment shader //
/////////////////////

#define GENERATE_AVATAR_SHADER_UNLIT_FRAG_PROGRAM(FragProgName, ColorFunc) \
  float4 FragProgName(v2f IN) : SV_Target { \
    UNITY_SETUP_INSTANCE_ID(IN); \
    \
    return ColorFunc(IN); \
  }

#endif
