// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "DroneRage/BulletHole"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _WallInterior("Wall Interior", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
    }

        SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry-11" "IgnoreProjector" = "True" "PreviewType" = "Plane" "PerformanceChecks" = "False" }
     
        CGINCLUDE
        #pragma target 3.0
        ENDCG

        Blend DstColor Zero
        AlphaToMask On
        Cull Back
        ColorMask A
        ZWrite On
        ZTest LEqual
        Offset -1 , -1

        Pass
        {
            Name "Wall Thickness"
            Blend SrcAlpha OneMinusSrcAlpha 
            ColorMask RGB
            AlphaToMask On
            ZWrite Off

            CGPROGRAM

            #ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
            //only defining to not throw compilation error over Unity 5.5
            #define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
            #endif
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct meshData
            {
                float4 vertex : POSITION;
                half4 texcoord : TEXCOORD0;
                half3 normal : NORMAL;
                half4 tangent : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct interpolators
            {
                half4 vertex : SV_POSITION;
                half2 texcoord : TEXCOORD0;
                half3 worldNormals : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                half3 worldTangent : TEXCOORD3;
                half3 worldBiTangent: TEXCOORD4;
                half3 tanViewDir : TEXCOORD6;
                half3 worldViewDirection : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uniform sampler2D _WallInterior;
            uniform half4 _Color;
            uniform sampler2D _MainTex;


            interpolators vert(meshData v)
            {
                interpolators o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormals = UnityObjectToWorldNormal(v.normal);
                o.texcoord = v.texcoord;

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldTangent = UnityObjectToWorldDir(v.tangent);
                float vertexTangentSign = v.tangent.w * unity_WorldTransformParams.w;
                o.worldBiTangent = cross(o.worldNormals, o.worldTangent) * vertexTangentSign;
                o.worldTangent = UnityObjectToWorldDir(v.tangent);
                o.worldViewDirection = normalize(_WorldSpaceCameraPos - o.worldPos);

                half3 tanToWorld0 = float3(o.worldTangent.x, o.worldBiTangent.x, o.worldNormals.x);
                half3 tanToWorld1 = float3(o.worldTangent.y, o.worldBiTangent.y, o.worldNormals.y);
                half3 tanToWorld2 = float3(o.worldTangent.z, o.worldBiTangent.z, o.worldNormals.z);
                o.tanViewDir = normalize(tanToWorld0 * o.worldViewDirection.x + tanToWorld1 * o.worldViewDirection.y + tanToWorld2 * o.worldViewDirection.z);

                return o;
            }


            fixed4 frag(interpolators i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                //parallax offset for wall interior texture
                half2 scaledUVs = ((i.texcoord.xy - 0.5) * 0.95) + 0.5; // scaling up the interior uvs to compensate for scaling with depth
                half2 uvOffset = (i.tanViewDir.xy * -0.2) + scaledUVs; //offsetting the UVs with view-dependent depth

                half4 holeMask = (tex2D(_MainTex, i.texcoord.xy));
                half4 finalColor = tex2D(_WallInterior, uvOffset);
                finalColor.a *= holeMask.a;
                return finalColor;
             }
          ENDCG
        }


        Pass
        {
            Name "Hole Puncher"
            ColorMask A

            CGPROGRAM

            #ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
            //only defining to not throw compilation error over Unity 5.5
            #define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
            #endif
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct meshData
            {
                float4 vertex : POSITION;
                half4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct interpolators
            {
                half4 vertex : SV_POSITION;
                half4 UV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uniform sampler2D _MainTex;
            uniform half4 _Color;

            interpolators vert(meshData v)
            {
                interpolators o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.UV = v.texcoord;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(interpolators i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                fixed4 finalColor;
                finalColor = (tex2D(_MainTex, i.UV));
                finalColor *= _Color;
                return finalColor;
            }
          ENDCG
        } 
    }
}
