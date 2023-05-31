#include "UnityImageBasedLighting.cginc"
#include "../../../../Scripts/ShaderUtils/OvrLightTypes.hlsl"

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

        // Completely per-pixel
        ambient_contrib = SHEvalLinearL0L1(half4(normal, 1.0));
        ambient_contrib += SHEvalLinearL2(half4(normal, 1.0));
        ambient += max(half3(0, 0, 0), ambient_contrib);

        #ifdef UNITY_COLORSPACE_GAMMA
            ambient = LinearToGammaSpace(ambient);
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

  o_gi.indirect.diffuse = AvatarShadeSHPerPixel(normalWorld, data.ambient, data.worldPos);

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

void getSHContribution(OvrLight light, float3 worldPos, float3 worldViewDir,
fixed attenuation, half3 ambient, half smoothness, half metallic, half occlusion,
half3 albedo, half3 normal, out float3 diffuse, out float3 specular)
    {

    UnityGIInput giInput;
    UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
    giInput.light.color = _LightColor0.rgb;
    giInput.light.dir = light.direction;

    giInput.worldPos = worldPos;
    giInput.worldViewDir = worldViewDir;
    giInput.atten = attenuation;
    giInput.lightmapUV = 0.0; // we don't have a light map right?
    giInput.ambient = ambient;

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


    AvatarShaderGlobalIllumination gi = GetGlobalIllumination(
        giInput,
        smoothness, // smoothness
        metallic, // metallic
        occlusion, // occlusion
        albedo, // albedo
        normal // normal
    );
      diffuse = gi.indirect.diffuse.rgb; // diffuse light should already be in the linear space from SH calculations.
      specular = gi.indirect.specular.rgb; // specular light should already be in the linear space from exr sampling.

}
