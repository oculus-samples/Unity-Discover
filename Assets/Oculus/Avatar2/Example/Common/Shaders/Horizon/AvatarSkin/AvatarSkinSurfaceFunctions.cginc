#ifndef AVATAR_SKIN_SURFACE_FUNCTIONS_CGINC
#define AVATAR_SKIN_SURFACE_FUNCTIONS_CGINC

#if defined(_LIGHTING_SYSTEM_VERTEX_GI) || defined (_LIGHTING_SYSTEM_UNITY)

    // Utilities
    #define SURFACE_ADDITIONAL_FIELDS_SKIN \
        half Thickness; \
        half BacklightScale; \
        half3 TranslucencyColor; \
        half3 BacklightColor;

#endif


#endif
