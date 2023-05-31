// Copyright (c) Meta Platforms, Inc. and affiliates.

#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

#define RECIPROCAL_TWO_PI_PLUS_HALF 0.1474233644
#define DIFFUSE_MIP_LEVEL 9
#define SPECULAR_MIP_LEVEL 0

// parameters
sampler2D _AlbedoTexture, _NormalMap, _OcclusionMap;
float4 _AlbedoTexture_ST;
float _Kd, _Ks, _Kr, _Roughness, _Kf, _Eta, _NormalBlend, _DiffuseRoughness, _OrthoRoughness, _Opacity, _OcclusionStrength;
float _Metalness;
float4 _AlbedoTint, _SpecularTint;
float _Kaffordance;
float4 _AffordanceColor;
int _SpecularMIP;

sampler2D _IBLTex;
half4 _IBLTex_HDR;
float _Exposure;

// vertex shader data
struct appdata
{
    float4 vertex : POSITION;
    float3 N : NORMAL;
    float4 T : TANGENT; // xyz = dir, w = sign
    float2 uv : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID // instancing
};

// fragment shader data
struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 N : TEXCOORD1;
    float3 T : TEXCOORD2;
    float3 B : TEXCOORD3;
    float4 wPos : TEXCOORD4;
    SHADOW_COORDS(5)
	UNITY_VERTEX_INPUT_INSTANCE_ID // instancing
	UNITY_VERTEX_OUTPUT_STEREO
};
 
// vertex shader
v2f vert(appdata v)
{
    v2f o;

    // instancing
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	UNITY_TRANSFER_INSTANCE_ID(v, o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _AlbedoTexture);
    o.wPos = mul(unity_ObjectToWorld, v.vertex);
	o.N = normalize(mul((float3x3)unity_ObjectToWorld, v.N));
	o.T = normalize(mul((float3x3)unity_ObjectToWorld, v.T));
	float3 binormal = cross(v.N, v.T.xyz); // * v.T.w;
	o.B = normalize(mul((float3x3)unity_ObjectToWorld, binormal));

    TRANSFER_SHADOW(o);
    return o;
}
 
// for lookups on latlong IBLs
float2 Cube2Latlong( float3 dir ) {
    float x = atan2( dir.z, dir.x ) * RECIPROCAL_TWO_PI_PLUS_HALF;
    float y = mad(dir.y,0.5,0.5); 
    return float2(x,y);
}

// schlick model of fresnel
float schlick(float eta, float3 I, float3 N)
{
    //float r0 = eta*eta;
    //return r0 + (1-r0)*(1.0f - cos(dot(I, N)));
    return pow(1.0 + dot(I, N), eta); // approximation that is more tweakable
}


// blinn-phong model of glossy specular reflection
float blinnPhong(float gloss, float3 N, float3 V, float3 L)
{
    float3 H = normalize(L + V);
    float NdotH = saturate(dot(H, N));
    float specK = exp2(gloss * 11) + 2;
    return pow(NdotH, specK) * gloss;
}

// Ward model of anisotropic reflection
float Ward(float3 N,float3 V,float3 L,float3 T, float3 B, float2 Kw)
{
    float3 H = normalize(L + V);
    float dotLN = dot(L, N);
    float dotHN = dot(H, N);
    float dotVN = dot(V, N);
    float dotHTAlphaX = dot(H, T) / Kw.x;
    float dotHBAlphaY = dot(H, B) / Kw.y;
    return sqrt(max(0.0, dotLN / dotVN)) * exp(-2.0 * (dotHTAlphaX * dotHTAlphaX + dotHBAlphaY * dotHBAlphaY) / (1.0 + dotHN));
}

// oren-nayar model of diffuse lighting
float orenNayar(float3 l,float3 v,float3 n,float r)
{
    float r2 = r*r;
    float a = 1.0 - 0.5*(r2/(r2+0.57));
    float b = 0.45*(r2/(r2+0.09));
    float nl = dot(n, l);
    float nv = dot(n, v);
    float ga = dot(v-n*nv,n-n*nl);
    return max(0.0,nl) * (a + b*max(0.0,ga) * sqrt((1.0-nv*nv)*(1.0-nl*nl)) / max(nl, nv));

}


