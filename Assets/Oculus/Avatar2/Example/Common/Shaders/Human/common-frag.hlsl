/////////////////////////////////////////////////////////
// Unity specific defines for Avatar SDK:

#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"
#include "UnityGIAvatar.hlsl"
#include "../../../../Scripts/ShaderUtils/AvatarSubmesh.cginc"

#define MATERIAL_METALLICROUGHNESS
#ifdef MATERIAL_MODE_TEXTURE
#define HAS_BASE_COLOR_MAP
#define HAS_OCCLUSION_MAP
#define HAS_COMBINED_OCCLUSION_MAP
#define HAS_THICKNESS_MAP
#define HAS_COMBINED_THICKNESS_MAP
#define HAS_METALLIC_ROUGHNESS_MAP
#endif

// this is expensive Quest so ensure these textures are present on a high quality profile
//#define HAS_NORMAL_MAP
//#define HAS_EMISSIVE_MAP

#define HAS_NORMAL_VEC3
// #define HAS_SPECULAR_GLOSSINESS_MAP
// #define HAS_DIFFUSE_MAP
// #define HAS_SPECULAR_GLOSSINESS_MAP

// Turn on Punctual here

#if !defined(LIGHTING_MODE_SH_ONLY) && !defined(LIGHTING_MODE_IBL_ONLY)
#define USE_PUNCTUAL
#endif

// Turn on the Composite methods here:
#define FB_MATERIALS_SKIN
#define EYE_GLINTS
#define EYE_GLINTS_BEHIND

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

//This should be moved to a common path so that more than one shader can use it.
//#include "AvatarFbxReview.cginc"

/////////////////////////////////////////////////////////
// Original ported code from pbr.frag:

#if defined(FB_MATERIALS_SKIN)
#define SKIN 1
#endif

#if defined(FB_MATERIALS_HAIR_STRAIGHT)
#define HAIR_STRAIGHT 1
#endif

#if defined(FB_MATERIALS_HAIR_COILY)
#define HAIR_COILY 1
#endif

// -----------------------------------------------------------------------------------
// Constants
// -----------------------------------------------------------------------------------
static const float_t GAMMA = 2.2;
static const float_t INV_GAMMA = 1.0 / GAMMA;
static const float_t PI = 3.141592653589793;
static const float_t PI_2 = 2.0 * PI;

// -----------------------------------------------------------------------------------
// STRUCTURES
// -----------------------------------------------------------------------------------

// KHR_lights_punctual extension.
// see https://github.com/KhronosGroup/glTF/tree/master/extensions/2.0/Khronos/KHR_lights_punctual
static const int LightType_Directional = 0;
static const int LightType_Point = 1;
static const int LightType_Spot = 2;

struct Light {
    vec3_t direction;
    float_t range;
    vec3_t color;
    float_t intensity;
    vec3_t position;
    float_t innerConeCos;
    float_t outerConeCos;
    int type; // LightType_
};

// NOTE: instead use OvrDefaultAppdata
// input to vertex shader
//struct meshdata {
//    vec4_t vertex;
//    vec4_t color;
//    vec3_t normal;
//    vec3_t tangent;
//    vec2_t texcoord;
//    vec2_t texcoord1;
//};

// -----------------------------------------------------------------------------------
// uniforms
// -----------------------------------------------------------------------------------

// camera
uniform vec3_t u_Camera; // camera position in world.

// Only used in the former vert shader.
// Now we macros from Avatar Custom and Unity functions like UnityObjectToWorldNormal().
// matricies
// uniform float4x4 u_ViewProjectionMatrix;
// uniform float4x4 u_ModelMatrix;
// uniform float4x4 u_NormalMatrix;

// Replaced by Unity lights...
// LIGHT_COUNT +1 in case LIGHT_COUNT = 0
//layout (std140) uniform u_LightArray {
//    Light u_Lights[LIGHT_COUNT + 1];
//};

// Originally require ALPHAMODE_MASK to work
// base GLTF
uniform float_t u_AlphaCutoff;

#ifdef HAS_NORMAL_MAP
uniform sampler2D u_NormalSampler;
#endif
uniform float_t u_NormalScale;

#ifdef HAS_EMISSIVE_MAP
uniform sampler2D u_EmissiveSampler;
#endif
uniform vec3_t u_EmissiveFactor;

#ifdef HAS_OCCLUSION_MAP
uniform sampler2D u_OcclusionSampler;
#endif
uniform float_t u_OcclusionStrength;

#ifdef HAS_THICKNESS_MAP
uniform sampler2D u_ThicknessSampler;
#endif
uniform float_t u_ThicknessFactor;
uniform vec3_t u_SubsurfaceColor;
uniform vec3_t u_SkinORMFactor;

#ifdef HAS_METALLIC_ROUGHNESS_MAP
uniform sampler2D u_MetallicRoughnessSampler;
#endif
#ifdef HAS_BASE_COLOR_MAP
uniform sampler2D u_BaseColorSampler;
#endif
uniform float_t u_MetallicFactor;
uniform float_t u_RoughnessFactor;
uniform vec4_t u_BaseColorFactor;

