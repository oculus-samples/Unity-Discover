
// create MSAA sample mask from alpha value for to emulate transparency
// used by DitherCoverageFromMaskMSAA4(), don't call this function
// for 4x MSAA, also works for more MSAA but at same quality, FFR friendly 
// @param alpha 0..1
uint _CoverageFromMaskMSAA4(float alpha) {
  // can be optimized futher
  uint Coverage = 0x00;
  if (alpha > 0 / 4.0) Coverage = 0x88;
  if (alpha > 1 / 4.0) Coverage = 0x99;
  if (alpha > 2 / 4.0) Coverage = 0xDD;
  if (alpha > 3 / 4.0) Coverage = 0xFF;
  return Coverage;
}

// see https://developer.oculus.com/blog/tech-note-shader-snippets-for-efficient-2d-dithering
// used by DitherCoverageFromMaskMSAA4(), renamed Dither17() to avoid name conflict, can be unified
float Dither17b(float2 svPosition, float frameIndexMod4) {
  float3 k0 = float3(2, 7, 23);
  float Ret = dot(float3(svPosition, frameIndexMod4), k0 / 17.0f);
  return frac(Ret);
}

// create MSAA sample mask from alpha value for to emulate transparency,
// for 4x MSAA,  can show artifacts with FFR
// @param alpha 0..1, outside range should behave the same as saturate(alpha)
// @param svPos xy from SV_Position
// @param true: with per pixel dither, false: few shades
uint CoverageFromMaskMSAA4(float alpha, float2 svPos, bool dither) {
  // using a constant in the parameters means the dynamic branch (if) gets compiled out,
  if (dither) {
    // the pattern is not animated over time, no extra perf cost
    float frameIndexMod4 = 0;
    // /4: to have the dithering happening with less visual impact as 4
    //     shades are already implemented with MSAA subsample rejection.
    // -=: subtraction because the function CoverageFromMaskMSAA4() shows visuals 
    //     effect >0 and we want to have some pixels not have effect depending on dithering.
    alpha -= Dither17b(svPos, frameIndexMod4) / 4;
  }
  else {
    // no dithering, no FFR artifacts with Quest
    alpha -= 0.5f / 4;
  }
  return _CoverageFromMaskMSAA4(alpha);
}
