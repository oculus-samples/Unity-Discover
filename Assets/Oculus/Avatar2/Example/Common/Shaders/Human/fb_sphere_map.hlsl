// References
// https://www.internalfb.com/intern/wiki/Avatar_SDK/GLTF/FB_sphere_map/
// https://en.wikipedia.org/wiki/Sphere_mapping
// https://www.clicktorelease.com/blog/creating-spherical-environment-mapping-shader/

#ifdef HAS_FB_SPHERE_MAP

uniform sampler2D u_SphereMapEnvSampler;
float3 getEnvironmentSphereMap(float3 n, float3 v, float roughness, float3 F0, float specularWeight) {

  // reflection vector
  float3 r = normalize(reflect(-v, n));

  // UV calculation based on reflection vector.
  float m = 2.0 * sqrt( pow( r.x, 2.0 ) + pow( r.y, 2.0 ) + pow( r.z + 1.0, 2.0 ) );
  float2 uv = float2(r.x, -r.y) / m + 0.5;

  // sample the sphere environment map
  float3 sphereMapColor = texture(u_SphereMapEnvSampler, uv).rgb;

  // factor in roughness.
  float3 sphereWeight = float3(1.0 - roughness);

  return sphereMapColor * sphereWeight * specularWeight;
}

#endif // HAS_FB_SPHERE_MAP
