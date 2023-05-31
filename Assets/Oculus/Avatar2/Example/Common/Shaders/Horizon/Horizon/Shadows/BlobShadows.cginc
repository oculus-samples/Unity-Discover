// how much quad vertices are pushed out. allows for shadow footprint to be larger than object. must be >= 1.
#define BLOB_QUAD_MULT 1.0

// distance in world space, how fast to fade off dynamic shadow map at edge
#define BLOB_SHADOW_MAP_EDGE 1.0

// maximum value for a surface to be considered vertical and to get no shadow (y component of normal)
#define BLOB_MIN_VERTICAL_SURFACE 0.3

// max value for blob shadow
#define BLOB_SHADOW_MAX_FACTOR 0.5

// tint casters green and receivers red.
#define BLOB_SHADOWS_SURFACE_DEBUG 0

// objects with a shadow footprint dimension between A and B will fade to 0 influence.
// this should be tuned along with the current shadowmap orthosize and resolution.
#define BLOB_DIMENSION_FADE_MIN 0.050
#define BLOB_DIMENSION_FADE_MAX 0.150

// uniforms

// location of blob shadow camera in world space
float3 _BlobCameraWorldPos;

// 1 / (size property of orthographic projection blob camera)
float _BlobOrthoScaleFactor;

// the far plane distance for the camera casting shadows
float _BlobCasterFarClip; 

// shadow map texture
sampler2D _BlobShadowMap;

// provided by Unity: { 1/w, 1/h, w, h }
float4 _BlobShadowMap_TexelSize;

// vertex shader for shadow map :
// - generates unrotated uniform-scale quads
// - blob footprint can be larger than blob itself (currently by a fixed factor)
void blobCasterVertToFrag(in float4 localVertex, in float2 uvIn, in float4x4 localToWorld, out float4 screenPos, out float2 uvOut, out float3 worldSpherePos, out float2 worldSphereNormal, out float2 worldSphereScale) {
  // 1. extract the axis scales
  float3 xaxis = mul(localToWorld, float4(1, 0, 0, 0)).xyz;
  float3 yaxis = mul(localToWorld, float4(0, 1, 0, 0)).xyz;
  float3 zaxis = mul(localToWorld, float4(0, 0, 1, 0)).xyz;
  float3 invScale = rcp(float3(length(xaxis), length(yaxis), length(zaxis)));
  float3 scaleXZ = float3(length(xaxis.xz), length(yaxis.xz), length(zaxis.xz));

  // find the bbox face whose downward facing area is largest.
  float xface = xaxis.y * invScale.x * scaleXZ.y * scaleXZ.z;
  float yface = yaxis.y * invScale.y * scaleXZ.x * scaleXZ.z;
  float zface = zaxis.y * invScale.z * scaleXZ.x * scaleXZ.y;
  float maxaface = max(abs(xface), max(abs(yface), abs(zface)));

  float2 u;
  float3 normal;
  if (abs(xface) == maxaface) {
    u = normalize(float2(yaxis.x, yaxis.z));
    normal = xaxis * invScale.x;

    float alpha1 = scaleXZ.y * invScale.y;
    float alpha2 = scaleXZ.z * invScale.z;
    if (abs(yface) >= abs(zface)) {
      // next axis is y
      worldSphereScale.x = alpha1 * scaleXZ.y + (1 - alpha1) * scaleXZ.x;
      worldSphereScale.y = alpha2 * scaleXZ.z + (1 - alpha2) * scaleXZ.z;
      //normal = alpha1 * xaxis * invScale.x + alpha2 * yaxis * invScale.y;
    } else {
      // next axis is z
      worldSphereScale.x = alpha1 * scaleXZ.y + (1 - alpha1) * scaleXZ.x;
      worldSphereScale.y = alpha2 * scaleXZ.z + (1 - alpha2) * scaleXZ.y;
      //normal = alpha1 * xaxis * invScale.x + alpha2 * zaxis * invScale.z;
    }
  } else if (abs(yface) == maxaface) {
    u = normalize(float2(xaxis.x, xaxis.z));
    normal = yaxis * invScale.y;

    float alpha1 = scaleXZ.x * invScale.x;
    float alpha2 = scaleXZ.z * invScale.z;
    if (abs(xface) >= abs(zface)) {
      // next axis is x
      worldSphereScale.x = alpha1 * scaleXZ.x + (1 - alpha1) * scaleXZ.y;
      worldSphereScale.y = alpha2 * scaleXZ.z + (1 - alpha2) * scaleXZ.z;
      //normal = alpha1 * yaxis * invScale.y + alpha2 * xaxis * invScale.x;
    } else {
      // next axis is z
      worldSphereScale.x = alpha1 * scaleXZ.x + (1 - alpha1) * scaleXZ.x;
      worldSphereScale.y = alpha2 * scaleXZ.z + (1 - alpha2) * scaleXZ.y;
      //normal = alpha1 * yaxis * invScale.y + alpha2 * zaxis * invScale.z;
    }
  } else {
    u = normalize(float2(xaxis.x, xaxis.z));
    normal = zaxis * invScale.z;

    float alpha1 = scaleXZ.x * invScale.x;
    float alpha2 = scaleXZ.y * invScale.y;
    if (abs(xface) >= abs(yface)) {
      // next axis is x
      worldSphereScale.x = alpha1 * scaleXZ.x + (1 - alpha1) * scaleXZ.y;
      worldSphereScale.y = alpha2 * scaleXZ.y + (1 - alpha2) * scaleXZ.z;
      //normal = alpha1 * zaxis * invScale.z + alpha2 * xaxis * invScale.x;
    }
    else {
      // next axis is y
      worldSphereScale.x = alpha1 * scaleXZ.x + (1 - alpha1) * scaleXZ.x;
      worldSphereScale.y = alpha2 * scaleXZ.y + (1 - alpha2) * scaleXZ.z;
      //normal = alpha1 * zaxis * invScale.z + alpha2 * yaxis * invScale.y;
    }
  }

  float2 v = float2(-u.y, u.x);
  u *= worldSphereScale.x;
  v *= worldSphereScale.y;

  worldSphereNormal = normal.xz;

  // 2. extract position for placement of quad
  worldSpherePos = mul(localToWorld, float4(0, 0, 0, 1)).xyz;

  // 3. form new matrix which has no rotation, only x-z scale
  float4 newx = float4(u.x, 0.0, v.x, worldSpherePos.x);
  float4 newy = float4(0.0, 1.0, 0.0, worldSpherePos.y);
  float4 newz = float4(u.y, 0.0, v.y, worldSpherePos.z);
  float4 neww = float4(0, 0, 0, 1);
  float4x4 decalToWorld = float4x4(newx, newy, newz, neww);

  float4 scaledVertex = float4(localVertex.xyz * BLOB_QUAD_MULT, 1); 

  // 4. use rebuilt transform to generate quad in xz plane.
  screenPos = mul(mul(UNITY_MATRIX_VP, decalToWorld), scaledVertex);
  uvOut = uvIn;
}

