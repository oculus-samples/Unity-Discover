#ifndef AVATAR_FBX_REVIEW_CGINC
#define AVATAR_FBX_REVIEW_CGINC

// This code is built around allowing the Fbx Review tool to
// quickly toggle color ramps being applied to Avatars various
// parts.

#if defined(_PALETTIZATION_SINGLE_RAMP)
sampler2D _ColorRamp0;
#elif defined(_PALETTIZATION_TWO_RAMP)
sampler2D _ColorRamp0;
sampler2D _ColorRamp1;
#endif

// support for greyscale -> color ramp lookup
float4 PalettizedAlbedo(float4 mainTex) {
#if defined(_PALETTIZATION_SINGLE_RAMP)
  return tex2D(_ColorRamp0, mainTex.r);
#elif defined(_PALETTIZATION_TWO_RAMP)
  float4 ramp0 = tex2D(_ColorRamp0, mainTex.r);
  float4 ramp1 = tex2D(_ColorRamp1, mainTex.r);
  float alpha = ramp1.a;
  ramp1.a = 1.0f;
  return lerp(ramp0.rgba, ramp1.rgba, alpha);
#else
  return mainTex;
#endif
}

#endif
