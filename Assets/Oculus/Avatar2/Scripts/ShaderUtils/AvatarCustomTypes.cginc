#ifndef AVATAR_CUSTOM_TYPES_INCLUDED
#define AVATAR_CUSTOM_TYPES_INCLUDED

// Keep this around for backwards compatibility
#define OVR_VERTEX_HAS_VERTEX_ID

// ------------------------------------------------------------------------------
// Define some macros for getting/setting a vertex field.
// Defined as macros to make potential field name changes easier to deal with.
// These macros are not required but may make it easier to integrate the SDK
// and use custom shaders
#ifndef OVR_VERTEX_POSITION_FIELD_NAME
#define OVR_VERTEX_POSITION_FIELD_NAME vertex
#endif

#define OVR_GET_VERTEX_POSITION_FIELD(v) v.OVR_VERTEX_POSITION_FIELD_NAME
#define OVR_SET_VERTEX_POSITION_FIELD(v, val) v.OVR_VERTEX_POSITION_FIELD_NAME = val;

// A "default" definition of a field
#define OVR_VERTEX_POSITION_FIELD float4 OVR_VERTEX_POSITION_FIELD_NAME : POSITION;

// ------------------------------------------------------------------------------
// Define some macros for getting/setting normal field
// Defined as macros to make potential field name changes easier to deal with.
#ifndef OVR_VERTEX_NORMAL_FIELD_NAME
#define OVR_VERTEX_NORMAL_FIELD_NAME normal
#endif

#define OVR_GET_VERTEX_NORMAL_FIELD(v) v.OVR_VERTEX_NORMAL_FIELD_NAME
#define OVR_SET_VERTEX_NORMAL_FIELD(v, val) v.OVR_VERTEX_NORMAL_FIELD_NAME = val;

// Define the "normal" field
#define OVR_VERTEX_NORMAL_FIELD float3 OVR_VERTEX_NORMAL_FIELD_NAME : NORMAL;

// ------------------------------------------------------------------------------
// Define some macros for getting/setting tangent field
// Defined as macros to make potential field name changes easier to deal with.
#ifndef OVR_VERTEX_TANGENT_FIELD_NAME
#define OVR_VERTEX_TANGENT_FIELD_NAME tangent
#endif

#define OVR_GET_VERTEX_TANGENT_FIELD(v) v.OVR_VERTEX_TANGENT_FIELD_NAME
#define OVR_SET_VERTEX_TANGENT_FIELD(v, val) v.OVR_VERTEX_TANGENT_FIELD_NAME = val;

// Define the "tangent" field
#define OVR_VERTEX_TANGENT_FIELD float4 OVR_VERTEX_TANGENT_FIELD_NAME : TANGENT;

// ------------------------------------------------------------------------------
// Define some macros for getting vertex ID field
// Defined as macros to make potential field name changes easier to deal with.
#ifndef OVR_VERTEX_VERT_ID_FIELD_NAME
#define OVR_VERTEX_VERT_ID_FIELD_NAME vid
#endif

#define OVR_GET_VERTEX_VERT_ID_FIELD(v) v.OVR_VERTEX_VERT_ID_FIELD_NAME

// Define the "vertex ID" field
#define OVR_VERTEX_VERT_ID_FIELD uint OVR_VERTEX_VERT_ID_FIELD_NAME : SV_VertexID;

// ------------------------------------------------------------------------------
// Define "required" fields for an vertex input structure

// NOTE: Due to the way surface shader work in Unity, some surface shaders may require
// having both the normal and tangent defined in the vertex input structure due to some
// surface shader inner workings. Due to that fact, the normal and tangent will be listed
// as "required" even if the information will be stored in a buffer instead. The hope/idea
// is that the shader compiler will optimize out the unused fields for vertex and fragment
// shaders and just fill in with some default values for surface shaders

