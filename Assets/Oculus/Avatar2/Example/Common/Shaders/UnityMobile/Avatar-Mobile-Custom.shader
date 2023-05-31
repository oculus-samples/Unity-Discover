// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

// Simplified Bumped Specular shader. Differences from regular Bumped Specular one:
// - no Main Color nor Specular Color
// - specular lighting directions are approximated per vertex
// - writes zero to alpha channel
// - Normalmap uses Tiling/Offset of the Base texture
// - no Deferred Lighting support
// - no Lightmap support
// - fully supports only 1 directional light. Other lights can affect it, but it will be per-vertex/SH.

Shader "Avatar/Mobile/Custom" {

  Properties {
    [PowerSlider(5.0)] _Shininess("Shininess", Range(0.03, 1)) = 0.078125

    [NoScaleOffset] _MainTex("Base (RGB) Gloss (A)", 2D) = "white" {}
    [ShowIfKeyword(HAS_NORMALS)]
    [NoScaleOffset] _BumpMap("Normalmap", 2D) = "bump" {}

    // AVATAR_SDK_2 BEGIN
    _OcclusionMetallicRoughnessMap("Occlusion Roughness Metallic (ORM)", 2D) = "white" {}
    [PowerSlider(1.0)] _RoughnessFactor ("RoughnessFactor", Range (0.0, 1)) = 0.8
    [PowerSlider(5.0)] _Extrusion("Extrusion", Range(-0.050, 0.050)) = 0.00
    // AVATAR_SDK_2 END

    [NoScaleOffset] u_AttributeTexture("GPU Skinning Source Texture", 2DArray) = "black" {}
  }

  SubShader {
    Tags{"RenderType" = "Opaque"}
    LOD 250

    CGPROGRAM
#pragma surface surf MobileBlinnPhongCustom vertex:vert exclude_path:prepass nolightmap noforwardadd halfasview interpolateview

    #pragma target 3.5 // necessary for use of SV_VertexID
    #include "../../../../Scripts/ShaderUtils/AvatarCustom.cginc"

    struct SurfaceOutputCustom {
      fixed3 Albedo; // diffuse color
      fixed3 Normal; // tangent space normal, if written
      fixed3 Emission;
      half Specular; // specular power in 0..1 range
      fixed Gloss; // specular intensity
      fixed Alpha; // alpha for transparencies
      fixed Metal; // determines color of specular highlight
    };

    inline fixed4 LightingMobileBlinnPhongCustom(SurfaceOutputCustom s, fixed3 lightDir, fixed3 halfDir, fixed atten)
    {
      fixed diff = max(0, dot(s.Normal, lightDir));
      fixed nh = max(0, dot(s.Normal, halfDir));
      fixed spec = pow(nh, s.Specular * 128) * s.Gloss;

      fixed4 c;
      fixed3 highlightColor = _LightColor0.rgb + s.Metal * (s.Albedo - _LightColor0.rgb);
      c.rgb = (s.Albedo * _LightColor0.rgb * diff + highlightColor * spec) * atten;

      UNITY_OPAQUE_ALPHA(c.a);
      return c;
    }

    // AVATAR_SDK_2 BEGIN
    sampler2D _OcclusionMetallicRoughnessMap;
    half _RoughnessFactor;
    // AVATAR_SDK_2 END

    sampler2D _MainTex;
#if HAS_NORMALS // AVATAR_SDK_2 REMOVE
    sampler2D _BumpMap;
#endif
    half _Shininess;

    struct Input {
      float2 uv_MainTex;
    };

    // AVATAR_SDK_2 BEGIN
    void vert(inout OvrDefaultAppdata v) {
      OvrInitializeDefaultAppdataAndPopulateWithVertexData(v);
    }
    // AVATAR_SDK_2 END

    void surf(Input IN, inout SurfaceOutputCustom o) {
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
        o.Metal = metallic;
        // AVATAR_SDK_2 END

        o.Alpha = tex.a;
        o.Specular = _Shininess;
#if HAS_NORMALS // AVATAR_SDK_2 REMOVE
        o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
#endif
    }
    ENDCG
  }

  FallBack "Mobile/VertexLit"
}
