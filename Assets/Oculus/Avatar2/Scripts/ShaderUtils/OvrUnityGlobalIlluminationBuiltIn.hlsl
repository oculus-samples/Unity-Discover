#ifndef OVR_UNITY_GLOBAL_ILLUMINATION_BUILT_IN_INCLUDED
#define OVR_UNITY_GLOBAL_ILLUMINATION_BUILT_IN_INCLUDED

#include "OvrGlobalIlluminationTypes.hlsl"

#include "UnityStandardCore.cginc"
#include "UnityLightingCommon.cginc"

inline OvrGlobalIllumination ConvertUnityGIToOvrGI(in UnityGI unityGI) {
  OvrGlobalIllumination ovr_gi;

  ovr_gi.light.color = unityGI.light.color;
  ovr_gi.light.direction = unityGI.light.dir;

  // Attentuation(s) already applied to color when using built-in pipeline GI
  ovr_gi.light.distanceAttenuation = 1.0;
  ovr_gi.light.shadowAttenuation = 1.0;

  ovr_gi.indirectDiffuse = unityGI.indirect.diffuse;
  ovr_gi.indirectSpecular = unityGI.indirect.specular;

  return ovr_gi;
}

inline OvrGlobalIllumination OvrGetUnityFragmentGI(
    float3 posWorld,
    half occlusion,
    half4 i_ambientOrLightmapUV,
    half smoothness,
    half3 normalWorld,
    half3 eyeVec,
    OvrLight light,
    bool reflections)
{
  UnityLight unity_light;
  unity_light.color = light.color;
  unity_light.dir = light.direction;
  unity_light.ndotl = 1.0; // ndotl is deprecated

  const UnityGI unity_gi = FragmentGI(
    posWorld,
    occlusion,
    i_ambientOrLightmapUV,
    light.distanceAttenuation * light.shadowAttenuation,
    smoothness,
    normalWorld,
    eyeVec,
    unity_light,
    reflections);

  return ConvertUnityGIToOvrGI(unity_gi);
}

inline OvrGlobalIllumination OvrGetUnityFragmentGI (
    float3 posWorld,
    half occlusion,
    half4 i_ambientOrLightmapUV,
    half smoothness,
    half3 normalWorld,
    half3 eyeVec,
    OvrLight light)
{
  return OvrGetUnityFragmentGI(posWorld, occlusion, i_ambientOrLightmapUV, smoothness, normalWorld, eyeVec, light, true);
}

inline void OvrGetUnityDiffuseGlobalIllumination(half3 lightColor, half3 lightDirection, float3 worldPos, float3 worldViewDir,
    half attenuation, half3 ambient, half smoothness, half metallic, half occlusion,
    half3 albedo, half3 normal, half3 specular_contribution, out float3 diffuse)
{
    OvrLight light;
    light.color = lightColor;
    light.direction = lightDirection;
    OvrGlobalIllumination ovr_gi = OvrGetUnityFragmentGI(worldPos, occlusion, half4(ambient, 1.0), smoothness, normal, -worldViewDir, light, false);
    diffuse = ovr_gi.indirectDiffuse;
}
inline void OvrGetUnityGlobalIllumination(half3 lightColor, half3 lightDirection, float3 worldPos, float3 worldViewDir,
    half attenuation, half3 ambient, half smoothness, half metallic, half occlusion,
    half3 albedo, half3 normal, half3 specular_contribution, out float3 diffuse, out float3 specular)
{
    OvrLight light;
    light.color = lightColor;
    light.direction = lightDirection;
    OvrGlobalIllumination ovr_gi = OvrGetUnityFragmentGI(worldPos, occlusion, half4(ambient, 1.0), smoothness, normal, -worldViewDir, light, true);
    diffuse = ovr_gi.indirectDiffuse;

    specular = ovr_gi.indirectSpecular;
}

#endif // OVR_UNITY_GLOBAL_ILLUMINATION_BUILT_IN_INCLUDED
