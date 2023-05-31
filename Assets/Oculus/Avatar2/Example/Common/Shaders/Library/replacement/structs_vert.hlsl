struct AvatarVertexInput
{
    float4 a_Color : COLOR;
    float4 a_ORMT : TEXCOORD3;
    float4 a_Normal : NORMAL;
    float4 a_Position : POSITION;
    float4 a_Tangent : TANGENT;
    float2 a_UV1 : TEXCOORD0;
    float2 a_UV2 : TEXCOORD1;
    uint a_instanceID : SV_InstanceID;
    uint a_vertexID : SV_VertexID;
};

struct VertexToFragment
{
    float4 v_Color : COLOR;
    float4 v_ORMT : TEXCOORD5;
    uint v_InstanceID : TEXCOORD8;
    float3 v_Normal : TEXCOORD2;
    float4 v_Tangent : TEXCOORD3;
    float2 v_UVCoord1 : TEXCOORD0;
    float2 v_UVCoord2 : TEXCOORD1;
    float4 v_Vertex : SV_POSITION;
    float3 v_WorldPos : TEXCOORD4;
    float3 v_SH : TEXCOORD6;
    //    float3 v_LightDir : TEXCOORD6;
};
