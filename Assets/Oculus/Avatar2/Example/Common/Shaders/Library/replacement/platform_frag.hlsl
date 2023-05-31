
float3 SRGBtoLINEAR(float3 srgbIn);

float3 ConvertOutputColorSpaceFromSRGB(float3 srgbInput)
{
#ifdef UNITY_COLORSPACE_GAMMA
  return srgbInput;
#else
  return SRGBtoLINEAR(srgbInput);
#endif
}

float3 LINEARtoSRGB(float3 color);

float3 ConvertOutputColorSpaceFromLinear(float3 linearInput)
{
#ifdef UNITY_COLORSPACE_GAMMA
  return LINEARtoSRGB(linearInput);
#else
  return linearInput;
#endif
}

float4 StaticSelectMaterialModeColor(sampler2D texSampler, float2 texCoords, float4 vertexColor) {
#if defined(MATERIAL_MODE_VERTEX)
  return vertexColor;
#else
  return tex2D(texSampler, texCoords);
#endif
}

int getLightCount() {
#ifdef USING_URP
    return GetAdditionalLightsCount() + 1;
#else
    return 1;
#endif
}

float3 getLightDirection() {
  #ifdef USING_URP
    return -GetMainLight().direction;
  #else
      return -_WorldSpaceLightPos0;
  #endif
}

float3 getLightColor() {
#ifdef USING_URP
    return _MainLightColor;
#else
    return _LightColor0;
#endif
}

float3 getLightPosition() {
#ifdef USING_URP
    return _MainLightPosition;
#else
    return _WorldSpaceLightPos0;
#endif
}

OvrLight getAdditionalLight(int idx, float3 worldPos){
#ifdef USING_URP
    return OvrGetAdditionalLight(idx, worldPos);
#else
    OvrLight dummy;
    return dummy;
#endif
}
