// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "MRBike/Lighting"
{
    Properties
    {
        [Header(Diffuse Lighting)] [Space(5)]
        [KeywordEnum(Lambert, Oren)] _Diffuse ("Diffuse Model", Float) = 0
        _Kd("Diffuse Strength", Range(0,1.25)) = 1.0
        _DiffuseRoughness("Diffuse Roughness", Range(0,5)) = 0.5
        [MainColor] _AlbedoTint("Albedo Tint", Color) = (1,1,1,1)

        [Space(5)]
        [Header(Specular Lighting)] [Space(5)]
        [KeywordEnum(Blinnphong, Ward)] _Specular ("Specular Model", Float) = 0
        _Ks("Glossiness", Range(0,1)) = 0.2
        _Roughness("Roughness", Range(0,1.5)) = 0.1
        _OrthoRoughness("Ortho Roughness", Range(0,1)) = 0.1
        _Metalness("Metalness", Range(0,1)) = 0
        _Kr("Reflectivity", Range(0,1)) = 0.1
        _Kf("Fresnel Strength", Range(0,1)) = 0.1
        _Eta("Fresnel Roughness", Range(0,7)) = 0.5
        _SpecularTint("Specular Tint", Color) = (0.2,0.2,0.2,1)

        [Space(5)]
        [Header(Albedo)] [Space(5)]
        [Toggle(SPECULAR_IN_ALPHA)] _SpecularInAlpha ("Albedo alpha is specularity", Float) = 0
        [MainTexture] _AlbedoTexture("Albedo Texture", 2D) = "white" {}

        [Space(5)]
        [Header(Normal Mapping)] [Space(5)]
        [Toggle(MIKKT)] _MikktNormals ("Mikkt normal mapping", Float) = 0
        _NormalBlend("Normal Blend", Range(0,1)) = 1.0
        [NoScaleOffset] [Normal] _NormalMap("Normal Map", 2D) = "bump" {}

        [Space(5)]
        [Header(Occlusion)] [Space(5)]
        [Toggle(OCCLUSION_MAP)] _UseOcclusionMap ("Use Occlusion Map", Float) = 0
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1.0
        [NoScaleOffset] _OcclusionMap("Occlusion Map", 2D) = "white" {}

        [Space(5)]
        [Header(Image Based Lighting)] [Space(5)]
        [Toggle(IBL)] _IBL ("Enable IBL Lighting", Float) = 0
        _Exposure("Exposure", Range(0,8)) = 1
        _SpecularMIP("Specular MIP Level", Integer) = 0
        [NoScaleOffset] _IBLTex("IBL", 2D) = "black" {}
    }

    SubShader
    {
 		Tags { "Queue"="Geometry"  "RenderType"="Opaque"}
        Blend One OneMinusSrcAlpha 
		LOD 200

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
 
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight  // exclude lightmap shader variants

            // custom variants
            #pragma multi_compile __ MIKKT
            #pragma multi_compile _DIFFUSE_LAMBERT _DIFFUSE_OREN
            #pragma multi_compile _SPECULAR_BLINNPHONG _SPECULAR_WARD
            #pragma multi_compile __ SPECULAR_IN_ALPHA
            #pragma multi_compile __ IBL
            #pragma multi_compile __ OCCLUSION_MAP

            #include "MRBike_Lighting.hlsl"
 
            ENDHLSL
        }
    }
}