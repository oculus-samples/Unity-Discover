#ifndef AVATAR_EYE_ALBEDO_HEAD_C_CGINC
#define AVATAR_EYE_ALBEDO_HEAD_C_CGINC

// Albedo function for eyes is similar to the function for textured component, so
// just import that file here and call the function
#include "../AvatarTextured/AvatarTexturedAlbedo.cginc"

half3 EyeAlbedo(half4 mainTex, half3 color, float2 uv) {
  float2 center = 0.5;

  half3 texturedAlbedo = TexturedAlbedo(mainTex, color);

  // Force pupil to be black at pupil scale
  float2 diff = uv - center;
  float len = length(diff);

  // ASSUMPTION/HARD CODE: Iris in texture is 0.175 radius
  float pupilEdgeLen = 0.175 * _PupilScale;
  return texturedAlbedo * smoothstep(pupilEdgeLen, pupilEdgeLen + 0.01, len);
}

#endif