// IBL
uniform samplerCUBE u_GGXEnvSampler;
uniform samplerCUBE u_LambertianEnvSampler;
// uniform sampler2D u_GGXLUT;  // the original GGX calclulation involved filtering the result through this LUT but it is overily expensive (and noisy) in VR
int u_MipCount; // specific to Unity, must be manually set to the mip count of the diffuse Lambertian sampler cubemap

// TONE MAPPING
uniform float_t u_Exposure;


// hair stuff.
#if defined(HAS_FLOW_MAP)
uniform sampler2D u_FlowSampler;
#endif

#ifdef HAIR_STRAIGHT
uniform vec3_t u_SpecularColorFactor;
uniform float_t u_SpecularWhiteIntensity;
uniform float_t u_SpecularShiftIntensity;
#endif

#ifdef HAIR_COILY
uniform vec3_t u_SpecularColorFactor;
uniform float_t u_SpecularIntensity;
uniform float_t u_FresnelPower;
uniform float_t u_FresnelOffset;
#endif

#ifdef EYE_GLINTS
uniform float_t u_EyeGlintFactor = 1.0;
uniform float_t u_EyeGlintColorFactor = 1.0;
#endif

// -----------------------------------------------------------------------------------
vec3_t linearTosRGB(vec3_t color) {
    return pow(color, vec3_t(INV_GAMMA, INV_GAMMA, INV_GAMMA));
}

// -----------------------------------------------------------------------------------
vec3_t sRGBToLinear(vec3_t srgbIn) {
    return pow(srgbIn, vec3_t(GAMMA, GAMMA, GAMMA));
}

// -----------------------------------------------------------------------------------
vec4_t sRGBToLinear(vec4_t srgbIn) {
    return vec4_t(sRGBToLinear(srgbIn.xyz), srgbIn.w);
}

// -----------------------------------------------------------------------------------
float_t saturate(float_t x) {
    return clamp(x, 0.0, 1.0);
}

// -----------------------------------------------------------------------------------
vec3_t saturate(vec3_t v)  {
    return clamp(v, 0.0, 1.0);
}

// -----------------------------------------------------------------------------------
float_t fastPow(float_t a, float_t b) {
    return a / ((1. - b) * a + b);
}

// -----------------------------------------------------------------------------------
vec3_t sampleTexCube(samplerCUBE cube, vec3_t normal, float_t mip) {
//    return textureLod(cube, normal, mip).rgb;  // might also use texCUBElod or SAMPLE_TEXTURECUBE_LOD
    return texCUBElod(cube, vec4_t(normal, mip));
}

// -----------------------------------------------------------------------------------
// material support
// -----------------------------------------------------------------------------------

// -----------------------------------------------------------------------------------
vec3_t getEmissive(vec2_t uv) {
    vec3_t e = vec3_t(0,0,0);
#ifdef HAS_EMISSIVE_MAP
    e = u_EmissiveFactor * tex2D(u_EmissiveSampler, uv).rgb;
#endif
    return e;
}

// -----------------------------------------------------------------------------------
vec4_t getBaseColor(interpolators i) {

    // Sample Base Color Map
    vec2_t uv = i.texcoord.xy;
    vec4_t baseColor = vec4_t(0.0f, 0.0f, 0.0f, 1.0f);

#if defined(MATERIAL_METALLICROUGHNESS)
#if defined(MATERIAL_MODE_TEXTURE) && defined(HAS_BASE_COLOR_MAP)
    baseColor = tex2D(u_BaseColorSampler, uv);
#else
    // we're using the vertexColor.a as a means to transmit the sub mesh type
    baseColor.rgb = i.color.rgb;
#endif
#endif

#if defined(MATERIAL_SPECULARGLOSSINESS)
    baseColor *= u_DiffuseFactor;
#elif defined(MATERIAL_METALLICROUGHNESS)
    baseColor *= u_BaseColorFactor;
#endif

    return baseColor;
}


// -----------------------------------------------------------------------------------
// IMAGE BASED LIGHTING
// -----------------------------------------------------------------------------------

// -----------------------------------------------------------------------------------
// NOTE: Moved from vertex shader. texCUBE is supposed to work in Unity with #pragma glsl but it doesn't work
vec3_t getIblDiffuseSample(vec3_t n) {
#if !defined(USE_IBL_DIFFUSE)
    return vec3_t(0.0, 0.0, 0.0);
#else

#ifdef FLIP_IBL_WINDING
    vec3_t diffuseCoords = vec3_t(-n.x, n.y, n.z);
#else
    vec3_t diffuseCoords = n;
#endif

    vec3_t result = texCUBE(u_LambertianEnvSampler, diffuseCoords).rgb;
    // input gamma set by Unity Samplers
    //#if !defined(USE_IBL_DIFFUSE_HDR)
    //    result = sRGBToLinear(result);
    //#endif
    return result;
#endif
}

