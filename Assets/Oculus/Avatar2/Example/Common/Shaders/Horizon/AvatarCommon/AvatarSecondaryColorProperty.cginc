#ifndef AVATAR_SECONDARY_COLOR_PROPERTY_CGINC
#define AVATAR_SECONDARY_COLOR_PROPERTY_CGINC

#if defined(AVATAR_SHADER_SECONDARY_COLOR_ARRAY)
    half3 _SecondaryColorArray[16];

    half3 GetSecondaryColor(int index) {
        return _SecondaryColorArray[index];
    }
#else
    half3 _SecondaryColor;

    half3 GetSecondaryColor() {
        return _SecondaryColor;
    }
#endif

#endif
