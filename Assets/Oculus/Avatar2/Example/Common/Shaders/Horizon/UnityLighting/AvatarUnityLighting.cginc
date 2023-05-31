#ifndef AVATAR_UNITY_LIGHTING_CGINC
#define AVATAR_UNITY_LIGHTING_CGINC

#include "UnityGlobalIllumination.cginc"

#include "AvatarCommon\AvatarCommonProperties.cginc"
#include "AvatarUnitySurfaceFields.cginc"
#include "AvatarUnityLightingTypes.cginc"

UnityGIInput GetGlobalIlluminationInput(
  v2f IN,
  fixed3 lightDir,
  float3 worldPos,
  float3 worldViewDir,
  fixed attenuation)
{
  // Setup lighting environment
  UnityGI gi;
  UNITY_INITIALIZE_OUTPUT(UnityGI, gi);

  gi.indirect.diffuse = 0;
  gi.indirect.specular = 0;
  gi.light.color = _LightColor0.rgb;
  gi.light.dir = lightDir;

  // Call GI (lightmaps/SH/reflections) lighting function
  UnityGIInput giInput;
  UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
  giInput.light = gi.light;
  giInput.worldPos = worldPos;
  giInput.worldViewDir = worldViewDir;
  giInput.atten = attenuation;

#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
  giInput.lightmapUV = IN.lmap;
#else
  giInput.lightmapUV = 0.0;
#endif

#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
  giInput.ambient = IN.sh;
#else
  giInput.ambient.rgb = 0.0;
#endif

  giInput.probeHDR[0] = unity_SpecCube0_HDR;
  giInput.probeHDR[1] = unity_SpecCube1_HDR;
#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
  giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
#endif

#ifdef UNITY_SPECCUBE_BOX_PROJECTION
  giInput.boxMax[0] = unity_SpecCube0_BoxMax;
  giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
  giInput.boxMax[1] = unity_SpecCube1_BoxMax;
  giInput.boxMin[1] = unity_SpecCube1_BoxMin;
  giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
#endif

  return giInput;
}

AvatarShaderGlobalIllumination GetGlobalIllumination(
  UnityGIInput data,
  half smoothness,
  half metallic,
  half occlusion,
  half3 albedo,
  half3 normal)
{
  Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(
    smoothness,
    data.worldViewDir,
    normal,
    lerp(unity_ColorSpaceDielectricSpec.rgb, albedo, metallic));

  UnityGI gi;
  UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
  half ambientOcclusion = lerp(1.0f, occlusion, _AmbientOcclusionEffect);
  gi = UnityGlobalIllumination(data, ambientOcclusion, normal, g);

  AvatarShaderLight light;
  light.direction = gi.light.dir;
  light.color = gi.light.color;

  AvatarShaderIndirect indirect;
  indirect.diffuse = gi.indirect.diffuse;
  indirect.specular = gi.indirect.specular;

  AvatarShaderGlobalIllumination avatarGI;
  avatarGI.light = light;
  avatarGI.indirect = indirect;

  return avatarGI;
}

///////////////////////////////////////
// vertex shader funcs and generator //
///////////////////////////////////////
v2f AvatarShaderVertInit(appdata v) {
  OVR_INITIALIZE_VERTEX_FIELDS(v)
  UNITY_SETUP_INSTANCE_ID(v);
  v2f o;
  UNITY_INITIALIZE_OUTPUT(v2f,o);
  UNITY_TRANSFER_INSTANCE_ID(v,o);
  UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

  return o;
}
OvrVertexData AvatarShaderVertTransform(appdata v, inout v2f o) {
  OvrVertexData vertexData = OVR_CREATE_VERTEX_DATA(v);
  float4 objPos = vertexData.position;
  float3 objNormal = vertexData.normal;

  o.pos = UnityObjectToClipPos(objPos);
  o.color = v.color;
  o.uv = v.uv;
  o.ormt = v.ormt;

  // Assumes only caring about xy component on v.uv
  // Duplicate it (by default) for properties map and effects map UVs
  o.propertiesMapUV.xy = o.uv.xy;
  o.effectsMapUV.xy = o.uv.xy;

  float3 worldPos = mul(unity_ObjectToWorld, objPos).xyz;
  float3 worldNormal = UnityObjectToWorldNormal(objNormal);

#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED) && !defined(UNITY_HALF_PRECISION_FRAGMENT_SHADER_REGISTERS)
  float4 objTangent = vertexData.tangent;
  fixed3 worldTangent = UnityObjectToWorldDir(objTangent.xyz);
  fixed tangentSign = objTangent.w * unity_WorldTransformParams.w;
  fixed3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign;

  o.tSpace0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x);
  o.tSpace1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y);
  o.tSpace2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z);
