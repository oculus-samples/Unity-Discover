#ifndef OVR_VERTEX_COMPUTE_INCLUDED
#define OVR_VERTEX_COMPUTE_INCLUDED

#include "../../../ShaderUtils/OvrDecodeUtils.cginc"
#include "../../../ShaderUtils/OvrDecodeFormats.cginc"

struct Vertex {
  float4 position;
  float3 normal;
  uint vertexIndex;
  uint4 jointIndices;
  float4 jointWeights;
  uint outputIndex;
};

///////////////////////////////////////////////////
// Neutral Pose
///////////////////////////////////////////////////

float4 GetNeutralPosePosition(
  ByteAddressBuffer data_buffer,
  uint positions_start_address,
  float3 bias,
  float3 scale,
  uint vertex_index,
  int format)
{
  static const uint STRIDE_X32 = 4u * 4u; // 4 32-bit uints for 4 32-bit values
  static const uint STRIDE_X16 = 2u * 4u; // 2 32-bit uints for 4 16-bit values
  static const uint STRIDE_X8 = 1u * 4u; // 1 32-bit uint for 4 8-bit values

  float4 position = float4(0.0, 0.0, 0.0, 1.0);

  [branch] switch(format) {
    case OVR_FORMAT_FLOAT_32:
      // 4 32-bit uints for 4 32-bit floats
      position.xyz = OvrUnpackFloat3x32(
        data_buffer,
        mad(vertex_index, STRIDE_X32, positions_start_address));
    break;
    case OVR_FORMAT_HALF_16:
      // 2 32-bit uints for 4 16 bit halfs
      position.xyz = OvrUnpackHalf3x16(
        data_buffer,
        mad(vertex_index, STRIDE_X16, positions_start_address));
    break;
    case OVR_FORMAT_UNORM_16:
      position.xyz = OvrUnpackUnorm3x16(
        data_buffer,
        mad(vertex_index, STRIDE_X16, positions_start_address));
    break;
    case OVR_FORMAT_SNORM_10_10_10_2:
      position.xyz = OvrUnpackVector_10_10_10_2(
        data_buffer,
        mad(vertex_index, STRIDE_X8, positions_start_address));
    break;
    case OVR_FORMAT_UNORM_8:
      position.xyz = OvrUnpackUnorm3x8(
        data_buffer,
        mad(vertex_index, STRIDE_X8, positions_start_address));
    break;
    default:
      break;
  }

  // Apply scale and bias
  position.xyz = mad(position.xyz, scale, bias);

  return position;
}

float3 GetNeutralPoseNormal(
  ByteAddressBuffer data_buffer,
  uint normals_start_address,
  uint vertex_index)
{
  // Only supporting 10-10-10-2 snorm at the moment
  static const uint STRIDE = 1u * 4u; // 1 32-bit uint for 3 10-bit SNorm and 1 2-bit extra
  return normalize(OvrUnpackSnorm3x10_10_10_2(
    data_buffer,
    mad(vertex_index, STRIDE, normals_start_address)));
}

float4 GetNeutralPoseTangent(
  ByteAddressBuffer data_buffer,
  uint tangents_start_address,
  uint vertex_index)
{
  // Only supporting full floats for positions at the moment
  static const uint STRIDE = 1u * 4u; // 1 32-bit uint for 3 10-bit snorm and 1 2bit extra
  float4 tangent = OvrUnpackSnorm4x10_10_10_2(
    data_buffer,
    mad(vertex_index, STRIDE, tangents_start_address));

  tangent.xyz = normalize(tangent.xyz);

  return tangent;
}

float4 GetJointWeights(
  in ByteAddressBuffer data_buffer,
  uint joint_weights_address,
  uint vertex_index,
  uint format)
{
  static const uint STRIDE_X32 = 4u * 4u; // 4 32-bit uints for 4 32-bit values
  static const uint STRIDE_X16 = 2u * 4u; // 2 32-bit uints for 4 16-bit values
  static const uint STRIDE_X8 = 1u * 4u; // 1 32-bit uint for 4 8-bit values

  float4 weights = float4(0.0, 0.0, 0.0, 0.0);

  [branch] switch(format) {
    case OVR_FORMAT_FLOAT_32:
      // 4 32-bit uints for 4 32-bit floats
      weights = OvrUnpackFloat4x32(
        data_buffer,
        mad(vertex_index, STRIDE_X32, joint_weights_address));
    break;
    case OVR_FORMAT_HALF_16:
      // 2 32-bit uints for 4 16 bit halfs
      weights = OvrUnpackHalf4x16(
        data_buffer,
        mad(vertex_index, STRIDE_X16, joint_weights_address));
    break;
    case OVR_FORMAT_UNORM_16:
      weights = OvrUnpackUnorm4x16(
        data_buffer,
        mad(vertex_index, STRIDE_X16, joint_weights_address));
    break;
    case OVR_FORMAT_UNORM_8:
      weights = OvrUnpackUnorm4x8(
        data_buffer,
        mad(vertex_index, STRIDE_X8, joint_weights_address));
    break;
    default:
      break;
  }

  return weights;
}

