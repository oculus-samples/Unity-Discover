// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

// Simplified Bumped Specular shader. Differences from regular Bumped Specular one:
// - no Main Color nor Specular Color
// - specular lighting directions are approximated per vertex
// - writes zero to alpha channel
// - Normalmap uses Tiling/Offset of the Base texture
// - no Deferred Lighting support
// - no Lightmap support
// - fully supports only 1 directional light. Other lights can affect it, but it will be per-vertex/SH.

Shader "Avatar/Mobile/Bumped Specular" {
Properties {
    [PowerSlider(5.0)] _Shininess ("Shininess", Range (0.03, 1)) = 0.078125
    _MainTex ("Base (RGB) Gloss (A)", 2D) = "white" {}
    [NoScaleOffset] _BumpMap ("Normalmap", 2D) = "bump" {}

    // AVATAR_SDK_2 BEGIN
    _OcclusionMetallicRoughnessMap("Occlusion Roughness Metallic (ORM)", 2D) = "white" {}
    [PowerSlider(1.0)] _RoughnessFactor ("RoughnessFactor", Range (0.0, 1)) = 0.8
    // AVATAR_SDK_2 END
}
SubShader {
    Tags { "RenderType"="Opaque" }
    LOD 250

CGPROGRAM
#pragma surface surf MobileBlinnPhong vertex:vert exclude_path:prepass nolightmap noforwardadd halfasview interpolateview

    #pragma target 3.5 // necessary for use of SV_VertexID
    #include "../../../../Scripts/ShaderUtils/AvatarCustom.cginc"

inline fixed4 LightingMobileBlinnPhong (SurfaceOutput s, fixed3 lightDir, fixed3 halfDir, fixed atten)
{
    fixed diff = max (0, dot (s.Normal, lightDir));
    fixed nh = max (0, dot (s.Normal, halfDir));
    fixed spec = pow (nh, s.Specular*128) * s.Gloss;

    fixed4 c;
    c.rgb = (s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * spec) * atten;
    UNITY_OPAQUE_ALPHA(c.a);
    return c;
}

// AVATAR_SDK_2 BEGIN
sampler2D _OcclusionMetallicRoughnessMap;
half _RoughnessFactor;
// AVATAR_SDK_2 END

sampler2D _MainTex;
sampler2D _BumpMap;
half _Shininess;

struct Input {
    float2 uv_MainTex;
};

void vert(inout OvrDefaultAppdata v) {
  OvrInitializeDefaultAppdataAndPopulateWithVertexData(v);
}

void surf (Input IN, inout SurfaceOutput o) {

    fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
    o.Albedo = tex.rgb;

    // AVATAR_SDK_2 BEGIN
    // https: // docs.unity3d.com/Manual/SL-SurfaceShaders.html
    // legacy shaders like this use a Spec/gloss model
    // while standard uses the metallic/roughness model
    fixed4 orm = tex2D(_OcclusionMetallicRoughnessMap, IN.uv_MainTex);
    float occlusion = orm.r;
    float roughness = orm.g;
    float metallic = orm.b;
    o.Gloss = (1 - roughness) * _RoughnessFactor;
    o.Albedo *= occlusion;
    o.Gloss *= occlusion;
    // AVATAR_SDK_2 END

    o.Alpha = tex.a;
    o.Specular = _Shininess;
    o.Normal = UnpackNormal (tex2D(_BumpMap, IN.uv_MainTex));
}
ENDCG
}

FallBack "Mobile/VertexLit"
}
