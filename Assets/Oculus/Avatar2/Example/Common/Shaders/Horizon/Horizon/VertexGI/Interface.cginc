//================================================================================
// Common shader interface to RenderToolkit's vertex-based global illumination
//
// This file should be directly included only by shaders that need to take over
// the main control flow. For simpler shaders, use VertexGI/Framework.cginc
// See Framework.cginc for an example on how to call the vertex-GI code.
//
// VertexGI uses interpolant semantics up to TEXCOORD11
//
// See https://fb.quip.com/j99FAwxuOGCX
// For usage, see VertexGI/Framework.cginc
//================================================================================

#include "../MacroRemapper.cginc"

#include "../FBToneMapping.cginc"

#if fbSAFE_MODE || fbFOG_CUBEMAP || ALPHA_MATERIAL
#define FRAG_NEED_WPOS  1  // World position is needed in the fragment shader
#endif

#if fbFOG_VTX
#define VGI_V2F_FIELDS_FOG\
	/* .w is used for fog  */\
	half4 l1 : TEXCOORD5;
#else
  #if fbFOG_EXP
    #define FRAG_NEED_WPOS  1
    #define VGI_V2F_FIELDS_FOG\
      half3  l1   : TEXCOORD5;
  #else
    #define VGI_V2F_FIELDS_FOG\
      half3  l1   : TEXCOORD5;
  #endif
#endif // fbFOG_VTX

#define VGI_V2F_FIELDS_ALPHAMAT\
	/* rgb:linear albedo/glow/avatar color, .a:smooth for ALPHA_MATERIAL might be nointerpolation */\
	half4 color : TEXCOORD6;\
	/* .x: metal(0/1), y: specMul(0/0.25/2), z:glow(0/1) for ALPHA_MATERIAL, TEXCOORD15 to not collide with Avatars */\
  nointerpolation half3 materialProps : TEXCOORD15;

#ifdef NORMAL_MAPPING
#define VGI_V2F_FIELDS_NORMAL_MAPPING\
	float4  tangent	  : TEXCOORD8;\
	float4  bitangent : TEXCOORD9;\
	half3  l2        : TEXCOORD10;\
	half3  l3        : TEXCOORD11;
#else
#define VGI_V2F_FIELDS_NORMAL_MAPPING
#endif

#if FRAG_NEED_WPOS
  #define VGI_V2F_FIELDS_WPOS\
    float4  wPos : TEXCOORD4;
#else
  #define VGI_V2F_FIELDS_WPOS
#endif

#ifdef TEXTURE_ARRAY
  #define VGI_TEXTURE_COORD_FIELD\
    float3  uv : TEXCOORD1;
#else
  #define VGI_TEXTURE_COORD_FIELD\
    float2  uv : TEXCOORD1;
#endif

// NOTE: Update these definitions if more interpolators become required
#define VGI_V2F_NEXT_AVAILABLE_TEXCOORD_SEMANTIC 12
#define VGI_V2F_SECOND_AVAILABLE_TEXCOORD_SEMANTIC 13

// Vertex-to-fragment attributes (interpolants) for GI
//
#define VGI_V2F_ATTRIBUTES\
	float4  pos                : SV_POSITION;\
	float3  normal             : TEXCOORD0;\
	VGI_TEXTURE_COORD_FIELD\
	/* .w is the normalized, non-zerocentered distance to the light, used for variance shadowmaps */\
	float4  staticShadowOffset : TEXCOORD2;\
	/* .xy UV location, z for depth comparison  *\
	/* .w is euclidian distance to inner cascade (1 at border) to do transition  */\
	/* NOTE: this should be conditional, too. For shaders that don't want shadow maps */\
	float4  shadowPos          : TEXCOORD3;\
\
  VGI_V2F_FIELDS_WPOS\
  VGI_V2F_FIELDS_FOG\
  VGI_V2F_FIELDS_ALPHAMAT\
  VGI_V2F_FIELDS_NORMAL_MAPPING\
  UNITY_VERTEX_INPUT_INSTANCE_ID


// Temporary data for the vertex-GI code's vertex program
// This allows us to hold variables, but split up the code into logical chunks.
struct vgi_vert_tmp {
  float3  position;
  float3  normal;
  half3  l0;
#ifdef NORMAL_MAPPING
  float3 tangent;
  half3  l1, l2;
#endif
};


// Temporary data for the vertex-GI code's fragment program.
// This allows us to hold variables, but split up the code into logical chunks.
struct vgi_frag_tmp {
  float3  normal;
  // rgb:linear albedo/glow, .a: for smoothness for ALPHA_MATERIAL
  half4   materialColor;
  // .x: metal(0/1), y: specMul(0/0.25/2), z:glow(0/1) for ALPHA_MATERIAL
  half3   materialProps;
  half    shadowFactor;

#if defined(NORMAL_MAPPING)
  float3  tangent;
  float4  rawNT;
#endif
};


