#ifndef OVR_AVATAR_VERTEX_FETCH_INCLUDED
#define OVR_AVATAR_VERTEX_FETCH_INCLUDED

// TODO*: Documentation here

#include "OvrDecodeFormats.cginc"

#include "OvrAvatarSupportDefines.hlsl"

//-------------------------------------------------------------------------------------
// Vertex based texture fetching related uniforms and functions.

// NOTE: This texture can be visualized in the Unity editor, just expand in inspector and manually change "Dimension" to "3D" on top line

#define OVR_ATTRIBUTE_PRECISION_FLOAT // hard coding this for now to test perf

// NOTE: According to Unity documentation here https://docs.unity3d.com/Manual/SL-DataTypesAndPrecision.html
// The standard declaration of sampler3D yields the following
// "For mobile platforms, these translate into “low precision samplers”, i.e. the textures are expected to
// have low precision data in them."
// Upon shader inspection, the declarations become "uniform mediump sampler3D" which
// is 16-bit precision. This is not desired as some of the data in the textures is
// expected to have 32-bit precision. So, for mobile platforms, make an option for explicitly
// setting 32-bit precision
#if defined(SHADER_API_MOBILE) && defined(OVR_ATTRIBUTE_PRECISION_FLOAT)
sampler3D_float u_AttributeTexture;
#else
sampler3D u_AttributeTexture;
#endif

int u_AttributeTexelX;
int u_AttributeTexelY;
int u_AttributeTexelW;

float u_AttributeTexInvSizeW;
float u_AttributeTexInvSizeH;
float u_AttributeTexInvSizeD;

float2 u_AttributeScaleBias;

//-------------------------------------------------------------------------------------
// Vertex based texture fetching related uniforms and functions.

float3 ovrGetAttributeTexCoord(
    int attributeRowOffset,
    uint vertIndex,
    int numAttributes,
    float slice) {
  // Compute texture coordinate in the attribute texture

  // Compute which row in the texel rect
  // the vertex index is
  int row = vertIndex / u_AttributeTexelW;
  int column = vertIndex % u_AttributeTexelW;

  row = row * numAttributes;

  // Calculate texel centers
  column = u_AttributeTexelX + column;
  row = u_AttributeTexelY + row + attributeRowOffset;

  const float3 coord = float3(float(column), float(row), slice);
  const float3 invSize = float3(
      u_AttributeTexInvSizeW,
      u_AttributeTexInvSizeH,
      u_AttributeTexInvSizeD);

  // Compute texture coordinate for texel center
  return (2.0 * coord + 1.0) * 0.5 * invSize;
}

float3 ovrGetPositionTexCoord(uint vid, int numAttributes, float slice) {
  return ovrGetAttributeTexCoord(0, vid, numAttributes, slice);
}

float3 ovrGetNormalTexCoord(uint vid, int numAttributes, float slice) {
  return ovrGetAttributeTexCoord(1, vid, numAttributes, slice);
}

float3 ovrGetTangentTexCoord(uint vid, int numAttributes, float slice) {
  return ovrGetAttributeTexCoord(2, vid, numAttributes, slice);
}

float4 OvrGetVertexPositionFromTexture(
    uint vid,
    int numAttributes,
    bool applyOffsetAndBias,
    float slice) {
  float4 pos = tex3Dlod(
      u_AttributeTexture,
      float4(ovrGetPositionTexCoord(vid, numAttributes, slice), 0));
  [branch]
  if (applyOffsetAndBias) {
    pos = pos * u_AttributeScaleBias.x + u_AttributeScaleBias.y;
  }
  return pos;
}

float4 OvrGetVertexNormalFromTexture(
    uint vid,
    int numAttributes,
    bool applyOffsetAndBias,
    float slice) {
  float4 norm = tex3Dlod(
      u_AttributeTexture,
      float4(ovrGetNormalTexCoord(vid, numAttributes, slice), 0));
  [branch]
  if (applyOffsetAndBias) {
    norm = norm * u_AttributeScaleBias.x + u_AttributeScaleBias.y;
  }
  return norm;
}

float4 OvrGetVertexTangentFromTexture(
    uint vid,
    int numAttributes,
    bool applyOffsetAndBias,
    float slice) {
  float4 tan = tex3Dlod(
      u_AttributeTexture,
      float4(ovrGetTangentTexCoord(vid, numAttributes, slice), 0));
  [branch]
  if (applyOffsetAndBias) {
    tan = tan * u_AttributeScaleBias.x + u_AttributeScaleBias.y;
  }
  return tan;
}

//-------------------------------------------------------------------------------------
// "External Buffers" vertex fetch setup
#if defined(OVR_SUPPORT_EXTERNAL_BUFFERS)
  #include "OvrDecodeUtils.cginc"

  ByteAddressBuffer _OvrPositionBuffer; // Bag of uints
  ByteAddressBuffer _OvrFrenetBuffer; // Bag of uints
  float3 _OvrPositionScale;
  float3 _OvrPositionBias;

  int _OvrPositionEncodingPrecision;

  int _OvrPositionsStartAddress;
  int _OvrFrenetStartAddress;

  //-------------------------------------------------------------------------------------
  // Avatar Vertex fetch setup

  float3 OvrGetPositionEntryFromExternalBuffer(uint entryIndex) {
    static const uint STRIDE_32 = 4u * 4u; // 4 32-bit uints for 4 32-bit floats
    static const uint STRIDE_16 = 4u * 2u; // 2 32-bit uints for 4 16 bit unorms or halfs

    float3 position = float3(0.0, 0.0, 0.0);

    [branch] switch(_OvrPositionEncodingPrecision) {
      case OVR_FORMAT_UNORM_16:
        // 2 32-bit uints for 4 16 bit unorms
        position.xyz = OvrUnpackUnorm3x16(
          _OvrPositionBuffer,
          mad(entryIndex, STRIDE_16, _OvrPositionsStartAddress));

        // Apply scale and offset to "de-normalize"
        position.xyz = mad(position.xyz, _OvrPositionScale, _OvrPositionBias);
      break;

      case OVR_FORMAT_FLOAT_32:
        // 4 32-bit uints for 4 32-bit floats
        position.xyz = OvrUnpackFloat3x32(
          _OvrPositionBuffer,
          mad(entryIndex, STRIDE_32, _OvrPositionsStartAddress));
      break;
      case OVR_FORMAT_HALF_16:
        position.xyz = OvrUnpackHalf3x16(
          _OvrPositionBuffer,
          mad(entryIndex, STRIDE_16, _OvrPositionsStartAddress));
      break;
      default:
        // error?
        break;
    }

    return position;
  }

  float4 OvrGetFrenetEntryFromExternalBuffer(uint entryIndex) {
    // Only supporting 10-10-10-2 snorm at the moment
    static const uint STRIDE = 4u; // 1 32-bit uint for 3 10-bit SNorm and 1 2-bit extra
    return OvrUnpackSnorm4x10_10_10_2(
      _OvrFrenetBuffer,
      mad(entryIndex, STRIDE, _OvrFrenetStartAddress));
  }

#endif

#endif // OVR_AVATAR_VERTEX_FETCH_INCLUDED