#endif

  o.worldPos.xyz = worldPos;
  o.worldNormal = worldNormal;

  return vertexData;
}

void AvatarShaderVertLighting(appdata v, inout v2f o) {
  float3 worldNormal = o.worldNormal;

#ifdef DYNAMICLIGHTMAP_ON
  o.lmap.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
#ifdef LIGHTMAP_ON
  o.lmap.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
#endif

  // SH/ambient and vertex lights
#ifndef LIGHTMAP_ON
  #if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
    o.sh = 0;
    // Approximated illumination from non-important point lights
    #ifdef VERTEXLIGHT_ON
      o.sh += Shade4PointLights (
        unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
        unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
        unity_4LightAtten0, worldPos, worldNormal);
    #endif
    o.sh = ShadeSHPerVertex (worldNormal, o.sh);
  #endif
#endif // !LIGHTMAP_ON

  UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy); // pass shadow and, possibly, light cookie coordinates to pixel shader
#ifdef FOG_COMBINED_WITH_TSPACE
  UNITY_TRANSFER_FOG_COMBINED_WITH_TSPACE(o, o.pos); // pass fog coordinates to pixel shader
#elif defined (FOG_COMBINED_WITH_WORLD_POS)
  UNITY_TRANSFER_FOG_COMBINED_WITH_WORLD_POS(o, o.pos); // pass fog coordinates to pixel shader
#else
  UNITY_TRANSFER_FOG(o, o.pos); // pass fog coordinates to pixel shader
#endif
}

#define GENERATE_AVATAR_UNITY_LIGHTING_DEFAULT_VERTEX_PROGRAM(VertProgramName) \
  v2f VertProgramName(appdata v) { \
    v2f o = AvatarShaderVertInit(v); \
    \
    AvatarShaderVertTransform(v, o); \
    \
    AvatarShaderVertLighting(v, o); \
    \
    return o; \
  }

#define GENERATE_AVATAR_UNITY_LIGHTING_VERTEX_PROGRAM(VertProgramName, CustomVertFunc) \
  v2f VertProgramName(appdata v) { \
    v2f o = AvatarShaderVertInit(v); \
    \
    OvrVertexData vertexData = AvatarShaderVertTransform(v, o); \
    \
    AvatarShaderVertLighting(v, o); \
    \
    CustomVertFunc(v, vertexData, o); \
    \
    return o; \
  }

/////////////////////////////////////////
// fragment shader funcs and generator //
/////////////////////////////////////////

void AvatarShaderFragInit(inout v2f IN) {
  UNITY_SETUP_INSTANCE_ID(IN);

  #ifdef FOG_COMBINED_WITH_TSPACE
    UNITY_EXTRACT_FOG_FROM_TSPACE(IN);
  #elif defined (FOG_COMBINED_WITH_WORLD_POS)
    UNITY_EXTRACT_FOG_FROM_WORLD_POS(IN);
  #else
    UNITY_EXTRACT_FOG(IN);
  #endif
}

float3 AvatarShaderFragGetWorldPos(v2f IN) {
  return IN.worldPos.xyz;
}

fixed3 AvatarShaderFragGetLightDir(float3 worldPos) {
  // Directional light only used the lightDir as a normalized direction
  // instead of a light position as well
#ifndef USING_DIRECTIONAL_LIGHT
  return normalize(UnityWorldSpaceLightDir(worldPos));
#else
  return _WorldSpaceLightPos0.xyz;
#endif
}

