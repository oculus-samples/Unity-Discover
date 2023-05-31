//================================================================================
// Common shader framework for Avatar Shaders using RenderToolkit's vertex-based global illumination
//
// This file should be included by common shaders that don't need to take over
// the main control flow
//
// See https://fb.quip.com/j99FAwxuOGCX
//================================================================================

#ifndef AVATAR_VGI_FRAMEWORK_CGINC
#define AVATAR_VGI_FRAMEWORK_CGINC

#include "../AvatarCommon/AvatarShaderTypes.cginc"
#include "../AvatarCommon/AvatarCommonProperties.cginc"
#include "../../../../../Scripts/ShaderUtils/AvatarCustom.cginc"
#include "AvatarVGITypes.cginc"
#include "../Horizon/VertexGI/SurfaceShader.cginc"

#ifdef FADE_ON
  #define DECLARE_COVERAGE_MASK  , out uint outputCoverageMask : SV_Coverage
  #define SET_COVERAGE_MASK outputCoverageMask = o.CoverageMask;
#else
  #define DECLARE_COVERAGE_MASK
  #define SET_COVERAGE_MASK
#endif

//================================
// Vertex program
//================================

void AvatarVGIVertInit(appdata v) {
    OVR_INITIALIZE_VERTEX_FIELDS(v);
}

void AvatarVGIVertTransform(appdata v, inout vgi_vert_tmp tmp) {
    OvrVertexData vData = OVR_CREATE_VERTEX_DATA(v);
    // Call the default transform
    vgi_vert_transform(v, vData, tmp);
}

#define DEFINE_DEFAULT_AVATAR_VGI_VERT(VertProgramName) \
    v2f VertProgramName(appdata v) { \
      /* Call the vertex-GI vertex program */ \
      vgi_vert_tmp  tmp; \
      UNITY_INITIALIZE_OUTPUT(vgi_vert_tmp, tmp);\
      /* Step 0: Initialize vertex input */ \
      AvatarVGIVertInit(v); \
      /* Step 1: transform position and normal */ \
      AvatarVGIVertTransform(v, tmp); \
      /* Step 2: transfer from vertex data and tmp to interpolators */ \
      v2f o = vgi_vert(v, tmp); \
      o.propertiesMapUV.xy = v.uv.xy;\
      o.effectsMapUV.xy = v.uv.xy;\
      return o; \
    }

#define DEFINE_AVATAR_VGI_VERT(VertProgramName, vert_func) \
    v2f VertProgramName(appdata v) { \
      /* Call the vertex-GI vertex program */ \
      vgi_vert_tmp  tmp; \
      UNITY_INITIALIZE_OUTPUT(vgi_vert_tmp, tmp) \
      /* Step 0: Initialize vertex input */ \
      AvatarVGIVertInit(v); \
      /* Step 1: transform position and normal */ \
      AvatarVGIVertTransform(v, tmp); \
      \
      /* Call GI code to return the fragment color */ \
      v2f o = vgi_vert(v, tmp); \
      o.propertiesMapUV.xy = v.uv.xy;\
      o.effectsMapUV.xy = v.uv.xy;\
      /* Call additional vert_func to further alter the v2f */ \
      vert_func(v, tmp, o); \
      return o; \
    }

AvatarShaderGlobalIllumination GetAvatarShaderGlobalIllumination(v2f i, vgi_frag_tmp tmp) {
  AvatarShaderLight light;
  light.direction = _LightVector.xyz;
  light.color = _LightColor.rgb * tmp.shadowFactor;

  AvatarShaderIndirect indirect;
  indirect.diffuse = calc_indirect_diffuse_light(i, tmp);
  indirect.specular = indirect.diffuse;

  AvatarShaderGlobalIllumination avatarGI;
  avatarGI.light = light;
  avatarGI.indirect = indirect;

  return avatarGI;
}

// Does not include "specular intensity" (fresnel/energy conservation) factor to mimic
// the behavior of the UnityGlobalIllumination
float VGIAmbientSpecular(half3 viewDir, half3 normal, half smooth, half metal) {
  half3 r = reflect(-viewDir, normal);
  // artificially force the surface to be less smooth, and return a less bright reflection (T65635945)
  return ambient_specular_from_cube_map(r, smooth*.5, metal)*.25;
}

