
#ifdef USE_SKINNING

in float4 a_joints_0;
in float4 a_weights_0;
uniform sampler2D u_JointMatrixSampler;
uniform sampler2D u_JointNormalMatrixSampler;

// ---------------------------------------------------
// INTERNAL
float4x4 sampleMat4(sampler2D buffer, int joint) {
  int texel = joint * 4;
  return float4x4(
    texelFetch(buffer, ivec2(texel + 0, 0), 0),
    texelFetch(buffer, ivec2(texel + 1, 0), 0),
    texelFetch(buffer, ivec2(texel + 2, 0), 0),
    texelFetch(buffer, ivec2(texel + 3, 0), 0)
  );
}

// ---------------------------------------------------
// INTERNAL
float4x4 sampleJointBuffer(int joint) {
  return sampleMat4(u_JointMatrixSampler, joint);
}

// ---------------------------------------------------
// INTERNAL
float4x4 sampleJointNormalBuffer(int joint) {
  return sampleMat4(u_JointNormalMatrixSampler, joint);
}

// ---------------------------------------------------
// PUBLIC
float4x4 getSkinningMatrix() {
  float4x4 jointX = sampleJointBuffer(int(a_joints_0.x));
  float4x4 jointY = sampleJointBuffer(int(a_joints_0.y));
  float4x4 jointZ = sampleJointBuffer(int(a_joints_0.z));
  float4x4 jointW = sampleJointBuffer(int(a_joints_0.w));

  float4x4 skin =  a_weights_0.x * jointX +
          a_weights_0.y * jointY +
          a_weights_0.z * jointZ +
          a_weights_0.w * jointW;
  return skin;
}

// ---------------------------------------------------
// PUBLIC
float4x4 getSkinningNormalMatrix() {
  float4x4 jointX = sampleJointNormalBuffer(int(a_joints_0.x));
  float4x4 jointY = sampleJointNormalBuffer(int(a_joints_0.y));
  float4x4 jointZ = sampleJointNormalBuffer(int(a_joints_0.z));
  float4x4 jointW = sampleJointNormalBuffer(int(a_joints_0.w));

  float4x4 skin =  a_weights_0.x * jointX +
          a_weights_0.y * jointY +
          a_weights_0.z * jointZ +
          a_weights_0.w * jointW;

  return skin;
}

#endif // USE_SKINNING
