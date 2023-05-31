#ifndef AVATAR_COMMON_UTILS_CGINC
#define AVATAR_COMMON_UTILS_CGINC

#define PI 3.14159265359

#include "UnityStandardBRDF.cginc"

#define MIN_PERCEPTUAL_ROUGHNESS 0.045

float powApprox(float a, float b) {
  return a / ((1.0f - b) * a + b);
}

float ClampPerceptualRoughness(float perceptualRoughness) {
    return clamp(perceptualRoughness, MIN_PERCEPTUAL_ROUGHNESS, 1.0);
}

#endif
