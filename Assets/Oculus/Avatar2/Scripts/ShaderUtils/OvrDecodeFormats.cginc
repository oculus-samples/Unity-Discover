#ifndef OVR_DECODE_FORMATS_INCLUDED
#define OVR_DECODE_FORMATS_INCLUDED

// NOTE: Changing these formats
// will also require updating the dispatching/drawing code as well
// to make sure the constants are the same
static const int OVR_FORMAT_FLOAT_32 = 0;
static const int OVR_FORMAT_HALF_16  = 1;
static const int OVR_FORMAT_UNORM_16 = 2;
static const int OVR_FORMAT_UINT_16 = 3;
static const int OVR_FORMAT_SNORM_10_10_10_2 = 4;
static const int OVR_FORMAT_UNORM_8 = 5;
static const int OVR_FORMAT_UINT_8 = 6;

#endif
