#ifndef AVATAR_UNLIT_TYPES_CGINC
#define AVATAR_UNLIT_TYPES_CGINC

#include "UnityCG.cginc"

// vertex input data
struct appdata {
  float4 vertex : POSITION;
  float4 tangent : TANGENT;
  float3 normal : NORMAL;
  float4 uv : TEXCOORD0;
  fixed4 color : COLOR;
  float4 ormt : TEXCOORD1;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

// vertex-to-fragment interpolation data
struct v2f {
  UNITY_POSITION(pos);
  fixed4 color : COLOR;
  float4 uv : TEXCOORD0;
  float4 propertiesMapUV : TEXCOORD1;
  float4 effectsMapUV : TEXCOORD2;
  float3 worldNormal : TEXCOORD3;
  float3 worldPos : TEXCOORD4;
  float4 ormt : TEXCOORD5;
  UNITY_VERTEX_INPUT_INSTANCE_ID
  UNITY_VERTEX_OUTPUT_STEREO
};

#endif
