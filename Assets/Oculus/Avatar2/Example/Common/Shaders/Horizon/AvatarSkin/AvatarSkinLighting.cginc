#ifndef AVATAR_SKIN_LIGHTING_CGINC
#define AVATAR_SKIN_LIGHTING_CGINC

#include "AvatarSkinProperties.cginc"
#include "../AvatarCommon/AvatarCommonLighting.cginc"

////////////////////////////////////////////////////
// Surface and Lighting Functions and Definitions //
////////////////////////////////////////////////////

half3 Translucency(
    half3 N,
    half3 L,
    half3 V,
    half power,
    half scale,
    half thickness,
    half3 translucencyColor,
    half translucencyDistortion)
{
    // The vertex GI lighting system has a specific keyword
    // for enabling direct lighting at all, so, pay attention to that
    #if !defined(__LIGHTING_SYSTEM_VERTEX_GI) || defined(DIRECTIONAL_LIGHT)
        // Calculate intensity of backlight (light translucent).
        // This is pulled from a "fast subsurface scattering" work here
        // https://www.alanzucconi.com/2017/08/30/fast-subsurface-scattering-1

        // H here isn't between L and V, but between L and N
        half3 H = normalize(L + N * translucencyDistortion);
        float VdotH = powApprox(saturate(dot(V, -H)), power * 64.0) * scale;
        return translucencyColor * VdotH * thickness;
    #else
        return 0.0h;
    #endif
}

half3 BacklitWarmth(half3 NegNdotL, half maximumContribution, half3 backlightColor) {
    // The vertex GI lighting system has a specific keyword
    // for enabling direct lighting at all, so, pay attention to that
    #if !defined(__LIGHTING_SYSTEM_VERTEX_GI) || defined(DIRECTIONAL_LIGHT)
        // Take the back light from 0 to a max over the 0 to 1 range of NegNdotL
        half amount = lerp(0.0h, maximumContribution, saturate(NegNdotL));
        return backlightColor * amount;
    #else
        return 0.0h;
    #endif
}

half3 FinalLightingCombine(
    half3 directDiffuse,
    half3 directSpecular,
    half3 indirectDiffuse,
    half3 indirectSpecular,
    half3 translucency,
    half3 backlitWarmth,
    half3 lightColor)
{
  #if defined(_RENDER_DEBUG_TRANSLUCENCY)
    return translucency * lightColor;
  #elif defined(_RENDER_DEBUG_BACKLIGHT)
    return backlitWarmth * lightColor;
  #elif AVATAR_COMMON_DEBUG_LIGHTING_ENABLED
    // Return the "AvatarCommonLighting.cginc" version (without backlitWamrth and translucency) to handle the debug rendering
    return FinalLightingCombine(directDiffuse, directSpecular, indirectDiffuse, indirectSpecular);
  #else
    // Full lighting
    half3 directLighting = (directDiffuse + directSpecular + translucency + backlitWarmth) * lightColor;
    return directLighting + indirectDiffuse + indirectSpecular;

  #endif
}

half3 AvatarSkinLighting(
    half3 albedo,
    float3 normal,
    float3 viewDir,
    half minDiffuse,
    half perceptualRoughness,
    half perceptualSmoothness,
    half metallic,
    half thickness,
    half backlightScale,
    half3 translucencyColor,
    half3 backlightColor,
    half occlusion,
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

    half NdotV = saturate(dot(normal, viewDir));
    
    half directOcclusion = lerp(1.0f, occlusion, _DirectOcclusionEffect);

    // For the moment, everything has the same direct specular
    half3 directSpecular = DirectSpecularLightingWithOcclusion(specColor, NdotH, LdotH, NdotL, roughness, directOcclusion);

    // All components have same direct diffuse and indirect lighting
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

    // Skin component needs some additional lighting functions
    half3 translucency = Translucency(
        normal,
        lightDirection,
        viewDir,
        _TranslucencyPower,
        _TranslucencyScale,
        thickness,
        translucencyColor,
        _Distortion);

    half3 backlitWarmth = BacklitWarmth(-rawNdotL, backlightScale, backlightColor);

    return FinalLightingCombine(directDiffuse, directSpecular, indirectDiffuse, indirectSpecular, translucency, backlitWarmth, gi.light.color);
}

#endif
