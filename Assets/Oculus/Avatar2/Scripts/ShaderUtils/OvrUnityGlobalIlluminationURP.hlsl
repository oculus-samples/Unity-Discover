#ifndef OVR_UNITY_GLOBAL_ILLUMINATION_URP_INCLUDED
#define OVR_UNITY_GLOBAL_ILLUMINATION_URP_INCLUDED

#include "OvrLightTypes.hlsl"
#include "OvrGlobalIlluminationTypes.hlsl"

// ASSUMPTION: This path exists if using URP
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

inline OvrGlobalIllumination OvrGetUnityGlobalIllumination(
  OvrLight light,
  BRDFData brdfData,
  BRDFData brdfDataClearCoat,
  float clearCoatMask,
  half3 bakedGI,
  half occlusion,
  half3 normalWS,
  half3 viewDirectionWS,
  bool reflections = true)
{
  // Pulled this straight from URP 10.1 Lighting.hlsl (with minor modifications to keep
  // specular and diffuse separate)
  half3 reflectVector = reflect(-viewDirectionWS, normalWS);
  half NoV = saturate(dot(normalWS, viewDirectionWS));
  half fresnelTerm = Pow4(1.0 - NoV);

  half3 indirectDiffuse = bakedGI * occlusion;
  half3 indirectSpecular = half3(0.0,0.0,0.0);
  if(reflections) {
    indirectSpecular = GlossyEnvironmentReflection(reflectVector, brdfData.perceptualRoughness, occlusion);
    indirectSpecular += indirectSpecular * EnvironmentBRDFSpecular(brdfData, fresnelTerm);
  }
  // Avatar SDK modifications
  indirectDiffuse = indirectDiffuse * brdfData.diffuse;
  // half3 color = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
  // END Avatar SDK modifications

  #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half3 coatIndirectSpecular = GlossyEnvironmentReflection(reflectVector, brdfDataClearCoat.perceptualRoughness, occlusion);
    // TODO: "grazing term" causes problems on full roughness
    half3 coatColor = EnvironmentBRDFClearCoat(brdfDataClearCoat, clearCoatMask, coatIndirectSpecular, fresnelTerm);

    // Blend with base layer using khronos glTF recommended way using NoV
    // Smooth surface & "ambiguous" lighting
    // NOTE: fresnelTerm (above) is pow4 instead of pow5, but should be ok as blend weight.
    half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * fresnelTerm;

    // Avatar SDK modifications
    // return color * (1.0 - coatFresnel * clearCoatMask) + coatColor;
    // In the above, color = indirectDiffuse + indirectSpecular....therefore both terms should
    // get multiplied. Then the specular will add the coatColor (somewhat arbitrary decision)
    half coatFactor = (1.0 - coatFresnel * clearCoatMask);
    indirectDiffuse *= coatFactor;
    if(reflections) {
      indirectSpecular = coatFactor * indirectSpecular + coatColor;
    }
    // END Avatar SDK modifications
  #endif

  OvrGlobalIllumination gi;
  gi.light = light;
  gi.indirectDiffuse = indirectDiffuse;
  gi.indirectSpecular = indirectSpecular;

  return gi;
}

inline OvrGlobalIllumination OvrGetUnityGlobalIllumination(
  OvrLight light,
  BRDFData brdfData,
  half3 bakedGI,
  half occlusion,
  half3 normalWS,
  half3 viewDirectionWS,
  bool reflections = true)
{
  const BRDFData noClearCoat = (BRDFData)0;
  return OvrGetUnityGlobalIllumination(
    light,
    brdfData,
    noClearCoat,
    0.0,
    bakedGI,
    occlusion,
    normalWS,
    viewDirectionWS,
    reflections);
}

inline void OvrGetUnityDiffuseGlobalIllumination(half3 lightColor, half3 lightDirection, float3 worldPos, float3 worldViewDir,
    half attenuation, half3 ambient, half smoothness, half metallic, half occlusion,
    half3 albedo, half3 normal, half3 specular_contribution, out float3 diffuse)
{
      OvrLight light;
      light.color = lightColor;
      light.direction = lightDirection;
      BRDFData brdfData;
      half3 spec = specular_contribution;
      half alpha = 1.0;
      InitializeBRDFData(albedo, metallic, spec, smoothness, alpha, brdfData);
      half3 ambient_contrib = 0.0;
      ambient_contrib += SHEvalLinearL0L1(normal,  unity_SHAr, unity_SHAg, unity_SHAb);
      ambient_contrib += SHEvalLinearL2(normal, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
      half3 bakedGI = ambient_contrib;

      OvrGlobalIllumination gi = OvrGetUnityGlobalIllumination(light, brdfData, bakedGI, occlusion, normal, worldViewDir, false);
      diffuse = gi.indirectDiffuse.rgb;
}

inline void OvrGetUnityGlobalIllumination(half3 lightColor, half3 lightDirection, float3 worldPos, float3 worldViewDir,
half attenuation, half3 ambient, half smoothness, half metallic, half occlusion,
half3 albedo, half3 normal, half3 specular_contribution, out float3 diffuse, out float3 specular)
    {
      OvrLight light;
      light.color = lightColor;
      light.direction = lightDirection;
      BRDFData brdfData;
      half3 spec = specular_contribution;
      half alpha = 1.0;
      InitializeBRDFData(albedo, metallic, spec, smoothness, alpha, brdfData);
      half3 ambient_contrib = 0.0;
      ambient_contrib += SHEvalLinearL0L1(normal,  unity_SHAr, unity_SHAg, unity_SHAb);
      ambient_contrib += SHEvalLinearL2(normal, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
      half3 bakedGI = ambient_contrib;

      OvrGlobalIllumination gi = OvrGetUnityGlobalIllumination(light, brdfData, bakedGI, occlusion, normal, worldViewDir, true);
      diffuse = gi.indirectDiffuse.rgb;
      specular = gi.indirectSpecular.rgb;

}

#endif // OVR_UNITY_GLOBAL_ILLUMINATION_URP_INCLUDED
