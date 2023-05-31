#ifndef OVR_DECODE_UTILS_INCLUDED
#define OVR_DECODE_UTILS_INCLUDED

static const uint SIZE_OF_UINT = 4u; // in bytes

int OvrBitfieldExtract10(int value, int offset) {
  value = value >> offset;
  value &= 0x03ff;
  if ((value & 0x0200) != 0) {
    value |= 0xfffffc00;
  }
  return value;
}

uint OvrBitfieldExtract(uint data, uint offset, uint numBits)
{
  const uint mask = (1u << numBits) - 1u;
  return (data >> offset) & mask;
}

// With sign extension
int OvrBitfieldExtract(int data, uint offset, uint numBits)
{
  int  shifted = data >> offset;      // Sign-extending (arithmetic) shift
  int  signBit = shifted & (1u << (numBits - 1u));
  uint mask    = (1u << numBits) - 1u;

  return -signBit | (shifted & mask); // Use 2-complement for negation to replicate the sign bit
}

uint OvrBitfieldInsert(uint base, uint insert, uint offset, uint numBits)
{
  uint mask = ~(0xffffffffu << numBits) << offset;
  mask = ~mask;
  base = base & mask;
  return base | (insert << offset);
}

// Unpack 1x float (32 bit) from a single 32 bit uint
float OvrUnpackFloat1x32(uint u) {
  // Just re-interpret as an float
  return asfloat(u);
}

// Pack 1x float (32 bit) into a single 32 bit uint
uint OvrPackFloat1x32(float val) {
  // Just re-interpret as a uint
  return asuint(val);
}

// Unpack 2x "half floats" (16 bit) from a single 32 bit uint
float2 OvrUnpackHalf2x16(uint u) {
  const uint y = (u >> 16) & 0xFFFFu;
  const uint x = u & 0xFFFFu;

  return float2(f16tof32(x), f16tof32(y));
}

// Pack 2x "half floats" (16 bit) from a single 32 bit uint
uint OvrPackHalf2x16(float2 halfs) {
  const uint x = f32tof16(halfs.x);
  const uint y = f32tof16(halfs.y);
  return x + (y << 16);
}

// Unpack 2 16-bit unsigned integers
uint2 OvrUnpackUint2x16(uint packed_values) {
  uint y = (packed_values >> 16) & 0xffffu;
  uint x = packed_values & 0xffffu;

  return uint2(x, y);
}

// Pack 2 16-bit unsigned integers into a single 32-bit uint
uint OvrPackUint2x16(uint2 vals) {
  return vals.x + (vals.y << 16);
}

// Unpack UNorm [0, 1] 2 16 bit entries (packed in a single 32 bit uint)
float2 OvrUnpackUnorm2x16(uint packed_values) {
  uint2 non_normalized = OvrUnpackUint2x16(packed_values);

  // Convert from 0 -> 65535 to 0 -> 1
  const float inv = 1.0 / 65535.0;

  return float2(non_normalized.x * inv, non_normalized.y * inv);
}

// Pack 2x unsigned normalized values (16 bit) into a single 32 bit uint
uint OvrPackUnorm2x16(float2 unorms) {
  // Convert from 0 -> 1 to 0 -> 65535
  const float factor = 65535.0;
  const uint x = round(saturate(unorms.x) * factor);
  const uint y = round(saturate(unorms.y) * factor);

  return OvrPackUint2x16(uint2(x, y));
}

// Unpack 4 8-bit unsigned integers from a single 32-bit uint
uint4 OvrUnpackUint4x8(uint four_packed_values) {
  uint w = (four_packed_values >> 24) & 0xFFu;
  uint z = (four_packed_values >> 16) & 0xFFu;
  uint y = (four_packed_values >> 8) & 0xFFu;
  uint x = four_packed_values & 0xFFu;

  return uint4(x, y, z, w);
}

// Pack 4 8-bit unsigned integers into a single 32-bit uint
uint OvrPackUint4x8(uint4 vals) {
  return vals.x + (vals.y << 8) + (vals.z << 16) + (vals.w << 24);
}

// Unpack UNorm [0, 1] 4 bytes (as a 32 bit uint)
float4 OvrUnpackUnorm4x8(uint four_packed_values) {
  uint4 non_normalized = OvrUnpackUint4x8(four_packed_values);

  // Convert from 0 -> 255 to 0 -> 1
  const float inv255 = 1.0 / 255.0;

  return float4(
    non_normalized.x * inv255,
    non_normalized.y * inv255,
    non_normalized.z * inv255,
    non_normalized.w * inv255);
}

