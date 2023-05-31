//========================================================================
// Dynamic GI shader core for the RenderToolkit shader system
//========================================================================

#pragma exclude_renderers xbox360 gles

// enable only for debugging, do not check in for production
// #define QUALITY_LEVEL_DEBUG 1

// 0: 2x2 dither(fast), 1: ~4x4 dither with 16 values (smoother quality but standing pattern),
// at some ALU cost
#define SHADOW_DITHER_QUALITY 0

// Uncoment this to enable variance shadowmaps. Sync this manually with the same #define in Sunlight.cs,
// so we don't create too many shader variants for mobile
// However, during the current transition period, we are using a runtime / UI switch (2x shader variants),
// so we can enable VSM one world at a time. During the transition, keep this commented out.
//#define SHADOWMAP_STATIC_VSM  1

// It's s slight optimization to deactivate specular highlighting from the direct sunlight,
// without it, all spec has to come from the ambient light. However, disabling it does
// cause a noticable visual divergence.
#if defined(DIRECTIONAL_LIGHT)
#define DIRECT_SPECULAR 1
#else
#define DIRECT_SPECULAR 0
#endif

// We use a function to return the value, so we don't break coding style. The style doesn't allow us to
// use non-uppercase #defines, but this name must match C#, which doesn't support #defines with values
int nShadowCascadeTiles() {
  return 1;
}

#include "../FBSafeMode.cginc"
#include "../../../../Scripts/ShaderUtils/AvatarCustom.cginc"

float4x4  _shadowMatrix;
#if defined(DISPLACEMENT) || defined (BLEND_SHAPES)
sampler2D _DispMap;
float     _DispScale;
#endif

#ifdef BLEND_SHAPES
float4x4 _HeadTransform;
#endif

float4  _LightColor;
float4  _LightVector;
float3  _SurfaceColor;
float3  _ViewVector;
float4  _EyeLightVector;
float4  _CameraVector;

#ifdef TEXTURE_COLOR
sampler2D  _MainTex;
#endif
float4     _MainTex_ST;

#ifdef NORMAL_MAPPING
#ifdef USE_NRML_MAP_NAME
sampler2D _NrmlMap; // To get around Unity "normal map warning"
#else
sampler2D _NormalMap;
#endif
sampler2D _ICMap;
#endif

#if ALPHA_MATERIAL
// reflection map for specular reflection
samplerCUBE _ReflectionCubeMap;
#endif

#ifdef SSS
sampler2D _SSSMap;
#endif

#ifdef SHADOWMAP_STATIC_VSM
float4x4  _LightWorldToLocalMatrix;
float     _ShadowMapCameraZFarInv;
float     _VarianceShadowBias;
#endif

// Used for the dynamic shadows:
UNITY_DECLARE_SHADOWMAP(_ShadowMap);

// Used for the static shadows:
#ifdef SHADOWMAP_STATIC_VSM
    // Variance shadowmaps need 2 channels, so they can't use the specialized shadow sampler.
    // Also, on mobile / GLES platforms, sampler2D defaults to low precision, so we need to explicitly specify this type.
    sampler2D_float  _ShadowMapStatic;
#else
  #if DYNAMIC_SHADOWS
    UNITY_DECLARE_SHADOWMAP(_ShadowMapStatic);
  #else
    sampler2D_float  _ShadowMapStatic;
  #endif
#endif

float4  _ShadowMapSize;
float4  _ShadowDither1;
float4  _ShadowDither2;

float4x4  _WorldToSunlight;
float4x4  _StaticWorldToSunlight;
float4x4  _ReflectionRotation;
float     _EditMode;

#ifdef DYNAMIC
#define LIGHT_PROBE 1
#endif

#ifdef LIGHT_PROBE
#ifdef UNITY_INSTANCING_ENABLED
CBUFFER_START(InstancingLightProbeData)
float4 _LightProbeArray[LIGHT_PROBE_ARRAY_SIZE];
CBUFFER_END
#else
float4  _LightProbe[LIGHT_PROBE_ARRAY_SIZE];
#endif
#endif

#ifdef ALPHA_MATERIAL
float4  _MaterialProperties[8];
#endif

#include "../FBFog.cginc"
#include "../Shadows/VSM.cginc"
#if defined(BLOB_SHADOWS)
#include "../Shadows/BlobShadows.cginc"
#endif

#ifdef UNITY_INSTANCING_ENABLED
UNITY_INSTANCING_BUFFER_START(Props)
UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_DEFINE_INSTANCED_PROP(int, _LightProbeIndex)
UNITY_INSTANCING_BUFFER_END(Props)
#endif

#ifndef UNITY_INSTANCING_ENABLED
float4  _Color;
#endif
float  _Lerp;

#ifdef SMOOTH_LOD_TRANSITION
float2  _LODData[8];
#endif

#if defined(DISPLACEMENT) && defined(BLEND_SHAPES)
int _DebugMode;
#endif

// per-object-id render props (used with batching)
// todo: optimize data packing
#if defined(PER_PRIM_PROPS) || defined(PER_PRIM_PROPS_GROUP)
float4 _ColorsById[MAX_BATCH_OBJECT_COUNT];
float4x4 _LocalToWorldById[MAX_BATCH_OBJECT_COUNT];
float4x4 _WorldToLocalById[MAX_BATCH_OBJECT_COUNT];
// due to a Quest-specific Unity bug, we're forced to use float4 here when we could use float.
// data in x component 0:hide, 1: visible
// data in y component is tint factor
float4 _VisibilityById[MAX_BATCH_OBJECT_COUNT];
#endif

