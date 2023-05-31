#include "UnityImageBasedLighting.cginc"

struct AvatarShaderLight {
  half3 direction;
  half3 color;
};

struct AvatarShaderIndirect {
  half3 diffuse;
  half3 specular;
};

struct AvatarShaderGlobalIllumination {
  AvatarShaderLight light;
  AvatarShaderIndirect indirect;
};

//-----------------------------------------------------------------------------------------
// The following use UnityStandardUtils.cginc as a reference, as of 2020.3.7

half3 AvatarShadeSHPerPixel (half3 normal, half3 ambient, float3 worldPos)
{
    half3 ambient_contrib = 0.0;

    #if defined(USE_SH_PER_PIXEL)
        // Completely per-pixel
        ambient_contrib = SHEvalLinearL0L1(half4(normal, 1.0));
        ambient_contrib += SHEvalLinearL2(half4(normal, 1.0));
        ambient += max(half3(0, 0, 0), ambient_contrib);

        #ifdef UNITY_COLORSPACE_GAMMA
            ambient = LinearToGammaSpace(ambient);
        #endif
    #else
        // Completely per-vertex
        // nothing to do here. Gamma conversion on ambient from SH takes place in the vertex shader, see ShadeSHPerVertex.
    #endif

    return ambient;
}

inline float3 AvatarBoxProjectedCubemapDirection(float3 worldRefl, float3 worldPos, float4 cubemapCenter, float4 boxMin, float4 boxMax) {
  // Do we have a valid reflection probe?
  UNITY_BRANCH
  if (cubemapCenter.w > 0.0) {
    float3 nrdir = normalize(worldRefl);

    float3 rbmax = (boxMax.xyz - worldPos) / nrdir;
    float3 rbmin = (boxMin.xyz - worldPos) / nrdir;

    float3 rbminmax = (nrdir > 0.0f) ? rbmax : rbmin;

    float fa = min(min(rbminmax.x, rbminmax.y), rbminmax.z);

    worldPos -= cubemapCenter.xyz;
    worldRefl = worldPos + nrdir * fa;
  }
  return worldRefl;
}

//-----------------------------------------------------------------------------------------
// The following use UnityGlobalIllumination.cginc as a reference, as of 2020.3.7

inline void AvatarResetUnityLight(out UnityLight outLight) {
  outLight.color = half3(0, 0, 0);
  outLight.dir = half3(0, 1, 0); // Irrelevant direction, just not null
  outLight.ndotl = 0; // Not used
}

inline void AvatarResetUnityGI(out UnityGI outGI) {
  AvatarResetUnityLight(outGI.light);
  outGI.indirect.diffuse = 0;
  outGI.indirect.specular = 0;
}

inline UnityGI AvatarUnityGI_Base(UnityGIInput data, half occlusion, half3 normalWorld) {
  UnityGI o_gi;
  AvatarResetUnityGI(o_gi);

  o_gi.light = data.light;
  o_gi.light.color *= data.atten;

#if defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL)
  o_gi.indirect.diffuse = AvatarShadeSHPerPixel(normalWorld, data.ambient, data.worldPos);
#endif

  o_gi.indirect.diffuse *= occlusion;
  return o_gi;
}

inline half3 AvatarUnityGI_IndirectSpecular(UnityGIInput data, half occlusion, Unity_GlossyEnvironmentData glossIn)
{
    half3 specular;

    #ifdef UNITY_SPECCUBE_BOX_PROJECTION
        // we will tweak reflUVW in glossIn directly (as we pass it to Unity_GlossyEnvironment twice for probe0 and probe1), so keep original to pass into AvatarBoxProjectedCubemapDirection
        half3 originalReflUVW = glossIn.reflUVW;
        glossIn.reflUVW = AvatarBoxProjectedCubemapDirection (originalReflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
    #endif

    #ifdef _GLOSSYREFLECTIONS_OFF
        specular = unity_IndirectSpecColor.rgb;
    #else
        half3 env0 = Unity_GlossyEnvironment (UNITY_PASS_TEXCUBE(unity_SpecCube0), data.probeHDR[0], glossIn);
        specular = env0;
    #endif

    return specular * occlusion;
}


inline UnityGI AvatarUnityGlobalIllumination(
    UnityGIInput data,
    half occlusion,
    half3 normalWorld,
    Unity_GlossyEnvironmentData glossIn) {
  UnityGI o_gi = AvatarUnityGI_Base(data, occlusion, normalWorld);
  o_gi.indirect.specular = AvatarUnityGI_IndirectSpecular(data, occlusion, glossIn);
  return o_gi;
}

//-----------------------------------------------------------------------------------------
// Finally our entry points into the Unity access functions come here:

UnityGIInput GetGlobalIlluminationInput(
    interpolators IN,
    fixed3 lightDir,
    float3 worldPos,
    float3 worldViewDir,
    fixed attenuation) {
  // Setup lighting environment
  UnityGI gi;
  UNITY_INITIALIZE_OUTPUT(UnityGI, gi);

  gi.indirect.diffuse = 0;
  gi.indirect.specular = 0;
  gi.light.color = _LightColor0.rgb;
  gi.light.dir = lightDir;

  // Call GI (lightmaps/SH/reflections) lighting function
  UnityGIInput giInput;
  UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
  giInput.light = gi.light;
  giInput.worldPos = worldPos;
  giInput.worldViewDir = worldViewDir;
  giInput.atten = attenuation;

#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
  giInput.lightmapUV = IN.lmap;
#else
  giInput.lightmapUV = 0.0;
#endif

#if defined(USE_SH_PER_VERTEX)
  giInput.ambient = IN.sh;
#else
  giInput.ambient.rgb = 0.0;
#endif

  giInput.probeHDR[0] = unity_SpecCube0_HDR;
  giInput.probeHDR[1] = unity_SpecCube1_HDR;
#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
  giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
#endif

#ifdef UNITY_SPECCUBE_BOX_PROJECTION
  giInput.boxMax[0] = unity_SpecCube0_BoxMax;
  giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
  giInput.boxMax[1] = unity_SpecCube1_BoxMax;
  giInput.boxMin[1] = unity_SpecCube1_BoxMin;
  giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
#endif

  return giInput;
}

AvatarShaderGlobalIllumination GetGlobalIllumination(
    UnityGIInput giInput,
    half smoothness,
    half metallic,
    half occlusion,
    half3 albedo,
    half3 normal) {
  Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(
      smoothness,
      giInput.worldViewDir,
      normal,
      lerp(unity_ColorSpaceDielectricSpec.rgb, albedo, metallic));

  UnityGI gi;
  UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
  half ambientOcclusion = occlusion;
  gi = AvatarUnityGlobalIllumination(giInput, ambientOcclusion, normal, g);

  AvatarShaderLight light;
  light.direction = gi.light.dir;
  light.color = gi.light.color;

  AvatarShaderIndirect indirect;
  indirect.diffuse = gi.indirect.diffuse;
  indirect.specular = gi.indirect.specular;

  AvatarShaderGlobalIllumination avatarGI;
  avatarGI.light = light;
  avatarGI.indirect = indirect;

  return avatarGI;
}