uint OvrPackUnorm4x8(float4 unorms) {
  const float factor = 255.0;
  const uint x = round(saturate(unorms.x) * factor);
  const uint y = round(saturate(unorms.y) * factor);
  const uint z = round(saturate(unorms.z) * factor);
  const uint w = round(saturate(unorms.w) * factor);

  return OvrPackUint4x8(uint4(x, y, z, w));
}

float4 OvrUnpackSnorm4x10_10_10_2(int four_packed_values) {
  int4 unpackedInt;
  unpackedInt.x = OvrBitfieldExtract(four_packed_values, 0, 10);
  unpackedInt.y = OvrBitfieldExtract(four_packed_values, 10, 10);
  unpackedInt.z = OvrBitfieldExtract(four_packed_values, 20, 10);
  unpackedInt.w = OvrBitfieldExtract(four_packed_values, 30, 2);

  // xyz is -511-511 w is -1-1
  float4 unpacked = float4(unpackedInt);
  // convert all to -1-1
  unpacked.xyz *= 1.0/511.0;

  return unpacked;
}

uint OvrPackSnorm4x10_10_10_2(float4 snorms) {
  static const float3 range = 511.0;
  float4 scaled = 0.0;
  scaled.xyz = snorms.xyz * range; // Convert from -1.0 -> 1.0 to -511.0 -> 511.0
  scaled.xyz = clamp(scaled.xyz, -range, range);
  scaled.xyz = round(scaled.xyz); // Round to nearest int
  scaled.w = clamp(scaled.w, -1.0, 1.0);
  scaled.w = round(scaled.w);

  // now convert from 16 bit to 10 bits, and pack into 32 bits
  int4 integers = int4(scaled);
  uint result = 0;
  result = OvrBitfieldInsert(result, uint(integers.x), 0, 10);
  result = OvrBitfieldInsert(result, uint(integers.y), 10, 10);
  result = OvrBitfieldInsert(result, uint(integers.z), 20, 10);
  result = OvrBitfieldInsert(result, uint(integers.w), 30, 2);

  return result;
}

// Takes 4 "raw, packed" bytes in a 10/10/10/2 format as a signed 32 bit integer (4 bytes).
// The 2 bits is used as a "bonus scale".
// Returns a 3 component (x,y,z) float vector
float3 OvrUnpackVector_10_10_10_2(int packed_value) {
  // bonus scale is still a unorm, if I convert it to an snorm, I lose one value.
  // that does mean I can't use the hardware to convert this format though, it has
  // to be unpacked by hand. If you do have hardware 10_10_10_2 conversion, it may
  // be better to just sample twice? once as unorm, once as snorm.
  uint bonusScaleIndex = uint(packed_value >> 30 & 0x03);

  const float bonus_scale_lookup[4] = {1.0f, 0.5f, 0.25f, 0.125f};
  const float bonus_scale = bonus_scale_lookup[bonusScaleIndex];

  int3 unpackedInt;
  unpackedInt.x = OvrBitfieldExtract10(packed_value, 0);
  unpackedInt.y = OvrBitfieldExtract10(packed_value, 10);
  unpackedInt.z = OvrBitfieldExtract10(packed_value, 20);

  float3 unpacked = float3(unpackedInt);
  // convert all to -1 to 1
  const float inv511 = 1.0 / 511.0;
  unpacked *= float3(inv511, inv511, inv511);

  unpacked = unpacked * bonus_scale;

  return unpacked;
}

/////////////////////////////////
// ByteAddressBuffer functions
/////////////////////////////////

uint OvrLoadUint(in ByteAddressBuffer data_buffer, uint address) {
  return data_buffer.Load(address);
}

uint2 OvrLoadUint2(in ByteAddressBuffer data_buffer, uint address) {
  return data_buffer.Load2(address);
}

uint3 OvrLoadUint3(in ByteAddressBuffer data_buffer, uint address) {
  return data_buffer.Load3(address);
}

uint4 OvrLoadUint4(in ByteAddressBuffer data_buffer, uint address) {
  return data_buffer.Load4(address);
}