#if defined(PER_PRIM_PROPS_GROUP) || defined(GROUP)
#ifdef LIGHT_PROBE
float4 _GroupLightProbeById[MAX_BATCH_GROUP_COUNT * LIGHT_PROBE_SIZE];
#endif
float4x4 _GroupLocalToWorldById[MAX_BATCH_GROUP_COUNT];
float4x4 _GroupWorldToLocalById[MAX_BATCH_GROUP_COUNT];
#endif

// a multiplicative factor applied to vertex colors; defaulted by the app to 1.0 (untinted)
float _GlobalTint;

struct appdata {
  OVR_REQUIRED_VERTEX_FIELDS
  float4  uv : OVR_FIRST_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;    // todo: why not float2?
  float4  uv2 : OVR_SECOND_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;    // todo: why not float2?
  float4  uv3 : OVR_THIRD_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;    // todo: why not float2?
#if defined(SMOOTH_LOD_TRANSITION) || defined(SMOOTH_ANIMATION)
  float4  lastPos : OVR_FOURTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;    // todo: why not float3?
  float4  lastNorm : OVR_FIFTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;    // todo: why not float3?
  float4  lastGI : OVR_SIXTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;    // todo: why not float3?
#ifdef NORMAL_MAPPING
  float4 lastTan : OVR_SEVENTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;
#endif
#endif
  float2 id : OVR_EIGHTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC; // .x: PPP batchId if GROUP, PER_PRIM_PROPS or PER_PRIM_PROPS_GROUP, .y: groupId if PER_PRIM_PROPS_GROUP or GROUP
   // .rgb:sRGB albedo/emissive color, .a for material properites if ALPHA_MATERIAL
  float4  color : COLOR;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct compactAppdata {
  // float3 on C++ side
  float4  position : POSITION;
  // RG: packed normal range, B: PPP batchId A: PPP group id
  float4  packedNormal : NORMAL;
   // .rgb:sRGB albedo/emissive color, .a for material properites if ALPHA_MATERIAL
  float4  color : COLOR;
  float4  uv : TEXCOORD0;
  float4  packedLighting : TEXCOORD1;
  UNITY_VERTEX_INPUT_INSTANCE_ID
};

// https://blog.selfshadow.com/2011/10/17/perp-vectors
// Hughes-Möller perpendicular vector generation
float3 perp_hm(float3 u) {
  float3 a = abs(u);
  float3 v;
  if (a.x <= a.y && a.x <= a.z)
    v = float3(0, -u.z, u.y);
  else if (a.y <= a.x && a.y <= a.z)
    v = float3(-u.z, 0, u.x);
  else
    v = float3(-u.y, u.x, 0);
  return v;
}

// ----------------------------------------
// https://knarkowicz.wordpress.com/2014/04/16/octahedron-normal-vector-encoding
float2 OctWrap(float2 v) {
  return (1.0 - abs( v.yx ) ) * ( v.xy >= 0.0 ? 1.0 : -1.0);
}
// @parma n normalized normal
// @return  f in 0..1 range
float2 EncodeOct(float3 n) {
  n /= ( abs( n.x ) + abs( n.y ) + abs( n.z ) );
  n.xy = n.z >= 0.0 ? n.xy : OctWrap( n.xy );
  n.xy = n.xy * 0.5 + 0.5;
  return n.xy;
}
// @param f in 0..1 range
// @return normalized normal
float3 DecodeOct(float2 f) {
  f = f * 2.0 - 1.0;
   // https://twitter.com/Stubbesaurus/status/937994790553227264
  float3 n = float3( f.x, f.y, 1.0 - abs( f.x ) - abs( f.y ) );
  float t = saturate( -n.z );
  n.xy += n.xy >= 0.0 ? -t : t;
  return normalize( n );
}
// ----------------------------------------


// ----------------------------------------
// decode HDR color stored in 32 bit, represents 0 exactly
// see same function in SubDTools.cpp for more info
// @param RGBe from 32bit RGBA
float3 FromRGBe(float4 RGBe) {
  float fexp = exp2(RGBe.a * 255.0f - 128);

  return RGBe.rgb * fexp;
}
// ----------------------------------------

appdata Decompress(compactAppdata c) {
  appdata ret;

  OVR_SET_VERTEX_POSITION_FIELD(ret, c.position);
  OVR_SET_VERTEX_NORMAL_FIELD(ret, DecodeOct(c.packedNormal.xy));
//  ret.normal = normalize(c.packedNormal.xyz - 127.0f / 255.0f);
  // todo: support normal mapping

  OVR_SET_VERTEX_TANGENT_FIELD(ret, float4(perp_hm(ret.normal), 1));
  ret.uv = c.uv;
  float3 HDRLighting = FromRGBe(c.packedLighting);
  ret.uv2 = float4(HDRLighting.rg, 0.0f, 0.0f);
  ret.uv3 = float4(HDRLighting.b, 0.0f, 0.0f, 0.0f);
  // todo: support smooth transition
#if defined(SMOOTH_LOD_TRANSITION) || defined(SMOOTH_ANIMATION)
  ret.lastPos = OVR_GET_VERTEX_POSITION_FIELD(ret);
  ret.lastNorm = float4(OVR_GET_VERTEX_NORMAL_FIELD(ret), 0);
  ret.lastGI = float4(ret.uv2.xy, ret.uv3.xy);
#ifdef NORMAL_MAPPING
  ret.lastTan = OVR_GET_VERTEX_TANGENT_FIELD(ret);
#endif
#endif
  //
  ret.id = float2((int)(c.packedNormal.b * 255.0f + 0.5f), (int)(c.packedNormal.a * 255.0f + 0.5f));
  // .a for material properites if ALPHA_MATERIAL
  ret.color = c.color;
  UNITY_TRANSFER_INSTANCE_ID(ret, c);

  return ret;
}

float Pow(float t, float n) {
  if (n < 1)
    t = 1;

  return t / (t - n * t + n);
}

float lengthSquared(float3 v) {
  return dot(v, v);
}



#define addSphereLight(positionX, colorX, r2)\
{\
  float3 v = (positionX - wpos.xyz);\
  float d2 = dot(v, v);\
  v /= sqrt(d2);\
  float3 irr = colorX * r2 / (d2 + r2);\
  float3 w = saturate(float3(dot(v, lv1), dot(v, lv2), dot(v, lv3)) + 0.1);\
  /*irr = colorX; */\
  ir0 += irr * w.x;\
  ir1 += irr * w.y;\
  ir2 += irr * w.z;\
}

#ifdef NORMAL_MAPPING
#define addSkyLight(color, v, spread)\
{\
  float3 irr = color;\
  float3 w = saturate(float3(dot(v, lv1), dot(v, lv2), dot(v, lv3)) + spread);\
  ir0 += irr * w.x;\
  ir1 += irr * w.y;\
  ir2 += irr * w.z;\
}
#else
#define addSkyLight(color, v, spread)\
{\
  float3 irr = color;\
  float w = saturate(dot(v, n) + spread);\
  ir0 += color * w;\
}
#endif


float3 unpackIrradiance(float src) {
  float  red = floor(src / 65536.);
  float  green = floor(src / 256.);
  float  blue = src - green * 256.;
  green -= red * 256.;

  return float3(red, green, blue);
}


void vgi_vert_transform(appdata v, OvrVertexData vertData, inout vgi_vert_tmp tmp) {
  tmp.position = vertData.position.xyz;
  tmp.normal = vertData.normal;

#if defined(NORMAL_MAPPING)
  #if defined(OVR_VERTEX_HAS_TANGENTS)
    tmp.tangent = vertData.tangent.xyz;
  #else
    tmp.tangent = float3(1.0, 0.0, 0.0); // some sensible default
  #endif
#endif

#if defined(NORMAL_MAPPING) && !defined(DYNAMIC)
  float  scale = v.uv2.x;
  tmp.l0 = unpackIrradiance(v.uv2.y) * scale;
  tmp.l1 = unpackIrradiance(v.uv3.x) * scale;
  tmp.l2 = unpackIrradiance(v.uv3.y) * scale;
#else
  tmp.l0 = half3(v.uv2.xy, v.uv3.x);
#endif

	{
#if defined(SMOOTH_LOD_TRANSITION) || defined(SMOOTH_ANIMATION)
#ifdef SMOOTH_LOD_TRANSITION
    float  lodGroup = frac(v.tangent.w) * 8.;
    float  w = _LODData[lodGroup].x == v.tangent.w ? _LODData[lodGroup].y : 0.;
#endif
#ifdef SMOOTH_ANIMATION
  float w = _Lerp;
#endif
#ifndef SHADER_API_MOBILE
    if (_EditMode == 0.) {
#endif
      tmp.position = lerp(tmp.position, v.lastPos.xyz, w);
      tmp.normal = lerp(tmp.normal, v.lastNorm.xyz, w);
#ifdef NORMAL_MAPPING
      tmp.tangent = lerp(tmp.tangent, v.lastTan.xyz, w);
#endif
#ifndef SHADER_API_MOBILE
    }
#endif

#if defined(NORMAL_MAPPING) && !defined(DYNAMIC)
    float3  lastL0 = unpackIrradiance(v.lastGI.y) * v.lastGI.x;
    float3  lastL1 = unpackIrradiance(v.lastGI.z) * v.lastGI.x;
    float3  lastL2 = unpackIrradiance(v.lastGI.w) * v.lastGI.x;

    tmp.l0 = lerp(tmp.l0, lastL0, w);
    tmp.l1 = lerp(tmp.l1, lastL1, w);
    tmp.l2 = lerp(tmp.l2, lastL2, w);
#else
    tmp.l0 = lerp(tmp.l0, v.lastGI.xyz, w);
#endif
#endif // SMOOTH_LOD_TRANSITION
  }
}

//================================
// Vertex program
//================================
v2f vgi_vert(appdata v, vgi_vert_tmp tmp) {
  v2f  o = (v2f)0;
  UNITY_INITIALIZE_OUTPUT(v2f, o);
  UNITY_SETUP_INSTANCE_ID(v);
  UNITY_TRANSFER_INSTANCE_ID(v, o);

  float4x4 objectToWorld = unity_ObjectToWorld;
  float4x4 worldToObject = unity_WorldToObject;

#if defined(PER_PRIM_PROPS) || defined(PER_PRIM_PROPS_GROUP)
  int primId = v.id.x;
#endif

#ifdef PER_PRIM_PROPS
  // if enabled, apply per-object-id transform
  objectToWorld = _LocalToWorldById[primId];
  worldToObject = _WorldToLocalById[primId];
#endif

#ifdef PER_PRIM_PROPS_GROUP
  int groupId = v.id.y;
  // _LocalToWorldById means "local to group" and _WorldToLocalById means "group to local" when PER_PRIM_PROPS_GROUP is defined
  objectToWorld = mul(_GroupLocalToWorldById[groupId], _LocalToWorldById[primId]);
  worldToObject = mul(_WorldToLocalById[primId], _GroupWorldToLocalById[groupId]); // since (AB)^-1 = (B^-1)(A^-1)
#endif

#ifdef GROUP
  int groupId = v.id.y;
  objectToWorld = _GroupLocalToWorldById[groupId];
  worldToObject = _GroupWorldToLocalById[groupId];
#endif


  half3  l0 = tmp.l0;
#ifdef NORMAL_MAPPING
  half3  l1 = tmp.l1;
  half3  l2 = tmp.l2;
#endif

#ifdef CUSTOM_VERTEX_CODE
  CUSTOM_VERTEX_CODE
#endif

  float3  n = normalize(mul(tmp.normal, (float3x3)worldToObject));
#ifdef NORMAL_MAPPING
  float3  t = mul(tmp.tangent, (float3x3)worldToObject);
  float3  b = normalize(cross(n, t));
  t = cross(b, n); // note: no mirrored uv support (yet)
  o.tangent = t.xyzz;
  o.bitangent = b.xyzz;
#endif

  float3 obj_pos = tmp.position;

#if defined(DISPLACEMENT) && defined(NORMAL_MAPPING)
  float3 d = float3(0., 0., 0.); // = v.color.rgb - 0.5;
#ifdef BLEND_SHAPES
  d = tex2Dlod(_DispMap, float4(v.uv.xy * float2(2., 1.) + float2(0., .5), 0, 0)).rgb;  // LowerLeft Quadrant
  d.x = -d.x;
  obj_pos += mul(d, (float3x3)_HeadTransform) * _DispScale;
#endif
#endif

  float4 wpos = mul(objectToWorld, float4(obj_pos, 1));

  // 0:off (stock Unity), 1: fixed T64913801: improve z buffer precison with content far from 0,0,0
  #define CAMERA_RELATIVE_POSITION 1

  if(CAMERA_RELATIVE_POSITION) {
    float4x4 unityMatrixV = UNITY_MATRIX_V;
    float3 translatedWPos = wpos.xyz - _WorldSpaceCameraPos;

    // remove translation in UNITY_MATRIX_V
    unityMatrixV[0][3] = 0;
    unityMatrixV[1][3] = 0;
    unityMatrixV[2][3] = 0;

    float4x4 unityMatrixVP = mul(UNITY_MATRIX_P, unityMatrixV);

    o.pos = mul(unityMatrixVP, float4(translatedWPos, 1));
  } else {
    o.pos = mul(UNITY_MATRIX_VP, wpos);
  }

#if defined(PER_PRIM_PROPS) || defined(PER_PRIM_PROPS_GROUP)
  // look up per-primitive visibility with vertex primId. if invisible, set the clip space position
  // to outside of the range [-1, 1] for all components so it's clipped away
  o.pos = lerp(float4(2, 2, 2, 1), o.pos, _VisibilityById[primId].x);
#endif

  o.normal = n.xyz;
#ifdef TEXTURE_ARRAY
  o.uv.xy = TRANSFORM_TEX(v.uv, _TexArray);
  // integer part of x is the slice
  o.uv.z = floor(v.uv.x);
#else
  o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
#endif

/*
  float  groundY = lightProbe[0].w;
  float  y = mul(_Object2World, pos).y - groundY;
  float  groundAttenuation = saturate(y*6+0.2);
  groundAttenuation *= groundAttenuation;
*/
  float3  ir0 = float3(0, 0, 0);
#ifdef NORMAL_MAPPING
  float3  ir1 = float3(0, 0, 0);
  float3  ir2 = float3(0, 0, 0);
#endif

#if defined(LIGHT_PROBE) || !SHADER_API_MOBILE
#ifndef NORMAL_MAPPING
  float3  t = mul((float3x3)objectToWorld, OVR_GET_VERTEX_TANGENT_FIELD(v).xyz);
  float3  b = normalize(cross(n, t));
  t = cross(b, n); // note: no mirrored normal map support yet
#endif

  float3  nn = n * 0.949;
  float3  tt = t * 0.316;
  float3  bb = b * 0.316 * 0.866;

  float3  lv1 = nn + tt;
  float3  lv2 = nn + bb - tt * 0.5;
  float3  lv3 = nn - bb - tt * 0.5;
#endif

#ifdef LIGHT_PROBE
  float  groundAttenuation = 0.;

#ifdef NORMAL_MAPPING
  half3 ao = l0.xyz;
  l0 = vgi_ProbeRadiance9(_LightProbe, lv1, 0.) * ao.x;
  l1 = vgi_ProbeRadiance9(_LightProbe, lv2, 0.) * ao.y;
  l2 = vgi_ProbeRadiance9(_LightProbe, lv3, 0.) * ao.z;
#else
#ifdef UNITY_INSTANCING_ENABLED
  int probeIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _LightProbeIndex);
  l0 = vgi_ProbeRadianceIndexed(_LightProbeArray, probeIndex, n, 0.);
#else

#if defined(PER_PRIM_PROPS_GROUP) || defined(GROUP)
  half3 sh = vgi_ProbeRadianceIndexed(_GroupLightProbeById, groupId, n, 0.);
#else
  half3 sh = vgi_ProbeRadiance(_LightProbe, n, 0.);
#endif

