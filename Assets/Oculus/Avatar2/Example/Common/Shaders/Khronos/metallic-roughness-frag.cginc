// Upgrade NOTE: replaced '_LightMatrix0' with 'unity_WorldToLight'

//
// This fragment shader defines a reference implementation for Physically Based Shading of
// a microfacet surface material defined by a glTF model.
//
// References:
// [1] Real Shading in Unreal Engine 4
//     http://blog.selfshadow.com/publications/s2013-shading-course/karis/s2013_pbs_epic_notes_v2.pdf
// [2] Physically Based Shading at Disney
//     http://blog.selfshadow.com/publications/s2012-shading-course/burley/s2012_pbs_disney_brdf_notes_v3.pdf
// [3] README.md - Environment Maps
//     https://github.com/KhronosGroup/glTF-WebGL-PBR/#environment-maps
// [4] "An Inexpensive BRDF Model for Physically based Rendering" by Christophe Schlick
//     https://www.cs.virginia.edu/~jdl/bib/appearance/analytic%20models/schlick94b.pdf

/////////////////////////////////////////////////////////
// Unity specific defines for Avatar SDK:

#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"

#define MATERIAL_METALLICROUGHNESS
#ifdef MATERIAL_MODE_TEXTURE
#define HAS_BASE_COLOR_MAP
#define HAS_EMISSIVE_MAP
#define HAS_OCCLUSION_MAP
#define HAS_METALLIC_ROUGHNESS_MAP
#endif

// #define HAS_SPECULAR_GLOSSINESS_MAP
// #define HAS_DIFFUSE_MAP
// #define HAS_SPECULAR_GLOSSINESS_MAP

// Turn on Punctual here
#if !defined(LIGHTING_MODE_SH_ONLY) && !defined(LIGHTING_MODE_IBL_ONLY)
#define USE_PUNCTUAL
#define EYE_GLINTS
#define EYE_GLINTS_BEHIND
#endif

#ifdef BRDF_LUT_MODE_ON
#define USE_IBL_BRDF_LUT
#endif

#ifdef USE_IBL
#define FLIP_IBL_WINDING // the z axis in Unity is backwards from Khronos
#endif

#define USE_HDR
#define USE_IBL_SPECULAR_LOD

// ALPHAMODE_MASK
#define ALPHAMODE_OPAQUE

// Unity Forward additive lighting adds light in multiple passes, so one light per pass is fine
#define LIGHT_COUNT 1

#include "AvatarFbxReview.cginc"

/////////////////////////////////////////////////////////
// Original ported code from metallic-roughness.frag:

#include "tonemapping.cginc"
#include "textures.cginc"
#include "functions.cginc"
#include "../../../../Scripts/ShaderUtils/AvatarSubmesh.cginc"
#include "UnityGIAvatar.cginc"

struct Light {
  float3 direction;
  float range;

  float3 color;
  float intensity;

  float3 position;
  float innerConeCos;

  float outerConeCos;
  int type;

  float2 padding;
};

static const int LightType_Directional = 0;
static const int LightType_Point = 1;
static const int LightType_Spot = 2;

#if defined(MATERIAL_SPECULARGLOSSINESS) || defined(MATERIAL_METALLICROUGHNESS)
uniform float u_MetallicFactor;
uniform float u_RoughnessFactor;
uniform float4 u_BaseColorFactor;

// AVATAR-SDK BEGIN
uniform float u_F0Factor;   // this factor allows for specular attenuation similar to Babylon.js. It is not originally in the Khronos shader.
#endif

uniform float u_EyeGlintFactor = 10.0;

uniform float u_DiffuseSmoothingFactor = 1.0;   // this factor allows for smooth diffuse light falloff similar to Babylon.js. It is not originally in the Khronos shader.
// AVATAR-SDK END

#ifdef MATERIAL_SPECULARGLOSSINESS
uniform float3 u_SpecularFactor;
uniform float4 u_DiffuseFactor;
uniform float u_GlossinessFactor;
#endif

int u_MipCount;

struct MaterialInfo {
  float perceptualRoughness; // roughness value, as authored by the model creator (input
                             // to shader)
  float3 reflectance0; // full reflectance color (normal incidence angle)

  float alphaRoughness; // roughness mapped to a more linear change in the roughness
                        // (proposed by [2])
  float3 diffuseColor; // color contribution from diffuse lighting