void OvrStoreUint(in RWByteAddressBuffer data_buffer, uint address, uint data) {
  data_buffer.Store(address, data);
}

void OvrStoreUint2(in RWByteAddressBuffer data_buffer, uint address, uint2 data) {
  data_buffer.Store2(address, data);
}

void OvrStoreUint3(in RWByteAddressBuffer data_buffer, uint address, uint3 data) {
  data_buffer.Store3(address, data);
}

void OvrStoreUint4(in RWByteAddressBuffer data_buffer, uint address, uint4 data) {
  data_buffer.Store4(address, data);
}

// Unity SurfaceShaders require any ByteAddressBuffers to be wrapped in SHADER_API_D3D11
float3 OvrUnpackVector_10_10_10_2(in ByteAddressBuffer data_buffer, uint address) {
  return OvrUnpackVector_10_10_10_2(OvrLoadUint(data_buffer, address));
}

float4 OvrUnpackSnorm4x10_10_10_2(in ByteAddressBuffer data_buffer, uint address) {
  const int packed_value = OvrLoadUint(data_buffer, address);
  return OvrUnpackSnorm4x10_10_10_2(packed_value);
}

float3 OvrUnpackSnorm3x10_10_10_2(in ByteAddressBuffer data_buffer, uint address) {
  return OvrUnpackSnorm4x10_10_10_2(data_buffer, address).xyz;
}

// 4x 32 bit uint -> 4x 32 bit float
float4 OvrUnpackFloat4x32(in ByteAddressBuffer data_buffer, uint address) {
  const uint4 packed_data = OvrLoadUint4(data_buffer, address);
  return asfloat(packed_data);
}

// 3x 32 bit uint -> 3x 32 bit float
float3 OvrUnpackFloat3x32(in ByteAddressBuffer data_buffer, uint address) {
  const uint3 packed_data = OvrLoadUint3(data_buffer, address);
  return asfloat(packed_data);
}

// 1x 32 bit uint -> 1x 32 bit float
float OvrUnpackFloat1x32(in ByteAddressBuffer data_buffer, uint address) {
  return OvrUnpackFloat1x32(OvrLoadUint(data_buffer, address));
}

// 16x 32 bit uint -> 32 bit float4x4
float4x4 OvrUnpackFloat16x32(in ByteAddressBuffer data_buffer, uint address) {
  float4 c0 = OvrUnpackFloat4x32(data_buffer, address);
  float4 c1 = OvrUnpackFloat4x32(data_buffer, address + 16u);
  float4 c2 = OvrUnpackFloat4x32(data_buffer, address + 32u);
  float4 c3 = OvrUnpackFloat4x32(data_buffer, address + 48u);

  return float4x4(
    c0.x, c1.x, c2.x, c3.x,
    c0.y, c1.y, c2.y, c3.y,
    c0.z, c1.z, c2.z, c3.z,
    c0.w, c1.w, c2.w, c3.w);
}

// 2x 32 bit uint -> 3x 16 bit "half floats"
float3 OvrUnpackHalf3x16(in ByteAddressBuffer data_buffer, uint address) {
  uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackHalf2x16(packed_data.x);
  float z = OvrUnpackHalf2x16(packed_data.y).x;

  return float3(xy, z);
}

// 2x 32 bit uint -> 4x 16 bit "half floats"
float4 OvrUnpackHalf4x16(in ByteAddressBuffer data_buffer, uint address) {
  uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackHalf2x16(packed_data.x);
  float2 zw = OvrUnpackHalf2x16(packed_data.y);

  return float4(xy, zw);
}

// 2x 32 bit uint -> 3x 16-bit unsigned int
uint3 OvrUnpackUint3x16(in ByteAddressBuffer data_buffer, uint address) {
  uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackUint2x16(packed_data.x);
  float z = OvrUnpackUint2x16(packed_data.y).x;

  return float3(xy, z);
}

// 1x 32 bit uint -> 2x 16-bit unsigned int
uint2 OvrUnpackUint2x16(in ByteAddressBuffer data_buffer, uint address) {
  const uint packed_data = OvrLoadUint(data_buffer, address);
  return OvrUnpackUint2x16(packed_data);
}

// 2x 32 bit uint -> 4x 16-bit unsigned int
uint4 OvrUnpackUint4x16(in ByteAddressBuffer data_buffer, uint address) {
  uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackUint2x16(packed_data.x);
  float2 zw = OvrUnpackUint2x16(packed_data.y);

  return float4(xy, zw);
}