//fragment shader for shadow map :
// - stores an occlusion factor (lower at edges of sphere, higher at center) + min and max
// - normalized to ortho camera frustum
// - min is calculated from top of frustum, max from bottom (camera is above looking down)
float4 blobCasterFrag(in float4 screenPos, in float2 uv, in float3 worldCenter, in float2 worldNormal, in float2 worldSphereScale) {
  // here we're trying to draw an ellipsoid height map on a flat quad.
  // imagine a graph of x^2/a^2 + y^2/b^2 + z^2/c^2 = 1, centered at the origin.
  // y will be our output channels, and we shift the uv's to use as our x and z.

  // offset uv to be centered at 0,0 and reject any pixel outside a 0.5 radius circle
  float2 offsetUV = uv - float2(0.5, 0.5);
  float uvLength2 = dot(offsetUV, offsetUV);

  // max radius squared (in uv space) of any circle inscribed within the quad
  const float influenceLength2 = 0.5 * 0.5;
  if (uvLength2 > influenceLength2) {
    return float4(0,0,0,0); 
  }

  float heightOffset = -dot(worldNormal, offsetUV * worldSphereScale);

  float2 pixelUV = float2(1.0 - 2.0 * screenPos.x * _BlobShadowMap_TexelSize.x, 1.0 - 2.0 * screenPos.y * _BlobShadowMap_TexelSize.y);
  float pixelR = sqrt(pixelUV.x * pixelUV.x + pixelUV.y * pixelUV.y);
  float innerR = ((1.0 / _BlobOrthoScaleFactor) - BLOB_SHADOW_MAP_EDGE) * _BlobOrthoScaleFactor;
  const float outerR = 1.0f;
  float edgeFade = 1.0 - saturate((pixelR - innerR)/(outerR - innerR));

  // fade out the shadow map at the top and bottom of the shadow frustum
  float frustumMidpoint = _BlobCameraWorldPos.y - _BlobCasterFarClip * 0.5f;
  float frustumTopMarginOffset = _BlobCasterFarClip * 0.5f - BLOB_SHADOW_MAP_EDGE;
  float objectOffset = abs(worldCenter.y - frustumMidpoint);
  float heightFade = 1.0f - saturate((objectOffset - frustumTopMarginOffset) / BLOB_SHADOW_MAP_EDGE);

  // 0 at edge of circle, 1 at origin.
  float occlusionFactor = edgeFade * heightFade * (1.0 - uvLength2 / influenceLength2);

  // hMin is the point on the sphere furthest from the caster camera.
  // it ranges from 0 to 1, with 0 being at camera.y and 1 at the far clip plane
  float diskWorldY = worldCenter.y + heightOffset;
  float invFarClip = 1.0f / _BlobCasterFarClip;
  float hMin = (_BlobCameraWorldPos.y - diskWorldY) * invFarClip;

  // hMax is the point on the sphere closest to (or behind) the caster camera.
  // it ranges from 0 to 1, with 0 being at the far clip plane and 1 at camera.y
  float hMax = (diskWorldY - (_BlobCameraWorldPos.y - _BlobCasterFarClip)) * invFarClip;

  return float4(occlusionFactor, hMin, hMax, min(worldSphereScale.x, worldSphereScale.y));
}

