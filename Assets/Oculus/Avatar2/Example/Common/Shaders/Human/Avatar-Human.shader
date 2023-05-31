// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// based off metallic_roughness.frag/vert from Khronos

Shader "Avatar/Human"
{
    Properties
    {
        // NOTE: This texture can be visualized in the Unity editor, just expand in inspector and manually change "Dimension" to "2D" on top line
        u_AttributeTexture("Vertex Attribute map", 3D) = "white" {}

        u_NormalSampler("Normal map", 2D) = "white" {}
        u_NormalScale("Normal map scale", Float) = 1.0
        u_NormalUVSet("Normal UV Set", Int) = 0

        u_EmissiveSampler("Emissive map", 2D) = "black" {}
        u_EmissiveSet("Emissive UV Set", Int) = 0
        u_EmissiveFactor("Emissive factor", Color) = (1, 1, 1, 1)

        u_OcclusionSampler("Occlusion map", 2D) = "white" {}
        u_OcclusionSet("Occlusion UV Set", Int) = 0
        u_OcclusionStrength("Occlusion scale", Float) = 1.0

        u_BaseColorSampler("Base Color", 2D) = "white" {}
        u_BaseColorUVSet("Base Color UV Set", Int) = 0

        u_BaseColorFactor("Base Color factor", Color) = (1, 1, 1, 1)

        u_MetallicRoughnessSampler("Metallic Roughness", 2D) = "white" {}
        u_MetallicRoughnessUVSet("Metallic Roughness UV Set", Int) = 0

        u_MetallicFactor("Metallic Factor", Range(0, 2)) = 1.0
        u_RoughnessFactor("Roughness Factor", Range(0, 2)) = 1.0
        u_F0Factor("F0 Factor", Range(0, 2)) = 1.0
        u_OcclusionStrength("Occlusion Strength", Range(0, 2)) = 1.0
        u_ThicknessFactor("Thickness Factor", Range(0, 2)) = 1.0

        u_SubsurfaceColor("Sub-Surface Color", Color) = (0, 0, 0, 1)
        u_SkinORMFactor("Skin-only ORM Factor", Vector) = (1, 1, 1)

// AVATAR SDK BEGIN
            u_EyeGlintFactor("Eye Glint Factor", Range(0, 2.0)) = 1.0
            u_EyeGlintColorFactor("Eye Glint Color Factor", Range(0, 2.0)) = 1.0

            u_DiffuseSmoothingFactor("Diffuse Smoothing Factor", Range(0, 8)) = 1.0
        // this factor allows for smooth diffuse light falloff similar to Babylon.js.
        // It is not originally in the Khronos shader.
// AVATAR SDK END


        [ShowIfKeyword(_PALETTIZATION_SINGLE_RAMP, _PALETTIZATION_TWO_RAMP)]
        _ColorRamp0("Color Ramp 1", 2D) = "white" {}
        [ShowIfKeyword(_PALETTIZATION_TWO_RAMP)]
        _ColorRamp1("Color Ramp 2", 2D) = "white" {}


        // These should not exist here, since they should be in global shader scope and handeled by an external manager:
        //
        //u_DiffuseEnvSampler("IBL Diffuse Cubemap Texture", Cube) = "white" {}
        //u_MipCount("IBL Diffuse Texture Mip Count", Int) = 10
        //u_SpecularEnvSampler ("IBL Specular Cubemap Texture", Cube) = "white" {}
        //u_brdfLUT ("BRDF LUT Texture", 2D) = "Assets/Oculus/Avatar2/Example/Scenes/BRDF_LUT" {}

        // DEBUG_MODES: Uncomment to use Debug modes, must match the multi_compile defined below
        // [KeywordEnum(None, BaseColor, Occlusion, Roughness, Metallic, Thickness, Normal, Normal Map, Emissive, View, Punctual, Punctual Specular, Punctual Diffuse, IBL, IBL Specular, IBL Diffuse, SH, No Tone Map, SubSurface Scattering)] Debug("Debug Render", Float) = 0

        // LIGHTING_MODES: Uncomment to use Lighting modes, must match the multi_compile defined below
        // [KeywordEnum(IBL plus Punctual, SH plus Punctual, IBL Only, SH Only, Punctual Only)] Lighting_Mode("Lighting Mode", Float) = 0

        // TONEMAP_MODES: Uncomment to use ToneMapping modes, must match the multi_compile defined below
        // [KeywordEnum(None, Uncharted, HejlRichard, ACES)] ToneMap("Tone Mapping", Float) = 0

        // BRDFLUT_MODES: Uncomment to use BRDF Look up Table (LUT) modes, must match the multi_compile defined below
        [KeywordEnum(On, Off)] BRDF_LUT_Mode("BRDF LUT Mode", Float) = 0
          
        // MATERIAL_MODES: Uncomment to use Material modes, must match the multi_compile defined below
        [KeywordEnum(Texture, Vertex)] Material_Mode("Material Mode", Float) = 0

        // Cull mode (Off, Front, Back)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Cull[_Cull]

        // Old Unity Render Pipeline, Single Light
        Pass
        {
            // Commenting out this lightmode because it breaks URP compilation.
            // Confirmed that this won't break non-URP projects.
            // Tags{"LightMode" = "ForwardBase"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile DIRECTIONAL POINT SPOT

            /////////////////////////////////////////////////////////
            // PRAGMAS: Pragmas cannot exist in cginc files so include them here

            // DEBUG_MODES: Must match the Properties specified above
            // #pragma multi_compile __ DEBUG_BASECOLOR DEBUG_OCCLUSION DEBUG_ROUGHNESS DEBUG_METALLIC DEBUG_THICKNESS DEBUG_NORMAL DEBUG_NORMAL_MAP DEBUG_EMISSIVE DEBUG_VIEW DEBUG_PUNCTUAL DEBUG_PUNCTUAL_SPECULAR DEBUG_PUNCTUAL_DIFFUSE DEBUG_IBL DEBUG_IBL_SPECULAR DEBUG_IBL_DIFFUSE DEBUG_SH DEBUG_NO_TONE_MAP DEBUG_SUBSURFACE_SCATTERING
            #define DEBUG_LIGHTING (defined(DEBUG_METALLIC) || defined(DEBUG_THICKNESS) || defined(DEBUG_ROUGHNESS) || defined(DEBUG_NORMAL) || defined(DEBUG_NORMAL_MAP) || defined(DEBUG_BASECOLOR) || defined(DEBUG_OCCLUSION) || defined(DEBUG_EMISSIVE) || defined(DEBUG_F0) || defined(DEBUG_ALPHA) || defined(DEBUG_VIEW) || defined(DEBUG_PUNCTUAL) || defined(DEBUG_PUNCTUAL_SPECULAR) || defined(DEBUG_PUNCTUAL_DIFFUSE) || defined(DEBUG_IBL) || defined(DEBUG_IBL_SPECULAR) || defined(DEBUG_IBL_DIFFUSE) || defined(DEBUG_SH) || defined(DEBUG_NO_TONE_MAP) || defined(DEBUG_SUBSURFACE_SCATTERING))

            // LIGHTING_MODES: this is done to match the options of the commercial GLTF viewers
            // #pragma multi_compile LIGHTING_MODE_IBL_PLUS_PUNCTUAL LIGHTING_MODE_SH_PLUS_PUNCTUAL LIGHTING_MODE_IBL_ONLY LIGHTING_MODE_SH_ONLY LIGHTING_MODE_PUNCTUAL_ONLY
            #define LIGHTING_MODE_IBL_PLUS_PUNCTUAL

            // TONEMAP_MODES: Must match the Properties specified above
            // #pragma multi_compile __ TONEMAP_UNCHARTED TONEMAP_HEJLRICHARD TONEMAP_ACES

            // BRDF_LUT_MODES: Must match the Properties specified above
            #pragma multi_compile BRDF_LUT_MODE_ON BRDF_LUT_MODE_OFF
      
            // MATERIAL_MODES: Must match the Properties specified above
            #pragma multi_compile MATERIAL_MODE_TEXTURE MATERIAL_MODE_VERTEX

            #pragma target 4.0 // necessary for use of SV_VertexID

            // Palettization modes for Avatar FBX tool
            #pragma multi_compile __ _PALETTIZATION_SINGLE_RAMP _PALETTIZATION_TWO_RAMP

            // In Avatar SDK we ALWAYS use the ORM property map extension. Indicate that here:
            #define USE_ORM_EXTENSION

            // Turn on IBL only here, not for the other additive passes
            #if defined(LIGHTING_MODE_IBL_PLUS_PUNCTUAL) || defined(LIGHTING_MODE_IBL_ONLY)
            #define USE_IBL // IBL only gets applied here in the first base pass
            #define USE_IBL_DIFFUSE
            #define USE_IBL_SPECULAR
            #endif
            #if defined(LIGHTING_MODE_SH_PLUS_PUNCTUAL) || defined(LIGHTING_MODE_SH_ONLY)
            // per vertex is faster than per pixel, and almost indistinguishable for our purpose
            #define USE_SH_PER_VERTEX
            // #define USE_SH_PER_PIXEL
            #endif

            // Include the Vertex Shader HERE
            #include "precision.hlsl"
            #include "common-vert.hlsl"

            // Include the Pixel Shader HERE
            #include "precision.hlsl"
            #include "fb_sphere_map.hlsl"
            #include "UnityLightingCommon.cginc" // for _LightColor0, replace with hlsl when we can
            #include "common-frag.hlsl"

            ENDCG
        }

        // Old Unity Render Pipeline, Up to 4 Additive Lights
        Pass
        {
            Tags{"LightMode" = "ForwardAdd"}

            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile DIRECTIONAL POINT SPOT

            /////////////////////////////////////////////////////////
            // PRAGMAS: Pragmas cannot exist in cginc files so include them here

            // DEBUG_MODES: Must match the Properties specified above
            // #pragma multi_compile __ DEBUG_BASECOLOR DEBUG_OCCLUSION DEBUG_ROUGHNESS DEBUG_METALLIC DEBUG_THICKNESS DEBUG_NORMAL DEBUG_NORMAL_MAP DEBUG_EMISSIVE DEBUG_VIEW DEBUG_PUNCTUAL DEBUG_PUNCTUAL_SPECULAR DEBUG_PUNCTUAL_DIFFUSE DEBUG_IBL DEBUG_IBL_SPECULAR DEBUG_IBL_DIFFUSE DEBUG_SH DEBUG_NO_TONE_MAP DEBUG_SUBSURFACE_SCATTERING
            #define DEBUG_LIGHTING (defined(DEBUG_METALLIC) || defined(DEBUG_THICKNESS) || defined(DEBUG_ROUGHNESS) || defined(DEBUG_NORMAL) || defined(DEBUG_NORMAL_MAP) || defined(DEBUG_BASECOLOR) || defined(DEBUG_OCCLUSION) || defined(DEBUG_EMISSIVE) || defined(DEBUG_F0) || defined(DEBUG_ALPHA) || defined(DEBUG_VIEW) || defined(DEBUG_PUNCTUAL) || defined(DEBUG_PUNCTUAL_SPECULAR) || defined(DEBUG_PUNCTUAL_DIFFUSE) || defined(DEBUG_IBL) || defined(DEBUG_IBL_SPECULAR) || defined(DEBUG_IBL_DIFFUSE) || defined(DEBUG_SH) || defined(DEBUG_NO_TONE_MAP) || defined(DEBUG_SUBSURFACE_SCATTERING))

            // LIGHTING_MODES: this is done to match the options of the commercial GLTF viewers
            // #pragma multi_compile __ LIGHTING_MODE_SH_ONLY LIGHTING_MODE_IBL_ONLY

            // VERTEX COLORS: activate this to transmit lo-fi model vert colors or sub mesh information in the alpha channel
    //            #pragma shader_feature HAS_VERTEX_COLOR_float4
            #define HAS_VERTEX_COLOR_float4

            // TONEMAP_MODES: Must match the Properties specified above
            // #pragma multi_compile __ TONEMAP_UNCHARTED TONEMAP_HEJLRICHARD TONEMAP_ACES

            // BRDF_LUT_MODES: Must match the Properties specified above
            #pragma multi_compile BRDF_LUT_MODE_ON BRDF_LUT_MODE_OFF
          
            // MATERIAL_MODES: Must match the Properties specified above
            #pragma multi_compile MATERIAL_MODE_TEXTURE MATERIAL_MODE_VERTEX

            #pragma target 4.0 // necessary for use of SV_VertexID

            // Include the Vertex Shader HERE
            #include "precision.hlsl"
            #include "common-vert.hlsl"

            // Include the Pixel Shader HERE
            #include "precision.hlsl"
            #include "fb_sphere_map.hlsl"
            #include "UnityLightingCommon.cginc" // for _LightColor0, replace with hlsl when we can
            #include "common-frag.hlsl"

            ENDCG
        }

        // Universal Render Pipeline (URP)
        Pass
        {
            Tags{"LightMode" = "UniversalForward"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            /////////////////////////////////////////////////////////
            // PRAGMAS: Pragmas cannot exist in cginc files so include them here

            // DEBUG_MODES: Must match the Properties specified above
            // #pragma multi_compile __ DEBUG_BASECOLOR DEBUG_OCCLUSION DEBUG_ROUGHNESS DEBUG_METALLIC DEBUG_THICKNESS DEBUG_NORMAL DEBUG_NORMAL_MAP DEBUG_EMISSIVE DEBUG_VIEW DEBUG_PUNCTUAL DEBUG_PUNCTUAL_SPECULAR DEBUG_PUNCTUAL_DIFFUSE DEBUG_IBL DEBUG_IBL_SPECULAR DEBUG_IBL_DIFFUSE DEBUG_SH DEBUG_NO_TONE_MAP DEBUG_SUBSURFACE_SCATTERING
            #define DEBUG_LIGHTING (defined(DEBUG_METALLIC) || defined(DEBUG_THICKNESS) || defined(DEBUG_ROUGHNESS) || defined(DEBUG_NORMAL) || defined(DEBUG_NORMAL_MAP) || defined(DEBUG_BASECOLOR) || defined(DEBUG_OCCLUSION) || defined(DEBUG_EMISSIVE) || defined(DEBUG_F0) || defined(DEBUG_ALPHA) || defined(DEBUG_VIEW) || defined(DEBUG_PUNCTUAL) || defined(DEBUG_PUNCTUAL_SPECULAR) || defined(DEBUG_PUNCTUAL_DIFFUSE) || defined(DEBUG_IBL) || defined(DEBUG_IBL_SPECULAR) || defined(DEBUG_IBL_DIFFUSE) || defined(DEBUG_SH) || defined(DEBUG_NO_TONE_MAP) || defined(DEBUG_SUBSURFACE_SCATTERING))

            // LIGHTING_MODES: this is done to match the options of the commercial GLTF viewers
            // #pragma multi_compile LIGHTING_MODE_IBL_PLUS_PUNCTUAL LIGHTING_MODE_SH_PLUS_PUNCTUAL LIGHTING_MODE_IBL_ONLY LIGHTING_MODE_SH_ONLY LIGHTING_MODE_PUNCTUAL_ONLY
            #define LIGHTING_MODE_IBL_PLUS_PUNCTUAL

            // VERTEX COLORS: activate this to transmit lo-fi model vert colors or sub mesh information in the alpha channel
    //            #pragma shader_feature HAS_VERTEX_COLOR_float4
            #define HAS_VERTEX_COLOR_float4

            // TONEMAP_MODES: Must match the Properties specified above
            // #pragma multi_compile __ TONEMAP_UNCHARTED TONEMAP_HEJLRICHARD TONEMAP_ACES

            // BRDF_LUT_MODES: Must match the Properties specified above
            #pragma multi_compile BRDF_LUT_MODE_ON BRDF_LUT_MODE_OFF
          
            // MATERIAL_MODES: Must match the Properties specified above
            #pragma multi_compile MATERIAL_MODE_TEXTURE MATERIAL_MODE_VERTEX

            #pragma target 4.0 // necessary for use of SV_VertexID

            // Palettization modes for Avatar FBX tool
            #pragma multi_compile __ _PALETTIZATION_SINGLE_RAMP _PALETTIZATION_TWO_RAMP

            // In Avatar SDK we ALWAYS use the ORM property map extension. Indicate that here:
            #define USE_ORM_EXTENSION

            // Turn on IBL only here, not for the other additive passes
            #if defined(LIGHTING_MODE_IBL_PLUS_PUNCTUAL) || defined(LIGHTING_MODE_IBL_ONLY)
            #define USE_IBL // IBL only gets applied here in the first base pass
            #define USE_IBL_DIFFUSE
            #define USE_IBL_SPECULAR
            #endif
            #if defined(LIGHTING_MODE_SH_PLUS_PUNCTUAL) || defined(LIGHTING_MODE_SH_ONLY)
            // per vertex is faster than per pixel, and almost indistinguishable for our purpose
            #define USE_SH_PER_VERTEX
            #endif

            // Include the Vertex Shader HERE
            #include "precision.hlsl"
            #include "common-vert.hlsl"

            // Include the Pixel Shader HERE
            #include "precision.hlsl"
            #include "fb_sphere_map.hlsl"
            #include "UnityLightingCommon.cginc" // for _LightColor0, replace with hlsl when we can
            #include "common-frag.hlsl"

            ENDCG
        }
    }
}