// 2x 32-bit uint -> 3x 16-bit unsigned normalized
float3 OvrUnpackUnorm3x16(in ByteAddressBuffer data_buffer, uint address) {
  uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackUnorm2x16(packed_data.x);
  float z = OvrUnpackUnorm2x16(packed_data.y).x;

  return float3(xy, z);
}

// 2x 32-bit uint -> 4x 16-bit unsigned normalized
float4 OvrUnpackUnorm4x16(in ByteAddressBuffer data_buffer, uint address) {
  uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackUnorm2x16(packed_data.x);
  float2 zw = OvrUnpackUnorm2x16(packed_data.y);

  return float4(xy, zw);
}

// 1x 32 bit uint -> 4x 8 bit unsigned normalized
float4 OvrUnpackUint4x8(in ByteAddressBuffer data_buffer, uint address) {
  return OvrUnpackUint4x8(OvrLoadUint(data_buffer, address));
}

// 1x 32-bit uint -> 3x 8-bit unsigned int
uint3 OvrUnpackUint3x8(in ByteAddressBuffer data_buffer, uint address) {
  return OvrUnpackUint4x8(data_buffer, address).xyz;
}

// 1x 32-bit uint -> 4x 8-bit unsigned normalized
float4 OvrUnpackUnorm4x8(in ByteAddressBuffer data_buffer, uint address) {
  return OvrUnpackUnorm4x8(OvrLoadUint(data_buffer, address));
}

// 1x 32-bit uint -> 3x 8-bit unsigned normalized
float3 OvrUnpackUnorm3x8(in ByteAddressBuffer data_buffer, uint address) {
  return OvrUnpackUnorm4x8(data_buffer, address).xyz;
}


//////////////////////////////////////////////////////////
// StructuredBuffer<uint> functions
//////////////////////////////////////////////////////////
uint OvrLoadUint(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint start_position = address / SIZE_OF_UINT;
  return data_buffer[start_position];
}

uint2 OvrLoadUint2(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint start_position = address / SIZE_OF_UINT;
  // Load 2x uints
  uint x = data_buffer[start_position];
  uint y = data_buffer[start_position + 1];
  return uint2(x, y);
}

uint3 OvrLoadUint3(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint start_position = address / SIZE_OF_UINT;
  // Load 3x uints
  uint x = data_buffer[start_position];
  uint y = data_buffer[start_position + 1];
  uint z = data_buffer[start_position + 2];
  return uint3(x, y, z);
}

uint4 OvrLoadUint4(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint start_position = address / SIZE_OF_UINT;
  // Load 4x uints
  uint x = data_buffer[start_position];
  uint y = data_buffer[start_position + 1];
  uint z = data_buffer[start_position + 2];
  uint w = data_buffer[start_position + 3];
  return uint4(x, y, z, w);
}

void OvrStoreUint(in RWStructuredBuffer<uint> data_buffer, uint address, uint data) {
  const uint start_position = address / SIZE_OF_UINT;
  data_buffer[start_position] = data;
}

void OvrStoreUint2(in RWStructuredBuffer<uint> data_buffer, uint address, uint2 data) {
  const uint start_position = address / SIZE_OF_UINT;
  // Store 2x uints
  data_buffer[start_position] = data.x;
  data_buffer[start_position + 1] = data.y;
}

void OvrStoreUint3(in RWStructuredBuffer<uint> data_buffer, uint address, uint3 data) {
  const uint start_position = address / SIZE_OF_UINT;
  // Store 3x uints
  data_buffer[start_position] = data.x;
  data_buffer[start_position + 1] = data.y;
  data_buffer[start_position + 2] = data.z;
}

void OvrStoreUint4(in RWStructuredBuffer<uint> data_buffer, uint address, uint4 data) {
  const uint start_position = address / SIZE_OF_UINT;
  // Store 4x uints
  data_buffer[start_position] = data.x;
  data_buffer[start_position + 1] = data.y;
  data_buffer[start_position + 2] = data.z;
  data_buffer[start_position + 3] = data.w;
}

float3 OvrUnpackVector_10_10_10_2(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackVector_10_10_10_2(OvrLoadUint(data_buffer, address));
}