  float3 reflectance90; // reflectance color at grazing angle
  float3 specularColor; // color contribution from specular lighting
};

// Calculation of the lighting contribution from an optional Image Based Light source.
// Precomputed Environment Maps are required uniform inputs and are computed as outlined in [1].
// See our README.md on Environment Maps [3] for additional discussion.
#if defined(USE_IBL) || defined (DEBUG_IBL) || defined (DEBUG_IBL_DIFFUSE) || defined (DEBUG_IBL_SPECULAR)
void getIBLContribution(inout float3 total, inout float3 diffuse, inout float3 specular, MaterialInfo materialInfo, float3 n, float3 v)
{
    float NdotV = clamp(dot(n, v), 0.0, 1.0);
    float3 reflection = normalize(reflect(-v, n));

#ifdef USE_IBL_BRDF_LUT
    float2 brdfSamplePoint = clamp(float2(NdotV, materialInfo.perceptualRoughness), float2(0.0, 0.0), float2(1.0, 1.0));
    // retrieve a scale and bias to F0. See [1], Figure 3
    float2 brdf = tex2D(u_brdfLUT, brdfSamplePoint).rg;
#endif

#ifdef FLIP_IBL_WINDING
    float3 diffuseCoords = float3(-n.x, n.y, n.z);
    float3 specularCoords = float3(-reflection.x, reflection.y, reflection.z);
#else
    float3 diffuseCoords = n;
    float3 specularCoords = reflection;
#endif

    float4 diffuseSample = texCUBE(u_DiffuseEnvSampler, diffuseCoords);

#ifdef USE_IBL_SPECULAR_LOD
    float lod = clamp(materialInfo.perceptualRoughness * float(u_MipCount), 0.0, float(u_MipCount));
    float4 specularSample = texCUBElod(u_SpecularEnvSampler, float4(specularCoords, lod));
#else
    float4 specularSample = texCUBE(u_SpecularEnvSampler, specularCoords);
#endif

    // We must make sure these values are all in Linear color space at this point:
#ifdef USE_HDR
    float3 diffuseLight = diffuseSample.rgb;
    float3 specularLight = specularSample.rgb;
#else
    float3 diffuseLight = SRGBtoLINEAR(diffuseSample).rgb;
    float3 specularLight = SRGBtoLINEAR(specularSample).rgb;
#endif

    diffuse = diffuseLight * materialInfo.diffuseColor;
#ifdef USE_IBL_BRDF_LUT
    specular = specularLight * (materialInfo.specularColor * brdf.x + brdf.y);
#else
    specular = specularLight * (materialInfo.specularColor);
#endif
    total = diffuse + specular;
}
#endif

#if defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL) || defined(DEBUG_SH)
float3 getSHContribution(UnityGIInput giInput, MaterialInfo materialInfo, float occlusion, float metallic, float3 n, float3 v)
{
    AvatarShaderGlobalIllumination gi = GetGlobalIllumination(
      giInput,
      1 - materialInfo.perceptualRoughness, // smoothness
      metallic, // metallic
      occlusion, // occlusion
      materialInfo.diffuseColor, // albedo
      n // normal
    );

    // These values are all in Linear color space at this point:
    float3 diffuseLight = gi.indirect.diffuse.rgb; // diffuse light should already be in the linear space from SH calculations.
    float3 specularLight = gi.indirect.specular.rgb; // specular light should already be in the linear space from exr sampling.

    float3 diffuse = diffuseLight * materialInfo.diffuseColor;
    float3 specular = specularLight * materialInfo.specularColor;
    return diffuse + specular;
}
#endif

// Lambert lighting
// see https://seblagarde.wordpress.com/2012/01/08/pi-or-not-to-pi-in-game-lighting-equation/
float3 diffuse(MaterialInfo materialInfo)
{
    return materialInfo.diffuseColor / M_PI;
}

// The following equation models the Fresnel reflectance term of the spec equation (aka F())
// Implementation of fresnel from [4], Equation 15
float3 specularReflection(MaterialInfo materialInfo, AngularInfo angularInfo)
{
    return materialInfo.reflectance0 + (materialInfo.reflectance90 - materialInfo.reflectance0) * pow(clamp(1.0 - angularInfo.VdotH, 0.0, 1.0), 5.0);
}

