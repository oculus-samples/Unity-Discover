#ifndef AVATAR_HAIR_ALBEDO_CGINC
#define AVATAR_HAIR_ALBEDO_CGINC

half3 HairAlbedo(half4 mainTex, half3 color, half3 secondaryColor) {
  return lerp(color.rgb, secondaryColor.rgb, mainTex.r);
}

#endif
