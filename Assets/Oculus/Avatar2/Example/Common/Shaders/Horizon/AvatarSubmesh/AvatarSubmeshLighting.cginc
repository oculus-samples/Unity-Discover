#ifndef AVATAR_SUBMESH_LIGHTING_CGINC
#define AVATAR_SUBMESH_LIGHTING_CGINC

#include "../Horizon/MacroRemapper.cginc"

// Lighting functions utilized by the submesh technique to make different sub meshes in the material look different

#include "..\AvatarSubmesh\AvatarSubmeshProperties.cginc"
#include "..\AvatarCommon\AvatarCommonLighting.cginc"
#include "..\AvatarEye\AvatarEyeGlint.cginc"

#define AVATAR_SUBMESH_DEBUG_LIGHTING_ENABLED defined(_RENDER_DEBUG_DIFFUSE) || defined(_RENDER_DEBUG_SPECULAR) || defined(_RENDER_DEBUG_INDIRECT_DIFFUSE) || defined(_RENDER_DEBUG_INDIRECT_SPECULAR)

// Caller supplied diffColor, specColor, and direct specular
// Computing direct diffuse, indirect lighting
half3 AvatarLightingSubmesh(
    half3 diffColor,
    half3 specColor,
    half3 directSpecular,
    half3 normal,
    float subMeshType,
    half3 viewDir,
    half minDiffuse,
    float roughness,
    half perceptualRoughness,
    half perceptualSmoothness,
    half metallic,
    half oneMinusReflectivity,
    half rawNdotL,
    half NdotL,
    half directOcclusion,
    AvatarShaderGlobalIllumination gi)
{
    half NdotV = saturate(dot(normal, viewDir));

    half3 directDiffuse = DirectDiffuseLightingWithOcclusion(diffColor, rawNdotL, minDiffuse, directOcclusion);

    half3 indirectDiffuse = IndirectDiffuseLighting(diffColor, gi.indirect);
    half3 indirectSpecular = IndirectSpecularLighting(
        specColor,
        roughness,
        perceptualRoughness,
        perceptualSmoothness,
        oneMinusReflectivity,
        NdotV,
        gi.indirect);

    return FinalLightingCombine(directDiffuse, directSpecular, indirectDiffuse, indirectSpecular, gi.light.color);
}

// Computes all lighting (direct diffuse, direct specular, indirect diffuse, indirect specular)
half3 AvatarLightingSubmesh(
    half3 albedo,
    float3 normal,
    float subMeshType,
    float3 viewDir,
    half minDiffuse,
    half perceptualRoughness,
    half perceptualSmoothness,
    half metallic,
    half directOcclusion,
    AvatarShaderGlobalIllumination gi)
{
    half oneMinusReflectivity;
    half3 specColor;
    half3 diffColor = DiffuseAndSpecularFromMetallic(
        albedo,
        metallic,
        /*out*/ specColor,
        /*out*/ oneMinusReflectivity);

    float3 lightDirection = gi.light.direction;
    float3 halfDirection = Unity_SafeNormalize(lightDirection + viewDir);

    half rawNdotL = clamp(dot(normal, lightDirection), -1.0, 1.0); // -1 to 1
    half NdotL = saturate(rawNdotL); // 0 to 1
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

    float NdotH = saturate(dot(normal, halfDirection));
    float LdotH = saturate(dot(lightDirection, halfDirection));

    bool useEyeGlint =
        ((subMeshType > (OVR_SUBMESH_TYPE_L_EYE - OVR_SUBMESH_TYPE_BUFFER) / 255.0) &&
         (subMeshType < (OVR_SUBMESH_TYPE_R_EYE + OVR_SUBMESH_TYPE_BUFFER) / 255.0));

    half3 directSpecular;
#ifdef EYE_GLINTS
    [branch]
    if (useEyeGlint) {
      directSpecular = DirectSpecularLightingWithOcclusionWithEyeGlints(specColor, NdotH, LdotH, NdotL, roughness, directOcclusion, normal, lightDirection, viewDir);
    }
    else
#endif
    {
      directSpecular = DirectSpecularLightingWithOcclusion(specColor, NdotH, LdotH, NdotL, roughness, directOcclusion);
    }

    half3 returnVal = AvatarLightingSubmesh(
        diffColor,
        specColor,
        directSpecular,
        normal,
        subMeshType,
        viewDir,
        minDiffuse,
        roughness,
        perceptualRoughness,
        perceptualSmoothness,
        metallic,
        oneMinusReflectivity,
        rawNdotL,
        NdotL,
        directOcclusion,
        gi);

    return returnVal;
}

#define AVATAR_SHADER_DECLARE_SUBMESH_LIGHTING_PARAMS(AlbedoVar, NormVar, RoughVar, SmoothVar, MetalVar, AlphaVar, MinDiffuseVar, subMeshTypeVar, SurfaceOutputVar) \
    half3 NormVar = GET_AVATAR_SHADER_SURFACE_NORMAL_FIELD(SurfaceOutputVar); \
    fixed3 AlbedoVar = GET_AVATAR_SHADER_SURFACE_ALBEDO_FIELD(SurfaceOutputVar); \
    \
    half MetalVar = GET_AVATAR_SHADER_SURFACE_METALLIC_FIELD(SurfaceOutputVar); \
    half AlphaVar = GET_AVATAR_SHADER_SURFACE_ALPHA_FIELD(SurfaceOutputVar); \
    \
    half RoughVar = GET_AVATAR_SHADER_SURFACE_ROUGHNESS_FIELD(SurfaceOutputVar); \
    half SmoothVar = GET_AVATAR_SHADER_SURFACE_SMOOTHNESS_FIELD(SurfaceOutputVar); \
    half MinDiffuseVar = GET_AVATAR_SHADER_SURFACE_MIN_DIFFUSE_FIELD(SurfaceOutputVar); \
    half occlusion = GET_AVATAR_SHADER_SURFACE_OCCLUSION_FIELD(SurfaceOutputVar); \
    half directOcclusion = lerp(1.0f, occlusion, _DirectOcclusionEffect); \
    float subMeshTypeVar = SurfaceOutputVar.SubMeshType;

#endif
