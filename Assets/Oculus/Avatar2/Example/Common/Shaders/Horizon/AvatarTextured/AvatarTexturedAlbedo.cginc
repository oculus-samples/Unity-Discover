#ifndef AVATAR_TEXTURED_ALBEDO_CGINC
#define AVATAR_TEXTURED_ALBEDO_CGINC

half3 TexturedAlbedo(half4 mainTex, half3 vertColor, half3 color) {
#ifdef MATERIAL_MODE_TEXTURE
  return mainTex.rgb * color;
#else
  return vertColor * color;
#endif
}

#endif
