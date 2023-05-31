//================================================
// Common color management code
// - tonemapping
// - device-specific colorspace correction
// - texture color linearization
//================================================

#ifndef FBToneMapping_cginc
#define FBToneMapping_cginc


// Workaround for darkness issues on Quest/Android
// see: https://fb.facebook.com/groups/1684852458198687/permalink/2344960525521207/
inline void _applyAndroidGammaWorkaround(inout float3 color) {
#ifdef ANDROID_GAMMA_WORKAROUND
//  color = pow(color, 1.0 / 2.2);
//  color = sqrt(color);  // Cheaper approximation of gamma 2.2
#endif
}

// Tonemapping global variables, set by a script
// x:Gain, y:Gamma, z:1/Gamma, w:unused
half4 _fbTonemapperParams;


//========================================================================================================
// De-gamma-correct a color. Disabled for now, as we use sRGB textures that the HW can linearize
half3 fbDeGamma(half3 inColor)
{
  return(inColor);
//return pow(inColor, 2.0);
}


//========================================================================================
// Device-dependent final color correction
// On some devices, this function may be compiled out, but to ensure cross-device
// color consistency, either this function or fbToneMap must be called from every shader.
//========================================================================================
half3 fbDeviceColor(half3 inColor) {
  half3  color = inColor;
  _applyAndroidGammaWorkaround(color.rgb);
  return inColor;
}


// from http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
half sRGB2Linear(half C_srgb) {
	// ~  C_lin_1 = pow(C_srgb, 2.2);

	half C_lin;

	[flatten] if (C_srgb <= 0.04045f)
		C_lin = C_srgb / 12.92f;
	else
		C_lin = pow((C_srgb + 0.055f) / 1.055f, 2.4f);

	return C_lin;
}
half3 sRGB2Linear(half3 C_srgb)
{
	return half3(sRGB2Linear(C_srgb.r), sRGB2Linear(C_srgb.g), sRGB2Linear(C_srgb.b));
}
// from http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
half Linear2sRGB(half C_lin) {
	// ~  C_srgb = pow(C_lin, 1 / 2.2);

	half C_srgb;

	[flatten] if (C_lin <= 0.0031308f)
		C_srgb = C_lin * 12.92;
	else
		C_srgb = 1.055f * pow(C_lin, 1.0f / 2.4f) - 0.055f;

	return C_srgb;
}
half3 Linear2sRGB(half3 C_lin) {
	return half3(Linear2sRGB(C_lin.r), Linear2sRGB(C_lin.g), Linear2sRGB(C_lin.b));
}

// https://developer.oculus.com/blog/tech-note-shader-snippets-for-efficient-2d-dithering
float Dither17(float2 svPosition) {
 	float2 k0 = float2(2.0f, 7.0f) / 17.0f;
 	float Ret = dot(svPosition, k0);
 	return frac(Ret);
}

// 0:off (default), 1:on (never check in like this)
// red dither: shader is affected but setting is low quality
// green dither: shader is affected but setting is low quality  
#define TEST_DITHER 0

// good read: http://loopit.dk/banding_in_games.pdf
// @param linearRGB LDR color, usually in 0..1 range
// @param svPosition xy from SV_Position
void Dither8BitRGB(inout half3 linearRGB, float2 svPosition) {
  // 0..1
  // for more functions see: https://developer.oculus.com/blog/tech-note-shader-snippets-for-efficient-2d-dithering
  half dither = Dither17(svPosition);
            
            
  // -3: if we output in sRGB (e.g. Unity in Gamma mode) we can use this simple shader
  // -2: visualize pattern
  // -1: off
  //  0: reference (slow)
  //  1: approximated with ^2, optimized
  //  2: same: apply (a+b)^2 = a*a + 4*a*b + b*b, faster?
  //  3: further approximated (removed d*d)
  //  4: further approximated (removed offset)
  int mode = -1;
  
#if SHADER_API_D3D11
  // A blue noise texture would be higher quality,
  // here we use math for integration simplicity.
  mode = 1;
#else
  #if HIGH_QUALITY
    mode = 4;
  #endif
  // low quality is accepting banding
#endif

#if TEST_DITHER
  #if HIGH_QUALITY
    linearRGB = float3(0,1,0) + dither; // green
  #else 
    linearRGB = float3(1,0,0) + dither; // red
  #endif
#endif
    
  if(mode == 0) {
    float3 sRGB = Linear2sRGB(linearRGB);
    sRGB += (dither - 0.5f) / 256.0f;
    linearRGB = sRGB2Linear(sRGB);

  } else if(mode == 1) {
    half3 sRGB = sqrt(linearRGB);
    sRGB += (dither - 0.5f) / 256.0f;
    linearRGB = sRGB * sRGB;

  } else if(mode == 2) {
    float d = (dither - 0.5f) / 256.0f;
    linearRGB += sqrt(linearRGB) * (4.0f * d) + d * d;

  } else if(mode == 3) {
    float d = (dither - 0.5f) / 256.0f;
    linearRGB += sqrt(linearRGB) * (4.0f * d);

  } else if(mode == 4) {
    linearRGB += sqrt(linearRGB) * (dither / 64.0f);

  } else if(mode == -2) {
    linearRGB = sRGB2Linear(dither < svPosition.x / 512.0f);

  } else if(mode == -3) {
    linearRGB += (dither - 0.5f) / 256.0f;

  }
}


//========================================================================================
// Apply tonemapping and device-dependent final color correction
//========================================================================================
// @param svPosition xy from SV_Position
half3 fbToneMap(half3 inColor, float2 svPosition) {

  half gain = _fbTonemapperParams.r;
  half gamma = _fbTonemapperParams.g;
  half invGamma = _fbTonemapperParams.b;

//  float3  color = pow(float3(1.0, 1.0, 1.0) - exp(-inColor * _fbGain), 1.0 / _fbGamma);

  half3  color;

// Gain and gamma with a nice "adaptive" exponential tonemapper
#if fbTmINVEXP
  // todo: exp(y) = pow(e, y) = exp2(y * log2(e))
  color = pow(half3(1.0, 1.0, 1.0) - exp(-inColor * gain), invGamma);
#else
  #if fbTmGAIN_GAMMA
  // Gain_and_gamma
  color = pow(inColor * gain, invGamma);
  #else
  color = inColor;
  #endif
#endif

  // uncomment to create test content to debug Dither8BitRGB()
//  color.rgb = (svPosition.x / 20000) + (int)(svPosition.y / 32) / (1024 / 32.0f);

  Dither8BitRGB(color.rgb, svPosition);

  _applyAndroidGammaWorkaround(color.rgb);
  return color;
}
#endif // FBToneMapping_cginc
