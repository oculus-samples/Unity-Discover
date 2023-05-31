Shader "Venues/Environment/S_Building"
{
    Properties
    {
        [Header(Walls)]
        _WallColor("Wall Color", color) = (1, 1, 1, 1)
        [Header(Windows)]
        _WindowTexSize("Window Texture Size", Float) = 1024
        _WindowSizeAndPadding("Window Number And Padding", Vector) = (12, 12, 10, 40)
        [Toggle(ENABLE_WINDOW_TEX)] _EnableWindowTex("Enable Window Texture", Float) = 0
        [HideIf(ENABLE_WINDOW_TEX, false)][NoScaleOffset] _WindowTex("Window Texture", 2D) = "white" {}
        [HideIf(ENABLE_WINDOW_TEX, true)] _WindowOnColor("Window On Color", color) = (1, 1, 1, 1)
        _WindowOffColor("Window Off Color", color) = (1, 1, 1, 1)
        _LightIntensity("Window Light Intensity", Range(0,1)) = 1
        _WindowThreshold("% Windows", Range(0, 1)) = 0.5
        _LitThreshold("% Lit Windows", Range(0, 1)) = 0.5
        [Header(Animation)]
        _Speed("Speed", Range(0, 0.1)) = 0.01
        _RandomSeed("Random Seed", Float) = 0
        [Header(Debug)]
        [Toggle(DEBUG)] _Debug("Enable Debug", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #pragma shader_feature ENABLE_WINDOW_TEX
            #pragma shader_feature DEBUG 

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv1 : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                half4 color : COLOR;
            };

            struct v2f
            {
                float2 uv1 : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
            };

            #if ENABLE_WINDOW_TEX
                sampler2D _WindowTex;
            #else
                half4 _WindowOnColor;
            #endif

            half4 _WindowSizeAndPadding, _WindowOffColor, _WallColor;
            half _WindowTexSize, _Speed, _LitThreshold, _WindowThreshold, _LightIntensity, _WindowFromTex, _RandomSeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;
                o.color = v.color;
                return o;
            }

            half random(in half2 st) {
                return frac(sin(dot(st.xy, half2(12.9898, 78.233))) * 43.5453);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half2 window_size = ((_WindowTexSize - (_WindowSizeAndPadding.xy * _WindowSizeAndPadding.yz)) / _WindowSizeAndPadding.xy) + _WindowSizeAndPadding.yz;
                half2 size = window_size / _WindowTexSize;
                half2 st1 = floor((i.uv2 - .5 * _WindowSizeAndPadding.zw / _WindowTexSize) / size) * size;
                half2 st2 = floor((i.uv2 + .5 * _WindowSizeAndPadding.zw / _WindowTexSize) / size) * size;
                half2 st = (st2 - st1);
                half mask = min(1., step(.01, st.r) + step(.01, st.g));
                st2 *= (1. - mask);
                half r = random(st2 + _RandomSeed);
                half val = step(1. - _LitThreshold, frac(_Time.y * r * _Speed + r * 10. + i.color.a * 20.)) * (1. - mask);

                #if ENABLE_WINDOW_TEX
                    fixed3 window_color = tex2D(_WindowTex, i.uv2) * _LightIntensity;
                #else
                    fixed3 window_color = _WindowOnColor * max(.5, r) * _LightIntensity;
                #endif

                fixed3 col = lerp(lerp(window_color, _WindowOffColor, val), _WallColor.rgb,  min(1., mask + step(_WindowThreshold, r)));

                #if DEBUG
                    half debug = r;
                    if (step(.5, i.color.a) > .5) {
                        col = half3(st2.r, st2.g, 0.);
                    }
                    else {
                        col = half3(r, r, r);
                    }
                #endif

                return fixed4(col, 1.);
            }
            ENDCG
        }
    }
}
