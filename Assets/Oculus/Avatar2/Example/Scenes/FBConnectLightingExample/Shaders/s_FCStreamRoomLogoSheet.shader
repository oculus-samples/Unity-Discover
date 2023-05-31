Shader "Venues/Environment/FC_Logo"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BgTex ("BG Texture", 2D) = "white" {}
        _Color ("Color", color) = (1, 1, 1)
        _Tick ("Animation Tick", Range(0, 1)) = 0
        _NumRows ("# Rows", float) = 4
        _NumCols ("# Columns", float) = 4
        _SheetFrameRatio ("Size Ratio frame / sheet", Range(0, 1)) = 0.25
        _FgColor ("Text Color", color) = (0, 0, 0, 1)
        _TimeLock("Time spent on locked frame (5 ~= 10sec)", float) = 5
        _FrameLock("Index of locked frame", float) = 8
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv1 : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv1 : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _BgTex;
            half _NumRows, _NumCols, _SheetFrameRatio, _Tick, _TimeLock, _FrameLock;
            fixed3 _FgColor;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);

                // UV Scaling
                float num_frames = 16.;
                float frame_rate = 8.;
                float t = _Time.y * frame_rate;
                t = lerp(fmod(t, num_frames), num_frames - 1., step(num_frames, fmod(t, num_frames * _TimeLock)));
                t += _FrameLock;

                half x_offset = fmod(floor(t), _NumRows);
                half y_offset = _NumCols - floor(t / _NumCols) - 1;
                o.uv2.x = v.uv2.x;
                o.uv2 = float2(o.uv2.x + x_offset, v.uv2.y + y_offset) * _SheetFrameRatio;
                o.uv2.y = o.uv2.y;
                o.uv1 = v.uv1;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half tex = tex2D(_MainTex, i.uv2).a;
                half3 bg = tex2D(_BgTex, i.uv1).rgb;
                fixed4 col = fixed4(lerp(bg, fixed3(0., 0., 0.), tex), 1);
                return col;
            }
            ENDCG
        }
    }
}