// -----------------------------------------------------------------------------------
vec3_t getIblSpecularSample(vec3_t reflection, float_t roughness) {
#if !defined(USE_IBL_SPECULAR)
    return vec3_t(0.0, 0.0, 0.0);
#else
// original code, not so good because they subtract 1 to account for sampling over cube face bug...
    // skip lower mip levels they don't work well for lighting with SDR textures
    // float_t lod = roughness * float_t(IBL_MIPCOUNT - 1);

    float_t lod = clamp(roughness * float_t(u_MipCount), 0.0, float_t(u_MipCount));
#ifdef FLIP_IBL_WINDING
    vec3_t specularCoords = vec3_t(-reflection.x, reflection.y, reflection.z);
#else
    vec3_t specularCoords = reflection;
#endif

//    vec3_t result = sampleTexCube(u_GGXEnvSampler, n, lod);
    vec3_t result = sampleTexCube(u_GGXEnvSampler, specularCoords, lod);

    // input gamma set by Unity Samplers
    //#if !defined(USE_IBL_SPECULAR_HDR)
    //    result = sRGBToLinear(result);
    //#endif
    return result;
#endif //USE_IBL_SPECULAR
}

// -----------------------------------------------------------------------------------
// SPHERICAL HARMONIC (SH) LIGHTING
// -----------------------------------------------------------------------------------

#if defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL) || defined(DEBUG_SH)
void getSHContribution(out float3 diffuse, out float3 specular, UnityGIInput giInput, float3 diffuseColor, float3 specularColor, float roughness, float occlusion, float metallic, float3 n, float3 v)
{
    AvatarShaderGlobalIllumination gi = GetGlobalIllumination(
        giInput,
        1 - roughness, // smoothness
        metallic, // metallic
        occlusion, // occlusion
        diffuseColor, // albedo
        n // normal
    );

    // These values are all in Linear color space at this point:
    diffuse = gi.indirect.diffuse.rgb; // diffuse light should already be in the linear space from SH calculations.
    specular = gi.indirect.specular.rgb; // specular light should already be in the linear space from exr sampling.
}
#endif

// -----------------------------------------------------------------------------------
// PUNCTUAL LIGHTING
// -----------------------------------------------------------------------------------
// NOTE: Swiftshader has issues indexing into the uniform array ( in this case the light array)
// from a function that is passed a int variable. So instead of having functions, we use the
// pre-processor.

#define LIGHT_DIRECTION(idx_) vec3_t(-u_Lights[idx_].direction)

// Unity generates the light color by multiplying it with the light intensity.
// Divide by PI is to convert radiometric value to Lambertian
// It's typically done in the brdf diffuse and brdf specular calculations
// but we also would need it for the soft diffuse so we just do it here
#define LIGHT_COLOR(idx_) vec3_t(u_Lights[idx_].color * u_Lights[idx_].intensity * (1./PI))

// -----------------------------------------------------------------------------------
// TONEMAPPING
// -----------------------------------------------------------------------------------
// sRGB => XYZ => D65_2_D60 => AP1 => RRT_SAT
static const float3x3 ACESInputMat = float3x3 (
    0.59719, 0.07600, 0.02840,
    0.35458, 0.90834, 0.13383,
    0.04823, 0.01566, 0.83777
);


// ODT_SAT => XYZ => D60_2_D65 => sRGB
static const float3x3 ACESOutputMat = float3x3 (
    1.60475, -0.10208, -0.00327,
    -0.53108,  1.10813, -0.07276,
    -0.07367, -0.00605,  1.07602
);

// -----------------------------------------------------------------------------------
// ACES tone map (faster approximation)
// see: https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
vec3_t toneMapACES_Narkowicz(vec3_t color) {
    const float_t A = 2.51;
    const float_t B = 0.03;
    const float_t C = 2.43;
    const float_t D = 0.59;
    const float_t E = 0.14;
    return clamp((color * (A * color + B)) / (color * (C * color + D) + E), 0.0, 1.0);
}

// -----------------------------------------------------------------------------------
// ACES filmic tone map approximation
// see https://github.com/TheRealMJP/BakingLab/blob/master/BakingLab/ACES.hlsl
vec3_t RRTAndODTFit(vec3_t color) {
    vec3_t a = color * (color + 0.0245786) - 0.000090537;
    vec3_t b = color * (0.983729 * color + 0.4329510) + 0.238081;
    return a / b;
}

// -----------------------------------------------------------------------------------
vec3_t toneMapACES_Hill(vec3_t color) {
    color = mul(color, ACESInputMat);
    color = RRTAndODTFit(color);
    color = mul(color, ACESOutputMat);
    color = clamp(color, 0.0, 1.0);
    return color;
}

// -----------------------------------------------------------------------------------
vec3_t toneMap(vec3_t color) {
    color *= u_Exposure;

#ifdef TONEMAP_ACES_NARKOWICZ
    color = toneMapACES_Narkowicz(color);
#endif

#ifdef TONEMAP_ACES_HILL
    color = toneMapACES_Hill(color);
#endif

#ifdef TONEMAP_ACES_HILL_EXPOSURE_BOOST
    color /= 0.6;
    color = toneMapACES_Hill(color);
#endif

#ifdef UNITY_COLORSPACE_GAMMA
    return linearTosRGB(color); // IMPORTANT: Use when in Unity GAMMA rendering mode
#else
    return color; // IMPORTANT: Use when in Unity LINEAR rendering mode
#endif
}


