#ifndef AVATAR_COMMON_LIGHTING_CGINC
#define AVATAR_COMMON_LIGHTING_CGINC

#include "Horizon/MacroRemapper.cginc"

// Lighting functions that all avatar shaders share

#include "UnityPBSLighting.cginc"

#include "AvatarShaderTypes.cginc"
#include "AvatarCommonProperties.cginc"
#include "AvatarCommonSurfaceFIelds.cginc"
#include "AvatarCommonUtils.cginc"

#define AVATAR_COMMON_DEBUG_LIGHTING_ENABLED defined(_RENDER_DEBUG_DIFFUSE) || defined(_RENDER_DEBUG_SPECULAR) || defined(_RENDER_DEBUG_INDIRECT_DIFFUSE) || defined(_RENDER_DEBUG_INDIRECT_SPECULAR)

half OffsetAndScaleMinimumDiffuse(half texChannel) {
    // channel will define the minimum diffuse, but texture stores values from 0 to 1, but
    // minimum diffuse can be from -1 to 1
    return texChannel * 2.0 - 1.0;
}

half3 DirectDiffuseLighting(half3 diffColor, half NdotL, half minDiffuse) {
    // The vertex GI lighting system has a specific keyword
    // for enabling direct lighting at all, so, pay attention to that
    #if !defined(__LIGHTING_SYSTEM_VERTEX_GI) || defined(DIRECTIONAL_LIGHT)
        // For direct lighting, per art direction, no area is to go to all black,
        // so there needs to be some shading even on the "other side" of the light.
        // To accomplish this, instead of the lighting contribution being from 0 -> 1
        // based on a saturated NdotL (thus only lighting the front hemisphere),
        // the complete range of NdoL will be in use, and there will be a defined
        // minimum at the "back pole" of the hemisphere.
        half diffuseIntensity = saturate(lerp(minDiffuse, 1.0h, NdotL * 0.5 + 0.5));
        return diffColor * diffuseIntensity;
    #else
        return 0.0;
    #endif
}

half3 DirectDiffuseLightingWithOcclusion(half3 diffColor, half NdotL, half minDiffuse, half directOcclusion) {
    return DirectDiffuseLighting(diffColor, NdotL, minDiffuse) * directOcclusion;
}

half3 DirectSpecularLighting(half3 specColor, float NdotH, float LdotH, half NdotL, float roughness) {
    // The vertex GI lighting system has a specific keyword
    // for enabling direct lighting at all, so, pay attention to that
    #if !defined(__LIGHTING_SYSTEM_VERTEX_GI) || defined(DIRECTIONAL_LIGHT)
        // From SIGGRAPH 2015 paper, but pulled from Unity shader source
        // https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_renaldas_2D00_slides.pdf
        float a = roughness;
        float a2 = a * a;

        float d = NdotH * NdotH * (a2 - 1.0f) + 1.00001f;
        float specularTerm = a2 / (max(0.1f, LdotH * LdotH) * (roughness + 0.5f) * (d * d) * 4);

        // on mobiles (where half actually means something) denominator have risk of overflow
        // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
        // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
        #if defined (SHADER_API_MOBILE)
            specularTerm = specularTerm - 1e-4f;
            specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
        #endif

        return (specularTerm * specColor) * NdotL;
    #else
        return 0.0h;
    #endif
}

half3 DirectSpecularLightingWithOcclusion(half3 specColor, float NdotH, float LdotH, half NdotL, float roughness, half directOcclusion) {
    return DirectSpecularLighting(specColor, NdotH, LdotH, NdotL, roughness) * directOcclusion;
}

half3 IndirectDiffuseLighting(half3 diffColor, AvatarShaderIndirect indirect) {
    return indirect.diffuse * diffColor;
}

half3 IndirectSpecularLighting(
    half3 specColor,
    float roughness,
    half perceptualRoughness,
    half perceptualSmoothness,
    half oneMinusReflectivity,
    half NdotV,
    AvatarShaderIndirect indirect)
{
    // Pulled from Unity Shader source
    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(realRoughness^2+1)

    // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
    // 1-x^3*(0.6-0.08*x)   approximation for 1/(x^4+1)
    half surfaceReduction = (0.6 - 0.08 * perceptualRoughness);

    surfaceReduction = 1.0 - roughness * perceptualRoughness * surfaceReduction;

    half grazingTerm = saturate(perceptualSmoothness + (1 - oneMinusReflectivity));
    return surfaceReduction * indirect.specular * FresnelLerpFast (specColor, grazingTerm, NdotV);
}

fixed3 Desat(fixed3 color) {
    fixed3 desatGrayscaleColor = Luminance(color);
    fixed3 desatColor = lerp(desatGrayscaleColor, _DesatTint, _DesatLerp);
    return lerp(color, desatColor, _DesatAmount);
}


half3 FinalLightingCombine(
    half3 directDiffuse,
    half3 directSpecular,
    half3 indirectDiffuse,
    half3 indirectSpecular,
    half3 lightColor)
{
  #if defined(_RENDER_DEBUG_DIFFUSE)
    return directDiffuse * lightColor;
  #elif defined(_RENDER_DEBUG_SPECULAR)
    return directSpecular * lightColor;
  #elif defined(_RENDER_DEBUG_INDIRECT_DIFFUSE)
    return indirectDiffuse;
  #elif defined(_RENDER_DEBUG_INDIRECT_SPECULAR)
    return indirectSpecular;
  #else
    // Full lighting
    half3 directLighting = (directDiffuse + directSpecular) * lightColor;
   return directLighting + indirectDiffuse + indirectSpecular;
  #endif
}

// Caller supplied diffColor, specColor, and direct specular
// Computing direct diffuse, indirect lighting
half3 AvatarLightingCommon(
    half3 diffColor,
    half3 specColor,
    half3 directSpecular,
    half3 normal,
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
half3 AvatarLightingCommon(
    half3 albedo,
    float3 normal,
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

    half3 directSpecular;
    directSpecular = DirectSpecularLightingWithOcclusion(specColor, NdotH, LdotH, NdotL, roughness, directOcclusion);

    half3 returnVal = AvatarLightingCommon(
        diffColor,
        specColor,
        directSpecular,
        normal,
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

#define AVATAR_SHADER_DECLARE_COMMON_LIGHTING_PARAMS(AlbedoVar, NormVar, RoughVar, SmoothVar, MetalVar, AlphaVar, MinDiffuseVar, SurfaceOutputVar) \
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
    half directOcclusion = lerp(1.0f, occlusion, _DirectOcclusionEffect);
#endif