// Smith Joint GGX
// Note: Vis = G / (4 * NdotL * NdotV)
// see Eric Heitz. 2014. Understanding the Masking-Shadowing Function in Microfacet-Based BRDFs. Journal of Computer Graphics Techniques, 3
// see Real-Time Rendering. Page 331 to 336.
// see https://google.github.io/filament/Filament.md.html#materialsystem/specularbrdf/geometricshadowing(specularg)
float visibilityOcclusion(MaterialInfo materialInfo, AngularInfo angularInfo)
{
    float NdotL = angularInfo.NdotL;
    float NdotV = angularInfo.NdotV;
    float alphaRoughnessSq = materialInfo.alphaRoughness * materialInfo.alphaRoughness;

    float GGXV = NdotL * sqrt(NdotV * NdotV * (1.0 - alphaRoughnessSq) + alphaRoughnessSq);
    float GGXL = NdotV * sqrt(NdotL * NdotL * (1.0 - alphaRoughnessSq) + alphaRoughnessSq);

    float GGX = GGXV + GGXL;
    if (GGX > 0.0)
    {
        return 0.5 / GGX;
    }
    return 0.0;
}

// The following equation(s) model the distribution of microfacet normals across the area being drawn (aka D())
// Implementation from "Average Irregularity Representation of a Roughened Surface for Ray Reflection" by T. S. Trowbridge, and K. P. Reitz
// Follows the distribution function recommended in the SIGGRAPH 2013 course notes from EPIC Games [1], Equation 3.
float microfacetDistribution(MaterialInfo materialInfo, AngularInfo angularInfo)
{
    float alphaRoughnessSq = materialInfo.alphaRoughness * materialInfo.alphaRoughness;
    float f = (angularInfo.NdotH * alphaRoughnessSq - angularInfo.NdotH) * angularInfo.NdotH + 1.0;
    return alphaRoughnessSq / (M_PI * f * f);
}

float3 getPointShade(float3 pointToLight, MaterialInfo materialInfo, float3 normal, float3 view)
{
    AngularInfo angularInfo = getAngularInfo(pointToLight, normal, view);

    if (angularInfo.NdotL > 0.0 || angularInfo.NdotV > 0.0)
    {
        // Calculate the shading terms for the microfacet specular shading model
        float3 F = specularReflection(materialInfo, angularInfo);
        float Vis = visibilityOcclusion(materialInfo, angularInfo);
        float D = microfacetDistribution(materialInfo, angularInfo);

        // Calculation of analytical lighting contribution
        float3 diffuseContrib = (1.0 - F) * diffuse(materialInfo);
        float3 specContrib = F * Vis * D;

        float3 sumContrib = float3(0.0, 0.0, 0.0);
#ifndef DEBUG_PUNCTUAL_DIFFUSE
        sumContrib += specContrib;
#endif
#ifndef DEBUG_PUNCTUAL_SPECULAR
        sumContrib += diffuseContrib;
#endif

        // Obtain final intensity as reflectance (BRDF) scaled by the energy of the light (cosine law)
        // AVATAR SDK BEGIN
        // return angularInfo.NdotL * (sumContrib);
        return pow(angularInfo.NdotL, u_DiffuseSmoothingFactor) * (sumContrib);
        // AVATAR SDK END
    }

    return float3(0.0, 0.0, 0.0);
}

// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_lights_punctual/README.md#range-property
float getRangeAttenuation(float range, float distance)
{
    if (range <= 0.0)
    {
        // negative range means unlimited
        return 1.0;
    }
    return max(min(1.0 - pow(distance / range, 4.0), 1.0), 0.0) / pow(distance, 2.0);
}

// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_lights_punctual/README.md#inner-and-outer-cone-angles
float getSpotAttenuation(float3 pointToLight, float3 spotDirection, float outerConeCos, float innerConeCos)
{
    float actualCos = dot(normalize(spotDirection), normalize(-pointToLight));
    if (actualCos > outerConeCos)
    {
        if (actualCos < innerConeCos)
        {
            return smoothstep(outerConeCos, innerConeCos, actualCos);
        }
        return 1.0;
    }
    return 0.0;
}

