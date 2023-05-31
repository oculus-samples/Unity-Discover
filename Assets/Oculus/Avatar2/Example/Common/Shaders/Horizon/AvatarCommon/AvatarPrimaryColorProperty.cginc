#ifndef AVATAR_PRIMARY_COLOR_PROPERTY_CGINC
#define AVATAR_PRIMARY_COLOR_PROPERTY_CGINC

// A color parameter as individual color or array of colors

#if defined(AVATAR_SHADER_COLOR_ARRAY)
  half3 _ColorArray[16];

  half3 GetPrimaryColor(uint index) {
    return _ColorArray[index];
  }
#else
  #if !defined(_LIGHTING_SYSTEM_VERTEX_GI)
    half3 _Color;
  #endif

  half3 GetPrimaryColor() {
    return _Color;
  }
#endif

#endif