  half3 ao = l0.xyz;
  l0 = sh * dot(ao, 1. / 3.);
#endif
#endif
#endif

#ifndef SHADER_API_MOBILE
  if (_EditMode) {
    addSkyLight(float3(1., 1., 1) * 0.5, float3(0, 1, 0), .75);
#ifdef NORMAL_MAPPING
    l0 = ir0;
    l1 = ir1;
    l2 = ir2;
#else
    l0 = ir0;
#endif
  }
#endif

#ifdef NORMAL_MAPPING
  o.l1.xyz = l0;
  o.l2 = l1;
  o.l3 = l2;
#else
  o.l1.xyz = l0;
#endif


  // Shadow mapping
  //
#if defined(BLOB_SHADOWS)
  o.shadowPos = wpos;
#elif defined(DIRECTIONAL_LIGHT)
  {
		float  shadowBias = _LightColor.w;

    // See Interface.cginc for the description of shadowPos
		float3  shadowPos = mul(_WorldToSunlight, wpos).xyz;
		o.shadowPos.w = float4(sqrt(dot(shadowPos.xy, shadowPos.xy)), 0., 0., 0.);

#if SHADOWMAP_STATIC_VSM
    float4  lightSpacePos = mul(_LightWorldToLocalMatrix, wpos);
    float   depth = lightSpacePos.z * _ShadowMapCameraZFarInv;
		o.staticShadowOffset.w = depth;
#endif

    shadowPos = shadowPos * .5 + .5;  // Zero-center shadowPos (0..1 -> -0.5..0.5)

    shadowPos.x *= 0.5;
    float2  offset0 = float2(0.5, 0.5);
    float2  offset1 = float2(0.5, 0.5);

#ifdef UNITY_REVERSED_Z
		shadowPos.z = 1. - shadowPos.z;
		o.staticShadowOffset.xyz = mul(_StaticWorldToSunlight, wpos).xyz * float3(offset0, -0.5) +
				float3(offset1, .5 + shadowBias * 2.) - shadowPos;
		shadowPos.z += shadowBias;
#else
		o.staticShadowOffset.xyz = mul(_StaticWorldToSunlight, wpos).xyz * float3(offset0, 0.5) +
				float3(offset1, .5 - shadowBias * 2.) - shadowPos;
		shadowPos.z -= shadowBias;
#endif

		o.shadowPos.xyz = shadowPos;
  }
#endif // BLOB_SHADOWS, DIRECTIONAL_LIGHT


#ifdef FRAG_NEED_WPOS
  o.wPos = wpos;
#endif