float3 AvatarShaderFragGetWorldViewDir(float3 worldPos) {
  return normalize(UnityWorldSpaceViewDir(worldPos));
}

float3 OverideColorWithDebug(float3 original, in v2f IN) {
#if defined(_RENDER_DEBUG_VERTEX_COLOR)
  original.rgb = 1; // IN.color.rgb;
#elif defined(_RENDER_DEBUG_UVS)
  original.rg = IN.uv.rg;
  original.b = 0;
#elif defined(_RENDER_DEBUG_WORLD_NORMAL)
  original.rgb = IN.worldNormal.xyz;
#elif defined(_RENDER_DEBUG_WORLD_POSITION)
  original.rgb = IN.worldPos.xyz;
#elif defined(_RENDER_DEBUG_SH)
  original.rgb = IN.sh.xyz;
#endif
  return original;
}

#define AVATAR_SHADER_FRAG_INIT_AND_CALL_SURFACE(SurfaceFuncName, SurfaceOutputType) \
  SurfaceOutputType o; \
  UNITY_INITIALIZE_OUTPUT(SurfaceOutputType, o); \
  \
  /* Initialize required values */ \
  SET_AVATAR_SHADER_SURFACE_ALBEDO_FIELD(o, 0.0); \
  SET_AVATAR_SHADER_SURFACE_SMOOTHNESS_FIELD(o, 0.0); \
  SET_AVATAR_SHADER_SURFACE_METALLIC_FIELD(o, 0.0); \
  SET_AVATAR_SHADER_SURFACE_OCCLUSION_FIELD(o, 1.0); \
  SET_AVATAR_SHADER_SURFACE_NORMAL_FIELD(o, normalize(IN.worldNormal)); \
  /* call surface function */ \
  SurfaceFuncName(IN, o);

#define AVATAR_SHADER_FRAG_LIGHTING(LightingFuncName) \
  /* compute lighting & shadowing factor */ \
  UNITY_LIGHT_ATTENUATION(atten, IN, worldPos) \
  fixed4 c = 0; \
  \
  /* Setup lighting environment */ \
  UnityGIInput giInput = GetGlobalIlluminationInput(IN, lightDir, worldPos, worldViewDir, atten); \
  \
  half smoothness = GET_AVATAR_SHADER_SURFACE_SMOOTHNESS_FIELD(o); \
  half metallic = GET_AVATAR_SHADER_SURFACE_METALLIC_FIELD(o); \
  half occlusion = GET_AVATAR_SHADER_SURFACE_OCCLUSION_FIELD(o); \
  \
  half3 albedo = GET_AVATAR_SHADER_SURFACE_ALBEDO_FIELD(o); \
  half3 normal = GET_AVATAR_SHADER_SURFACE_NORMAL_FIELD(o); \
  \
  AvatarShaderGlobalIllumination gi = GetGlobalIllumination(giInput, smoothness, metallic, occlusion, albedo, normal); \
  \
  /* realtime lighting: call lighting function */ \
  c += LightingFuncName(o, worldViewDir, gi); \
  \
  UNITY_APPLY_FOG(_unity_fogCoord, c); /* apply fog */ \
  c.rgb = OverideColorWithDebug(c.rgb, IN); \
  UNITY_OPAQUE_ALPHA(c.a); \
  return c;

#define GENERATE_AVATAR_UNITY_LIGHTING_FRAGMENT_PROGRAM(ProgName, SurfFunc, SurfOutputType, LightingFunc) \
  fixed4 ProgName(v2f IN) : SV_Target { \
    AvatarShaderFragInit(IN); \
    \
    float3 worldPos = AvatarShaderFragGetWorldPos(IN); \
    fixed3 lightDir = AvatarShaderFragGetLightDir(worldPos); \
    float3 worldViewDir = AvatarShaderFragGetWorldViewDir(worldPos); \
    \
    AVATAR_SHADER_FRAG_INIT_AND_CALL_SURFACE(SurfFunc, SurfOutputType) \
    AVATAR_SHADER_FRAG_LIGHTING(LightingFunc) \
  }

#endif
