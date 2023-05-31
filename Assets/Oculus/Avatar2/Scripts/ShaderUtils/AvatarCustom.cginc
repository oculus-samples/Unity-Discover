#ifndef AVATAR_CUSTOM_INCLUDED
#define AVATAR_CUSTOM_INCLUDED

// TODO*: Documentation here
//#include "UnityCG.cginc"
#include "UnityInstancing.cginc"
#include "AvatarCustomTypes.cginc"
#include "OvrAvatarSupportDefines.hlsl"
#include "OvrAvatarVertexFetch.hlsl"
#include "OvrAvatarCommonVertexParams.hlsl"

void OvrPopulateVertexDataFromExternalTexture(bool hasTangents, bool applyOffsetAndBias, inout OvrVertexData vertData) {
  const uint vertexId = vertData.vertexId;

  int numAttributes = 2;

  if (hasTangents) {
    numAttributes = 3;
    vertData.tangent = OvrGetVertexTangentFromTexture(
      vertexId,
      numAttributes,
      applyOffsetAndBias,
      _OvrAttributeInterpolationValue);
  }

  vertData.position = OvrGetVertexPositionFromTexture(
    vertexId,
    numAttributes,
    applyOffsetAndBias,
    _OvrAttributeInterpolationValue);
  vertData.normal = OvrGetVertexNormalFromTexture(
    vertexId,
    numAttributes,
    applyOffsetAndBias,
    _OvrAttributeInterpolationValue);
}

#if defined(OVR_SUPPORT_EXTERNAL_BUFFERS)
  void OvrPopulateVertexDataFromExternalBuffers(bool hasTangents, bool interpolateAttributes, inout OvrVertexData vertData) {
    const uint vertexId = vertData.vertexId;

    const uint numPosEntriesPerVert = _OvrNumOutputEntriesPerAttribute;
    const uint numFrenetEntriesPerVert = (hasTangents ? 2u : 1u) * _OvrNumOutputEntriesPerAttribute;

    const uint startIndexOfPositionEntries = vertexId * numPosEntriesPerVert;
    const uint startIndexOfFrenetEntries = vertexId * numFrenetEntriesPerVert;

    const uint posEntryIndex = startIndexOfPositionEntries + _OvrAttributeOutputLatestAnimFrameEntryOffset;
    const uint normEntryIndex = startIndexOfFrenetEntries + _OvrAttributeOutputLatestAnimFrameEntryOffset;

    [branch]
    if (interpolateAttributes) {
      const float lerpValue = _OvrAttributeInterpolationValue;

      // Grab the latest animation frame's position as well as the previous animation frame's position
      const float3 latestPos = OvrGetPositionEntryFromExternalBuffer(posEntryIndex);
      const float3 prevPos = OvrGetPositionEntryFromExternalBuffer(startIndexOfPositionEntries + _OvrAttributeOutputPrevAnimFrameEntryOffset);
      const float3 latestNorm = OvrGetFrenetEntryFromExternalBuffer(normEntryIndex);
      const float3 prevNorm = OvrGetFrenetEntryFromExternalBuffer(startIndexOfFrenetEntries + _OvrAttributeOutputPrevAnimFrameEntryOffset);

      vertData.position = float4(lerp(prevPos, latestPos, lerpValue), 1.0);
      vertData.normal = lerp(prevNorm, latestNorm, lerpValue);

      [branch]
      if (hasTangents) {
        const uint tanEntriesStartIndex = startIndexOfFrenetEntries + _OvrNumOutputEntriesPerAttribute;
        const float4 latestTan = OvrGetFrenetEntryFromExternalBuffer(tanEntriesStartIndex + _OvrAttributeOutputLatestAnimFrameEntryOffset);
        const float3 prevTan = OvrGetFrenetEntryFromExternalBuffer(tanEntriesStartIndex + _OvrAttributeOutputPrevAnimFrameEntryOffset).xyz;

        vertData.tangent = float4(lerp(prevTan, latestTan, lerpValue), latestTan.w);
      }
    } else {
      vertData.position = float4(OvrGetPositionEntryFromExternalBuffer(posEntryIndex), 1.0);
      vertData.normal = OvrGetFrenetEntryFromExternalBuffer(normEntryIndex);

      [branch]
      if (hasTangents) {
        const uint tanEntryIndex = normEntryIndex + _OvrNumOutputEntriesPerAttribute;
        vertData.tangent = OvrGetFrenetEntryFromExternalBuffer(tanEntryIndex);
      }
    }
  }
