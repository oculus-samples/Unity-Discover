#ifndef AVATAR_EYE_INTERPOLATORS_CGINC
#define AVATAR_EYE_INTERPOLATORS_CGINC

#include "AvatarEyeProperties.cginc"

////////////////////////////////
// Eye Specific Interpolators //
////////////////////////////////

float2 GetNormalizedUVForEye(float2 origNormalizedUVs, float right, float up) {
    float2 center = float2(0.5f, 0.5f);
    float2 newUVs = (origNormalizedUVs - center) * _UVScale + center + float2(right, up);

    // Explicitly do a a clamp here in case of atlassed textures, textured mode of clamp
    // will be insufficient
    return saturate(newUVs);
}

float2 GetNormalizedUVForLeftEye(float2 origNormalizedUVs) {
    return GetNormalizedUVForEye(origNormalizedUVs, _LeftEyeRight, _LeftEyeUp);
}

float2 GetNormalizedUVForRightEye(float2 origNormalizedUVs) {
    return GetNormalizedUVForEye(origNormalizedUVs, _RightEyeRight, _RightEyeUp);
}

#endif
