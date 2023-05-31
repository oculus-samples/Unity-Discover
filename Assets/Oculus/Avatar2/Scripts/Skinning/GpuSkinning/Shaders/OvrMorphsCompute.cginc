#ifndef OVR_MORPHS_COMPUTE_INCLUDED
#define OVR_MORPHS_COMPUTE_INCLUDED

#include "../../../ShaderUtils/OvrDecodeUtils.cginc"
#include "../../../ShaderUtils/OvrDecodeFormats.cginc"

float GetMorphTargetWeight(
  in ByteAddressBuffer data_buffer,
  uint morph_target_weights_start_address,
  uint morph_target_index)
{
  static const uint STRIDE = 4u; // single 32-bit float
  const uint address = mad(STRIDE, morph_target_index, morph_target_weights_start_address);
  return OvrUnpackFloat1x32(data_buffer, address);
}

// Yes, macros. Was too much copy and paste otherwise

// ASSUMPTION: Assumes some variable names to limit number of parameters to the macro
// so, if stuff fails to compile, it might be do to bad assumed variable names
#define OVR_APPLY_RECTANGULAR_MORPHS_BODY(unpack_func) \
  for (mt_idx = 0; mt_idx < num_morphs; mt_idx++) { \
    const float weight = GetMorphTargetWeight( \
      dynamic_data_buffer, \
      morph_target_weights_start_address, \
      mt_idx); \
\
    pos_sum += weight * unpack_func(static_data_buffer, address); \
\
    address += attribute_row_stride; \
    norm_sum += weight * unpack_func(static_data_buffer, address); \
\
    address += attribute_row_stride; \
  }

// ASSUMPTION: Assumes some variable names to limit number of parameters to the macro
// so, if stuff fails to compile, it might be do to bad assumed variable names
#define OVR_APPLY_RECTANGULAR_MORPHS_BODY_WITH_TANGENTS(unpack_func) \
  for (mt_idx = 0; mt_idx < num_morphs; mt_idx++) { \
    const float weight = GetMorphTargetWeight( \
      dynamic_data_buffer, \
      morph_target_weights_start_address, \
      mt_idx); \
\
    pos_sum += weight * unpack_func(static_data_buffer, address); \
\
    address += attribute_row_stride; \
    norm_sum += weight * unpack_func(static_data_buffer, address); \
\
    address += attribute_row_stride; \
    tan_sum += weight * unpack_func(static_data_buffer, address); \
\
    address += attribute_row_stride; \
}

