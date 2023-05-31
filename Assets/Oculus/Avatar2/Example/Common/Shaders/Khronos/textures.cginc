// TODO: Replace all float3x3 UV transforms with the Unity ST format if needed...

// General Material
#ifdef HAS_NORMAL_MAP
uniform sampler2D u_NormalSampler;
uniform float u_NormalScale;
uniform int u_NormalUVSet;
//uniform float3x3 u_NormalUVTransform;
#endif

#ifdef HAS_EMISSIVE_MAP
uniform sampler2D u_EmissiveSampler;
uniform int u_EmissiveUVSet;
uniform float3 u_EmissiveFactor;
//uniform float3x3 u_EmissiveUVTransform;
#endif

#ifdef HAS_OCCLUSION_MAP
#ifndef USE_ORM_EXTENSION
uniform sampler2D u_OcclusionSampler;
uniform int u_OcclusionUVSet;
#endif
uniform float u_OcclusionStrength;
//uniform float3x3 u_OcclusionUVTransform;
#endif

// Metallic Roughness Material
#ifdef HAS_BASE_COLOR_MAP
uniform sampler2D u_BaseColorSampler;
uniform int u_BaseColorUVSet;
//uniform float3x3 u_BaseColorUVTransform;
#endif

#ifdef HAS_METALLIC_ROUGHNESS_MAP
uniform sampler2D u_MetallicRoughnessSampler;
uniform int u_MetallicRoughnessUVSet;
//uniform float3x3 u_MetallicRoughnessUVTransform;
#endif

// Specular Glossiness Material
#ifdef HAS_DIFFUSE_MAP
uniform sampler2D u_DiffuseSampler;
uniform int u_DiffuseUVSet;
//uniform float3x3 u_DiffuseUVTransform;
#endif

#ifdef HAS_SPECULAR_GLOSSINESS_MAP
uniform sampler2D u_SpecularGlossinessSampler;
uniform int u_SpecularGlossinessUVSet;
//uniform float3x3 u_SpecularGlossinessUVTransform;
#endif

// IBL
#if defined(USE_IBL) || defined(DEBUG_IBL)
uniform samplerCUBE u_DiffuseEnvSampler;
uniform samplerCUBE u_SpecularEnvSampler;
uniform sampler2D u_brdfLUT;
#endif

float2 getNormalUV(float2 v_UVCoord1, float2 v_UVCoord2) 
{
    float3 uv = float3(v_UVCoord1, 1.0);
#ifdef HAS_NORMAL_MAP
    uv.xy = u_NormalUVSet < 1 ? v_UVCoord1 : v_UVCoord2;
    // TODO: Replace this with the Unity ST format if needed...
    //#ifdef HAS_NORMAL_UV_TRANSFORM
    //uv *= u_NormalUVTransform;
    //#endif
#endif
    return uv.xy;
}

float2 getEmissiveUV(float2 v_UVCoord1, float2 v_UVCoord2)
{
    float3 uv = float3(v_UVCoord1, 1.0);
#ifdef HAS_EMISSIVE_MAP
    uv.xy = u_EmissiveUVSet < 1 ? v_UVCoord1 : v_UVCoord2;
    // TODO: Replace this with the Unity ST format if needed...
    //#ifdef HAS_EMISSIVE_UV_TRANSFORM
    //uv *= u_EmissiveUVTransform;
    //#endif
#endif

    return uv.xy;
}

#ifndef USE_ORM_EXTENSION
float2 getOcclusionUV(float2 v_UVCoord1, float2 v_UVCoord2) 
{
    float3 uv = float3(v_UVCoord1, 1.0);
#ifdef HAS_OCCLUSION_MAP
    uv.xy = u_OcclusionUVSet < 1 ? v_UVCoord1 : v_UVCoord2;
    // TODO: Replace this with the Unity ST format if needed...
    //#ifdef HAS_OCCLSION_UV_TRANSFORM
    //uv *= u_OcclusionUVTransform;
    //#endif
#endif
    return uv.xy;
}
#endif

float2 getBaseColorUV(float2 v_UVCoord1, float2 v_UVCoord2)
{
    float3 uv = float3(v_UVCoord1, 1.0);
#ifdef HAS_BASE_COLOR_MAP
    uv.xy = u_BaseColorUVSet < 1 ? v_UVCoord1 : v_UVCoord2;
    // TODO: Replace this with the Unity ST format if needed...
    //#ifdef HAS_BASECOLOR_UV_TRANSFORM
    //uv *= u_BaseColorUVTransform;
    //#endif
#endif
    return uv.xy;
}

float2 getMetallicRoughnessUV(float2 v_UVCoord1, float2 v_UVCoord2)
{
    float3 uv = float3(v_UVCoord1, 1.0);
#ifdef HAS_METALLIC_ROUGHNESS_MAP
    uv.xy = u_MetallicRoughnessUVSet < 1 ? v_UVCoord1 : v_UVCoord2;
    // TODO: Replace this with the Unity ST format if needed...
    //#ifdef HAS_METALLICROUGHNESS_UV_TRANSFORM
    //uv *= u_MetallicRoughnessUVTransform;
    //#endif
#endif
    return uv.xy;
}

float2 getSpecularGlossinessUV(float2 v_UVCoord1, float2 v_UVCoord2)
{
    float3 uv = float3(v_UVCoord1, 1.0);
#ifdef HAS_SPECULAR_GLOSSINESS_MAP
    uv.xy = u_SpecularGlossinessUVSet < 1 ? v_UVCoord1 : v_UVCoord2;
    // TODO: Replace this with the Unity ST format if needed...
    //#ifdef HAS_SPECULARGLOSSINESS_UV_TRANSFORM
    //uv *= u_SpecularGlossinessUVTransform;
    //#endif
#endif
    return uv.xy;
}

float2 getDiffuseUV(float2 v_UVCoord1, float2 v_UVCoord2)
{
    float3 uv = float3(v_UVCoord1, 1.0);
#ifdef HAS_DIFFUSE_MAP
    uv.xy = u_DiffuseUVSet < 1 ? v_UVCoord1 : v_UVCoord2;
    // TODO: Replace this with the Unity ST format if needed...
    //#ifdef HAS_DIFFUSE_UV_TRANSFORM
    //uv *= u_DiffuseUVTransform;
    //#endif
#endif
    return uv.xy;
}
