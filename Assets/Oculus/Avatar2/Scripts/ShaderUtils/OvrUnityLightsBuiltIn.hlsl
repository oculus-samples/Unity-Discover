#ifndef OVR_UNITY_LIGHTS_BUILT_IN_INCLUDED
#define OVR_UNITY_LIGHTS_BUILT_IN_INCLUDED

#include "OvrLightTypes.hlsl"

// Wrapping structs/API for getting a defined light struct out of Unity's "built in" light system

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "UnityLightingCommon.cginc"

half3 OvrGetUnityLightDirection(half3 worldPos) {
  return normalize(_WorldSpaceLightPos0.xyz - worldPos);
}

half3 OvrGetUnityDirectionalLightDirection() {
  return _WorldSpaceLightPos0.xyz;
}

#if defined(POINT) || defined(SPOT) || defined(POINT_COOKIE)
#  define OVR_GET_UNITY_LIGHT_DIRECTION(worldPos) OvrGetUnityLightDirection(worldPos);
#else
#  define OVR_GET_UNITY_LIGHT_DIRECTION(worldPos) OvrGetUnityDirectionalLightDirection();
#endif

// Forward Rendering
#define OVR_GET_FRAGMENT_UNITY_LIGHT(lightName, vertexInput, worldPos) \
  OvrLight lightName; \
\
  lightName.color = _LightColor0; \
\
  lightName.direction = OVR_GET_UNITY_LIGHT_DIRECTION(worldPos); \
\
  /* For built-in pipeline, cannot separate shadow and distance attenuation. */ \
  UNITY_LIGHT_ATTENUATION(attenuation, vertexInput, worldPos) \
  lightName.distanceAttenuation = attenuation; \
  lightName.shadowAttenuation = 1.0;

#endif // OVR_UNITY_LIGHTS_BUILT_IN_INCLUDED
