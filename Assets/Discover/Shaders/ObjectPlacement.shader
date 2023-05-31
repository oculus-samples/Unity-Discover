// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "Discover/Object Placement"
{
    Properties
    {
        _Color("State Color", Color) = (0.2896938,0.4076439,0.5849056,0)
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
            Name "Unlit"
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"


            struct appdata
            {
                float4 vertex : POSITION;
                half3 vertexNormal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                half4 vertex : SV_POSITION;
                half3 worldNormal : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uniform half4 _Color;
            uniform half _IsValidPlacement;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.vertexNormal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 normals = normalize(i.worldNormal);
                half3 worldViewDirection = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half fresnel = dot(worldViewDirection, normals);
                fresnel = 1.0 - fresnel;
                fresnel = saturate(fresnel + 0.25);

                // valid placement location or not
                half4 invalidColor = half4(1.0, 0.0, 0.1, 0.8);
                half4 finalColor = lerp(invalidColor, _Color, _IsValidPlacement) * fresnel;
                return finalColor;
            }
            ENDCG
        }
    }
}
