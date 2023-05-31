#ifndef AVATAR_SHADER_TYPES_CGINC
#define AVATAR_SHADER_TYPES_CGINC

#include "UnityInstancing.cginc"
#include "UnityCG.cginc"

struct AvatarShaderLight {
    half3 direction;
    half3 color;
};

struct AvatarShaderIndirect {
    half3 diffuse;
    half3 specular;
};

struct AvatarShaderGlobalIllumination {
    AvatarShaderLight light;
    AvatarShaderIndirect indirect;
};

#if defined(_LIGHTING_SYSTEM_VERTEX_GI)

    #include "../AvatarVGI/AvatarVGITypes.cginc"

#elif defined(_LIGHTING_SYSTEM_UNITY)

    #include "../UnityLighting/AvatarUnityLightingTypes.cginc"

#elif defined(_LIGHTING_SYSTEM_UNLIT)

     #include "../Unlit/UnlitTypes.cginc"

#endif

#endif
