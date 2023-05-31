#ifndef OVR_UNITY_LIGHTS_URP_INCLUDED
#define OVR_UNITY_LIGHTS_URP_INCLUDED

// Wrapping structs/API for getting a defined light struct out of Unity's "URP" light system

// ASSUMPTION: This path exists if using URP
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#include "OvrLightTypes.hlsl"

///////////////////////////////////////////////////////////////////////////////
//                      Light Abstraction                                    //
///////////////////////////////////////////////////////////////////////////////


// Convert from Unity struct to Ovr struct
OvrLight ConvertUnityLightToOvrLight(in Light light) {
  OvrLight ovrLight;
  ovrLight.color = light.color;
  ovrLight.direction = light.direction;
  ovrLight.distanceAttenuation = light.distanceAttenuation;
  ovrLight.shadowAttenuation = light.shadowAttenuation;

  return ovrLight;
}

OvrLight OvrGetUnityMainLight()
{
  // Convert from Unity struct to Ovr struct
  return ConvertUnityLightToOvrLight(GetMainLight());
}

OvrLight OvrGetUnityMainLight(float4 shadowCoord)
{
    return ConvertUnityLightToOvrLight(GetMainLight(shadowCoord));
}

OvrLight OvrGetUnityMainLight(float4 shadowCoord, float3 positionWS, half4 shadowMask)
{
  return ConvertUnityLightToOvrLight(GetMainLight(shadowCoord, positionWS, shadowMask));
}

// Fills a light struct given a perObjectLightIndex
OvrLight OvrGetAdditionalPerObjectLight(int perObjectLightIndex, float3 positionWS)
{
  return ConvertUnityLightToOvrLight(GetAdditionalPerObjectLight(perObjectLightIndex, positionWS));
}

// Fills a light struct given a loop i index. This will convert the i
// index to a perObjectLightIndex
OvrLight OvrGetAdditionalLight(uint i, float3 positionWS)
{
  const int perObjectLightIndex = GetPerObjectLightIndex(i);
  return ConvertUnityLightToOvrLight(GetAdditionalPerObjectLight(perObjectLightIndex, positionWS));
}

OvrLight OvrGetAdditionalLight(uint i, float3 positionWS, half4 shadowMask)
{
  return ConvertUnityLightToOvrLight(GetAdditionalLight(i, positionWS, shadowMask));
}

#endif // OVR_UNITY_LIGHTS_URP_INCLUDED