// Compiler should hopefully optimize out any potential branches due to static const bool values.
// Compiler should also optimize out unused parameters
void ApplyRectangularMorphsWithTangents(
    in ByteAddressBuffer static_data_buffer,
    in ByteAddressBuffer dynamic_data_buffer,
    uint morph_target_deltas_start_address,
    uint morph_target_weights_start_address,
    uint num_morphs,
    uint num_morphed_vertices,
    int morph_target_deltas_format,
    inout float4 position,
    float3 pos_scale,
    inout float3 normal,
    float3 norm_scale,
    inout float4 tangent,
    float3 tan_scale,
    uint vertex_index)
{
  static const uint STRIDE_32 = 4u * 4u;
  static const uint STRIDE_16 = 2u * 4u;
  static const uint STRIDE_10_10_10_2 = 4u;
  static const uint STRIDE_8 = 4u;

  // ASSUMPTION: Data for a given morph target is arranged
  // with all position deltas, then all normal deltas
  uint address = 0;
  uint attribute_row_stride = 0;

  float3 pos_sum = 0.0;
  float3 norm_sum = 0.0;
  float3 tan_sum = 0.0;

  uint mt_idx = 0;

  [branch] switch(morph_target_deltas_format)
  {
    case OVR_FORMAT_FLOAT_32:
      address = mad(STRIDE_32, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_32;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY_WITH_TANGENTS(OvrUnpackFloat3x32)
    break;
    case OVR_FORMAT_HALF_16:
      address = mad(STRIDE_16, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_16;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY_WITH_TANGENTS(OvrUnpackHalf3x16)
    break;
    case OVR_FORMAT_UNORM_16:
      address = mad(STRIDE_16, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_16;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY_WITH_TANGENTS(OvrUnpackUnorm3x16)
    break;
    case OVR_FORMAT_SNORM_10_10_10_2:
      address = mad(STRIDE_10_10_10_2, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_10_10_10_2;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY_WITH_TANGENTS(OvrUnpackVector_10_10_10_2)
    break;
    case OVR_FORMAT_UNORM_8:
      address = mad(STRIDE_8, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_8;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY_WITH_TANGENTS(OvrUnpackUnorm3x8)
    break;
    default:
      // error?
    break;
  }

  position.xyz += pos_scale * pos_sum;
  normal += norm_scale * norm_sum;
  tangent.xyz += tan_scale * tan_sum;
}

// Compiler should hopefully optimize out any potential branches due to static const bool values.
// Compiler should also optimize out unused parameters
void ApplyRectangularMorphsNoTangents(
    in ByteAddressBuffer static_data_buffer,
    in ByteAddressBuffer dynamic_data_buffer,
    uint morph_target_deltas_start_address,
    uint morph_target_weights_start_address,
    uint num_morphs,
    uint num_morphed_vertices,
    uint morph_target_deltas_format,
    inout float4 position,
    float3 pos_scale,
    inout float3 normal,
    float3 norm_scale,
    uint vertex_index)
{
  static const int STRIDE_32 = 4u * 4u; // In memory as 4 component vectors for alignment purposes (needed?)
  static const int STRIDE_16 = 2u * 4u;
  static const int STRIDE_10_10_10_2 = 4u;
  static const int STRIDE_8 = 4u;

  // ASSUMPTION: Data for a given morph target is arranged
  // with all position deltas, then all normal deltas
  uint address = 0;
  uint attribute_row_stride = 0;

  float3 pos_sum = 0.0;
  float3 norm_sum = 0.0;

  uint mt_idx = 0;

  [branch] switch(morph_target_deltas_format)
  {
    case OVR_FORMAT_FLOAT_32:
      address = mad(STRIDE_32, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_32;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(OvrUnpackFloat3x32)
    break;
    case OVR_FORMAT_HALF_16:
      address = mad(STRIDE_16, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_16;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(OvrUnpackHalf3x16)
    break;
    case OVR_FORMAT_UNORM_16:
      address = mad(STRIDE_16, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_16;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(OvrUnpackUnorm3x16)
    break;
    case OVR_FORMAT_SNORM_10_10_10_2:
      address = mad(STRIDE_10_10_10_2, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_10_10_10_2;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(OvrUnpackVector_10_10_10_2)
    break;
    case OVR_FORMAT_UNORM_8:
      address = mad(STRIDE_8, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_8;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(OvrUnpackUnorm3x8)
    break;
    default:
      // error?
    break;
  }

  position.xyz += pos_scale * pos_sum;
  normal += norm_scale * norm_sum;
}

// Compiler should hopefully optimize out any potential branches due to static const bool values.
// Compiler should also optimize out unused parameters
void ApplyRectangularMorphs(
    in ByteAddressBuffer static_data_buffer,
    in ByteAddressBuffer dynamic_data_buffer,
    uint morph_target_deltas_start_address,
    uint morph_target_weights_start_address,
    uint num_morphs,
    uint num_morphed_vertices,
    uint morph_target_deltas_format,
    inout float4 position,
    float3 pos_scale,
    inout float3 normal,
    float3 norm_scale,
    inout float4 tangent,
    float3 tan_scale,
    uint vertex_index,
    bool has_tangents)
{
  if (has_tangents) {
    ApplyRectangularMorphsWithTangents(
      static_data_buffer,
      dynamic_data_buffer,
      morph_target_deltas_start_address,
      morph_target_weights_start_address,
      num_morphs,
      num_morphed_vertices,
      morph_target_deltas_format,
      position,
      pos_scale,
      normal,
      norm_scale,
      tangent,
      tan_scale,
      vertex_index);
  }
  else
  {
    ApplyRectangularMorphsNoTangents(
       static_data_buffer,
       dynamic_data_buffer,
       morph_target_deltas_start_address,
       morph_target_weights_start_address,
       num_morphs,
       num_morphed_vertices,
       morph_target_deltas_format,
       position,
       pos_scale,
       normal,
       norm_scale,
       vertex_index);
  }
}

#endif