  fbFog_vert(_fbFogColor.a, _WorldSpaceCameraPos, wpos.xyz, o.l1.w);

  // compute o.color and o.materialProps
  {
    half4 sRGBWithAlpha = 1;

#ifdef VERTEX_COLOR
#if defined(PER_PRIM_PROPS) || defined(PER_PRIM_PROPS_GROUP)
    // if enabled, apply per-prim-id properties
    sRGBWithAlpha = _ColorsById[primId];
#else
    sRGBWithAlpha = v.color;
#endif
#endif
    // ^2 is sRGB to linear appoximation
    o.color = half4(sRGBWithAlpha.rgb * sRGBWithAlpha.rgb, sRGBWithAlpha.a);

#ifdef VERTEX_COLOR
#if defined(PER_PRIM_PROPS) || defined(PER_PRIM_PROPS_GROUP)
    o.color.rgb *= _VisibilityById[primId].y; // PPP tint factor
#else
    o.color.rgb *= _GlobalTint;
#endif
#endif

#ifdef SOLID_COLOR
#ifdef UNITY_INSTANCING_ENABLED
    o.color *= UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
#else
    o.color *= _Color;
#endif
#endif

#if ALPHA_MATERIAL
    // 0..7, search "materialProperties" in C#
    half matIndex;
    // 0:rough..1:polished
    half smooth;
    {
      // extract matIndex (3 bits) and smooth (5 bits) from alpha channel (8 bits)
      half tmp = o.color.a * (255. / 32.);
      matIndex = floor(tmp);
      smooth = saturate((tmp - matIndex - 2. / 32.) * 32./29.);
    }

    half4 materialProperties = _MaterialProperties[matIndex];
    half metal = materialProperties.x;
    half specMul = materialProperties.z;
    half glow = materialProperties.w;

    o.color.a = smooth;
    o.materialProps = half3(metal, specMul, glow);
#else
    o.color.a = 0;
    // compiler should remove this dead code
    o.materialProps = half3(0, 0, 0);
#endif
  }