float3 applyDirectionalLight(Light light, MaterialInfo materialInfo, float3 normal, float3 view)
{
    float3 pointToLight = -light.direction;
    float3 shade = getPointShade(pointToLight, materialInfo, normal, view);
    return light.intensity * light.color * shade;
}

float3 applyPointLight(Light light, MaterialInfo materialInfo, float3 normal, float3 view, float3 v_Position)
{
    float3 pointToLight = light.position - v_Position;
    float distance = length(pointToLight);
    float attenuation = getRangeAttenuation(light.range, distance);
    float3 shade = getPointShade(pointToLight, materialInfo, normal, view);
    return attenuation * light.intensity * light.color * shade;
}

float3 applySpotLight(Light light, MaterialInfo materialInfo, float3 normal, float3 view, float3 v_Position)
{
    float3 pointToLight = light.position - v_Position;
    float distance = length(pointToLight);
    float rangeAttenuation = getRangeAttenuation(light.range, distance);
    float spotAttenuation = getSpotAttenuation(pointToLight, light.direction, light.outerConeCos, light.innerConeCos);
    float3 shade = getPointShade(pointToLight, materialInfo, normal, view);
    return rangeAttenuation * spotAttenuation * light.intensity * light.color * shade;
}

Light GetUnityLight(float3 worldPos, float3 lightDir, fixed atten) {
    Light l;
        float3 pos = _WorldSpaceLightPos0;
        float3 toLight = _WorldSpaceLightPos0.xyz - worldPos;
        float3 pointdir = normalize(toLight);
        float attenuation = 1 / (1 + dot(pointdir, pointdir));

        // instead of using the original attentuation algorithm we utilize the common Unity pattern with vertex shader calculation
        // l.type = _WorldSpaceLightPos0.w < 0.5 ? LightType_Directional : LightType_Point;
        l.type = LightType_Directional;

#if defined(DIRECTIONAL)
        l.direction = -lightDir;
        l.range = -1.0;
        l.color = _LightColor0; // includes both the color and amplitude, not normalized
        l.intensity = 5.0;  // 1.0 in Unity is 5klux, also 0.2 intensity in Unity = 1.0 intensity in GLTF, this will be modulated by the color anyways
        l.position = pos;
        l.innerConeCos = 1.0;
        l.outerConeCos = 0.5;
#else
        float3 lightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz;
        float range = length(toLight) / length(lightCoord);

        l.direction = -lightDir;
        l.range = range;
        l.color = _LightColor0 * atten; // includes both the color and amplitude, not normalized
        l.intensity = 5.0;  // 1.0 in Unity is 5klux, also 0.2 intensity in Unity = 1.0 intensity in GLTF, this will be modulated by the color anyways
        l.position = pos;
        l.innerConeCos = 1.0;
        l.outerConeCos = 0.5;
#endif

    return l;
}

// Calculation of punctual lights (directional, point, and spotlights)
// This code is moved here from the original Khronos shader for bookkeeping and
// due to the fact that some of our shader compliers do not support for loops on multiple lights
#ifdef USE_PUNCTUAL
float3 getPunctualContribution(MaterialInfo materialInfo, float3 normal, float3 view, float3 v_Position, float3 worldPos, float3 lightDir, fixed atten)
{
    float3 color = float3(0.0, 0.0, 0.0);

// Unity Forward additive lighting adds light in multiple passes, so one light per pass is fine
// for (int li = 0; li < LIGHT_COUNT; ++li) {

    Light light = GetUnityLight(worldPos, lightDir, atten); // see above for this custom Unity light function, instead of u_Lights[li];
    if (light.type == LightType_Directional) {
        color += applyDirectionalLight(light, materialInfo, normal, view);
    } else if (light.type == LightType_Point) {
        color += applyPointLight(light, materialInfo, normal, view, v_Position);
    } else if (light.type == LightType_Spot) {
        color += applySpotLight(light, materialInfo, normal, view, v_Position);
    }

// }
    return color;
}
#endif

