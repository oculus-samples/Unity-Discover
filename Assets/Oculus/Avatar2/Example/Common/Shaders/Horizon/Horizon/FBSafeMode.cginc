// 0.0: normal rendering. 1.0 full safe mode.
float     _SafeModeFactor;
float     _SafeModeBubbleRadius;

//
// Determine blend amount from edge of safe mode bubble to outside of bubble
//

float fbSafeModeBlend(float viewDistance) {
  float  safeModeFactor = clamp(_SafeModeBubbleRadius - viewDistance, 0.0, 1.0);         // is the pixel within safe mode bubble?
  return lerp(0.0, lerp(1.0, 0.0, safeModeFactor) , _SafeModeFactor);
}

float fbSafeModeAlpha(float viewDistance) {
  return fbSafeModeBlend(viewDistance);
}

//
// Return desaturated / transparent color based on "safe mode" settings
//
float4 fbSafeModeColor(float3 col, float viewDistance) {
  float darkValue = 0.05;
  float3 safeModeMeshColor = float3(darkValue, darkValue, darkValue);
  
  float  safeModeFactor = fbSafeModeBlend(viewDistance);
  float  saturation = 1 - lerp(1.0, safeModeFactor, _SafeModeFactor);
  
  //  Simple color desaturation
  float  luma = col.r * 0.3 + col.g * 0.59 + col.g * 0.11;
  luma = pow(luma, 0.14) * 0.75;

  // col = lerp(float3(luma, luma, luma), col, saturation);
  col = lerp(float3(luma, luma, luma) * darkValue, col, saturation);

  return float4(col, 1.0);
}

float4 fbSafeModeGrid(float4 col, float3 pos, float viewDistance) {
	// Code moved to FBSafeModeWire.shader as USE_MATH_METHOD
	return col;
}