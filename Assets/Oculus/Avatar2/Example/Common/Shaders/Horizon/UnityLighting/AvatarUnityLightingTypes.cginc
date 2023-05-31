#ifndef AVATAR_UNITY_LIGHTING_TYPES_CGINC
#define AVATAR_UNITY_LIGHTING_TYPES_CGINC

#include "UnityCG.cginc"
#include "AutoLight.cginc"

#include "../../../../Scripts/ShaderUtils/AvatarCustom.cginc"

// vertex input data
struct appdata {
  OVR_REQUIRED_VERTEX_FIELDS
  float4 uv : OVR_FIRST_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;
  float4 ormt : OVR_SECOND_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;
  float4 texcoord2 : OVR_THIRD_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;
  float4 texcoord3 : OVR_FOURTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;
  fixed4 color : COLOR;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

// vertex-to-fragment interpolation data
// no lightmaps:
#ifndef LIGHTMAP_ON
// half-precision fragment shader registers:
#ifdef UNITY_HALF_PRECISION_FRAGMENT_SHADER_REGISTERS
#define FOG_COMBINED_WITH_WORLD_POS
struct v2f {
  UNITY_POSITION(pos);
  float4 color: COLOR;
  float4 uv: TEXCOORD0;
  float4 propertiesMapUV : TEXCOORD1;
  float4 effectsMapUV : TEXCOORD2;
  float3 worldNormal : TEXCOORD3;
  float4 worldPos : TEXCOORD4;
#if UNITY_SHOULD_SAMPLE_SH
  half3 sh : TEXCOORD5; // SH
#endif
  UNITY_LIGHTING_COORDS(6,7)
#if SHADER_TARGET >= 30
  float4 lmap : TEXCOORD8;
#endif
  float4 ormt : TEXCOORD9;
  UNITY_VERTEX_INPUT_INSTANCE_ID
  UNITY_VERTEX_OUTPUT_STEREO
};
#endif
// high-precision fragment shader registers:
#ifndef UNITY_HALF_PRECISION_FRAGMENT_SHADER_REGISTERS
struct v2f {
  UNITY_POSITION(pos);
  float4 color: COLOR;
  float4 uv: TEXCOORD0;
  float4 propertiesMapUV : TEXCOORD1;
  float4 effectsMapUV : TEXCOORD2;
  float3 worldNormal : TEXCOORD3;
  float3 worldPos : TEXCOORD4;
#if UNITY_SHOULD_SAMPLE_SH
  half3 sh : TEXCOORD5; // SH
#endif
  UNITY_FOG_COORDS(6)
  UNITY_SHADOW_COORDS(7)
#if SHADER_TARGET >= 30
  float4 lmap : TEXCOORD8;
#endif
  float4 ormt : TEXCOORD9;
  UNITY_VERTEX_INPUT_INSTANCE_ID
  UNITY_VERTEX_OUTPUT_STEREO
};
#endif
#endif
// with lightmaps:
#ifdef LIGHTMAP_ON
// half-precision fragment shader registers:
#ifdef UNITY_HALF_PRECISION_FRAGMENT_SHADER_REGISTERS
#define FOG_COMBINED_WITH_WORLD_POS
struct v2f {
  UNITY_POSITION(pos);
  float4 color: COLOR;
  float4 uv: TEXCOORD0;
  float4 propertiesMapUV : TEXCOORD1;
  float4 effectsMapUV : TEXCOORD2;
  float3 worldNormal : TEXCOORD3;
  float4 worldPos : TEXCOORD4;
  float4 lmap : TEXCOORD5;
  float4 ormt : TEXCOORD9;
  UNITY_LIGHTING_COORDS(6,7)
  UNITY_VERTEX_INPUT_INSTANCE_ID
  UNITY_VERTEX_OUTPUT_STEREO
};
#endif
// high-precision fragment shader registers:
#ifndef UNITY_HALF_PRECISION_FRAGMENT_SHADER_REGISTERS
struct v2f {
  UNITY_POSITION(pos);
  float4 color: COLOR;
  float4 uv: TEXCOORD0;
  float4 propertiesMapUV : TEXCOORD1;
  float4 effectsMapUV : TEXCOORD2;
  float3 worldNormal : TEXCOORD3;
  float3 worldPos : TEXCOORD4;
  float4 lmap : TEXCOORD5;
  UNITY_FOG_COORDS(6)
  UNITY_SHADOW_COORDS(7)
#ifdef DIRLIGHTMAP_COMBINED
  float3 tSpace0 : TEXCOORD8;
  float3 tSpace1 : TEXCOORD9;
  float3 tSpace2 : TEXCOORD10;
#endif
  float4 ormt : TEXCOORD11;
  UNITY_VERTEX_INPUT_INSTANCE_ID
  UNITY_VERTEX_OUTPUT_STEREO
};
#endif
#endif

///////////////////////////////////////////////
// End vertex to fragment interpolation data //
///////////////////////////////////////////////


#endif
