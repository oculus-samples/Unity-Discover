/////////////////////////////////////////////////////////
// Unity specific defines for Avatar SDK:

// Shader feature defines. If we want to change these via the shader manager,
// convert them to multi_compile or shader_feature

// #define HAS_VERTEX_COLOR_float3
// #define HAS_VERTEX_COLOR_float4
#define HAS_NORMALS
#define OVR_VERTEX_HAS_VERTEX_ID
// #define HAS_SECOND_UV

#if defined(OVR_VERTEX_HAS_TANGENTS)
#define HAS_TANGENTS
#endif

// Avatar SDK specific include files, shared with all the other avatar shaders:
#define OVR_VERTEX_POSITION_FIELD_NAME v_Position
#define OVR_VERTEX_NORMAL_FIELD_NAME v_Normal
#define OVR_VERTEX_TANGENT_FIELD_NAME v_TBN
#define OVR_VERTEX_VERT_ID_FIELD_NAME v_Id
#define OVR_VERTEX_TEXCOORD_FIELD_NAME v_UVCoord1
#define OVR_VERTEX_COLOR_FIELD_NAME v_Color
#define OVR_VERTEX_ORMT_FIELD_NAME v_ORMT
#include "UnityCG.cginc"
#include "../../../../Scripts/ShaderUtils/AvatarCustomTypes.cginc"

#include "../../../../Scripts/ShaderUtils/AvatarCustom.cginc"

// This includes the required macros for shadow and attenuation values.
#include "AutoLight.cginc"

/////////////////////////////////////////////////////////
// Original ported code from primitive.vert:

// Composite v2f structure derived from the original Khronos cginc files.
struct v2f
{
    float4 v_Color : COLOR;
 #ifdef MATERIAL_MODE_VERTEX
    float4 v_ORMT : TEXCOORD1; // holds ormt vertex color channel
 #endif

    float4 v_Position : SV_POSITION;

    float2 v_UVCoord1 : TEXCOORD0;
    float2 v_UVCoord2 : TEXCOORD11;    // always define this to simplify functions

#ifdef HAS_NORMALS
#ifdef HAS_TANGENTS
    float3 v_Tangent : TEXCOORD6;
    float3 v_Bitangent : TEXCOORD7;
#endif
    float3 v_Normal : TEXCOORD2;
#endif

#if defined(OVR_VERTEX_HAS_VERTEX_ID)
    UNITY_VERTEX_INPUT_INSTANCE_ID  // uint v_Id : TEXCOORD3;
#endif

    float3 worldPos : TEXCOORD3;

    float3 lightDir : TEXCOORD4;
    DECLARE_LIGHT_COORDS(5)

#ifdef USE_SH_PER_VERTEX // vertex based sh
    half3 sh : TEXCOORD8;
#endif

    UNITY_VERTEX_OUTPUT_STEREO
};

//-----------------------------------------------------------------------------------------
// The following use UnityStandardUtils.cginc as a reference, as of 2020.3.7

half3 AvatarShadeSHPerVertex(half3 normal, half3 ambient) {
#if defined(USE_SH_PER_PIXEL)
  // Completely per-pixel
  // nothing to do here
#else
  // Completely per-vertex
  ambient += max(half3(0, 0, 0), ShadeSH9(half4(normal, 1.0)));
#endif
  return ambient;
}

//-----------------------------------------------------------------------------------------

v2f vert(OvrDefaultAppdata v) {
    OvrInitializeDefaultAppdata(v);

    v2f o;
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    // Object -> world transformation
    OvrVertexData vertexData = OVR_CREATE_VERTEX_DATA(v);

    // Pass through clip space position, uv
    o.v_Position = UnityObjectToClipPos(vertexData.position);

#ifdef HAS_SECOND_UV
    o.v_UVCoord2 = o.v_UVCoord2;
#else
    o.v_UVCoord2 = o.v_UVCoord1;
#endif

#ifdef MATERIAL_MODE_VERTEX
    o.v_ORMT = OVR_GET_VERTEX_ORMT(v);
    o.v_Color = OVR_GET_VERTEX_COLOR(v);
#else
    o.v_Color.a = OVR_GET_VERTEX_COLOR(v).a;
#endif

#ifdef HAS_NORMALS
#ifdef HAS_TANGENTS
    float4 tangent = vertexData.tangent;

//    float3 normalW = normalize(float3(u_NormalMatrix * float4(getNormal(vertexData.position, o.v_UVCoord1, o.v_UVCoord2, vertexData.normal).xyz, 0.0)));
//    float3 tangentW = normalize(float3(u_ModelMatrix * float4(tangent.xyz, 0.0)));

    float3 normalW = UnityObjectToWorldNormal(vertexData.normal);
    float3 tangentW = UnityObjectToWorldDir(tangent.xyz);
    float3 bitangentW = cross(normalW, tangentW) * tangent.w;
    o.v_Tangent = tangentW;
    o.v_Bitangent = bitangentW;
    o.v_Normal =  normalW;
#else
    o.v_Normal = UnityObjectToWorldNormal(vertexData.normal);
#endif
#endif

    o.v_UVCoord1 = OVR_GET_VERTEX_TEXCOORD(v);

    o.worldPos = mul(unity_ObjectToWorld, vertexData.position).xyz;

    o.lightDir = WorldSpaceLightDir(vertexData.position).xyz;

    // taken from "AutoLight.cginc", COMPUTE_LIGHT_COORDS(), but specialized to accomodate vertexData.position
#ifdef POINT
    o._LightCoord = mul(unity_WorldToLight, mul(unity_ObjectToWorld, vertexData.position)).xyz;
#endif
#ifdef SPOT
    o._LightCoord = mul(unity_WorldToLight, mul(unity_ObjectToWorld, vertexData.position));
#endif

#ifdef USE_SH_PER_VERTEX // vertex based sh
    o.sh = 0;
    o.sh = AvatarShadeSHPerVertex(o.v_Normal, o.sh);
#endif

    return o;
}