fixed4 frag (v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    // Metallic and Roughness material properties are packed together
    // In glTF, these factors can be specified by fixed scalar values
    // or from a metallic-roughness map
    float perceptualRoughness = 0.0;
    float metallic = 0.0;
    float4 baseColor = float4(0.0, 0.0, 0.0, 1.0);
    float3 diffuseColor = float3(0,0,0);
    float3 specularColor = float3(0,0,0);
    float3 f0 = float3(0.04, 0.04, 0.04);

#ifdef MATERIAL_SPECULARGLOSSINESS

#ifdef HAS_SPECULAR_GLOSSINESS_MAP
    float4 sgSample =
        SRGBtoLINEAR(tex2D(u_SpecularGlossinessSampler, getSpecularGlossinessUV(i.v_UVCoord1, i.v_UVCoord2)));
    perceptualRoughness =
        (1.0 - sgSample.a * u_GlossinessFactor); // glossiness to roughness
    f0 = sgSample.rgb * u_SpecularFactor; // specular
#else
    f0 = u_SpecularFactor;
    perceptualRoughness = 1.0 - u_GlossinessFactor;
#endif // ! HAS_SPECULAR_GLOSSINESS_MAP

// AVATAR SDK BEGIN
    // IMPORTANT: Not originally part of the shader but used for tuning in viewers like Babylon
    f0 *= u_F0Factor;
// AVATAR SDK END

    // f0 = specular
    specularColor = f0;
    float oneMinusSpecularStrength = 1.0 - max(max(f0.r, f0.g), f0.b);
    diffuseColor = baseColor.rgb * oneMinusSpecularStrength;

#ifdef DEBUG_METALLIC
    // do conversion between metallic M-R and S-G metallic
    metallic = solveMetallic(baseColor.rgb, specularColor, oneMinusSpecularStrength);
#endif // ! DEBUG_METALLIC

#endif // ! MATERIAL_SPECULARGLOSSINESS

#ifdef MATERIAL_METALLICROUGHNESS

#ifdef HAS_METALLIC_ROUGHNESS_MAP
    // Roughness is stored in the 'g' channel, metallic is stored in the 'b' channel.
    // This layout intentionally reserves the 'r' channel for (optional) occlusion map
    // data
#ifdef MATERIAL_MODE_TEXTURE
    float4 ormSample = tex2D(u_MetallicRoughnessSampler, getMetallicRoughnessUV(i.v_UVCoord1, i.v_UVCoord2));
    perceptualRoughness = ormSample.g * u_RoughnessFactor;
    metallic = ormSample.b * u_MetallicFactor;
#else
    perceptualRoughness = i.v_ORMT.g * u_RoughnessFactor;
    metallic = i.v_ORMT.b * u_MetallicFactor;
#endif
#else
    metallic = u_MetallicFactor;
    perceptualRoughness = u_RoughnessFactor;
#endif

// AVATAR BEGIN
// In order to match Babylon, Roughness must be squared
//    perceptualRoughness = perceptualRoughness* perceptualRoughness;
// AVATAR END

    // The albedo may be defined from a base texture or a flat color
#if defined(HAS_BASE_COLOR_MAP) && defined(MATERIAL_MODE_TEXTURE)
    baseColor = tex2D(u_BaseColorSampler, getBaseColorUV(i.v_UVCoord1, i.v_UVCoord2)) *
        u_BaseColorFactor; // NOTE: for ease of use of the Unity editor, I moved the u_BaseColorFactor into the SRGBtoLinear conversion.
#else
    baseColor.rgb = i.v_Color.rgb * u_BaseColorFactor.rgb;
    baseColor.a = u_BaseColorFactor.a;
#endif

    // Avatar Fbx Review Tool
#ifdef UNITY_COLORSPACE_GAMMA
    baseColor = SRGBtoLINEAR(PalettizedAlbedo(baseColor));  // this is needed because it seems the standalone integration does not load the basecolor as SRGB as it should be
#else
    baseColor = PalettizedAlbedo(baseColor);
#endif

    // IMPORTANT: Not originally part of the shader but used for tuning in viewers like Babylon
    f0 *= u_F0Factor;

    diffuseColor = baseColor.rgb * (float3(1.0,1.0,1.0) - f0) * (1.0 - metallic);

    specularColor = lerp(f0, baseColor.rgb, metallic);

#endif // ! MATERIAL_METALLICROUGHNESS

#ifdef ALPHAMODE_MASK
    if (baseColor.a < u_AlphaCutoff) {
        discard;
    }
    baseColor.a = 1.0;
#endif

#ifdef ALPHAMODE_OPAQUE
    baseColor.a = 1.0;
#endif

#ifdef MATERIAL_UNLIT
    gl_FragColor = float4(LINEARtoSRGB(baseColor.rgb), baseColor.a);
    return;
#endif

    perceptualRoughness = clamp(perceptualRoughness, 0.0, 1.0);
    metallic = clamp(metallic, 0.0, 1.0);

    // Roughness is authored as perceptual roughness; as is convention,
    // convert to material roughness by squaring the perceptual roughness [2].
    float alphaRoughness = perceptualRoughness * perceptualRoughness;

    // Compute reflectance.
    float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);

    float3 specularEnvironmentR0 = specularColor.rgb;
    // Anything less than 2% is physically impossible and is instead considered to be
    // shadowing. Compare to "Real-Time-Rendering" 4th editon on page 325.
    float reflectanceclamp = clamp(reflectance * 50.0, 0.0, 1.0);
    float3 specularEnvironmentR90 = float3(reflectanceclamp, reflectanceclamp,reflectanceclamp);