float_t ComputeSpecular(vec3_t worldSpaceLightDir, vec3_t worldViewDir, vec3_t shadingNormal, float_t NdotL, float_t NdotV, float_t roughPow2, float_t roughPow4, float_t invRoughPow4) {
    vec3_t h = normalize(worldSpaceLightDir - worldViewDir);
    float_t NdotH = saturate(dot(shadingNormal, h));
    float_t ggx = NdotL * sqrt(NdotV * NdotV * invRoughPow4 + roughPow2) + NdotV * sqrt(NdotL * NdotL * invRoughPow4 + roughPow2);
    ggx = ggx > 0. ? .5 / ggx : 0.;
    // Implementation from "Average Irregularity Representation of a Roughened Surface for Ray Reflection" by T. S. Trowbridge, and K. P. Reitz
    float_t t = 1./(1. - NdotH * NdotH * invRoughPow4);
    return NdotL * t * t * roughPow4 * ggx;
}

// -----------------------------------------------------------------------------------
// HAIR_STRAIGHT
// -----------------------------------------------------------------------------------

#if defined(HAIR_STRAIGHT)
// -----------------------------------------------------------------------------------
vec3_t ComputeHairSpecular(vec3_t lightVector, vec3_t worldViewDir, vec3_t tangent, vec3_t normal, float_t roughness, float_t metallic, vec3_t hairColor) {
    float_t nonMetallic = 1.0 - metallic;
    float_t smoothness = 1.0 - roughness + 0.00001;        // smoothness of 0 causes problems with pow function
    vec3_t t1 = tangent;
    vec3_t t2 = t1 + normal * 0.2;
    vec3_t floatAngleVector = normalize(worldViewDir - lightVector);
    float_t cosA1 = abs(dot(floatAngleVector, t1));
    float_t cosA2 = abs(dot(floatAngleVector, t2));
    float_t whiteSpecular = fastPow(max(1. - cosA2 * cosA2, 0.), 100. * smoothness) * u_SpecularWhiteIntensity;
    vec3_t coloredSpecular = u_SpecularColorFactor * fastPow(max(1. - cosA1 * cosA1, 0.), 100. * smoothness) * metallic * hairColor;
    return (whiteSpecular + coloredSpecular) * (smoothness + .5);
}
#endif

// -----------------------------------------------------------------------------------
void getORMT(in vec2_t uv, in vec4_t ormt, inout float_t occlusion, inout float_t roughness, inout float_t metallic, inout float_t thickness , in float_t subMeshType) {
#if defined(HAS_METALLIC_ROUGHNESS_MAP) && defined(MATERIAL_MODE_TEXTURE)
    vec4_t metRough = tex2D(u_MetallicRoughnessSampler, uv);
    roughness =  metRough.g * u_RoughnessFactor;
    metallic = metRough.b * u_MetallicFactor;
#elif defined(MATERIAL_MODE_VERTEX)
    roughness = ormt.g * u_RoughnessFactor;
    metallic = ormt.b * u_MetallicFactor;
#endif

#if defined(HAS_COMBINED_OCCLUSION_MAP) && defined(MATERIAL_MODE_TEXTURE)
    occlusion = metRough.r * u_OcclusionStrength;
#elif defined(HAS_OCCLUSION_MAP) && defined(MATERIAL_MODE_TEXTURE)
    occlusion = tex2D(u_OcclusionSampler,  uv).r;
    occlusion *= u_OcclusionStrength;
#elif defined(MATERIAL_MODE_VERTEX)
    occlusion = ormt.r * u_OcclusionStrength;
#endif

    // NOTE: When we start using hair we may need to modify this ORM by hair fade value
    //       In that case we'll have to include: defined(HAIR_STRAIGHT) || defined(HAIR_COILY)
#if defined(SKIN)
    bool useSkin = ((subMeshType > (SUBMESH_TYPE_BODY - SUBMESH_TYPE_BUFFER) / 255.0) && (subMeshType < (SUBMESH_TYPE_HEAD + SUBMESH_TYPE_BUFFER) / 255.0));
    [branch]
    if (useSkin)
    {
        occlusion *= u_SkinORMFactor.r;
        roughness *= u_SkinORMFactor.g;
        metallic *= u_SkinORMFactor.b;
    }
#endif

#if defined(HAS_COMBINED_THICKNESS_MAP) && defined(MATERIAL_MODE_TEXTURE)
    thickness = metRough.a * u_ThicknessFactor;
#elif defined(HAS_THICKNESS_MAP) && defined(MATERIAL_MODE_TEXTURE)
    thickness = tex2D(u_ThicknessSampler,  uv).a;
    thickness *= u_ThicknessFactor;
#elif defined(MATERIAL_MODE_VERTEX)
    thickness = ormt.a * u_ThicknessFactor;
#endif

    // DO NOT SUBMIT, JUST A TEST
    //bool useBody = ((subMeshType > (SUBMESH_TYPE_BODY - SUBMESH_TYPE_BUFFER) / 255.0) && (subMeshType < (SUBMESH_TYPE_BODY + SUBMESH_TYPE_BUFFER) / 255.0));
    //if (useSkin)
    //{
    //    thickness = 0.25;
    //    thickness *= u_ThicknessFactor;
    //}
}


