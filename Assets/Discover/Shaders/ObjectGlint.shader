// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "Discover/Object Glint"
{
    Properties
    {
        _Color("State Color", Color) = (0.2896938, 0.4076439, 0.5849056, 0)
        _BounceScrub("Bounce Time Scrub", Range(0 , 1)) = 0.0
        _BounceScale("Bounce Scale", Range(0 , 0.2)) = 0.1
        _Highlighted("Highlighted", Range(0 , 1)) = 0.0
        _HighlightedOffsetScale("Highlighted offset scale", Range(0 , 1)) = 1.0
        _TimeOffset("Time offset", Range(0 , 1)) = 0.0

        _IsValidPlacement("IsValidPlacement", Range(0 , 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGINCLUDE
        #pragma target 3.0
        ENDCG
        Blend Off
        AlphaToMask Off
        Cull Back
        ColorMask RGBA
        ZWrite On
        ZTest LEqual
        Offset 0 , 0

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "UniversalForward" "PassFlags" = "OnlyDirectional" }
            
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                half3 vertexNormal : NORMAL;
                half4 vertexColor : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                half4 vertex : SV_POSITION;
                half bouncer : TEXCOORD1;
                half3 worldNormal : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                float4 vertexColor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uniform half4 _Color;
            uniform half _BounceScrub;
            uniform half _BounceScale;
            uniform half _Highlighted;
            uniform half _HighlightedOffsetScale;
            uniform half _TimeOffset;
            uniform half _IsValidPlacement;

            float4 RotateInYByDegrees(float4 vertex, float degrees)
            {
                float amount = degrees * PI / 180.0;
                float sina, cosa;
                sincos(amount, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float4(mul(m, vertex.xz), vertex.yw).xzyw;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 vertexPos = v.vertex;
                o.vertexColor = v.vertexColor;

                //rotate
                float highlightedSpinValue = sin((_Time.y + _TimeOffset) * 2) * _Highlighted;
                vertexPos = RotateInYByDegrees(vertexPos, highlightedSpinValue * 10);

                vertexPos = mul(unity_ObjectToWorld, vertexPos); //into world space for bouncing
                
                //raise and bounce
                vertexPos.y += _Highlighted * 0.05 * _HighlightedOffsetScale;
                o.bouncer = sin((_Time.y + _TimeOffset) * 4);
                vertexPos.y += o.bouncer * _Highlighted * 0.025 ;


                o.worldPos = vertexPos.xyz;
                vertexPos = mul(unity_WorldToObject, vertexPos); //back into object space
                // things with vertex alpha of 0 will not bounce or spin
                vertexPos = lerp(v.vertex, vertexPos, v.vertexColor.a);
                o.vertex = TransformObjectToHClip(vertexPos.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.vertexNormal);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 normals = normalize(i.worldNormal);
                half3 worldViewDirection = normalize(GetWorldSpaceViewDir(i.worldPos));
                half fresnel = dot(worldViewDirection, normals);
                fresnel = 1.0 - fresnel;

                half3 worldSpaceLightDir = GetMainLight().direction;
                half wrappedLambert = dot(normals, worldSpaceLightDir) * 0.5 + 0.5;
                half lightThreshold = smoothstep(0.65, 0.7, wrappedLambert);
                half lighting = saturate ((fresnel * lightThreshold) + 0.25);


                half4 invalidColor = half4(1.0, 0.0, 0.1, 0.5);
                half4 finalColor = lerp(invalidColor, _Color, _IsValidPlacement) * lighting;
                half finalAlpha = lerp(invalidColor.a, _Color.a, _IsValidPlacement);
                // masking out the things that don't bounce or spin, e.g. the border frame
                finalAlpha *= i.vertexColor.a;

                //selected + highlighted
                half bounceHighlight = _Highlighted * 0.25;
                bounceHighlight *= saturate (i.bouncer * 1.5);
                finalColor = saturate(finalColor + bounceHighlight);
                finalColor.a = finalAlpha;
                finalColor.a += (1.0 - i.vertexColor.a) * bounceHighlight ;

               return finalColor;
            }
            ENDHLSL
        }
    }


}
