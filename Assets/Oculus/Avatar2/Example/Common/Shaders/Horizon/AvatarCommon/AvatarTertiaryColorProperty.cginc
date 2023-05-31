#ifndef AVATAR_TERTIARY_COLOR_PROPERTY_CGINC
#define AVATAR_TERTIARY_COLOR_PROPERTY_CGINC

#if defined(AVATAR_SHADER_TERTIARY_COLOR_ARRAY)
    half4 _TertiaryColorArray[16];

    half4 GetTertiaryColor(int index) {
        return _TertiaryColorArray[index];
    }
#else
    half4 _TertiaryColor;

    half4 GetTertiaryColor() {
        return _TertiaryColor;
    }
#endif

#endif