// AVATAR BEGIN
// In order for this shader to even come close to Babylon and other off the shelf viewers, the
// specularEnvironmentR90 has to be affected by the specular color. If not, all sort of greyish
// desaturation colors occur.
    specularEnvironmentR90 *= specularColor.rgb;
// AVATAR END

    MaterialInfo materialInfo;
    materialInfo.perceptualRoughness = perceptualRoughness;
    materialInfo.reflectance0 = specularEnvironmentR0;
    materialInfo.alphaRoughness = alphaRoughness;
    materialInfo.diffuseColor = diffuseColor;
    materialInfo.reflectance90 = specularEnvironmentR90;
    materialInfo.specularColor = specularColor;

    // LIGHTING
    float3 color = float3(0.0, 0.0, 0.0);

#ifdef HAS_TANGENTS
    float3 normal = getNormal(i.v_Position, i.v_UVCoord1, i.v_UVCoord2, i.v_Tangent, i.v_Bitangent, i.v_Normal);
#else
    float3 normal = getNormal(i.v_Position, i.v_UVCoord1, i.v_UVCoord2, i.v_Normal);
#endif

    // NOTE: (jsepulveda, 4/3/21)
    // This is the most difficult part to port - the original GLSL code used u_Camera.
    // u_Camera must be in local space, and be calculated from _WorldSpaceCameraPos as:
    float3 localCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0));
    float3 view = normalize(_WorldSpaceCameraPos - i.worldPos); // VERY IMPORTANT: Cannot use i.v_Position here

#if defined(USE_PUNCTUAL) || defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL) || defined(DEBUG_SH)
    fixed atten = LIGHT_ATTENUATION(i); // SPOT/POINT: This gets you the attenuation + shadow value.
#endif

#ifdef USE_PUNCTUAL
#ifdef EYE_GLINTS
    bool useEyeGlint =
        ((i.v_Color.a > (SUBMESH_TYPE_L_EYE - SUBMESH_TYPE_BUFFER) / 255.0) &&
         (i.v_Color.a < (SUBMESH_TYPE_R_EYE + SUBMESH_TYPE_BUFFER) / 255.0));
    [branch]
    if (useEyeGlint)
    {
      // color = float4(0, 1, 1, 1); // useful to debug eye cutout

#ifdef EYE_GLINTS_BEHIND
      // create a second reflected spec light to maintain an eye glint from the backside
      float3 mirroredLightDirection = float3(-i.lightDir.x, i.lightDir.y, -i.lightDir.z);
      materialInfo.diffuseColor = float3(0,0,0); // attenuate all the diffuse part, glint only comes from spec
      color += getPunctualContribution(materialInfo, normal, view, i.v_Position, i.worldPos, mirroredLightDirection, atten);
      materialInfo.diffuseColor = diffuseColor; // restore to original
#endif

      materialInfo.reflectance0 *= u_EyeGlintFactor; // amplify the spec into an eye glint
    }
#endif

   color += getPunctualContribution(materialInfo, normal, view, i.v_Position, i.worldPos, i.lightDir, atten);

    materialInfo.reflectance0 = specularEnvironmentR0; // restore to original
#endif

    // Calculate lighting contribution from image based lighting source (IBL)
