#ifndef AVATAR_SHADER_FRAMEWORK_CGINC
#define AVATAR_SHADER_FRAMEWORK_CGINC

#include "AvatarShaderTypes.cginc"
#include "AvatarShaderMacros.cginc"

///////////////////////
// Generation Macros //
///////////////////////
#if defined(_LIGHTING_SYSTEM_UNITY)

    #include "../UnityLighting/AvatarUnityLighting.cginc"

    #define GENERATE_AVATAR_SHADER_VERT_FRAG(VertProgName, FragProgName, VertFunc, SurfFunc, SurfOutputType, LightingFunc) \
        GENERATE_AVATAR_UNITY_LIGHTING_VERTEX_PROGRAM(VertProgName, VertFunc) \
        GENERATE_AVATAR_UNITY_LIGHTING_FRAGMENT_PROGRAM(FragProgName, SurfFunc, SurfOutputType, LightingFunc)

#elif defined(_LIGHTING_SYSTEM_VERTEX_GI)

    #define DYNAMIC 1

    #include "../AvatarVGI/AvatarVGIFramework.cginc"

    #define GENERATE_AVATAR_SHADER_VERT_FRAG(VertProgName, FragProgName, VertFunc, SurfFunc, SurfOutputType, LightingFunc) \
        DEFINE_AVATAR_VGI_VERT(VertProgName, VertFunc) \
        DEFINE_AVATAR_VGI_FRAG_PBS(FragProgName, SurfFunc, SurfOutputType, LightingFunc)

#endif

#endif //end (ifndef AVATAR_SHADER_FRAMEWORK_CGINC)