uint4 GetJointIndices(
  in ByteAddressBuffer data_buffer,
  uint joint_indices_address,
  uint vertex_index,
  uint format)
{
  static const uint STRIDE_X16 = 2u * 4u; // 2 32-bit uints for 4 16-bit values
  static const uint STRIDE_X8 = 1u * 4u; // 1 32-bit uint for 4 8-bit values

  uint4 indices = uint4(0u, 0u, 0u, 0u);

  [branch] switch(format) {
    case OVR_FORMAT_UINT_16:
      indices = OvrUnpackUint4x16(
        data_buffer,
        mad(vertex_index, STRIDE_X16, joint_indices_address));
    break;
    case OVR_FORMAT_UINT_8:
      indices = OvrUnpackUint4x8(
        data_buffer,
        mad(vertex_index, STRIDE_X8, joint_indices_address));
    break;
    default:
      break;
  }

  return indices;
}

uint GetOutputIndex(
  in ByteAddressBuffer data_buffer,
  uint output_indices_offset_bytes,
  uint vertex_index)
{
  // ASSUMPTION: Data stored for the output index is an array of Uint16s

  // Somewhat tricky here as we need to calculate the address, but that needs to be a multiple of 4
  // (ByteAddressBuffer = bag of uints)
  // So, this function needs to load a particular uint and then separate out the uint16s
  static const uint STRIDE = 2u; // 1 16-bit unsigned int, stride is in bytes
  uint address = mad(vertex_index, STRIDE, output_indices_offset_bytes); // in bytes

  // Address not guaranteed to be multiple of 4 here,
  // so round down to multiple of 4
  address = address / SIZE_OF_UINT * SIZE_OF_UINT;

  // Convert from bytes to uint offset
  uint2 unpacked = OvrUnpackUint2x16(data_buffer, address);

  // Now decide which one to pull
  return vertex_index % 2u == 0u ? unpacked.x : unpacked.y;
}

void PopulateVertexNoTangents(
  in ByteAddressBuffer static_data_buffer,
  uint positions_offset_bytes,
  uint normals_offset_bytes,
  uint joint_weights_offset_bytes,
  uint joint_weights_format,
  uint joint_indices_offset_bytes,
  uint joint_indices_format,
  uint output_index_mapping_offset_bytes,
  uint position_format,
  float3 position_bias,
  float3 position_scale,
  uint vertex_index,
  inout Vertex vertex)
{
  vertex.position = GetNeutralPosePosition(
    static_data_buffer,
    positions_offset_bytes,
    position_bias.xyz,
    position_scale.xyz,
    vertex_index,
    position_format);
  vertex.normal = GetNeutralPoseNormal(static_data_buffer, normals_offset_bytes, vertex_index);

  vertex.outputIndex = GetOutputIndex(static_data_buffer, output_index_mapping_offset_bytes, vertex_index);

  const uint4 joint_indices = GetJointIndices(
    static_data_buffer,
    joint_indices_offset_bytes,
    vertex_index,
    joint_indices_format);

  vertex.jointWeights = GetJointWeights(
    static_data_buffer,
    joint_weights_offset_bytes,
    vertex_index,
    joint_weights_format);
  vertex.jointIndices = joint_indices * 2u; // * 2 because of interleaved matrices, so 2x per joint
}

Vertex GetVertexStaticData(
  in ByteAddressBuffer static_data_buffer,
  uint positions_offset_bytes,
  uint normals_offset_bytes,
  uint joint_weights_offset_bytes,
  int joint_weights_format,
  uint joint_indices_offset_bytes,
  int joint_indices_format,
  uint output_index_mapping_offset_bytes,
  int position_format,
  float3 position_bias,
  float3 position_scale,
  uint vertex_index)
{
  Vertex result = (Vertex)0;

  PopulateVertexNoTangents(
    static_data_buffer,
    positions_offset_bytes,
    normals_offset_bytes,
    joint_weights_offset_bytes,
    joint_weights_format,
    joint_indices_offset_bytes,
    joint_indices_format,
    output_index_mapping_offset_bytes,
    position_format,
    position_bias,
    position_scale,
    vertex_index,
    result);

  result.vertexIndex = vertex_index;
  return result;
}

#endif