float4 OvrUnpackSnorm4x10_10_10_2(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackSnorm4x10_10_10_2(OvrLoadUint(data_buffer, address));
}

float3 OvrUnpackSnorm3x10_10_10_2(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackSnorm4x10_10_10_2(data_buffer, address).xyz;
}

// 4x 32 bit uint -> 4x 32 bit float
float4 OvrUnpackFloat4x32(in StructuredBuffer<uint> data_buffer, uint address) {
  // Load 4x uints
  const uint4 packed_data = OvrLoadUint4(data_buffer, address);
  return asfloat(packed_data);
}

// 3x 32 bit uint -> 3x 32 bit float
float3 OvrUnpackFloat3x32(in StructuredBuffer<uint> data_buffer, uint address) {
  // Load 3x uints
  const uint3 packed_data = OvrLoadUint3(data_buffer, address);
  return asfloat(packed_data);
}

// 1x 32 bit uint -> 1x 32 bit float
float OvrUnpackFloat1x32(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackFloat1x32(OvrLoadUint(data_buffer, address));
}

// 16x 32 bit uint -> 32 bit float4x4
float4x4 OvrUnpackFloat16x32(in StructuredBuffer<uint> data_buffer, uint address) {
  float4 c0 = OvrUnpackFloat4x32(data_buffer, address);
  float4 c1 = OvrUnpackFloat4x32(data_buffer, address + 16);
  float4 c2 = OvrUnpackFloat4x32(data_buffer, address + 32);
  float4 c3 = OvrUnpackFloat4x32(data_buffer, address + 48);
  return float4x4(
    c0.x, c1.x, c2.x, c3.x,
    c0.y, c1.y, c2.y, c3.y,
    c0.z, c1.z, c2.z, c3.z,
    c0.w, c1.w, c2.w, c3.w);
}

// 2x 32 bit uint -> 4x 16 bit "half floats"
float4 OvrUnpackHalf4x16(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackHalf2x16(packed_data.x);
  float2 zw = OvrUnpackHalf2x16(packed_data.y);

  return float4(xy, zw);
}

// 2x 32 bit uint -> 3x 16 bit "half floats"
float3 OvrUnpackHalf3x16(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackHalf4x16(data_buffer, address).xyz;
}

// 2x 32 bit uint -> 4x 16-bit unsigned int
uint4 OvrUnpackUint4x16(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackUint2x16(packed_data.x);
  float2 zw = OvrUnpackUint2x16(packed_data.y);

  return float4(xy, zw);
}

// 2x 32 bit uint -> 3x 16-bit unsigned int
uint3 OvrUnpackUint3x16(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackUint4x16(data_buffer, address).xyz;
}

// 1x 32 bit uint -> 2x 16-bit unsigned int
uint2 OvrUnpackUint2x16(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint packed_data = OvrLoadUint(data_buffer, address);
  return OvrUnpackUint2x16(packed_data);
}

// 2x 32-bit uint -> 4x 16-bit unsigned normalized
float4 OvrUnpackUnorm4x16(in StructuredBuffer<uint> data_buffer, uint address) {
  const uint2 packed_data = OvrLoadUint2(data_buffer, address);
  float2 xy = OvrUnpackUnorm2x16(packed_data.x);
  float2 zw = OvrUnpackUnorm2x16(packed_data.y);

  return float4(xy, zw);
}

// 2x 32-bit uint -> 3x 16-bit unsigned normalized
float3 OvrUnpackUnorm3x16(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackUnorm4x16(data_buffer, address).xyz;
}

// 1x 32 bit uint -> 4x 8 bit unsigned normalized
uint4 OvrUnpackUint4x8(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackUint4x8(OvrLoadUint(data_buffer, address));
}

// 1x 32-bit uint -> 3x 8-bit unsigned int
uint3 UnpackUint3x8(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackUint4x8(data_buffer, address).xyz;
}

// 1x 32-bit uint -> 4x 8-bit unsigned normalized
float4 OvrUnpackUnorm4x8(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackUnorm4x8(OvrLoadUint(data_buffer, address));
}

// 1x 32-bit uint -> 3x 8-bit unsigned normalized
float3 OvrUnpackUnorm3x8(in StructuredBuffer<uint> data_buffer, uint address) {
  return OvrUnpackUnorm4x8(data_buffer, address).xyz;
}

#endif