  return o;
}

float myPow(float a, float b) {
  return a / ((1. - b) * a + b);
}

float rgbToLuma(float3 c) {
  return dot(c, float3(0.299, 0.587, 0.114));
}

// 2 fmul, 1 fadd, 1 frac from:
// https://developer.oculus.com/blog/tech-note-shader-snippets-for-efficient-2d-dithering
float Dither16(float2 Pos, int frameIndexMod4)
{
	uint3  k0 = uint3(36, 7, 26);

	float  Ret = dot(float3(Pos.xy, frameIndexMod4), k0 / 16.0f);

	return frac(Ret);
}

// 2 fmul, 1 fadd, 1 frac// to not align with Dither16, could be put into one function
float Dither17(float2 Pos, int frameIndexMod4)
{
  // Note: was uint3(2, 7, 23), the new variant seems to align better with:
  // Dither16   uint3 k0 = uint3(7, 2, 23);
   uint3 k0 = uint3(7, 2, 23);

   float Ret = dot(float3(Pos.xy, frameIndexMod4), k0 / 17.0f);

   return frac(Ret);
}

#ifdef NORMAL_MAPPING
half4 sample_normal_map(float2 uv) {
#ifdef USE_NRML_MAP_NAME
    return tex2D(_NrmlMap, uv);
#else
    return tex2D(_NormalMap, uv);
#endif
}
#endif


