/////////////////////////////////////////////////////////
// Unity specific defines for Avatar SDK:

// Shader feature defines. If we want to change these via the shader manager,
// convert them to multi_compile or shader_feature

#define HAS_NORMALS
#define OVR_VERTEX_HAS_VERTEX_ID

#if defined(OVR_VERTEX_HAS_TANGENTS)
#define HAS_TANGENTS
#endif

// Avatar SDK specific include files, shared with all the other avatar shaders:
#define OVR_VERTEX_POSITION_FIELD_NAME vertex
#define OVR_VERTEX_NORMAL_FIELD_NAME normal
#define OVR_VERTEX_TANGENT_FIELD_NAME tangent
#define OVR_VERTEX_VERT_ID_FIELD_NAME v_Id
#define OVR_VERTEX_TEXCOORD_FIELD_NAME texcoord
#define OVR_VERTEX_COLOR_FIELD_NAME color
#include "UnityCG.cginc"
#include "../../../../Scripts/ShaderUtils/AvatarCustomTypes.cginc"

#include "../../../../Scripts/ShaderUtils/AvatarCustom.cginc"

// This includes the required macros for shadow and attenuation values.
#include "AutoLight.cginc"

/////////////////////////////////////////////////////////
// Original ported code from primitive.vert:

// Composite interpolators structure derived from the original Khronos cginc files.
struct interpolators {
    float4 color : COLOR;
//#ifdef MATERIAL_MODE_VERTEX
    float4 ormt : TEXCOORD1; // holds ormt vertex color channel
//#endif

    float4 vertex : SV_POSITION;

    float2 texcoord : TEXCOORD0;
    float2 texcoord11 : TEXCOORD11;    // always define this to reserve for the future
    // NOTE: There has been discussion of reserving texccord11 for the uncombined/original toxcoords.
    // Such a feature would allow us to use the original texture maps as opposed to an atlased map.
    // However such a gesture would immedeately double the number of texture samples, and only for
    // one sub-mesh. So such a feature would have to be used very sparingly. Potential applications
    // could be a custom pattern for the outfit, or an un-atlased flow map for only the hair.

#ifdef HAS_NORMALS
#ifdef HAS_TANGENTS
    float3 tangent : TEXCOORD6;
    float3 bitangent : TEXCOORD7;
#endif
    float3 normal : TEXCOORD2;
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

#if defined(HAIR_STRAIGHT)
    float3 indirectSpecularVector : TEXCOORD8;
#endif

    // can't get IBL diffuse from the vert sahder:
    // float3 diffuseLight : TEXCOORD9;
    // float3 softDiffuseLight : TEXCOORD10;

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

interpolators vert(OvrDefaultAppdata v) {
    OvrInitializeDefaultAppdata(v);

    interpolators o;
    UNITY_INITIALIZE_OUTPUT(interpolators, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    // Object -> world transformation
//    OvrVertexData vertexData = OVR_CREATE_VERTEX_DATA(v);
    OvrVertexData vertexData = OvrCreateVertexData(v.vertex, v.normal, v.tangent, v.v_Id);




// This alternative to OVR_CREATE_VERTEX_DATA breaks the skinning
    //vertexData.position = v.vertex;
    //vertexData.normal = v.normal;
    //vertexData.tangent = v.tangent;
    //vertexData.vertexId = v.v_Id;

    //bool hasTangents = false;
    //bool applyOffsetAndBias = true;
    //OvrPopulateVertexDataFromExternalTexture(hasTangents, applyOffsetAndBias, vertexData);



    // Pass through clip space position, uv
    o.vertex = UnityObjectToClipPos(vertexData.position);

#ifdef MATERIAL_MODE_VERTEX
    o.ormt = OVR_GET_VERTEX_ORMT(v);
    o.color = OVR_GET_VERTEX_COLOR(v);
#else
    o.color.a = OVR_GET_VERTEX_COLOR(v).a;
#endif

#ifdef HAS_NORMALS
#ifdef HAS_TANGENTS
    float4 tangent = vertexData.tangent;

//    float3 worldNormal = normalize(float3(u_NormalMatrix * float4(getNormal(vertexData.position, o.texcoord, o.v_UVCoord2, vertexData.normal).xyz, 0.0)));
//    float3 worldTangent = normalize(float3(u_ModelMatrix * float4(tangent.xyz, 0.0)));

    float3 worldNormal = UnityObjectToWorldNormal(vertexData.normal);
    float3 worldTangent = UnityObjectToWorldDir(tangent.xyz);
    float3 worldBitangent = normalize(cross(worldNormal, worldTangent) * tangent.w);
    o.tangent = worldTangent;
    o.bitangent = worldBitangent;
    o.normal = worldNormal;
#else
    o.normal = UnityObjectToWorldNormal(vertexData.normal);
#endif
#endif

    o.texcoord = OVR_GET_VERTEX_TEXCOORD(v);

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
    o.sh = AvatarShadeSHPerVertex(o.normal, o.sh);
#endif

// can't get IBL diffuse from the vert sahder:
//    o.diffuseLight = getIblDiffuseSample(o.normal);
//    o.softDiffuseLight = o.diffuseLight; // WHAT!?, This might only be used when HAS_TANGENT_VEC4
#if defined(HAIR_STRAIGHT)
    o.indirectSpecularVector = o.normal; // WHAT!?, This might only be used when HAS_TANGENT_VEC4
#endif

    // BELOW CODE REQUIRES VALID TANGENT DATA
#if defined(HAS_TANGENT_VEC4)
#error // this hasn't been ported from the glsl yet...'
#endif // HAS_TANGENT_VEC4

    return o;
}
