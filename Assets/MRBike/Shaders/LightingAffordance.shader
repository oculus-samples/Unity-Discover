// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "MRBike/LightingAffordance"
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
        [Header(Transparency)] [Space(5)]
        _Opacity("Transparency", Range(0,1)) = 1.0
        [Toggle(PREMULTIPLY)] _PreMult ("Pre-Multiply Alpha", Float) = 0

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

        [Space(5)]
        [Header(Affordance)] [Space(5)]
        _Kaffordance("Affordance", Range(0,1)) = 0
        _AffordanceColor("Affordance Color", Color) = (1,0,0,1)
        _OutlineWidth("Outline Width", Float) = 0.1
    }

    SubShader
    {
 		Tags { "Queue"="Transparent"  "RenderType"="Transparent"}
        Blend One OneMinusSrcAlpha
		LOD 200

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // custom variants
            #pragma multi_compile __ MIKKT
            #pragma multi_compile _DIFFUSE_LAMBERT _DIFFUSE_OREN
            #pragma multi_compile _SPECULAR_BLINNPHONG _SPECULAR_WARD
            #pragma multi_compile __ SPECULAR_IN_ALPHA
            #pragma multi_compile __ IBL
            #pragma multi_compile __ OCCLUSION_MAP
            #pragma multi_compile __ PREMULTIPLY

            #define AFFORDANCE 1
            #define TRANSPARENT 1

            #include "MRBike_Lighting.hlsl"

            ENDHLSL

        }

        Pass
        {
            Name "Outline"
            
            Tags { "LightMode" = "UniversalForward" }

            Cull Front

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _OutlineWidth;
            float _Kaffordance;
            float4 _AffordanceColor;

            // fragment shader data
            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : NORMAL;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                v.vertex.xyz += v.normal * lerp(0, _OutlineWidth, _Kaffordance);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }


            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_AffordanceColor.rgb,1);
            }

            ENDCG
        }
    }
}