#define OVR_REQUIRED_VERTEX_FIELDS \
  OVR_VERTEX_POSITION_FIELD \
  OVR_VERTEX_NORMAL_FIELD \
  OVR_VERTEX_TANGENT_FIELD \
  OVR_VERTEX_VERT_ID_FIELD


// Define next "set" of texture coordinate semantics which are available.
// This is useful to not require updating structs as more texture coordinate
// semantics become required
// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
// NOTE: Update these definitions if more interpolators become required
// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
#define OVR_FIRST_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD0
#define OVR_SECOND_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD1
#define OVR_THIRD_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD2
#define OVR_FOURTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD3
#define OVR_FIFTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD4
#define OVR_SIXTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD5
#define OVR_SEVENTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD6
#define OVR_EIGHTH_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC TEXCOORD7

// ------------------------------------------------------------------------------
// Define a "default" vertex structure

// ------------------------------------------------------------------------------
// Define some macros for getting/setting texture coordinate
// Defined as macros to make potential field name changes easier to deal with.
#ifndef OVR_VERTEX_TEXCOORD_FIELD_NAME
#define OVR_VERTEX_TEXCOORD_FIELD_NAME texcoord
#endif

#define OVR_GET_VERTEX_TEXCOORD(v) v.OVR_VERTEX_TEXCOORD_FIELD_NAME
#define OVR_SET_VERTEX_TEXCOORD(v, val) v.OVR_VERTEX_TEXCOORD_FIELD_NAME = val;

#define OVR_VERTEX_TEXCOORD_FIELD float4 OVR_VERTEX_TEXCOORD_FIELD_NAME : OVR_FIRST_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;

// ------------------------------------------------------------------------------
// Define some macros for getting/setting color
// Defined as macros to make potential field name changes easier to deal with.
#ifndef OVR_VERTEX_COLOR_FIELD_NAME
#define OVR_VERTEX_COLOR_FIELD_NAME color
#endif

#define OVR_GET_VERTEX_COLOR(v) v.OVR_VERTEX_COLOR_FIELD_NAME
#define OVR_SET_VERTEX_COLOR(v, val) v.OVR_VERTEX_COLOR_FIELD_NAME = val;

#define OVR_VERTEX_COLOR_FIELD float4 OVR_VERTEX_COLOR_FIELD_NAME : COLOR;

// ------------------------------------------------------------------------------
// Define some macros for getting/setting ormt
// Defined as macros to make potential field name changes easier to deal with.
#ifndef OVR_VERTEX_ORMT_FIELD_NAME
#define OVR_VERTEX_ORMT_FIELD_NAME ormt
#endif

#define OVR_GET_VERTEX_ORMT(v) v.OVR_VERTEX_ORMT_FIELD_NAME
#define OVR_SET_VERTEX_ORMT(v, val) v.OVR_VERTEX_ORMT_FIELD_NAME = val;

#define OVR_VERTEX_ORMT_FIELD \
  float4 OVR_VERTEX_ORMT_FIELD_NAME : OVR_SECOND_AVAILABLE_VERTEX_TEXCOORD_SEMANTIC;

#define OVR_DEFAULT_VERTEX_FIELDS \
  OVR_REQUIRED_VERTEX_FIELDS      \
  OVR_VERTEX_TEXCOORD_FIELD       \
  OVR_VERTEX_COLOR_FIELD          \
  OVR_VERTEX_ORMT_FIELD           \
  uint instanceID : SV_InstanceID;
  //UNITY_VERTEX_INPUT_INSTANCE_ID

// Define a default vertex input struct
struct OvrDefaultAppdata {
  OVR_DEFAULT_VERTEX_FIELDS
};

// ------------------------------------------------
// Define a structure for required data, per vertex
// This struct will be used in vertex programs instead
// of the vertex structure for Oculus related functions
struct OvrVertexData {
  float4 position;
  float3 normal;
  float4 tangent;

  uint vertexId;
};

#endif // AVATAR_CUSTOM_TYPES_INCLUDED
