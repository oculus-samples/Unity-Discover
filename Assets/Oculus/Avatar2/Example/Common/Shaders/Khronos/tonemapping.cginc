uniform float u_Exposure;

static const float GAMMA = 2.2;
static const float INV_GAMMA = 1.0 / 2.2;

// linear to sRGB approximation
// see http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
float3 LINEARtoSRGB(float3 color)
{
  return pow(color, float3(INV_GAMMA, INV_GAMMA, INV_GAMMA));
}

float4 LINEARtoSRGB(float4 color) {
  return float4(pow(color.xyz, float3(INV_GAMMA, INV_GAMMA, INV_GAMMA)), color.w);
}

// sRGB to linear approximation
// see http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
float4 SRGBtoLINEAR(float4 srgbIn)
{
  return float4(pow(srgbIn.xyz, float3(GAMMA, GAMMA, GAMMA)), srgbIn.w);
}

float3 SRGBtoLINEAR(float3 srgbIn) {
  return pow(srgbIn.xyz, float3(GAMMA, GAMMA, GAMMA));
}

// Uncharted 2 tone map
// see: http://filmicworlds.com/blog/filmic-tonemapping-operators/
float3 toneMapUncharted2Impl(float3 color)
{
    static const float UA = 0.15;
    static const float UB = 0.50;
    static const float UC = 0.10;
    static const float UD = 0.20;
    static const float UE = 0.02;
    static const float UF = 0.30;
    return ((color*(UA*color+UC*UB)+UD*UE)/(color*(UA*color+UB)+UD*UF))-UE/UF;
}

float3 toneMapUncharted(float3 color)
{
    static const float W = 11.2;
    color = toneMapUncharted2Impl(color * 2.0);
    float3 whiteScale = 1.0 / toneMapUncharted2Impl(float3(W, W, W));
    return LINEARtoSRGB(color * whiteScale);
}

// Hejl Richard tone map
// see: http://filmicworlds.com/blog/filmic-tonemapping-operators/
float3 toneMapHejlRichard(float3 color)
{
    color = max(float3(0.0, 0.0, 0.0), color - float3(0.004, 0.004, 0.004));
    return (color*(6.2*color+.5))/(color*(6.2*color+1.7)+0.06);
}

// ACES tone map
// see: https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
float3 toneMapACES(float3 color)
{
    static const float A = 2.51;
    static const float B = 0.03;
    static const float C = 2.43;
    static const float D = 0.59;
    static const float E = 0.14;
    return LINEARtoSRGB(clamp((color * (A * color + B)) / (color * (C * color + D) + E), 0.0, 1.0));
}

float3 toneMap(float3 color)
{
    color *= u_Exposure;

#ifdef TONEMAP_UNCHARTED
    return toneMapUncharted(color);
#endif

#ifdef TONEMAP_HEJLRICHARD
    return toneMapHejlRichard(color);
#endif

#ifdef TONEMAP_ACES
    return toneMapACES(color);
#endif

#ifdef UNITY_COLORSPACE_GAMMA
    return LINEARtoSRGB(color); // IMPORTANT: Use when in Unity GAMMA rendering mode
#else
    return color; // IMPORTANT: Use when in Unity LINEAR rendering mode
#endif
}
