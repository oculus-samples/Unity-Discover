#ifndef OVR_AVATAR_MOTION_VECTORS_CORE_INCLUDED
#define OVR_AVATAR_MOTION_VECTORS_CORE_INCLUDED
#include "UnityCG.cginc"
#include "AvatarCustomTypes.cginc"
#include "OvrAvatarSupportDefines.hlsl"
#include "OvrAvatarVertexFetch.hlsl"
#include "OvrAvatarCommonVertexParams.hlsl"

float _OvrPrevRenderFrameInterpolationValue;
int _OvrAttributeOutputPrevRenderFrameLatestAnimFrameOffset;
int _OvrAttributeOutputPrevRenderFramePrevAnimFrameOffset;

float4x4 unity_MatrixPreviousM;
float4x4 unity_StereoMatrixPrevVP[2];

#define unity_MatrixPrevVP unity_StereoMatrixPrevVP[unity_StereoEyeIndex]

struct OvrMotionVectorsVertex
{
  OVR_VERTEX_POSITION_FIELD
  float3 previousPositionOS : TEXCOORD4; // Needs to be TEXCOORD4 to be auto populated by Unity
  OVR_VERTEX_VERT_ID_FIELD
  uint instanceID : SV_InstanceID;
  //UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OvrMotionVectorsVaryings
{
  float4 positionCS : SV_POSITION;
  float4 curPositionCS : TEXCOORD0;
  float4 prevPositionCS : TEXCOORD1;
  UNITY_VERTEX_OUTPUT_STEREO
};


float3 OvrMotionVectorsGetObjectSpacePositionFromTexture(uint vertexId, int numAttributes, bool applyOffsetAndBias) {
  return OvrGetVertexPositionFromTexture(vertexId, numAttributes, applyOffsetAndBias, _OvrAttributeInterpolationValue);
}

float3 OvrMotionVectorsGetPrevObjectSpacePositionFromTexture(uint vertexId, int numAttributes, bool applyOffsetAndBias) {
  return OvrGetVertexPositionFromTexture(vertexId, numAttributes, applyOffsetAndBias, _OvrPrevRenderFrameInterpolationValue);
}

#if defined(OVR_SUPPORT_EXTERNAL_BUFFERS)
  float3 OvrMotionVectorsGetObjectSpacePositionFromBuffer(uint vertexId) {
    const uint numPosEntriesPerVert = _OvrNumOutputEntriesPerAttribute;
    const uint startIndexOfPositionEntries = vertexId * numPosEntriesPerVert;
    const uint posEntryIndex = startIndexOfPositionEntries + _OvrAttributeOutputLatestAnimFrameEntryOffset;

    return OvrGetPositionEntryFromExternalBuffer(posEntryIndex);
  }

  float3 OvrMotionVectorsGetInterpolatedObjectSpacePositionFromBuffer(uint vertexId) {
    const uint numPosEntriesPerVert = _OvrNumOutputEntriesPerAttribute;
    const uint startIndexOfPositionEntries = vertexId * numPosEntriesPerVert;

    const uint latestPosEntryIndex = startIndexOfPositionEntries + _OvrAttributeOutputLatestAnimFrameEntryOffset;
    const uint prevPosEntryIndex = startIndexOfPositionEntries + _OvrAttributeOutputPrevAnimFrameEntryOffset;

    const float3 p0 = OvrGetPositionEntryFromExternalBuffer(prevPosEntryIndex);
    const float3 p1 = OvrGetPositionEntryFromExternalBuffer(latestPosEntryIndex);

    return lerp(p0, p1, _OvrAttributeInterpolationValue);
  }

  float3 OvrMotionVectorsGetPrevObjectSpacePositionFromBuffer(uint vertexId) {
    const uint numPosEntriesPerVert = _OvrNumOutputEntriesPerAttribute;
    const uint startIndexOfPositionEntries = vertexId * numPosEntriesPerVert;
    const uint posEntryIndex = startIndexOfPositionEntries + _OvrAttributeOutputPrevRenderFrameLatestAnimFrameOffset;

    return OvrGetPositionEntryFromExternalBuffer(posEntryIndex);
  }

