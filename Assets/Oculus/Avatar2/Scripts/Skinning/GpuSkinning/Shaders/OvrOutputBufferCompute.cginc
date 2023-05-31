#ifndef OVR_OUTPUT_BUFFER_COMPUTE_INCLUDED
#define OVR_OUTPUT_BUFFER_COMPUTE_INCLUDED

#include "../../../ShaderUtils/OvrDecodeUtils.cginc"
#include "../../../ShaderUtils/OvrDecodeFormats.cginc"

//////////////////////////////////////////////////////
// Output
//////////////////////////////////////////////////////

void StoreVertexNormal(
  inout RWByteAddressBuffer output_buffer,
  uint output_buffer_start_address,
  in float3 normal,
  uint output_index)
{
  static const uint STRIDE = 4u; // 1 32-bit uint for 10_10_10_2

  // Normalize on store
  const uint address = mad(output_index, STRIDE, output_buffer_start_address);
  OvrStoreUint(output_buffer, address, OvrPackSnorm4x10_10_10_2(float4(normalize(normal.xyz), 0.0)));
}

void StoreVertexTangent(
  inout RWByteAddressBuffer output_buffer,
  uint output_buffer_start_address,
  in float4 tangent,
  uint output_index)
{
  static const uint STRIDE = 4u; // 1 32-bit uint for 10_10_10_2

  // Normalize on store
  const uint address = mad(output_index, STRIDE, output_buffer_start_address);
  OvrStoreUint(output_buffer, address, OvrPackSnorm4x10_10_10_2(float4(normalize(tangent.xyz), tangent.w)));
}

void StoreVertexPositionFloat4x32(
  inout RWByteAddressBuffer output_buffer,
  uint output_buffer_start_address,
  in float4 position,
  uint output_index)
{
  static const int POS_STRIDE = 4u * 4u; // 4 32-bit uints for 4 32-bit floats
  const uint address = mad(output_index, POS_STRIDE, output_buffer_start_address);
  const uint4 packed_data = asuint(position);

  OvrStoreUint4(output_buffer, address, packed_data);
}

void StoreVertexPositionHalf4x16(
  inout RWByteAddressBuffer output_buffer,
  uint output_buffer_start_address,
  in float4 position,
  uint output_index)
{
  static const uint STRIDE = 4u * 2u; // 2 32-bit uints for 4 16-bit halfs
  const uint address = mad(output_index, STRIDE, output_buffer_start_address);
  const uint2 packed_data = uint2(OvrPackHalf2x16(position.xy), OvrPackHalf2x16(position.zw));

  OvrStoreUint2(output_buffer, address, packed_data);
}

void StoreVertexPositionUnorm4x16(
  inout RWByteAddressBuffer output_buffer,
  uint output_buffer_start_address,
  in float4 position,
  in float3 position_bias,
  in float3 inv_position_scale,
  uint output_index)
{
  static const uint STRIDE = 4u * 2u; // 2 32-bit uints for 4 16-bit unorms
  const uint address = mad(output_index, STRIDE, output_buffer_start_address);

  // Normalize to 0 -> 1 but given the bias and scale
  // ASSUMPTION: Assuming the position_bias and position_scale will be large enough
  // to place in the range 0 -> 1
  float4 normalized = float4((position.xyz - position_bias) * inv_position_scale, position.w);
  const uint2 packed_data = uint2(OvrPackUnorm2x16(normalized.xy), OvrPackUnorm2x16(normalized.zw));

  OvrStoreUint2(output_buffer, address, packed_data);
}

void StoreVertexPositionUnorm4x8(
  inout RWByteAddressBuffer output_buffer,
  uint output_buffer_start_address,
  in float4 position,
  in float3 position_bias,
  in float3 inv_position_scale,
  uint output_index)
{
  static const uint STRIDE = 4u; // 1 32-bit uints for 4 8-bit unorms
  const uint address = mad(output_index, STRIDE, output_buffer_start_address);

  // Normalize to 0 -> 1 but given the offset and scale
  // ASSUMPTION: Assuming the position_offset and position_scale will be large enough
  // to place in the range 0 -> 1
  const float4 normalized = float4((position.xyz - position_bias) * inv_position_scale, position.w);
  const uint packed_data = OvrPackUnorm4x8(normalized);

  OvrStoreUint(output_buffer, address, packed_data);
}

uint CalculatePositionOutputIndex(
  uint vertex_output_index,
  uint num_slices_per_attribute,
  uint output_slice)
{
  return vertex_output_index * num_slices_per_attribute + output_slice;
}

uint CalculateNormalOutputIndex(
  uint vertex_output_index,
  uint num_slices_per_attribute,
  uint output_slice,
  bool has_tangents)
{
  // *2 if interleaving tangent
  return (has_tangents ? 2u : 1u) * vertex_output_index * num_slices_per_attribute + output_slice;
}

void StoreVertexPosition(
  inout RWByteAddressBuffer output_buffer,
  uint output_buffer_start_address,
  int format,
  float3 position_bias,
  float3 inv_position_scale,
  in float4 position,
  uint output_index)
{
  [branch] switch(format)
  {
    case OVR_FORMAT_FLOAT_32:
      StoreVertexPositionFloat4x32(
        output_buffer,
        output_buffer_start_address,
        position,
        output_index);
    break;
    case OVR_FORMAT_HALF_16:
      StoreVertexPositionHalf4x16(
        output_buffer,
        output_buffer_start_address,
        position,
        output_index);
    break;
    case OVR_FORMAT_UNORM_16:
      StoreVertexPositionUnorm4x16(
        output_buffer,
        output_buffer_start_address,
        position,
        position_bias,
        inv_position_scale,
        output_index);
    break;
    case OVR_FORMAT_UNORM_8:
      StoreVertexPositionUnorm4x8(
        output_buffer,
        output_buffer_start_address,
        position,
        position_bias,
        inv_position_scale,
        output_index);
    break;
    default:
      break;
  }
}

// Compiler should hopefully optimize out any potential branches due to static const bool values,
// and otherwise, branches should be based on uniform parameters passed in which
// should make their just the branch and not cause diverging branches across workgroups
// Compiler should also optimize out unused parameters
void StoreVertexOutput(
  inout RWByteAddressBuffer position_output_buffer,
  inout RWByteAddressBuffer output_buffer,
  uint position_output_buffer_start_address,
  uint output_buffer_start_address,
  float3 position_bias,
  float3 inv_position_scale,
  int position_format,
  in float4 position,
  in float3 normal,
  in float4 tangent,
  uint vertex_output_index,
  bool has_tangents,
  uint num_slices_per_attribute,
  uint output_slice)
{
  // * 2 due to double buffering, then maybe +1 if writing to second slice
  const uint pos_output_index = CalculatePositionOutputIndex(
    vertex_output_index,
    num_slices_per_attribute,
    output_slice);
  const uint norm_output_index = CalculateNormalOutputIndex(
    vertex_output_index,
    num_slices_per_attribute,
    output_slice,
    has_tangents);

  StoreVertexPosition(
    position_output_buffer,
    position_output_buffer_start_address,
    position_format,
    position_bias,
    inv_position_scale,
    position,
    pos_output_index);

  StoreVertexNormal(
    output_buffer,
    output_buffer_start_address,
    normal,
    norm_output_index);

  if (has_tangents) {
    const int tangent_output_index = norm_output_index + num_slices_per_attribute;

    StoreVertexTangent(output_buffer, output_buffer_start_address, tangent, tangent_output_index);
  }
}

#endif
