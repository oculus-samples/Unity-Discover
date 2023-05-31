Shader "Avatar/Horizon"
{
    Properties
    {
      [NoScaleOffset]
      [HideIfKeyword(_SHADER_TYPE_SOLID_COLOR, _SHADER_TYPE_HAIR)] // Hide main tex if solid color or hair
      _MainTex ("Main Texture", 2D) = "white" {}

      _Color ("Color", Color) = (1, 1, 1, 1)

      [ShowIfKeyword(_SHADER_TYPE_HAIR, _SHADER_TYPE_SKIN)]
      _SecondaryColor ("Secondary Color", Color) = (1, 1, 1, 1)

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _TertiaryColor ("Tertiary Color", Color) = (1, 1, 1, 0)

      [NoScaleOffset]
      _PropertiesMap("Properties Map", 2D) = "white" {}

      [NoScaleOffset]
      [ShowIfKeyword(_SHADER_TYPE_SKIN, _SHADER_TYPE_HAIR)] // Skin and hair have effects map
      _EffectsMap("Effects Map", 2D) = "black" {}

      _EyeGlintFactor("Eye Glint Factor", Range(1, 20)) = 10.0    // Amplifys the size of the spec highlight known as the eye glint

      // The following three values diverge from the PBR standard and are mostly decide upon by art direction.
      // They are valid within the range [0.0.1.0], but if they are never changed from these defaults, it's best to optimize them out for production.
      _MinDiffuse("Minimum Diffuse Bias", Range(0, 1)) = 0.5    // Note: if we don't ever change this value from 0.5 at production time we should remove it
      _AmbientOcclusionEffect("Occlusion effect on Ambient Light", Range(0, 1)) = 1.0    // Note: if we don't ever change this value from 1.0 at production time we should remove it
      _DirectOcclusionEffect("Occlusion effect on Direct Light", Range(0, 1)) = 1.0    // Note: if we don't ever change this value from 1.0 at production time we should remove it

      // Hair properties

      // Eye Properties

      // TODO* Once Head C is on permanently, remove some properties
      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, false, USE_HEAD_C, true)]
      _LeftEyeUp("Left Eye Up", Float) = 0

      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, false, USE_HEAD_C, true)]
      _LeftEyeRight("Left Eye Right", Float) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, false, USE_HEAD_C, true)]
      _RightEyeUp("Right Eye Up", Float) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, false, USE_HEAD_C, true)]
      _RightEyeRight("Right Eye Right", Float) = 0

      // (A || B) && !C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, false)]
      _UVScale("UV Scale", Range(0, 10)) = 1

      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, USE_HEAD_C, false)]
      _LeftEyeTX("Left Eye X Interpolation", Range(-1, 1)) = 0

      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, USE_HEAD_C, false)]
      _LeftEyeTY("Left Eye Y Interpolation", Range(-1, 1)) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, USE_HEAD_C, false)]
      _RightEyeTX("Right Eye X Interpolation", Range(-1, 1)) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, USE_HEAD_C, false)]
      _RightEyeTY("Right Eye Y Interpolation", Range(-1, 1)) = 0

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeXMin("Eye Min X", Float) = 0

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeXMid("Eye Mid X", Float) = 0.5

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeXMax("Eye Max X", Float) = 1

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeYMin("Eye Min Y", Float) = 0

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeYMid("Eye Mid Y", Float) = 0.5

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeYMax("Eye Max Y", Float) = 1

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _PupilScale("Pupil Scale", Range(0, 1)) = 0.5

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _IrisScale("Iris Scale", Range(0, 1)) = 0.5

      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeUVScale("Eye Scale", Range(0, 3)) = 0.5

      // Skin Properties
      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _Distortion("Translucency Distortion", Range(0, 1)) = 0.5

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _TranslucencyPower("Translucency Power", Range(0, 1)) = 1.0

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _TranslucencyScale("Translucency Scale", Range(0, 2)) = 1.0

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _BacklightScale("Backlight Scale", Range(0, 1)) = 0.2

      [Header (Desaturation Mode Properties)]
      _DesatAmount("AFK Desaturate", Range(0,1)) = 0
      _DesatTint("Desaturation Tint", Color) = (0.255, 0.314, 0.502, 1)
      _DesatLerp("Desaturation Fade", Range(0, 1)) = 0

      // DEBUG_MODES: Uncomment to use Debug modes
      // [KeywordEnum(None, Diffuse, Specular, Indirect_Diffuse, Indirect_Specular, Backlight, Translucency, Vertex Color, UVs, World Normal, World Position, SH)] _Render_Debug("Debug Render", Float) = 0

      [Header (Lighting Systems)]
      [KeywordEnum(Unity, Vertex GI)] _Lighting_System("Lighting System", Float) = 0

      [Header (Shader Type)]
      [KeywordEnum(Solid Color, Textured, Skin, Hair, Left Eye, Right Eye, SubMesh)] _Shader_Type("Shader Type", Float) = 0

      [Header (Material Mode)]
      [KeywordEnum(Texture, Vertex)] Material_Mode("Material Mode", Float) = 0

      // Will set "USE_HEAD_C" shader keyword when set.
      [Toggle(USE_HEAD_C)] _HeadC ("Use Head C Eye Props?", Float) = 0
    }

    SubShader
    {
        Tags{
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            CGPROGRAM

            #pragma target 3.5 // necessary for use of SV_VertexID
            #pragma vertex vertForwardBase
            #pragma fragment fragForwardBase

            #pragma multi_compile __ LIGHTMAP_ON
            #pragma multi_compile __ LIGHTPROBE_SH
            #pragma multi_compile __ DIRECTIONAL_LIGHT
            #pragma multi_compile __ SHADOWMAP_STATIC_VSM
            #pragma multi_compile MATERIAL_MODE_TEXTURE MATERIAL_MODE_VERTEX

            #pragma multi_compile __ DESAT
            #pragma multi_compile __ DEBUG_TINT

            #pragma shader_feature __ USE_HEAD_C

            // DEBUG_MODES: Uncomment to use Debug modes
            // #pragma multi_compile __ _RENDER_DEBUG_DIFFUSE _RENDER_DEBUG_SPECULAR _RENDER_DEBUG_INDIRECT_DIFFUSE _RENDER_DEBUG_INDIRECT_SPECULAR _RENDER_DEBUG_BACKLIGHT  _RENDER_DEBUG_TRANSLUCENCY _RENDER_DEBUG_VERTEX_COLOR _RENDER_DEBUG_UVS _RENDER_DEBUG_WORLD_NORMAL _RENDER_DEBUG_WORLD_POSITION _RENDER_DEBUG_SH

            #pragma shader_feature _LIGHTING_SYSTEM_UNITY _LIGHTING_SYSTEM_VERTEX_GI
            #pragma shader_feature _SHADER_TYPE_SOLID_COLOR _SHADER_TYPE_TEXTURED _SHADER_TYPE_SKIN _SHADER_TYPE_HAIR _SHADER_TYPE_LEFT_EYE _SHADER_TYPE_RIGHT_EYE _SHADER_TYPE_SUBMESH

            #include "Avatar-Horizon.cginc"

            ENDCG
        }

        Pass
        {
            Name "MotionVectors"
            Tags{ "LightMode" = "MotionVectors"}
            Tags { "RenderType" = "Opaque" }

            HLSLPROGRAM

            #pragma target 3.5 // necessary for use of SV_VertexID
            #pragma vertex OvrMotionVectorsVertProgram
            #pragma fragment OvrMotionVectorsFragProgram

            #include "../../../../Scripts/ShaderUtils/OvrAvatarMotionVectorsCore.hlsl"

            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM

            #pragma target 3.5 // necessary for use of SV_VertexID
            #pragma vertex vertForwardBase
            #pragma fragment fragForwardBase

            #pragma multi_compile __ LIGHTMAP_ON
            #pragma multi_compile __ LIGHTPROBE_SH
            #pragma multi_compile __ DIRECTIONAL_LIGHT
            #pragma multi_compile __ SHADOWMAP_STATIC_VSM
            #pragma multi_compile MATERIAL_MODE_TEXTURE MATERIAL_MODE_VERTEX

            #pragma multi_compile __ DESAT
            #pragma multi_compile __ DEBUG_TINT

            #pragma shader_feature __ USE_HEAD_C

            // DEBUG_MODES: Uncomment to use Debug modes
            // #pragma multi_compile __ _RENDER_DEBUG_DIFFUSE _RENDER_DEBUG_SPECULAR _RENDER_DEBUG_INDIRECT_DIFFUSE _RENDER_DEBUG_INDIRECT_SPECULAR _RENDER_DEBUG_BACKLIGHT  _RENDER_DEBUG_TRANSLUCENCY _RENDER_DEBUG_VERTEX_COLOR _RENDER_DEBUG_UVS _RENDER_DEBUG_WORLD_NORMAL _RENDER_DEBUG_WORLD_POSITION _RENDER_DEBUG_SH

            #pragma shader_feature _LIGHTING_SYSTEM_UNITY _LIGHTING_SYSTEM_VERTEX_GI
            #pragma shader_feature _SHADER_TYPE_SOLID_COLOR _SHADER_TYPE_TEXTURED _SHADER_TYPE_SKIN _SHADER_TYPE_HAIR _SHADER_TYPE_LEFT_EYE _SHADER_TYPE_RIGHT_EYE _SHADER_TYPE_SUBMESH

            #include "Avatar-Horizon.cginc"

            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }

            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma target 3.5 // necessary for use of SV_VertexID
            #pragma vertex vertForwardAdd
            #pragma fragment fragForwardAdd

            #pragma multi_compile __ LIGHTMAP_ON
            #pragma multi_compile __ LIGHTPROBE_SH
            #pragma multi_compile __ DIRECTIONAL_LIGHT
            #pragma multi_compile __ SHADOWMAP_STATIC_VSM
            #pragma multi_compile MATERIAL_MODE_TEXTURE MATERIAL_MODE_VERTEX

            #pragma multi_compile __ DESAT
            #pragma multi_compile __ DEBUG_TINT

            #pragma shader_feature __ USE_HEAD_C

            // DEBUG_MODES: Uncomment to use Debug modes
            // #pragma multi_compile __ _RENDER_DEBUG_DIFFUSE _RENDER_DEBUG_SPECULAR _RENDER_DEBUG_INDIRECT_DIFFUSE _RENDER_DEBUG_INDIRECT_SPECULAR _RENDER_DEBUG_BACKLIGHT  _RENDER_DEBUG_TRANSLUCENCY _RENDER_DEBUG_VERTEX_COLOR _RENDER_DEBUG_UVS _RENDER_DEBUG_WORLD_NORMAL _RENDER_DEBUG_WORLD_POSITION _RENDER_DEBUG_SH

            #pragma shader_feature _LIGHTING_SYSTEM_UNITY _LIGHTING_SYSTEM_VERTEX_GI
            #pragma shader_feature _SHADER_TYPE_SOLID_COLOR _SHADER_TYPE_TEXTURED _SHADER_TYPE_SKIN _SHADER_TYPE_HAIR _SHADER_TYPE_LEFT_EYE _SHADER_TYPE_RIGHT_EYE _SHADER_TYPE_SUBMESH

            #include "Avatar-Horizon.cginc"

            ENDCG
        }
    }

}