#endif


// First, define a function which takes explicit data types, then define a macro which expands
// an arbitrary vertex structure definition into the function parameters
OvrVertexData OvrCreateVertexData(
  float4 vPos,
  float3 vNorm,
  float4 vTan,
  uint vertexId)
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

  OvrVertexData vertData;
  vertData.vertexId = vertexId;
  vertData.position = vPos;
  vertData.normal = vNorm;
  vertData.tangent = vTan;

  // Hope that the compiler branches here. The [branch] attribute here seems to lead to compile
  // probably due to "use of gradient function, such as tex3d"
  if (fetchMode == OVR_VERTEX_FETCH_MODE_EXTERNAL_TEXTURES) {
    // Backwards compatibility with existing keywords.
    // OVR_VERTEX_FETCH_TEXTURE_UNORM means normalized attributes, OVR_VERTEX_FETCH_TEXTURE
    // means not normalized. Neither keyword means that the "fetch mode" was via
    // a property and there is no property for normalized attributes or not. So in that
    // scenario, always apply offset and bias
    #if defined(OVR_VERTEX_FETCH_TEXTURE)
      static const bool applyOffsetAndBias = false;
    #else
      static const bool applyOffsetAndBias = true;
    #endif

    OvrPopulateVertexDataFromExternalTexture(hasTangents, applyOffsetAndBias, vertData);
#if defined(OVR_SUPPORT_EXTERNAL_BUFFERS)
  } else if (fetchMode == OVR_VERTEX_FETCH_MODE_EXTERNAL_BUFFERS) {
    OvrPopulateVertexDataFromExternalBuffers(hasTangents, interpolateAttributes, vertData);
#endif
  }

  return vertData;
} // end OvrCreateVertexData

#define OVR_CREATE_VERTEX_DATA(v) \
  OvrCreateVertexData( \
    OVR_GET_VERTEX_POSITION_FIELD(v), \
    OVR_GET_VERTEX_NORMAL_FIELD(v), \
    OVR_GET_VERTEX_TANGENT_FIELD(v), \
    OVR_GET_VERTEX_VERT_ID_FIELD(v))

// Initialization for "required fields" in the vertex input struct for the vertex shader.
// Written as a macro to be expandable in future
#define OVR_INITIALIZE_VERTEX_FIELDS(v)

// Initializes the fields for a defined default vertex structure
void OvrInitializeDefaultAppdata(inout OvrDefaultAppdata v) {
  OVR_INITIALIZE_VERTEX_FIELDS(v);
#ifdef UNIVERSAL_LIGHTING_INCLUDED
  #ifdef UNITY_STEREO_INSTANCING_ENABLED
    #if defined(SHADER_API_GLES3)
      unity_StereoEyeIndex = round(fmod(v.instanceID, 2.0));
      unity_InstanceID = unity_BaseInstanceID + (v.instanceID >> 1);
    #else
      unity_StereoEyeIndex = v.instanceID & 0x01;
      unity_InstanceID = unity_BaseInstanceID + (v.instanceID >> 1);
    #endif
  #else
  // there used to be a global variable unity_InstanceID? For now, do nothing
  //  unity_InstanceID = v.instanceID + unity_BaseInstanceID;
  #endif
#else
  UNITY_SETUP_INSTANCE_ID(v);
#endif
}

// Initializes the fields for a defined default vertex structure
// and creates the OvrVertexData for the vertex as well as overrides
// applicable fields in OvrDefaultAppdata with fields from OvrVertexData.
// Mainly useful in surface shader vertex functions.
OvrVertexData OvrInitializeDefaultAppdataAndPopulateWithVertexData(inout OvrDefaultAppdata v) {
  OvrInitializeDefaultAppdata(v);
  OvrVertexData vertexData = OVR_CREATE_VERTEX_DATA(v);

  OVR_SET_VERTEX_POSITION_FIELD(v, vertexData.position);
  OVR_SET_VERTEX_NORMAL_FIELD(v, vertexData.normal);
  OVR_SET_VERTEX_TANGENT_FIELD(v, vertexData.tangent);

  return vertexData;
}

#endif // AVATAR_CUSTOM_INCLUDED
