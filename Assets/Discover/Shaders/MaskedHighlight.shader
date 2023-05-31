// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "Discover/MaskedHighlight"
{
    Properties
    {
        _Color("Base Color", Color) = (0.2896938, 0.4076439, 0.5849056, 0.25)
        _HighlightColor("HighlightColor", Color) = (1.0, 1.0, 0.3)
        _ClockwiseHighlight("ClockwiseHighlight", Range(0 , 1)) = 0.0
        _CounterClockwiseHighlight("CounterClockwiseHighlight", Range(0 , 1)) = 0.0
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                half4 vertex : SV_POSITION;
                half4 vertexColor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uniform half4 _Color;
            uniform half4 _HighlightColor;
            uniform half _ClockwiseHighlight;
            uniform half _CounterClockwiseHighlight;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                half4 arrowColor = _Color;
                arrowColor += _HighlightColor * v.color.r * _CounterClockwiseHighlight;
                arrowColor += _HighlightColor * v.color.g * _ClockwiseHighlight;
                arrowColor = saturate(arrowColor);

                o.vertexColor = arrowColor;
                o.vertex = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                fixed4 finalColor = i.vertexColor;
                return finalColor;
            }
            ENDCG
        }
    }


}