#if defined(USE_IBL) || defined (DEBUG_IBL) || defined (DEBUG_IBL_DIFFUSE) || defined (DEBUG_IBL_SPECULAR)
    float3 totalIbl;
    float3 diffuseIbl;
    float3 specularIbl;
    getIBLContribution(totalIbl, diffuseIbl, specularIbl, materialInfo, normal, view);
    color += totalIbl;
#endif

#if defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL) || defined(DEBUG_SH)
    UnityGIInput giInput = GetGlobalIlluminationInput(i, i.lightDir, i.worldPos, view, atten); // the view here is supposed to be worldViewDir
#endif

#if defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL)
    // in what follows, set occlusion to 1 since it is calculated later using our own method
    color += getSHContribution(giInput, materialInfo, 1.0, metallic, normal, view);
#endif

    float ao = 1.0;
    // Apply optional PBR terms for additional (optional) shading
#ifdef HAS_OCCLUSION_MAP
#ifdef MATERIAL_MODE_TEXTURE
#ifdef USE_ORM_EXTENSION
    ao = ormSample.r;
#else
    ao = tex2D(u_OcclusionSampler, getOcclusionUV(i.v_UVCoord1, i.v_UVCoord2)).r;
#endif
#else
    ao = i.v_ORMT.r;
#endif
    color = lerp(color, color * ao, u_OcclusionStrength);
#endif

    float3 emissive = float3(0,0,0);
#ifdef HAS_EMISSIVE_MAP
    emissive = SRGBtoLINEAR(tex2D(u_EmissiveSampler, getEmissiveUV(i.v_UVCoord1, i.v_UVCoord2))).rgb *
        u_EmissiveFactor;
    color += emissive;
#endif

    float4 gl_FragColor;    // local variable for compatibility with source glsl shader

#if DEBUG_DISABLE_FOR_ADDITIVE
    gl_FragColor = float4(0, 0, 0, baseColor.a);

#else

#if !(DEBUG_LIGHTING)

    // regular shading
    gl_FragColor = float4(toneMap(color), baseColor.a);

#else // debug output

#ifdef DEBUG_METALLIC
#ifdef UNITY_COLORSPACE_GAMMA
  gl_FragColor.rgb = float3(metallic, metallic, metallic);
#else
  gl_FragColor.rgb = SRGBtoLINEAR(float3(metallic, metallic, metallic));
#endif
#endif

#ifdef DEBUG_ROUGHNESS
#ifdef UNITY_COLORSPACE_GAMMA
    gl_FragColor.rgb = float3(perceptualRoughness, perceptualRoughness,perceptualRoughness);
#else
    gl_FragColor.rgb = SRGBtoLINEAR(float3(perceptualRoughness, perceptualRoughness,perceptualRoughness));
#endif
#endif

#ifdef DEBUG_NORMAL
#ifdef HAS_TANGENTS
    gl_FragColor.rgb = getNormal(i.v_Position, i.v_UVCoord1, i.v_UVCoord2, i.v_Tangent, i.v_Bitangent, i.v_Normal);
#else
    gl_FragColor.rgb = getNormal(i.v_Position, i.v_UVCoord1, i.v_UVCoord2, i.v_Normal);
#endif
#endif

#ifdef DEBUG_NORMAL_MAP
#ifdef HAS_NORMAL_MAP
    gl_FragColor.rgb = tex2D(u_NormalSampler, getNormalUV(i.v_UVCoord1,i.v_UVCoord2)).rgb;
#else
    gl_FragColor.rgb = float3(0.5, 0.5, 1.0);
#endif
#endif

#ifdef DEBUG_BASECOLOR
#ifdef UNITY_COLORSPACE_GAMMA
    gl_FragColor.rgb = baseColor.rgb;
#else
    gl_FragColor.rgb = LINEARtoSRGB(baseColor.rgb);
#endif
#endif

#ifdef DEBUG_OCCLUSION
#ifdef UNITY_COLORSPACE_GAMMA
    gl_FragColor.rgb = float3(ao, ao, ao);
#else
    gl_FragColor.rgb = SRGBtoLINEAR(float3(ao, ao, ao));
#endif
#endif

#ifdef DEBUG_EMISSIVE
    gl_FragColor.rgb = LINEARtoSRGB(emissive);
#endif

#ifdef DEBUG_F0
    gl_FragColor.rgb = f0;
#endif

#ifdef DEBUG_ALPHA
    gl_FragColor.rgb = float3(baseColor.a, baseColor.a, baseColor.a);
