#ifndef AVATAR_EYE_ALBEDO_CGINC
#define AVATAR_EYE_ALBEDO_CGINC

// Albedo function for eyes is the same as for textured component, so
// just import that file here and call the function
#include "../AvatarTextured/AvatarTexturedAlbedo.cginc"

half3 EyeAlbedo(half4 mainTex, half3 vertColor, half3 color) {
  return TexturedAlbedo(mainTex, vertColor, color);
}

#endif
