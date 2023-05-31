//======================================================================================
// Common fog / haze code
// Supports constant fog density and density fall-off with altitude (height fog).
// Sun position-based scattering is a work in progress
//======================================================================================

// rgb:color, a:density*density
float4 _fbFogColor;

// [0]: xyzw: fogStartDistance, fogEndDistance, fogDensity, fogScale
// [1]: xyzw: fogDensityAltFalloffExp, fogDensityAltFalloffStart, 1/(fogEndDistance-fogStartDistance), 0
float4 _fbFogParams[2];

#ifdef fbFOG_CUBEMAP
samplerCUBE  _fbFogSkyBoxTexture;
#endif

#if (fbFOG_VTX && fbFOG_CUBEMAP) || fbFOG_EXP
#define VERT_NEED_WPOS  1  // World position is needed in the vertex shader
#define FRAG_NEED_WPOS  1  // World position is needed in the fragment shader
#endif

#if fbFOG_VTX
#define VERT_NEED_WPOS  1
#endif

//
// Macro for the fragment shader protion of fbFog
//
#if fbFOG_VTX
  // Vertex shader-based fog.

  // todo: use exp2()
  #define fbFog_vert(mDensitySq, mCameraPos, mPos, mOutW)\
  {\
    float fogScale = _fbFogParams[0].w;\
    mOutW = exp(-mDensitySq * lengthSq((mCameraPos - mPos) * fogScale));\
  }

  #if fbFOG_CUBEMAP
    #define fbFog_frag(mResult, mLerpW)\
    {\
      mResult.rgb = fbFogVert(mResult.rgb, _WorldSpaceCameraPos, i.wPos.xyz, mLerpW);\
    }
  #else
    #define fbFog_frag(mResult, mLerpW)\
    {\
      mResult.rgb = lerp(_fbFogColor.rgb, mResult.rgb, mLerpW);\
    }
  #endif
#else
  #define fbFog_vert(mDensitySq, mCameraPos, mPos, mOutW)

  // Fragment shader-based fog. It allows for more flexibility.
  #if fbFOG_EXP
    #define fbFog_frag(mResult, mLerpW)\
    {\
      mResult.rgb = fbFog(mResult.rgb, _WorldSpaceCameraPos, i.wPos.xyz);\
    }
  #else
    #define fbFog_frag(mResult, mLerpW)
  #endif
#endif



float lengthSq(float3 v) {
  return dot(v, v);
}


half3 fbFogVert(half3 color, float3 worldSpaceCameraPos, float3 worldSpacePos, half lerpW) 
{
  float3  viewRay = worldSpacePos - worldSpaceCameraPos;
  half3  fogColor = _fbFogColor.rgb;

#ifdef fbFOG_CUBEMAP
  fogColor = texCUBE(_fbFogSkyBoxTexture, viewRay).rgb;
#endif

  return lerp(fogColor, color, lerpW);
}


half3 fbFog(half3 color, float3 worldSpaceCameraPos, float3 worldSpacePos)
{
  float3  viewRay = worldSpacePos - worldSpaceCameraPos;
  half3  fogColor = _fbFogColor.rgb;

#ifdef fbFOG_CUBEMAP
  fogColor = texCUBE(_fbFogSkyBoxTexture, viewRay).rgb;
#endif
  
  float fogStartDistance = _fbFogParams[0].x;
  float fogEndDistance = _fbFogParams[0].y;
  //  1 / (fogEndDistance - fogStartDistance)
  float fogInvFogRange = _fbFogParams[1].z;
  float fogDensity = _fbFogParams[0].z;
  float fogScale = _fbFogParams[0].w;

  // linear
//  float   viewDistance = length(viewRay) * fogScale;
//  float fogT = saturate((fogEndDistance - viewDistance) * fogInvFogRange);
//  half3  result = lerp(fogColor.rgb, color, fogT);

#ifdef fbFOG_EXP
  
  float fogDensityAltFalloffExp = _fbFogParams[1].x;
  float fogDensityAltFalloffStart = _fbFogParams[1].y;

  // Exponential-squared fog, density decreasing with altitude
  float localFogDensity = fogDensity / (1.0 + pow((fogDensityAltFalloffStart + worldSpacePos.y * fogScale), fogDensityAltFalloffExp));
  float viewDistance = max(0.0, length(viewRay) * fogScale - fogStartDistance);
  float fogF = viewDistance * localFogDensity;
  half fogT = exp(- fogF * fogF);     // todo: use exp2()
// We can optimize out the sqrt in length(viewRay), if fogStartDistance is 0.
//float  viewRayS = viewRay * _fbFogScale;float   viewDistanceSq = dot(viewRayS, viewRayS), fogFSq = viewDistanceSq * _fbFogColor.a, fogT = exp(-fogFSq);
  half3  result = lerp(fogColor, color, fogT);

#else

  half3  result = color;

#endif
  
  return result;
}


//
// More accurate fog with in-scatter from the Sun
// In progress
//
float3 fbFog_InScatter(float3 color, float3 worldSpaceCameraPos, float3 worldSpacePos, float4 lightVector)
{
  float3  viewRay = worldSpacePos - worldSpaceCameraPos;
  float   viewDistance = length(viewRay);
  float3  viewDir = viewRay / viewDistance;
  
  float fogDensity = _fbFogParams[0].z;

// Exponential fog
//	float  fogT = 1.0f / pow(e, viewDistance * fogDensity);

// Exponential-squared fog
  float fogF = viewDistance * fogDensity;
  float fogT = exp(-fogF * fogF);     // todo: use exp2()
  float  fogTInv = 1.0f - fogT;
  float3  result = color * fogT + _fbFogColor * fogTInv;
//	float   inScatter = max(0.0f, dot(viewDir, lightVector.xyz));
//	result += inScatter * _fbFogColor;
  return result;
}


//
// More accurate fog with volumetric shadows
// In progress
//
float3 fbFog_Vol(float3 color, float3 worldSpaceCameraPos, float3 worldSpacePos, float4 lightVector, int rayMarchSteps)
{
  float3  viewRay = worldSpacePos - worldSpaceCameraPos;
  float   viewDistance = length(viewRay);
  float3  viewDir = viewRay / viewDistance;

  float fogDensity = _fbFogParams[0].z;

// Exponential fog
//	float  fogT = 1.0f / pow(e, viewDistance * fogDensity);

// Exponential-squared fog
  float   fogF = viewDistance * fogDensity, fogT = exp(- fogF * fogF);     // todo: use exp2()
  float   fogTInv = 1.0f - fogT;
  float3  result = color * fogT + _fbFogColor * fogTInv;
  float   inScatter = max(0.0f, dot(viewDir, lightVector.xyz));

//float shadowFactor = UNITY_SAMPLE_SHADOW(_ShadowMap, shadowDepth).r;

  result += inScatter * _fbFogColor;
  return result;
}
