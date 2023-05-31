#ifndef OVR_SKINNING_COMPUTE_INCLUDED
#define OVR_SKINNING_COMPUTE_INCLUDED

#include "../../../ShaderUtils/OvrDecodeUtils.cginc"

float4x4 GetJointMatrix(
  in ByteAddressBuffer data_buffer,
  uint joint_matrices_start_address,
  uint joint_index)
{
  // Load 4x float4s, store in matrix, return
  static const uint STRIDE = 16u * 4u; // 16 floats in matrix. Each float is 4 bytes

  const uint matrix_data_start_address = mad(joint_index, STRIDE, joint_matrices_start_address);
  return OvrUnpackFloat16x32(data_buffer, matrix_data_start_address);
}

void ApplySkinning(
  int max_joints_to_skin,
  in ByteAddressBuffer dynamic_data_buffer,
  uint joint_matrices_start_address,
  inout float4 position,
  inout float3 normal,
  inout float3 tangent,
  in float4 joint_weights,
  in uint4 joint_indices,
  bool has_tangents)
{
  // The weights used should all sum up to 1.0 to prevent distortion
  // In cases where the encoded data encodes up to 4 weights, but
  // less are used due to a runtime setting, the weights may have to be recalculated
  float sum_of_weights = joint_weights.x;
  float4x4 blended_model_matrix = GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.x) * joint_weights.x;
  float4x4 blended_normal_matrix = GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.x + 1u) * joint_weights.x;

  [branch]
  if (max_joints_to_skin > 1 && joint_weights.y > 0) {
    blended_model_matrix += GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.y) * joint_weights.y;
    blended_normal_matrix += GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.y + 1u) * joint_weights.y;
    sum_of_weights += joint_weights.y;

    [branch]
    if (max_joints_to_skin > 2 && joint_weights.z > 0) {
      blended_model_matrix += GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.z) * joint_weights.z;
      blended_normal_matrix += GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.z + 1u) * joint_weights.z;
      sum_of_weights += joint_weights.z;

      [branch]
      if (max_joints_to_skin > 3 && joint_weights.w > 0) {
        blended_model_matrix += GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.w) * joint_weights.w;
        blended_normal_matrix += GetJointMatrix(dynamic_data_buffer, joint_matrices_start_address, joint_indices.w + 1u) * joint_weights.w;
        sum_of_weights += joint_weights.w;
      } // end if max_joints_to_skin > 3
    } // end if max_joints_to_skin > 2
  } // end if max_joints_to_skin > 1

  blended_model_matrix = blended_model_matrix / sum_of_weights;
  blended_normal_matrix = blended_normal_matrix / sum_of_weights;

  position.xyz = mul(blended_model_matrix, position).xyz;
  normal = mul(blended_normal_matrix, float4(normal, 0.0)).xyz;

  if (has_tangents) {
    tangent.xyz = mul(blended_model_matrix, float4(tangent.xyz, 0.0)).xyz;
  }
}

#endif