//================================
// Fragment program first stage
// - 'tmp' initialization
// - normal mapping
//================================
void vgi_frag_init(v2f i, inout vgi_frag_tmp tmp) {
#ifdef NORMAL_MAPPING
#ifdef BLEND_SHAPES
  half4 nT = tex2D(_DispMap, i.uv * float2(2., 1.)); // _DispMap is floating point between -1 and 1
  nT += sample_normal_map(i.uv) * 2. - 1.; // Static normal map is 8 bit RGBA between 0 and 1. Convert to -1 to 1

  // rawNT is value from 0 to 1
  half4 rawNT = nT * 0.5 + 0.5;
#else
  half4  rawNT = sample_normal_map(i.uv);
  float3  nT = rawNT.xyz * 2. - 1; // Convert to -1 to 1
#endif
  float3  n = i.tangent.xyz * nT.x - i.bitangent.xyz * nT.y + i.normal.xyz * nT.z;
  tmp.rawNT = rawNT;
#else
  float3  n = i.normal.xyz;
#endif

  tmp.normal = normalize(n);

  tmp.materialColor = i.color;
  tmp.materialProps = i.materialProps;

  // Initialize to 1, so vgi_frag_dynamicshadow is not called, shadowing (or lack thereof) will work as expected
  tmp.shadowFactor = 1.;
}




//========================================
// Fragment program lighting stage
// This may be replaced by custom code.
// See Framework.cginc
//========================================
void vgi_frag_dynamicshadow(v2f i, inout vgi_frag_tmp tmp) {
#if defined(BLOB_SHADOWS)
  tmp.shadowFactor = blobShadowFactor(i.shadowPos, tmp.normal);
#else
  float3  shadowDepthOrig = i.shadowPos.xyz;
  float3  shadowDepth = shadowDepthOrig;
  float   shadowDistance = i.shadowPos.w;

#if SHADOW_DITHER_QUALITY
  // 0 .. 0.5
  // 6 fmul, 2 fadd, 2 frac (2 fmul could be saved if output range 0..1 is used)
  float2  halfDither = float2(Dither16(i.pos.xy, 0), Dither17(i.pos.xy, 0)) * .5;
  shadowDepth.xy += (halfDither - .25) * _ShadowMapSize.zw;
  // Softer PCF comparison (should align with depth buffer precision, todo: verify)
  shadowDepth.z += (halfDither - .25) * 0.0002f;
  // Soft transition between cascades
  shadowDistance += halfDither.x * (1.5 * 0.1);
#else // SHADOW_DITHER_QUALITY
  // 0 .. 0.5
  // 2 fmul, 2 fadd, 1 frac
  half2  offset = frac((i.pos.xy - .5) * .5);
  offset.y = abs(offset.x - offset.y);  // x ^= y

  shadowDepth.xy += (offset - .25) * _ShadowMapSize.zw;
  // test if uv coordinates fall outside dynamic shadow range. If so, use static shadow
  // (0 .. 1.5) * 0.1
  shadowDistance += (offset.x * 2. + offset.y) * .1;
#endif // SHADOW_DITHER_QUALITY

  half  shadowFactor;

// Only static objects were rendered into the global shadow cascade: blend / use both cascades
  {
#ifdef SHADOWMAP_STATIC_VSM
    // Use a variance shadow map for static shadows
    float   depth = i.staticShadowOffset.w + _VarianceShadowBias;
    float2  shadowmapSample = tex2D(_ShadowMapStatic, shadowDepthOrig + i.staticShadowOffset.xyz).rg;
    shadowFactor = ComputeVSMShadowFactor(shadowmapSample, depth);
#else
  #if DYNAMIC_SHADOWS
    // static shadow sample
    shadowFactor = UNITY_SAMPLE_SHADOW(_ShadowMapStatic, shadowDepth + i.staticShadowOffset.xyz).r;
    // dynamic shadow sample
    if(shadowDistance < 1.000) {
      shadowFactor *= UNITY_SAMPLE_SHADOW(_ShadowMap, shadowDepth).r;
    }
  #else
    // static shadow sample, alternative to UNITY_SAMPLE_SHADOW to maintain simplicity
    float3 shadowCoords = shadowDepth + i.staticShadowOffset.xyz;
    shadowFactor = step(shadowCoords.z, tex2D(_ShadowMapStatic, shadowCoords.xy).r);
    // no dynamic shadow in low quality :(
  #endif // DYNAMIC_SHADOWS
#endif  // SHADOWMAP_STATIC_VSM
  }

  tmp.shadowFactor = shadowFactor;
#endif // BLOB_SHADOWS
}