#endif

#ifdef DEBUG_VIEW
    gl_FragColor.rgb = view;
#endif

#ifdef DEBUG_PUNCTUAL
#ifdef USE_PUNCTUAL
    gl_FragColor.rgb = getPunctualContribution(materialInfo, normal, view, i.v_Position, i.worldPos, i.lightDir, atten);
    gl_FragColor = float4(toneMap(gl_FragColor.rgb), gl_FragColor.a);
#endif
#endif

#ifdef DEBUG_PUNCTUAL_SPECULAR
#ifdef USE_PUNCTUAL
    gl_FragColor.rgb = getPunctualContribution(materialInfo, normal, view, i.v_Position, i.worldPos, i.lightDir, atten);
    gl_FragColor = float4(toneMap(gl_FragColor.rgb), gl_FragColor.a);
#endif
#endif

#ifdef DEBUG_PUNCTUAL_DIFFUSE
#ifdef USE_PUNCTUAL
    gl_FragColor.rgb = getPunctualContribution(materialInfo, normal, view, i.v_Position, i.worldPos, i.lightDir, atten);
    gl_FragColor = float4(toneMap(gl_FragColor.rgb), gl_FragColor.a);
#endif
#endif

#ifdef DEBUG_IBL
    gl_FragColor.rgb = totalIbl;
    gl_FragColor = float4(toneMap(gl_FragColor.rgb), gl_FragColor.a);
#endif

#ifdef DEBUG_IBL_DIFFUSE
    gl_FragColor.rgb = diffuseIbl;
    gl_FragColor = float4(toneMap(gl_FragColor.rgb), gl_FragColor.a);
#endif

#ifdef DEBUG_IBL_SPECULAR
    gl_FragColor.rgb = specularIbl;
float3 reflection = normalize(reflect(-view, normal));
float3 specularCoords = float3(-reflection.x, reflection.y, reflection.z);
float lod = clamp(materialInfo.perceptualRoughness * float(u_MipCount), 0.0, float(u_MipCount));
float4 specularSample = texCUBElod(u_SpecularEnvSampler, float4(specularCoords, lod));
gl_FragColor.rgb = specularSample * materialInfo.specularColor; // lerp(f0, baseColor.rgb, metallic);

    gl_FragColor = float4(toneMap(gl_FragColor.rgb), gl_FragColor.a);
#endif

#ifdef DEBUG_SH
    gl_FragColor.rgb = getSHContribution(giInput, materialInfo, 1.0, metallic, normal, view);
    gl_FragColor = float4(toneMap(gl_FragColor.rgb), gl_FragColor.a);
#endif

#ifdef DEBUG_NO_TONE_MAP
    gl_FragColor = float4(color, baseColor.a);
#endif

#ifdef DEBUG_SUBMESHES
    float sudmeshId = i.v_Color.a + (0.5/255.0);
    if (sudmeshId < 1024.0 / 255.0) {
        gl_FragColor.rgb = vec3(1.0, 1.0, 1.0); // earrings       // 10
    }
    if (sudmeshId < 512.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.1, 0.1, 0.1);  // headwear     // 9
    }
    if (sudmeshId < 256.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.2, 0.1, 0.05);  // Facial Hair // 8
    }
    if (sudmeshId < 128.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.5, 0.0, 0.0);  // Lashes       // 7
    }
    if (sudmeshId < 64.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.0, 1.0, 0.0);  // R eye        // 6
    }
    if (sudmeshId < 32.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.0, 0.0, 1.0);  // L eye        // 5
    }
    if (sudmeshId < 16.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.24, 0.19, 0.08);  // brow      // 4
    }
    if (sudmeshId < 8.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.345, 0.27, 0.11);  // hair     // 3
    }
    if (sudmeshId < 4.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.77, 0.65, 0.65); // head       // 2
    }
    if (sudmeshId < 2.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.77, 0.65, 0.65); // body       // 1
    }
    if (sudmeshId < 1.0 / 255.0) {
        gl_FragColor.rgb = vec3(0.2, 0.2, 0.2); // outfit        // 0
    }

#endif

    gl_FragColor.a = 1.0;

#endif // DEBUG_LIGHTING
#endif // DEBUG_DISABLE_FOR_ADDITIVE

    return gl_FragColor;
}