// -----------------------------------------------------------------------------------
// Get normal, tangent and bitangent vectors.
void getTBN(interpolators i, out vec3_t t, out vec3_t b, out vec3_t n) {
    vec2_t UV = i.texcoord;
    vec3_t pos = i.vertex.xyz;

    // Compute geometric normal
#ifdef HAS_NORMAL_VEC3
    vec3_t ng = normalize(i.normal);   // should be normalized from vert shader
#else
    vec3_t ng = normalize(cross(ddx(pos), ddy(pos)));
#endif

    // Compute tangent & bitangent
#ifdef HAS_TANGENT_VEC4
    t = normalize(i.tangent);      // should be normalized from vert shader
    b = normalize(i.bitangent);    // should be normalized from vert shader
#else
    vec3_t uv_dx = ddx(vec3_t(UV.x, UV.y, 0.0));
    vec3_t uv_dy = ddy(vec3_t(UV.x, UV.y, 0.0));
    vec3_t t_ = (uv_dy.y * ddx(pos) - uv_dx.y * ddy(pos)) /
    (uv_dx.x * uv_dy.y - uv_dy.x * uv_dx.y);
    t = normalize(t_ - ng * dot(ng, t_));
    b = cross(ng, t);
#endif

    // Compute pertubed normal
#ifdef HAS_NORMAL_MAP
        n = tex2D(u_NormalSampler, UV).rgb * 2.0 - vec3_t(1,1,1);
        n *= vec3_t(u_NormalScale, u_NormalScale, 1.0);
        n = mul(float3x3(t, b, ng), normalize(n));
#else
        n = ng;
#endif

}