half3 calc_indirect_diffuse_light(v2f i, vgi_frag_tmp tmp, half3 sss) {
#ifdef NORMAL_MAPPING
  half3 diffuseIC = tex2D(_ICMap, tmp.rawNT.xy).rgb * 2 - 1. / 3.;

  // Alpha value of normal map/displacement map is an occlusion value,
  // scale the indirect lighting by that occlusion
  half3 indirectDiffuse =
    (i.l1.rgb * diffuseIC.x + i.l2.rgb * diffuseIC.y + i.l3.rgb * diffuseIC.z);

    // Only multiply by alpha if not using blend shapes (as the AO will hopefully be
    // calculated by some other method)
    // NOTE: This should be temporary until assets properly map out AO
#ifndef BLEND_SHAPES
    indirectDiffuse = indirectDiffuse * tmp.rawNT.a;
#endif

#ifdef SSS
  return lerp(indirectDiffuse, (i.l1.rgb + i.l2.rgb + i.l3.rgb) * (1. / 3.), sss);
#else
  return indirectDiffuse;
#endif // ifdef SSS
#else // ifndef NORMAL_MAPPING
  // Not normal mapping, just one color instead of 3
  return i.l1;
#endif
}

half3 calc_indirect_diffuse_light(v2f i, vgi_frag_tmp tmp) {
    return calc_indirect_diffuse_light(i, tmp, 0.0);
}


#if defined(ALPHA_MATERIAL)
half default_ambient_specular(float3 reflectVector, float specPower, half specIntensity) {
  float3  rAbs = abs(reflectVector); // fake 6 directional light specular in +x, -x, +y, -y, +z, -z
  // Use max instead of adding to keep lowest intensity below .5
  return myPow(max(max(rAbs.x, rAbs.y), rAbs.z) + 0.005, specPower) * specIntensity;
}

half ambient_specular_from_cube_map(half3 reflectVector, half smooth, half metal) {
  // Use the top 5 mip levels only to support small cube maps (size 64) and avoid artifacts
  // due to compression and creation of cubemaps from 2D texure data
  // todo: use shader variable for mip level multiplier instead of hardcoding it to 4
  // We use 1/2 intensity maps and scale up here so low-dynamic-range get higher range
  return texCUBElod(_ReflectionCubeMap, half4(reflectVector, (1. - smooth) * 4.)).r * 2.;
}
#endif

