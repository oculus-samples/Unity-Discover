#ifndef OVR_GLOBAL_ILLUMINATION_TYPES_INCLUDED
#define OVR_GLOBAL_ILLUMINATION_TYPES_INCLUDED

#include "OvrLightTypes.hlsl"

struct OvrGlobalIllumination {
  OvrLight light;

  half3 indirectDiffuse;
  half3 indirectSpecular;
};

#endif // OVR_GLOBAL_ILLUMINATION_TYPES_INCLUDED