// -----------------------------------------------------------------------------------
// FRAGMENT SHADER
// -----------------------------------------------------------------------------------
vec4_t frag(interpolators i) : SV_Target {

    float_t subMeshType = i.color.a;

    // Sample BaseColor Map
    vec4_t baseColor = getBaseColor(i);

    // fast path for unlit materials
#ifdef MATERIAL_UNLIT
    return vec4_t(linearTosRGB(baseColor.rgb), baseColor.a);
#endif

    // Basics
    vec3_t worldPos = i.worldPos;
//    vec3_t worldViewDir = -normalize(i.viewDir);
    vec3_t worldViewDir = -normalize(_WorldSpaceCameraPos - i.worldPos); // VERY IMPORTANT: Cannot use i.position here

    // Sample ORMT Map
    float_t occlusion = 1.0;
    float_t roughness = 1.0;
    float_t metallic = 0.0;
    float_t thickness = 1.0;
    getORMT(i.texcoord, i.ormt, occlusion, roughness, metallic, thickness, subMeshType);

    // Tangent, Bitangent, Normal
    vec3_t shadingTangent;
    vec3_t shadingBitangent;
    vec3_t shadingNormal;
    getTBN(i, shadingTangent, shadingBitangent, shadingNormal);

    // setup some constants
    // standard dielectric values
    const float_t f0 = .04;
    const float_t f90 = 1.0;
    const float_t InvExposure = 1.0 / u_Exposure;
    float_t alpha = baseColor.a;
    float_t nonMetallic = 1.0 - metallic;
    float_t thinness = 1.0 - thickness + 0.00001;
    float_t ao = lerp(1., occlusion, u_OcclusionStrength);
    vec3_t emissive = getEmissive(i.texcoord);
    float_t specularWeight = 1.0;

    // flow map, if available
    // DirectionAngle=r, Fade=g, Power=b, Shift=a
#if defined(HAS_FLOW_MAP)
    vec4_t flowMap = tex2D(u_FlowSampler, i.texcoord);

    // this is how flowAngle will arrive from the pipeline, a value from [0,1]:
    // float_t hairFlowR = atan(hairFlow.y, hairFlow.x) / PI_2;

    // taking the single value and converting it back to vector:
    float_t flowAngle = flowMap.x * PI_2;
    vec2_t hairFlow;
    hairFlow.x = cos(flowAngle);
    hairFlow.y = sin(flowAngle);

    float_t hairFade = flowMap.y;
    float_t hairSpecularPower = flowMap.b;
    float_t hairShift = flowMap.a;
#endif

    // reflection and lighting vectors
//    vec3_t reflectionVector = worldViewDir - 2.0 * shadingNormal * dot(worldViewDir, shadingNormal);
    vec3_t reflectionVector = normalize(reflect(worldViewDir, shadingNormal));

    float_t NdotV = saturate(-dot(shadingNormal, worldViewDir));

    // compute a fresnel term used to brighten specular reflections near grazing angles
    // Schlick approximation except we use NdotV instead of VdotH so we can use Fresnel value for both IBL and punctual light
    float_t f = (f0 + (f90 - f0) * pow(saturate(dot(shadingNormal, worldViewDir)), 5.0)) * nonMetallic + metallic;

#if defined(HAIR_STRAIGHT)
    // give hair less of fresnel affect than other surfaces
    float_t hairFresnel2 = f * f;
    f = lerp(f,f * .5 + .5,hairFade);
#endif

#if defined(HAIR_COILY)
    float_t hairFresnel2 = saturate((0.5 + u_FresnelOffset) + dot(shadingNormal, worldViewDir));
    f = fastPow(hairFresnel2, u_FresnelPower);
#endif

    // calculate lighting
    vec3_t p_diffuse = vec3_t(0, 0, 0);
    vec3_t p_specular = vec3_t(0, 0, 0);
    vec3_t ambient_diffuse = vec3_t(0, 0, 0);
    vec3_t ambient_specular = vec3_t(0, 0, 0);

    // compute some brdf terms that are constant for every light
    float_t r2 = roughness * roughness;
    float_t r4 = r2 * r2;
    float_t invR4 = 1. - r4;

#if defined(HAIR_STRAIGHT)
    // hair specific lighting
    vec3_t hairTangent = shadingTangent*hairFlow.x + shadingBitangent * hairFlow.y;
    hairTangent += shadingNormal * (shadingNormal.x * hairFlow.x + shadingNormal.y * hairFlow.y) * .5;
    hairTangent += shadingNormal * ((hairShift - 0.5) * u_SpecularShiftIntensity);

    // IBL
#if defined(USE_IBL_DIFFUSE)
    ambient_diffuse = getIblDiffuseSample(shadingNormal);
#endif
    vec3_t regularSpec = getIblSpecularSample(reflectionVector, roughness);
    vec3_t hairSpec = ComputeHairSpecular(i.indirectSpecularVector, worldViewDir, hairTangent, shadingNormal, roughness, metallic, baseColor.rgb) * ambient_diffuse;
#if defined(USE_IBL_SPECULAR)
    ambient_specular = lerp(regularSpec,hairSpec,hairFade);
#endif

#if defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL) || defined(DEBUG_SH)
    fixed atten = LIGHT_ATTENUATION(i); // SPOT/POINT: This gets you the attenuation + shadow value.
    UnityGIInput giInput = GetGlobalIlluminationInput(i, i.lightDir, i.worldPos, -worldViewDir, atten); // the view here is supposed to be worldViewDir
    getSHContribution(ambient_diffuse, ambient_specular, giInput, baseColor, baseColor, roughness, occlusion, metallic, shadingNormal, worldViewDir)
#endif

#if defined(USE_PUNCTUAL)
    // punctual
    //for (int lightIdx = 0; lightIdx < LIGHT_COUNT; ++lightIdx) {
    //    vec3_t worldSpaceLightDir = LIGHT_DIRECTION(lightIdx);
    //    vec3_t directionalLightColor = LIGHT_COLOR(lightIdx) * InvExposure;
        vec3_t worldSpaceLightDir = i.lightDir;
        vec3_t directionalLightColor = _LightColor0 * InvExposure;

        float_t NdotL = saturate(dot(worldSpaceLightDir, shadingNormal));

        p_diffuse += NdotL * directionalLightColor;

        vec3_t hairPunctualSpec = ComputeHairSpecular(worldSpaceLightDir, worldViewDir, hairTangent, shadingNormal, roughness, metallic, baseColor.rgb);
        float_t punctualSpec = ComputeSpecular(worldSpaceLightDir, worldViewDir, shadingNormal, NdotL, NdotV, r2, r4, invR4);
        p_specular += lerp(vec3_t(punctualSpec, punctualSpec, punctualSpec),hairPunctualSpec, hairFade) * directionalLightColor;
    //}
#endif

#else
    // non hair lighting.
    // IBL
#if defined(USE_IBL_DIFFUSE)
    ambient_diffuse = getIblDiffuseSample(shadingNormal);
#endif
#if defined(USE_IBL_SPECULAR)
    ambient_specular = getIblSpecularSample(reflectionVector, roughness);
#endif

#if defined(USE_SH_PER_VERTEX) || defined(USE_SH_PER_PIXEL) || defined(DEBUG_SH)
    fixed atten = LIGHT_ATTENUATION(i); // SPOT/POINT: This gets you the attenuation + shadow value.
    UnityGIInput giInput = GetGlobalIlluminationInput(i, i.lightDir, i.worldPos, -worldViewDir, atten); // the view here is supposed to be worldViewDir
    vec3_t sh_diffuse = vec3_t(0, 0, 0);
    vec3_t sh_specular = vec3_t(0, 0, 0);
    getSHContribution(sh_diffuse, sh_specular, giInput, baseColor, baseColor, roughness, occlusion, metallic, shadingNormal, worldViewDir);
    ambient_diffuse += sh_diffuse;
    ambient_specular += sh_specular;
#endif

    // punctual lighting
#if defined(USE_PUNCTUAL)
    //for (int lightIdx = 0; lightIdx < LIGHT_COUNT; ++lightIdx) {
    //    vec3_t worldSpaceLightDir = LIGHT_DIRECTION(lightIdx);
    //    vec3_t directionalLightColor = LIGHT_COLOR(lightIdx) * InvExposure;
        vec3_t worldSpaceLightDir = i.lightDir;
        vec3_t directionalLightColor = _LightColor0 * InvExposure;

        float_t NdotL = saturate(dot(worldSpaceLightDir, shadingNormal));

        float_t punctualSpec = ComputeSpecular(worldSpaceLightDir, worldViewDir, shadingNormal, NdotL, NdotV, r2, r4, invR4);

        // specular without the fresnel term (It gets multiplied later to total specular)
        p_diffuse += NdotL * directionalLightColor;
        p_specular += punctualSpec * directionalLightColor;
    //}
#endif

#endif

    vec3_t totalDiffuse = ambient_diffuse + p_diffuse;
    vec3_t totalSpecular = ambient_specular + p_specular;

#ifdef HAS_FB_SPHERE_MAP
    vec3_t sphereMapSpecular  = getEnvironmentSphereMap(shadingNormal, worldViewDir, roughness, vec3_t(f0, f0, f0), specularWeight);
    // ugh. the sphere map specular is being drowned out for some reason,
    // so this hardcoded scalar is a hack to bring it back up to match V03 look.
    totalSpecular += sphereMapSpecular * 12.0;
#endif

    // hair modifies the specular contribution.
#if defined(HAIR_STRAIGHT)
    totalSpecular *= lerp(1.0,hairSpecularPower,hairFade);
#endif
#if defined(HAIR_COILY)
    totalSpecular *= u_SpecularColorFactor.rgb * alpha * u_SpecularIntensity;
#endif

    // cacl diffuse, specular, subsurface.
    vec3_t diffuseColor = totalDiffuse * ao * nonMetallic * baseColor.rgb;
    vec3_t specularColor = (nonMetallic + baseColor.rgb * metallic) * totalSpecular * f * ao;
    vec3_t subsurfaceColor = vec3_t(0,0,0);

    // only human materials should have subsurface scattering
    // note that SSS is not fully energy conserving, but that would take the baseColor to also include
    // the SSS contribution which is difficult for the artists to do

    // this is a potentially 'more correct' calculation of diffuseColor
    // by factoring in the light that is NOT being scattered ala ( 1.0 - u_SubsurfaceColor )
    //diffuseColor = totalDiffuse * (1.0 - u_SubsurfaceColor * .5 * thinness) * ao * nonMetallic * baseColor.rgb;

#if defined(SKIN)
    subsurfaceColor = ambient_diffuse * u_SubsurfaceColor * thinness * saturate(ao + .4) * baseColor.rgb;
#endif

#if defined(HAIR_STRAIGHT) || defined(HAIR_COILY)
    subsurfaceColor = lerp(vec3_t(0,0,0), ambient_diffuse * u_SubsurfaceColor * thinness * saturate(ao + .4) * baseColor.rgb, hairFade);
#endif

#if !defined(SKIN) && !defined(HAIR_STRAIGHT) && !defined(HAIR_COILY)
// Manipulate the specular behavior to better match the v3 shader for clothing.
// Adhoc fresnel for not hair/skin which modulates the glancing specular by the roughness.
    float_t fc = lerp(f0, lerp(.8, .1, roughness), pow(1.0 - saturate(dot(shadingNormal, -worldViewDir)), 5.0));
    vec3_t fcnonmetallic = nonMetallic * vec3_t(fc, fc, fc);
    specularColor = (baseColor.rgb * metallic + fcnonmetallic) * ao * totalSpecular;
#endif

#ifdef USE_PUNCTUAL
#ifdef EYE_GLINTS
    bool useEyeGlint =
        ((subMeshType > (SUBMESH_TYPE_L_EYE - SUBMESH_TYPE_BUFFER) / 255.0) &&
         (subMeshType < (SUBMESH_TYPE_R_EYE + SUBMESH_TYPE_BUFFER) / 255.0));
    [branch]
    if (useEyeGlint)
    {
        // specularColor += vec4_t(0, 1, 1, 1); // useful to debug eye cutout

#ifdef EYE_GLINTS_BEHIND
      // create a second reflected spec light to maintain an eye glint from the backside
        vec3_t mirroredLightDirection = vec3_t(-i.lightDir.x, i.lightDir.y, -i.lightDir.z);
        float_t mirroredNdotL = saturate(dot(mirroredLightDirection, shadingNormal));

///        materialInfo.diffuseColor = vec3_t(0, 0, 0); // attenuate all the diffuse part, glint only comes from spec
        float_t rpunctualSpec = ComputeSpecular(mirroredLightDirection, worldViewDir, shadingNormal, mirroredNdotL, NdotV, r2, r4, invR4);
        vec3_t rp_specular = rpunctualSpec * directionalLightColor;
        specularColor += u_EyeGlintFactor * lerp(rpunctualSpec, rp_specular, u_EyeGlintColorFactor);
///        materialInfo.diffuseColor = diffuseColor; // restore to original
#endif

///        materialInfo.reflectance0 *= u_EyeGlintFactor; // amplify the spec into an eye glint

        specularColor += u_EyeGlintFactor * lerp(punctualSpec, p_specular, u_EyeGlintColorFactor);
    }
#endif

///    materialInfo.reflectance0 = specularEnvironmentR0; // restore to original
#endif

    // calc final color
    vec3_t finalColor = vec3_t(0,0,0);
    finalColor += diffuseColor;
    finalColor += specularColor;
    finalColor += subsurfaceColor;

#ifdef HAS_EMISSIVE_MAP
    finalColor += emissive;
#endif

// NORMAL OUTPUT
#if !(DEBUG_LIGHTING)
    finalColor = toneMap(finalColor);
// finalColor.rgb - vec3_t(1,1,0);

    return vec4_t(finalColor.rgb, alpha);
#endif

// DEBUGGING OUTPUT
    finalColor = vec3_t(0,0,0);
#ifdef DEBUG_BASECOLOR
    finalColor = toneMap(baseColor.rgb);
#endif

#ifdef DEBUG_ALPHA
    finalColor = vec3_t(baseColor.aaa);
#endif

#ifdef DEBUG_OCCLUSION
    finalColor = toneMap(vec3_t(occlusion, occlusion, occlusion));
#endif

#ifdef DEBUG_ROUGHNESS
    finalColor = toneMap(vec3_t(roughness, roughness, roughness));
#endif

#ifdef DEBUG_METALLIC
    finalColor = toneMap(vec3_t(metallic, metallic, metallic));
#endif

#ifdef DEBUG_THICKNESS
    finalColor = vec3_t(thickness, thickness, thickness);
#endif

#ifdef DEBUG_EMISSIVE
    finalColor.rgb = toneMap(emissive);
#endif

#ifdef DEBUG_NORMAL_MAP
#ifdef HAS_NORMAL_MAP
    finalColor = tex2D(u_NormalSampler, i.texcoord.xy).rgb;
#else
    finalColor = vec3_t(0.5, 0.5, 1.0);
#endif
#endif

#ifdef DEBUG_NORMAL // raw normal
    finalColor = i.normal;
//    finalColor = (i.normal + 1.0) / 2.0;
#endif

#ifdef DEBUG_NORMAL_GEOMETRY  // normal with normal map
    finalColor = shadingNormal;
//    finalColor = (shadingNormal + 1.0) / 2.0;
#endif

#ifdef DEBUG_TANGENT
    finalColor = shadingTangent * 0.5 + vec3_t(0.5, 0.5, 0.5);
#endif

#ifdef DEBUG_BITANGENT
    finalColor = shadingBitangent * 0.5 + vec3_t(0.5, 0.5, 0.5);
#endif

#ifdef DEBUG_F0
    finalColor.rgb = vec3_t(f, f, f);
#endif

#ifdef DEBUG_VIEW
    finalColor = worldViewDir;
#endif

#ifdef DEBUG_TOTAL_DIFFUSE
    finalColor = toneMap((totalDiffuse);
#endif

#ifdef DEBUG_TOTAL_SPECULAR
    finalColor = toneMap((totalSpecular);
#endif

#if defined(DEBUG_IBL) || defined(DEBUG_SH)
    finalColor = toneMap(ambient_diffuse * ao * nonMetallic * baseColor.rgb +
                        (nonMetallic + baseColor.rgb * metallic) * ambient_specular * f * ao);
#endif

#ifdef DEBUG_IBL_DIFFUSE
    finalColor = toneMap(ambient_diffuse * ao * nonMetallic * baseColor.rgb);
#endif

#ifdef DEBUG_IBL_SPECULAR
    finalColor = toneMap((nonMetallic + baseColor.rgb * metallic) * ambient_specular * f * ao);
#endif

#ifdef DEBUG_PUNCTUAL
    finalColor = toneMap(p_diffuse * ao * nonMetallic * baseColor.rgb +
                        (nonMetallic + baseColor.rgb * metallic) * p_specular * f * ao);
#endif

#ifdef DEBUG_PUNCTUAL_DIFFUSE
    finalColor = toneMap(p_diffuse * ao * nonMetallic * baseColor.rgb);
#endif

#ifdef DEBUG_PUNCTUAL_SPECULAR
    finalColor = toneMap((nonMetallic + baseColor.rgb * metallic) * p_specular * f * ao);
#endif

#ifdef DEBUG_ANISOTROPY
#if defined(HAS_FLOW_MAP)
{
    vec4_t flow = tex2D(u_FlowSampler, i.texcoord.xy); // flow.rg, Specular Shift.b
    finalColor = flow.rgb;
}
#endif
#endif

#ifdef DEBUG_ANISOTROPY_DIRECTION
#if defined(HAS_FLOW_MAP)
{
  finalColor = vec3_t(hairFlow.xy, 0.0);
}
#endif
#endif

#ifdef DEBUG_ANISOTROPY_TANGENT
#if defined(HAS_FLOW_MAP)
{
    vec3_t hairTangent = shadingTangent*hairFlow.x - shadingBitangent * hairFlow.y;
    finalColor = hairTangent;
}
#endif
#endif

#ifdef DEBUG_ANISOTROPY_BITANGENT
#if defined(HAS_FLOW_MAP)
{
    vec3_t hairTangent = shadingTangent*hairFlow.x - shadingBitangent * hairFlow.y;
    vec3_t hairBitangent = normalize(cross(hairTangent, shadingNormal));
    finalColor = hairBitangent;
}
#endif
#endif

#ifdef DEBUG_SUBSURFACE_SCATTERING
    finalColor = toneMap(subsurfaceColor);
#endif

    return vec4_t(finalColor.rgb, 1.0);
}
