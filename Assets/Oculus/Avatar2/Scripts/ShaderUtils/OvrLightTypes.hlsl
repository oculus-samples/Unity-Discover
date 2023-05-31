#ifndef OVR_LIGHT_TYPES_INCLUDED
#define OVR_LIGHT_TYPES_INCLUDED

struct OvrLight {
  half3 direction;
  half3 color;

  half distanceAttenuation;
  half shadowAttenuation;
};

#endif // OVR_LIGHT_TYPES_INCLUDED