AvatarShaderGlobalIllumination GetAvatarShaderGlobalIllumination(v2f i, half3 viewDir, vgi_frag_tmp tmp, half smoothness, half metallic, half occlusion) {
  AvatarShaderLight light;

  light.direction = _LightVector.xyz;
  light.color = _LightColor.rgb * tmp.shadowFactor;

  AvatarShaderIndirect indirect;
  half ambientOcclusion = lerp(1.0f, occlusion, _AmbientOcclusionEffect);
  indirect.diffuse = calc_indirect_diffuse_light(i, tmp) * ambientOcclusion;
  indirect.specular = indirect.diffuse * VGIAmbientSpecular(viewDir, tmp.normal, smoothness, metallic) * ambientOcclusion;

  AvatarShaderGlobalIllumination avatarGI;
  avatarGI.light = light;
  avatarGI.indirect = indirect;

  return avatarGI;
}

//================================
// Fragment program
//================================

#define DEFINE_AVATAR_VGI_FRAG(FragProgramName, SurfFuncName, SurfaceOutputType, LightingFunc) \
    float4 FragProgramName(v2f i) : COLOR { \
      vgi_frag_tmp  tmp; \
      UNITY_INITIALIZE_OUTPUT(vgi_frag_tmp, tmp);\
\
      /* Call the first stage of the fragment program:\
       'tmp' initialization and normal mapping */ \
      vgi_frag_init(i, tmp); \
\
      /* Call the shadowmapping code. This is optional. \
         If you don't want dynamic shadows, just don't call this.*/  \
      vgi_frag_dynamicshadow(i, tmp); \
\
      /* Call "surf" function */ \
      SurfaceOutputType o; \
      UNITY_INITIALIZE_OUTPUT(SurfaceOutputType, o); \
      SurfFuncName(i, o, tmp); \
\
      /* Parse out lighting information from the fragment input */ \
      AvatarShaderGlobalIllumination gi = GetAvatarShaderGlobalIllumination(i, tmp); \
\
      /* Call lighting function, expects normalized view direction vector */ \
      half4 result = LightingFunc(o, -normalize(i.wPos - _WorldSpaceCameraPos), gi); \
\
      /* Call the final stage: \
         Fog and tone mapping */ \
      float4 finalColor = vgi_frag_final(i, result.rgb); \
      finalColor.a = result.a; \
\
      return finalColor; \
    }

#define DEFINE_AVATAR_VGI_FRAG_PBS(FragProgramName, SurfFuncName, SurfaceOutputType, LightingFunc) \
    float4 FragProgramName(v2f i \
      DECLARE_COVERAGE_MASK) : COLOR { \
      vgi_frag_tmp  tmp; \
      UNITY_INITIALIZE_OUTPUT(vgi_frag_tmp, tmp);\
\
      /* Call the first stage of the fragment program:\
       'tmp' initialization and normal mapping */ \
      vgi_frag_init(i, tmp); \
\
      /* Call the shadowmapping code. This is optional. \
         If you don't want dynamic shadows, just don't call this.*/  \
      vgi_frag_dynamicshadow(i, tmp); \
\
      /* Call "surf" function */ \
      SurfaceOutputType o; \
      UNITY_INITIALIZE_OUTPUT(SurfaceOutputType, o); \
      SurfFuncName(i, o, tmp); \
\
      /* Grab some fields from surface output that are required to be there */ \
      half3 viewDir = -normalize(i.wPos - _WorldSpaceCameraPos); \
      half smoothness = GET_AVATAR_SHADER_SURFACE_SMOOTHNESS_FIELD(o); \
      half metallic = GET_AVATAR_SHADER_SURFACE_METALLIC_FIELD(o); \
      half occlusion = GET_AVATAR_SHADER_SURFACE_OCCLUSION_FIELD(o); \
\
      /* Parse out lighting information from the fragment input */ \
      AvatarShaderGlobalIllumination gi = GetAvatarShaderGlobalIllumination(i, viewDir, tmp, smoothness, metallic, occlusion); \
\
      /* Call lighting function, expects normalized view direction vector */ \
      half4 result = LightingFunc(o, viewDir, gi); \
\
      /* Call the final stage: \
         Fog and tone mapping */ \
      float4 finalColor = vgi_frag_final(i, result.rgb); \
      finalColor.a = result.a; \
\
      SET_COVERAGE_MASK \
\
      return finalColor; \
    }


#endif
