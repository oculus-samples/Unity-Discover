#ifndef OVR_AVATAR_COMMON_VERTEX_PARAMS_INCLUDED
#define OVR_AVATAR_COMMON_VERTEX_PARAMS_INCLUDED

#include "OvrAvatarSupportDefines.hlsl"

// This file is for parameters used by meta avatars SDK vertex programs
// for fetching the normal rendering pass(es) as well as the "motion vectors" pass

// In an effort to not require third parties to define or multi_compile some specific
// keywords, make some logic use branching instead of preprocessor directives. This
// will be a performance hit, but should hopefully be minimal since
// the branches are based on uniforms. Should only be the cost of the branch and not
// vary across warps/waveforms.
static const int OVR_VERTEX_FETCH_MODE_STRUCT = 0;
static const int OVR_VERTEX_FETCH_MODE_EXTERNAL_BUFFERS  = 1;
static const int OVR_VERTEX_FETCH_MODE_EXTERNAL_TEXTURES  = 2;

bool _OvrHasTangents;
int _OvrVertexFetchMode;
bool _OvrInterpolateAttributes;

// Interpolation value for the "current" render frame
float _OvrAttributeInterpolationValue;

#if defined(OVR_SUPPORT_EXTERNAL_BUFFERS)
  int _OvrNumOutputEntriesPerAttribute;
  int _OvrAttributeOutputLatestAnimFrameEntryOffset;
  int _OvrAttributeOutputPrevAnimFrameEntryOffset;

#endif

#endif // OVR_AVATAR_COMMON_VERTEX_PARAMS_INCLUDED