//
// Common components
//

// keep this synced with BatchManager.kMaxBatchObjectCount
#ifdef PER_PRIM_PROPS
#define MAX_BATCH_OBJECT_COUNT 50
#else
#if defined(PER_PRIM_PROPS_GROUP) || defined(GROUP)
#define MAX_BATCH_OBJECT_COUNT 50
#endif
#endif

// keep this synced with BatchManager.kMaxGroupsPerBatch
#define MAX_BATCH_GROUP_COUNT 20

#define M_PI  3.14159265358979323846
// we use 9 float4's to represent 3-band RGB spherical harmonics; the w component is unused
#define LIGHT_PROBE_SIZE 9

#define LIGHT_PROBE_COUNT 1

// Only use these defines if we are a dynamic subd object
// or else we break the UI shader which uses instancing,
// but only 1 probe for all instances
#ifdef DYNAMIC
#ifdef UNITY_INSTANCING_ENABLED
  #define LIGHT_PROBE_COUNT 50
#else
#if defined(PER_PRIM_PROPS_GROUP) || defined(GROUP)
  #define LIGHT_PROBE_COUNT (MAX_BATCH_GROUP_COUNT)
#endif
#endif
#endif

#define LIGHT_PROBE_ARRAY_SIZE (LIGHT_PROBE_COUNT*LIGHT_PROBE_SIZE)

// This is set by the "sunlight" script
//
float4  _MainCameraLightProbe[LIGHT_PROBE_SIZE];

// we always use interpolated light probes,
// but we may want a shader variant later
#define INTERPOLATED_LIGHT_PROBE 1

#if INTERPOLATED_LIGHT_PROBE
// Current time in seconds since program started
// that may be offset for long running programs for better accuraccy
float _LightProbeTime;

//========================================================
// Return radiance using a 4-coefficient SH light probe
// plus a 4-coefficient delta for interpolation
// for a smooth transition from an old light probe value to new one
//========================================================
half3 vgi_ProbeRadianceIndexed(
    float4 probeData[LIGHT_PROBE_ARRAY_SIZE],
    int index,
    // n assumed to be normalized
    float3 n,
    float attenuation) {
  int offset = LIGHT_PROBE_SIZE * index;
  float startTime = probeData[0 + offset].w;
  // reciprocal of interpolation time
  float recDeltaTime = probeData[1 + offset].w;

  float t = saturate((_LightProbeTime - startTime) * recDeltaTime);
  half3 result = (probeData[0 + offset].xyz + probeData[4 + offset].xyz * t) * M_PI +
      (probeData[1 + offset].xyz + probeData[5 + offset].xyz * t) * n.y +
      (probeData[2 + offset].xyz + probeData[6 + offset].xyz * t) * n.z +
      (probeData[3 + offset].xyz + probeData[7 + offset].xyz * t) * n.x;

  return max(half3(0., 0., 0.), result);
}
#else
//========================================================
// Return radiance, using a 9-coefficient SH light probe
//========================================================
half3 vgi_ProbeRadianceIndexed(float4 probeData[LIGHT_PROBE_ARRAY_SIZE], int index, float3 n, float attenuation) {
  int offset = LIGHT_PROBE_SIZE * index;
  half3 result =
    max(float3(0, 0, 0),
        (probeData[0 + offset].xyz * 0.282095 + probeData[1 + offset].xyz * ((-0.488603 * 2. / 3.) * n.y) +
         probeData[2 + offset].xyz * ((0.488603 * 2. / 3.) * n.z) +
         probeData[3 + offset].xyz * ((-0.488603 * 2. / 3.) * n.x) +
         probeData[4 + offset].xyz * ((1.092548 / 4.) * n.x * n.y) +
         probeData[5 + offset].xyz * ((-1.092548 / 4.) * n.y * n.z) +
         probeData[6 + offset].xyz * ((0.315392 / 4.) * (3.0f * n.z * n.z - 1.0f)) +
         probeData[7 + offset].xyz * ((-1.092548 / 4.) * n.x * n.z) +
         probeData[8 + offset].xyz * ((0.546274 / 4.) * (n.x * n.x - n.y * n.y))) *
            M_PI);

  // if (n.y < 0)
  //   result *= saturate(1 + n.y * (1 - attenuation));

  return result;
}
#endif

half3 vgi_ProbeRadiance(float4 probeData[LIGHT_PROBE_ARRAY_SIZE], float3 n, float attenuation) {
  return vgi_ProbeRadianceIndexed(probeData, 0, n, attenuation);
}