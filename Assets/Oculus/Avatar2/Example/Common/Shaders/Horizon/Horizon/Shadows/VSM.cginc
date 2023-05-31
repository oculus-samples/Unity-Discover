//========================================================
// Variance shadow-mapping
//========================================================


// xyz: varianceShadowOffset, varianceShadowExpansion, 1/(1-varianceShadowExpansion), w:unused
float4 _VarianceShadowParams;

// @param samp from texture lookup
// @return 0..1, 0:in shadow 1:not in shadow
half ComputeVSMShadowFactor(float2 samp, float fragmentDepth) {
  // Must be >0, to avoid division by 0
  // Used for alleviating light bleeding, by expanding the shadows to fill in the gaps.
  float varianceShadowExpansion = _VarianceShadowParams.y;
  // Variance shadowmap depth offset. Applied before storing depth values in the variance shadowmap,
  // to improve precision, by moving values closer to 0.0f.
  // May be needed when a low precision VSM (e.g. RGHalf) is used.
  float vSMOffset = _VarianceShadowParams.x;

  // https://www.gdcvault.com/play/1023808/Rendering-Antialiased-Shadows-with-Moment
  // https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch08.html
  // The moments of the fragment live in "_shadowTex"
  float2  s = samp.rg;
  
  fragmentDepth -= vSMOffset;

  // Average / expected depth and depth^2 across the texels
  // E(x) and E(x^2)
  float  x = s.r; 
  float  x2 = s.g;

  // Variance of the texel, based on var = E(x^2) - E(x)^2
  // https://en.wikipedia.org/wiki/Algebraic_formula_for_the_variance#Proof
  float  var = x2 - x*x; 

  // Calculate initial probability based on the basic depths
  // If our depth is closer than x, then the fragment has a 100%
  // probability of being lit (p=1)
  float  p = fragmentDepth <= x;

  // Calculate the upper bound of the probability using Chebyshev's inequality
  // https://en.wikipedia.org/wiki/Chebyshev%27s_inequality
  float  delta = fragmentDepth - x;
  float  p_max = var / (var + delta*delta);

  float invQuotient = _VarianceShadowParams.z;

  // To alleviate light bleeding, expand the shadows to fill in the gaps
  p_max = saturate((p_max - varianceShadowExpansion) * invQuotient);

  return max(p, p_max);
}
