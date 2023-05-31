// this file attempts to emulate the glsl precision flags. Here are the original statements
// precision highp float;
// precision highp int;

// However in hlsl this switch is not as easily done.
// It can be overriden in the shader compiler, but that affects all variables.
// A more controlled strategy is to use these types:
#define float_t half
#define vec2_t half2
#define vec3_t half3
#define vec4_t half4