  float3 OvrMotionVectorsGetPrevInterpolatedObjectSpacePositionFromBuffer(uint vertexId) {
    const uint numPosEntriesPerVert = _OvrNumOutputEntriesPerAttribute;
    const uint startIndexOfPositionEntries = vertexId * numPosEntriesPerVert;

    const uint pos1EntryIndex = startIndexOfPositionEntries + _OvrAttributeOutputPrevRenderFrameLatestAnimFrameOffset;
    const uint pos0EntryIndex = startIndexOfPositionEntries + _OvrAttributeOutputPrevRenderFramePrevAnimFrameOffset;

    const float3 p0 = OvrGetPositionEntryFromExternalBuffer(pos0EntryIndex);
    const float3 p1 = OvrGetPositionEntryFromExternalBuffer(pos1EntryIndex);

    return lerp(p0, p1, _OvrPrevRenderFrameInterpolationValue);
  }
#endif

void OvrMotionVectorsGetPositions(uint vertexId, inout float3 currentPos, inout float3 prevPos)
{
  // Backward compatibility/optimization support if application is ok with additional variants
  // The shader compiler should optimize out branches that are based on static const values
  #if defined(OVR_VERTEX_FETCH_VERT_BUFFER)
    static const int fetchMode = OVR_VERTEX_FETCH_MODE_STRUCT;
  #elif defined(OVR_VERTEX_FETCH_EXTERNAL_BUFFER) && defined(OVR_SUPPORT_EXTERNAL_BUFFERS)
    static const int fetchMode = OVR_VERTEX_FETCH_MODE_EXTERNAL_BUFFERS;
  #elif defined(OVR_VERTEX_FETCH_TEXTURE) || defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)
    static const int fetchMode = OVR_VERTEX_FETCH_MODE_EXTERNAL_TEXTURES;
  #else
    const int fetchMode = _OvrVertexFetchMode;
  #endif

  #if defined(OVR_VERTEX_HAS_TANGENTS)
    static const bool hasTangents = true;
  #elif defined(OVR_VERTEX_NO_TANGENTS)
    static const bool hasTangents = false;
  #else
    const bool hasTangents = _OvrHasTangents;
  #endif

  #if defined(OVR_VERTEX_INTERPOLATE_ATTRIBUTES)
    static const bool interpolateAttributes = true;
  #elif defined(OVR_VERTEX_DO_NOT_INTERPOLATE_ATTRIBUTES)
    static const bool interpolateAttributes = false;
  #else
    const bool interpolateAttributes = _OvrInterpolateAttributes;
  #endif

  // Hope that the compiler branches here. The [branch] attribute here seems to lead to compile
  // probably due to "use of gradient function, such as tex3d"
  if (fetchMode == OVR_VERTEX_FETCH_MODE_EXTERNAL_TEXTURES) {
    const int numAttributes =  hasTangents ? 3 : 2;

    #if defined(OVR_VERTEX_FETCH_TEXTURE)
        static const bool applyOffsetAndBias = false;
    #else
        static const bool applyOffsetAndBias = true;
    #endif

    currentPos = OvrMotionVectorsGetObjectSpacePositionFromTexture(vertexId, numAttributes, applyOffsetAndBias);
    prevPos = OvrMotionVectorsGetPrevObjectSpacePositionFromTexture(vertexId, numAttributes, applyOffsetAndBias);
#ifdef OVR_SUPPORT_EXTERNAL_BUFFERS
  } else if (fetchMode == OVR_VERTEX_FETCH_MODE_EXTERNAL_BUFFERS) {
    [branch]
    if (interpolateAttributes) {
      currentPos = OvrMotionVectorsGetInterpolatedObjectSpacePositionFromBuffer(vertexId);
      prevPos = OvrMotionVectorsGetPrevInterpolatedObjectSpacePositionFromBuffer(vertexId);
    } else {
      currentPos = OvrMotionVectorsGetObjectSpacePositionFromBuffer(vertexId);
      prevPos = OvrMotionVectorsGetPrevObjectSpacePositionFromBuffer(vertexId);
    }
#endif
  }
}

OvrMotionVectorsVaryings OvrMotionVectorsVertProgram(OvrMotionVectorsVertex input)
{
  UNITY_SETUP_INSTANCE_ID(input);
  OvrMotionVectorsVaryings output;
  UNITY_INITIALIZE_OUTPUT(OvrMotionVectorsVaryings, output);
  UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

  // Pull from vertex buffer
  float3 currentPos = OVR_GET_VERTEX_POSITION_FIELD(input).xyz;
  float3 prevPos = input.previousPositionOS;
  const uint vertexId = OVR_GET_VERTEX_VERT_ID_FIELD(input);

  OvrMotionVectorsGetPositions(vertexId, currentPos, prevPos);

  output.positionCS = UnityObjectToClipPos(currentPos);
  output.curPositionCS = output.positionCS;
  float3 prevPosWorld = mul(unity_MatrixPreviousM, float4(prevPos, 1.0f)).xyz;
  output.prevPositionCS = mul(unity_MatrixPrevVP, float4(prevPosWorld, 1.0));

  return output;
}

half4 OvrMotionVectorsFragProgram(OvrMotionVectorsVaryings IN) : SV_Target
{
  float3 screenPos = IN.curPositionCS.xyz / IN.curPositionCS.w;
  float3 screenPosPrev = IN.prevPositionCS.xyz / IN.prevPositionCS.w;

  half4 color = 1;
  color.xyz = screenPos - screenPosPrev;
  return color;
}

#endif // OVR_AVATAR_MOTION_VECTORS_CORE_INCLUDED
