// textures.glsl needs to be included

static const float M_PI = 3.141592653589793;
static const float c_MinReflectance = 0.04;

struct AngularInfo
{
    float NdotL;                  // cos angle between normal and light direction
    float NdotV;                  // cos angle between normal and view direction
    float NdotH;                  // cos angle between normal and half vector
    float LdotH;                  // cos angle between light direction and half vector

    float VdotH;                  // cos angle between view direction and half vector

    float3 padding;
};

// Find the normal for this fragment, pulling either from a predefined normal map
// or from the interpolated mesh normal and tangent attributes.
#ifdef HAS_TANGENTS
float3 getNormal(float3 v_Position, float2 v_UVCoord1, float2 v_UVCoord2, float3 v_Tangent, float3 v_Bitangent, float3 v_Normal)
#else
float3 getNormal(float3 v_Position, float2 v_UVCoord1, float2 v_UVCoord2, float3 v_Normal)
#endif
{
    float2 UV = getNormalUV(v_UVCoord1,v_UVCoord2);

    // Retrieve the tangent space matrix
#ifndef HAS_TANGENTS
    float3 pos_dx = ddx(v_Position);
    float3 pos_dy = ddy(v_Position);
    float3 tex_dx = ddx(float3(UV, 0.0));
    float3 tex_dy = ddy(float3(UV, 0.0));
    float3 t = (tex_dy.y * pos_dx - tex_dx.y * pos_dy) / (tex_dx.x * tex_dy.y - tex_dy.x * tex_dx.y);

#ifdef HAS_NORMALS
    float3 ng = normalize(v_Normal);
#else
    float3 ng = cross(pos_dx, pos_dy);
#endif

    t = normalize(t - ng * dot(ng, t));
    float3 b = normalize(cross(ng, t));
    float3x3 tbn = float3x3(t, b, ng);
#else // HAS_TANGENTS
    float3x3 tbn = float3x3(v_Tangent, v_Bitangent, v_Normal);
#endif

#ifdef HAS_NORMAL_MAP
    float3 n = texture2D(u_NormalSampler, UV).rgb;
    n = normalize(tbn * ((2.0 * n - 1.0) * float3(u_NormalScale, u_NormalScale, 1.0)));
#else
    // The tbn matrix is linearly interpolated, so we need to re-normalize
    float3 n = normalize(tbn[2].xyz);
#endif

    return n;
}

float getPerceivedBrightness(float3 vec)
{
    return sqrt(0.299 * vec.r * vec.r + 0.587 * vec.g * vec.g + 0.114 * vec.b * vec.b);
}

// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_pbrSpecularGlossiness/examples/convert-between-workflows/js/three.pbrUtilities.js#L34
float solveMetallic(float3 diffuse, float3 specular, float oneMinusSpecularStrength) {
    float specularBrightness = getPerceivedBrightness(specular);

    if (specularBrightness < c_MinReflectance) {
        return 0.0;
    }

    float diffuseBrightness = getPerceivedBrightness(diffuse);

    float a = c_MinReflectance;
    float b = diffuseBrightness * oneMinusSpecularStrength / (1.0 - c_MinReflectance) + specularBrightness - 2.0 * c_MinReflectance;
    float c = c_MinReflectance - specularBrightness;
    float D = b * b - 4.0 * a * c;

    return clamp((-b + sqrt(D)) / (2.0 * a), 0.0, 1.0);
}

AngularInfo getAngularInfo(float3 pointToLight, float3 normal, float3 view)
{
    // Standard one-letter names
    float3 n = normalize(normal);           // Outward direction of surface point
    float3 v = normalize(view);             // Direction from surface point to view
    float3 l = normalize(pointToLight);     // Direction from surface point to light
    float3 h = normalize(l + v);            // Direction of the vector between l and v

    float NdotL = clamp(dot(n, l), 0.0, 1.0);
    float NdotV = clamp(dot(n, v), 0.0, 1.0);
    float NdotH = clamp(dot(n, h), 0.0, 1.0);
    float LdotH = clamp(dot(l, h), 0.0, 1.0);
    float VdotH = clamp(dot(v, h), 0.0, 1.0);

    AngularInfo returnval;
    returnval.NdotL = NdotL;
    returnval.NdotV = NdotV;
    returnval.NdotH = NdotH;
    returnval.LdotH = LdotH;
    returnval.VdotH = VdotH;
    returnval.padding = float3(0, 0, 0);

    return returnval;
}
