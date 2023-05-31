#ifndef AVATAR_EYE_GLINT_CGINC
#define AVATAR_EYE_GLINT_CGINC

#include "../AvatarCommon/AvatarCommonLighting.cginc"
#include "../AvatarEye/AvatarEyeProperties.cginc"

//////////////////////////////////////
// Eye Glint specific calculations. //
//////////////////////////////////////

half3 DirectSpecularLightingWithOcclusionWithEyeGlints(half3 specColor, float NdotH, float LdotH, half NdotL, float roughness, half directOcclusion, float3 normal, float3 lightDirection, float3 viewDir) {
    // first cseate a version of the original spec light, amplified by _EyeGlintFactor
#ifdef EYE_GLINTS
    LdotH /= _EyeGlintFactor;
    NdotL *= _EyeGlintFactor;
#endif
    half3 directSpecular = DirectSpecularLightingWithOcclusion(specColor, NdotH, LdotH, NdotL, roughness, directOcclusion);

#ifdef EYE_GLINTS_BEHIND
    // create a second reflected spec light to maintain an eye glint from the backside, original spec intensity
    lightDirection = float3(-lightDirection.x, lightDirection.y, -lightDirection.z);
    float3 halfDirection = Unity_SafeNormalize(lightDirection + viewDir);
    half rawNdotL = clamp(dot(normal, lightDirection), -1.0, 1.0); // -1 to 1
    NdotL = saturate(rawNdotL); // 0 to 1
    NdotH = saturate(dot(normal, halfDirection));
    LdotH = saturate(dot(lightDirection, halfDirection));
    directSpecular += max(float3(0,0,0),DirectSpecularLightingWithOcclusion(specColor, NdotH, LdotH, NdotL, roughness, directOcclusion));
#endif
    return directSpecular;
}


#endif
