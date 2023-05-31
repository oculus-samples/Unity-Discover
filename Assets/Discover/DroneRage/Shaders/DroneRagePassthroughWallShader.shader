// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "DroneRage/DroneRagePassthroughWall"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Culling Mode", Float) = 2
        _MainTex("MainTex", 2D) = "white" {}
        _Color("Glow", Color) = (0.2896938, 0.4076439, 1.0, 0)
        _ColorCore("Glow Core", Color) = (0.2896938, 0.4076439, 1.0, 0)

    }

    SubShader
    {
        Tags { "Queue" = "Transparent-1" "RenderType" = "Transparent" }
        Pass
        {

                ZWrite Off
                ZTest Less
                BlendOp Add, Max
                Blend SrcAlpha One, One One
                Cull[_CullMode]
                Offset -10 , 0

                CGPROGRAM

                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_instancing

                #include "UnityCG.cginc"

                struct meshData
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float3 normal : NORMAL;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct interpolators
                {
                    half2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                    float3 worldPos : TEXCOORD1;
                    float3 worlduv : TEXCOORD2;
                    float3 triplanarWeights: TEXCOORD3;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                uniform sampler2D _MainTex;
                uniform half4 _Color;
                uniform half4 _ColorCore;
                uniform float4 _MainTex_ST;


                interpolators vert(meshData v)
                {
                    interpolators o;

                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    UNITY_TRANSFER_INSTANCE_ID(v, o);

                    o.vertex = UnityObjectToClipPos(v.vertex);

                    o.uv = v.uv;
                    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                    half3 normal = UnityObjectToWorldNormal(v.normal);
                    half3 triW = abs(normal);
                    triW = triW / (triW.x + triW.y + triW.z);
                    o.triplanarWeights = triW;

                    o.worlduv = o.worldPos;
                    o.worlduv.y -= _Time.x;

                    return o;
                }

                fixed4 frag(interpolators i) : SV_TARGET
                {
                    UNITY_SETUP_INSTANCE_ID(i);
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                    // sin waves to add variation
                    half worldWobble = sin((i.worldPos.x + _Time.x) * 10);
                    worldWobble += sin((i.worldPos.z - _Time.x) * 10);

                    // rims
                    half rimSharp  = smoothstep(0.21, 0.2, i.uv.y);
                    rimSharp *= smoothstep(0.17, 0.18, i.uv.y);
                    half rimSoft = smoothstep(0.5, 0.2, i.uv.y);
                    rimSoft *= smoothstep(0.0, 0.17, i.uv.y);

                    //triplanar
                    float4 baseTextureZY = tex2D(_MainTex, i.worlduv.zy * _MainTex_ST.xy + _MainTex_ST.zw);
                    float4 baseTextureXY = tex2D(_MainTex, i.worlduv.xy * _MainTex_ST.xy + _MainTex_ST.zw);
                    float4 baseTexture = i.triplanarWeights.x * baseTextureZY + i.triplanarWeights.z * baseTextureXY + 0.001;

                    // texture and masks
                    baseTexture -= worldWobble * 0.1;
                    half topMask = smoothstep(1.0, 0.2, i.uv.y);
                    topMask *= smoothstep(0.17, 0.18, i.uv.y);
                    half sharpTexture = baseTexture.r * topMask ;

                    // sharp lines
                    half sharpTextureBottomPop = smoothstep(0.5, 0.2, i.uv.y);
                    sharpTextureBottomPop *= smoothstep(0.16, 0.17, i.uv.y);
                    sharpTextureBottomPop = sharpTextureBottomPop * sharpTextureBottomPop;
                    sharpTextureBottomPop *= 0.15;

                    half sharpMask = 0.0;
                    sharpMask = smoothstep(0.17 - sharpTextureBottomPop, 0.2 - sharpTextureBottomPop, sharpTexture);
                    sharpMask *= smoothstep(0.21 + sharpTextureBottomPop, 0.205 + sharpTextureBottomPop, sharpTexture);
                    sharpMask = saturate (rimSharp + sharpMask);
                    half4 sharpFinal = _ColorCore * sharpMask;

                    // soft wash
                    half softTexture = baseTexture.b * topMask;
                    half softMask = 0.0;
                    softMask = smoothstep(0.0, 0.2, softTexture);
                    softMask *= smoothstep(1.0, 0.2, softTexture);
                    softMask = saturate(softMask + rimSoft);
                    softMask *= 0.4;
                    half4 softFinal = _Color * softMask;
      
                    return half4(softFinal + sharpFinal);

                }
                ENDCG
            }








    }
}