//========================================
// Fragment program lighting stage
// This may be replaced by custom code.
// See Framework.cginc
//========================================
half3 vgi_frag_lighting(v2f i, vgi_frag_tmp tmp) {
  UNITY_SETUP_INSTANCE_ID(i);
  float2  uv = i.uv.xy;

#ifdef SSS
  half3 sss = tex2D(_SSSMap, uv).rgb;
#endif

#ifdef CUSTOM_FRAGMENT_CODE
  CUSTOM_FRAGMENT_CODE
#endif

  {
    // Note: .a: was changed from (smooth + matIndex)/ 8.0f to smoothness only
    // we can consider *2 and saturate to allow textures to got rough or smooth

#ifdef TEXTURE_COLOR
    tmp.materialColor *= tex2D(_MainTex, uv);
#endif
#ifdef TEXTURE_ARRAY
    tmp.materialColor *= UNITY_SAMPLE_TEX2DARRAY(_TexArray, i.uv.xyz);
#endif
  }

  // assume normalized vectors
 half nDotL = dot(tmp.normal, _LightVector.xyz);
#ifdef SSS
  half3 diffuseIntensity = saturate(lerp(
      dot(tmp.normal, _LightVector.xyz).xxx,
      (dot(i.normal.xyz, _LightVector.xyz) + .3) / 1.3,
      sss));
  half3 indirectDiffuse = calc_indirect_diffuse_light(i, tmp, sss);
#else
  half diffuseIntensity = saturate(nDotL);
  half3 indirectDiffuse = calc_indirect_diffuse_light(i, tmp);
#endif // SSS

  half3 result = tmp.materialColor.rgb * ( indirectDiffuse
#if defined(DIRECTIONAL_LIGHT)
    + _LightColor.rgb * (tmp.shadowFactor * diffuseIntensity)
#endif // DIRECTIONAL_LIGHT
    );

#if defined(BLOB_SHADOWS)
  result *= tmp.shadowFactor;
#endif // BLOB_SHADOWS

#if ALPHA_MATERIAL
  half smooth = tmp.materialColor.w;
  half metal = tmp.materialProps.x;
  half specMul = tmp.materialProps.y;
  half glow = tmp.materialProps.z;

  half notMetal = 1. - metal;

  half3 eye = normalize(i.wPos.xyz - _WorldSpaceCameraPos);
  half f = saturate(1.1 + dot(tmp.normal, eye) + metal);
  half specIntensity = specMul *
    f*f // fresnel
    // adjust intenstiy and remove non-metal specular at 0 smoothness
      * (metal + smooth * notMetal * 4.);

  float3 r = eye - tmp.normal * (2. * dot(tmp.normal, eye));

  // antialias reflections by lowering smoothness
  // where there is a large change to the reflection vector
  // from pixel to pixel. Factor of 16 found by experimentation
  // and power-of-2 multiply very efficient on target mobile GPU
  smooth *= 1. - saturate((lengthSquared(ddx(r)) + lengthSquared(ddy(r))) * 16.);
  half ambientSpecular = ambient_specular_from_cube_map(r, smooth, metal);
#if DIRECT_SPECULAR
  // half sunSpecular = reflectionMapData.b * saturate(nDotL + .25);
  half sunSpecular =
      myPow(saturate(dot(r, _LightVector.xyz)) + 0.003, smooth * (80. + 160. * notMetal)) *
      saturate(nDotL + .25);
#endif // DIRECT_SPECULAR

  result = result * notMetal +
    (indirectDiffuse * ambientSpecular
  #if DIRECT_SPECULAR
       + _LightColor.rgb * (tmp.shadowFactor * sunSpecular)
  #endif // DIRECT_SPECULAR
      ) *
      (notMetal + tmp.materialColor.rgb * metal) * specIntensity +
    glow * tmp.materialColor.rgb; // emissive/glow
#endif // ALPHA_MATERIAL

// LIGHTING DEBUGGER: Here is most of the logic that can be enabled via multi_compile in the shader files.
#if defined(_RENDER_DEBUG_DIRECT_DIFFUSE)
  #if defined(DIRECTIONAL_LIGHT)
    result.rgb = diffuseIntensity * _LightColor.rgb;
  #else
    result.rgb = 0;
  #endif
#elif defined(_RENDER_DEBUG_DIRECT_SPECULAR)
  #if DIRECT_SPECULAR && ALPHA_MATERIAL
    result.rgb = (notMetal + txColor.rgb * metal) * specIntensity + sunSpecular * _LightColor.rgb;
  #else
    result.rgb = 0;
  #endif
#elif defined(_RENDER_DEBUG_INDIRECT_DIFFUSE)
    result.rgb = indirectDiffuse;
#elif defined(_RENDER_DEBUG_INDIRECT_SPECULAR)
  #if ALPHA_MATERIAL
    result.rgb = ambientSpecular;
  #else
    result.rgb = 0;
  #endif
#elif defined(_RENDER_DEBUG_SHADOW)
    result.rgb = tmp.shadowFactor;
#elif defined(_RENDER_DEBUG_VERTEX_COLOR)
  result.rgb = tmp.materialColor;
#elif defined(_RENDER_DEBUG_TEXTURE_COLOR)
    result.rgb = txColor;
#elif defined(_RENDER_DEBUG_REFLECTION)
  #if ALPHA_MATERIAL
    result.rgb = reflectionMapData;
  #else
    result.rgb = 0;
  #endif
#elif defined(_RENDER_DEBUG_NORMALS)
  #if defined(NORMAL_MAPPING)
    result = tmp.rawNT;  // Normal
  #else
    result.rgb = 0;
  #endif
#elif defined(_RENDER_DEBUG_DISPLACEMENT)
  #if defined(DISPLACEMENT) && defined(NORMAL_MAPPING)
    result = tex2D(_DispMap, uv*(float2(2., 1.)) + float2(0., .5)).xyz;  // Displacement
  #else
    result.rgb = 0;
  #endif
#elif defined(_RENDER_DEBUG_SSS)
  #ifdef SSS
    result.rgb = sss;
  #else
    result.rgb = 0;
  #endif
#elif defined(_RENDER_DEBUG_FOG)
    result.rgb = 0;
#else
    // return the result that was calculated by the original combined shader.
#endif

  return result;
}

//================================
// Fragment program final stage
// - fog
// - tonemapping
//================================
float4 vgi_frag_final(v2f i, half3 color) {
  half4  result = float4(color, 1.0);

  // In "safe mode", desaturate colors and reduce contrast beyond a radius around the user
#if fbSAFE_MODE
  float3  viewDistance = length(i.wPos.xyz - _WorldSpaceCameraPos);
  // result = fbSafeModeGrid(result, i.wPos.xyz, viewDistance);
  // clip(result.a - 0.1);
#elif !defined(_RENDER_DEBUG_DIRECT_DIFFUSE) \
   && !defined(_RENDER_DEBUG_DIRECT_SPECULAR) \
   && !defined(_RENDER_DEBUG_INDIRECT_DIFFUSE) \
   && !defined(_RENDER_DEBUG_INDIRECT_SPECULAR) \
   && !defined(_RENDER_DEBUG_SHADOW) \
   && !defined(_RENDER_DEBUG_VERTEX_COLOR) \
   && !defined(_RENDER_DEBUG_TEXTURE_COLOR) \
   && !defined(_RENDER_DEBUG_REFLECTION) \
   && !defined(_RENDER_DEBUG_NORMALS) \
   && !defined(_RENDER_DEBUG_DISPLACEMENT) \
   && !defined(_RENDER_DEBUG_SSS)
  fbFog_frag(result, i.l1.w);
#endif
  result.rgb = fbToneMap(result.rgb, i.pos.xy);

// Quality level debugger
#if QUALITY_LEVEL_DEBUG
#if LOW_QUALITY
#endif
#if HIGH_QUALITY
    result.r = 1.0;
#else
    result.b = 1.0;
#endif
#endif // QUALITY_LEVEL_DEBUG

#if defined(BLOB_SHADOWS) && BLOB_SHADOWS_SURFACE_DEBUG
  result = blobSurfaceDebug(result);
#endif

  return result;
}
