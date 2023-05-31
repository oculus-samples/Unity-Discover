#ifndef AVATAR_EYE_INTERPOLATORS_HEAD_C_CGINC
#define AVATAR_EYE_INTERPOLATORS_HEAD_C_CGINC

#include "AvatarEyePropertiesHeadC.cginc"

///////////////////////////////////////
// Head C Eye Specific Interpolators //
///////////////////////////////////////

float cubic_interpolation_3point(float a, float b, float c, float t) {
    float result =  c * (2.0 * t * t - t) + b * (-4.0 * t * t + 4.0 * t) + a * (2.0 * t * t - 3.0 * t + 1.0);
    return result;
}

float2 GetNormalizedUVForEye(float2 origNormalizedUVs, float xMin, float xMid, float xMax, float yMin, float yMid, float yMax, float tX, float tY) {
    float2 center = 0.5;

    float scale =  1.0 / max(0.000001, _EyeUVScale);
    float2 newUVs = (origNormalizedUVs - center) * scale + center;

    // do a cubic interpolation between min and max through the mid point
    // (This code pulled from maya shader)
    float remapped_tX = cubic_interpolation_3point(xMin, xMid, xMax, (tX + 1.0) * 0.5);
    float remapped_tY = cubic_interpolation_3point(yMin, yMid, yMax, (tY + 1.0) * 0.5);

    newUVs.x = newUVs.x + remapped_tX;
    newUVs.y = newUVs.y + remapped_tY;

    // ASSUMPTION/HARD CODE: Iris in texture is 0.175 radius in the texture, apply iris scale

    // Iris scale of 1.0 means the iris goes to the "edge" of the eye
    // and iris scale of 0 means no iris.

    // Since the iris in the texture is centered at 0.5 and has 0.175 radius, an
    // iris scale of 0.35 means that the underlying UVs should not change.
    // Iris scale of 1.0 means UVs instead of being from 0 -> 1, will be from 0 -> 0.35

    // NOTE: To work around a bug in the Maya shader (so that artists don't have to re-export art),
    // this shader is also implementing the same behavior. Behavior is that _IrisScale of 0.5 means
    // "the whole eye" (instead of 1.0)
    scale = 0.35 / max(0.000001, _IrisScale * 2.0); // 2.0 here due to replicating Maya shader bug
    newUVs = (newUVs - center) * scale + center;

    // Explicitly do a a clamp here in case of atlassed textures, textured mode of clamp
    // will be insufficient
    return saturate(newUVs);
}

float2 GetNormalizedUVForLeftEye(float2 origNormalizedUVs) {
    return GetNormalizedUVForEye(
        origNormalizedUVs,
        _EyeXMin,
        _EyeXMid,
        _EyeXMax,
        _EyeYMin,
        _EyeYMid,
        _EyeYMax,
        _LeftEyeTX,
        _LeftEyeTY);
}

float2 GetNormalizedUVForRightEye(float2 origNormalizedUVs) {
    return GetNormalizedUVForEye(
        origNormalizedUVs,
        _EyeXMin * -1.0,
        _EyeXMid * -1.0,
        _EyeXMax * -1.0,
        _EyeYMin,
        _EyeYMid,
        _EyeYMax,
        _RightEyeTX * -1.0,
        _RightEyeTY);
}

#endif