// fragment shader
fixed4 frag(v2f i) : SV_Target
{

    fixed4 compC = 0;


    // instancing

	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);


    // compute normals

    #if defined(MIKKT)
		float3 Nt = tex2D(_NormalMap, i.uv).xyz;
		Nt = normalize(Nt * 2 - 1);
        Nt = normalize( lerp( float3(0,0,1), Nt, _NormalBlend ) );
        float3 N = normalize(i.N);
        float3 T = normalize(i.T);
        float3 B = normalize(i.B);
		float3x3 Mwt = float3x3(T, B, N);
		float3x3 Mtw = transpose(Mwt); // inverse of orthogonal matrix is its transpose
		N = mul(Mtw, Nt);
    #else
        float3 N = normalize(i.N);
    #endif


    // shadows

    UNITY_LIGHT_ATTENUATION(atten, i, i.wPos);


    // diffuse

    float4 albedoTex = tex2D(_AlbedoTexture, i.uv);
    albedoTex.rgb *= _AlbedoTint;

    float specMult = 1;
    #if defined(SPECULAR_IN_ALPHA)
        specMult = albedoTex.a;
    #endif

    #if defined(OCCLUSION_MAP)
        float occlusion = tex2D(_OcclusionMap, i.uv).r;
        albedoTex.rgb *= lerp(1, occlusion, _OcclusionStrength);
    #endif

    float3 L = normalize(_WorldSpaceLightPos0.xyz); // directional light .w = 0, so just normalize the vector

    float NdotL = saturate(dot(N, L));
    float3 V = normalize(_WorldSpaceCameraPos - i.wPos);

    #if defined(_DIFFUSE_LAMBERT)
        float3 diffLight = NdotL * _LightColor0.xyz;
    #else                 
        float3 diffLight = orenNayar(L, V, N, _DiffuseRoughness) * _LightColor0.xyz;
    #endif
    diffLight *= atten;



    half3 diffIBL = 0;
    #if defined(IBL)
        half4 dibl = tex2Dlod( _IBLTex, float4(Cube2Latlong(N),0,DIFFUSE_MIP_LEVEL));
        diffIBL = DecodeHDR(dibl, _IBLTex_HDR);
        diffIBL *= unity_ColorSpaceDouble.rgb * _Exposure;
    #endif
            
    compC.rgb += albedoTex.rgb * _Kd * (diffLight + diffIBL);




    // specular

    #if defined(_SPECULAR_BLINNPHONG)
        #if defined(SPECULAR_IN_ALPHA)
            float3 specLight = blinnPhong(albedoTex.a * _Roughness, N, V, L) * _LightColor0.xyz;
        #else
            float3 specLight = blinnPhong(_Roughness, N, V, L) * _LightColor0.xyz;
        #endif
    #else
        #if defined(MIKKT)
            B = normalize(cross(N,T)); // hack 
            T = normalize(cross(N,B));
        #else
            float3 T = normalize(i.T);
            float3 B = normalize(i.B);
        #endif
        #if defined(SPECULAR_IN_ALPHA)
            float3 specLight = Ward(N, V, L, T, B, float2(albedoTex.a * _Roughness, albedoTex.a * _OrthoRoughness));
        #else
            float3 specLight = Ward(N, V, L, T, B, float2(_Roughness, _OrthoRoughness));
        #endif
    #endif

    float3 R = -reflect(V, N);
    half4 sibl = tex2Dlod( _IBLTex, float4(Cube2Latlong(R),0,_SpecularMIP));
    half3 specIBL = DecodeHDR(sibl, _IBLTex_HDR);
    specIBL *= unity_ColorSpaceDouble.rgb * _Exposure;
    float fresnel = schlick(_Eta, -V, N);
    float3 specC = lerp(1, _SpecularTint, _Metalness) * (_Ks * specLight + _Kr * specIBL+ _Kf * fresnel); 

    compC.rgb += specC;

    #if defined(AFFORDANCE)

        // affordance
        float luma = max(max(compC.r, compC.g), compC.b); // show the underlying texture
        float3 highlight = specC * pow(dot(N,V),4); // keep highlights off glancing surfaces
        compC.rgb = lerp(compC.rgb, saturate(_AffordanceColor.rgb * mad(luma,0.5,0.5) + highlight), _Kaffordance);

    #endif

    #if defined(TRANSPARENT)
        #if defined(SPECULAR_IN_ALPHA)
            compC.a = _Opacity;
        #else
            compC.a = _Opacity * albedoTex.a;
        #endif
        #if defined(PREMULTIPLY)
            compC.rgb *= compC.a;
        #endif
    #else
        compC.a = 1;
    #endif

    return compC;
}
 
 
 