// fragment shader for drawing shadow map:
//  reject empty pixels and just show the occlusion factor and min height as R and G
float4 blobCasterDebug(in sampler2D shadowMap, in float2 uv) {
  float4 shadowParams = tex2D(shadowMap, uv);
  if (shadowParams.x == 0) {
    return float4(0, 0, 0, 1);
  }
  return float4(shadowParams.x, shadowParams.y, 0, 1);
}

// surface shader tweak: 
//  takes final output color from surface shader and tints a thing green if it casts blob shadows, red if it receives blob shadows.
float4 blobSurfaceDebug(in float4 color) {
#if !defined(DYNAMIC)
    // color shadow receivers reddish
    color.r = clamp(color.r * 1.2, 0, 1);
    color.gb *= 0.8;
#else
    // color shadow casters greenish
    color.g = clamp(color.r * 1.2, 0, 1);
    color.rb *= 0.8;
#endif
    return color;
}

float blobShadowFactorInternal(in float2 coord, in float groundY, in float cameraY, in float cameraFarClip, in sampler2D shadowMap) {
  // retrieve shadow value for world space pixel, remove bias
  float4 shadowParams = tex2D(shadowMap, coord);

  // detect uninitialized value = no shadows
  float occlusionFactor = shadowParams.x;
  if (occlusionFactor < 0.0f) {
    return 0.0;
  }

  // reconstruct min and max height in world space
  float heightMin = cameraY - shadowParams.y * cameraFarClip;
  float heightMax = cameraY - cameraFarClip * ( 1 - shadowParams.z );
  float minDim = shadowParams.w;

  // surfaces above or below blob influence heights = no shadows
  if (groundY > heightMax) {
    return 0.0;
  }

  // 0 = no shadow, 1 = full shadow
  float pixelHeightFactor = 1.0 - (heightMin - groundY) / _BlobCasterFarClip;

  // we approach 0 faster with higher powers, so edges look good, but there is also more sausage-linking
  float pixelOcclusionFactor = pow(occlusionFactor, 2.0);

  // things that only have a couple pixels in the shadow map are weaker
  float thinFactor = smoothstep(0.0, 1.0, (minDim - BLOB_DIMENSION_FADE_MIN) / (BLOB_DIMENSION_FADE_MAX - BLOB_DIMENSION_FADE_MIN));

  // (pixellated samples are weaker) *
  // (higher shadows are lighter / lower are darker) * 
  // (pixels in the center of spheres are darker than ones on the edge) *
  // (no darker than max factor)
  return thinFactor * pixelHeightFactor * pixelOcclusionFactor * BLOB_SHADOW_MAX_FACTOR;
}

// returns a value in [0,1] to be multiplied with diffuse color to darken it. 
// 0 = full shadow / black, 1 = no shadow / surface color unchanged.
float blobShadowFactorGround(in float3 worldPos, in float3 worldNormal) {

  // convert world space to texture coordinate
  float2 coord = float2(0.5, 0.5) + 0.5 * (worldPos.xz - _BlobCameraWorldPos.xz) * _BlobOrthoScaleFactor;
  if (coord.x <= 0 || coord.x >= 1.0 || coord.x <= 0 || coord.x >= 1.0) {
    // skip any further calculations if we're outside the shadow map
    return 1.0;
  }

  // this introduces a band around shaded hovering things. the tradeoff is that undersides of things are not shadowed.
  float normalFactor = saturate((worldNormal.y + BLOB_MIN_VERTICAL_SURFACE) / (2.0 * BLOB_MIN_VERTICAL_SURFACE));
  float pixelFactor = blobShadowFactorInternal(coord, worldPos.y, _BlobCameraWorldPos.y, _BlobCasterFarClip, _BlobShadowMap);

  // things that receive blob shadowing: 
  // - (no contribution on surfaces pointing down) * (common shadow factor from above)
  // - subtract from 1.0 to put into the scale surface shader expects.
  return 1.0 - normalFactor * pixelFactor;
}

// called from surface shader, different paths if something receives shadows or casts them (which would get self shadow)
float blobShadowFactor(in float3 worldPos, in float3 worldNormal) {
#if !defined(DYNAMIC)
  return blobShadowFactorGround(worldPos, worldNormal);
#else
  return 1.0;
#endif
}
