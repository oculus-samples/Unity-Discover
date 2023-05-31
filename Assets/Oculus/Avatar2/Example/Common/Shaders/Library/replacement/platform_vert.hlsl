

float4 getVertexInClipSpace(float3 pos) {
    #ifdef USING_URP
        return mul (UNITY_MATRIX_VP, mul (UNITY_MATRIX_M, float4 (pos,1.0)));
    #else
        return UnityObjectToClipPos(pos);
    #endif
}
#ifdef USING_URP

struct VertexInput {
    float2 uv0;
    float2 uv1;
};

float4 VertexGIForward(VertexInput v, float3 posWorld, float3 normalWorld){
      return float4(SampleSHVertex(normalWorld), 1.0);
}


#endif
