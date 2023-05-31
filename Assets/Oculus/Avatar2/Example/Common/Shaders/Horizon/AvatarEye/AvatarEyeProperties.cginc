#ifndef AVATAR_EYE_PROPERTIES_CGINC
#define AVATAR_EYE_PROPERTIES_CGINC

#include "../AvatarTextured/AvatarTexturedProperties.cginc"


////////////////////////////////////
// Properties for the Eye Glints. //
////////////////////////////////////

#define EYE_GLINTS             // amplifys the specular power for eyes, creating an artistic eye glint
#define EYE_GLINTS_BEHIND      // mirrors the spec light arond the Y axis such that an eye glint is almost always visible

half _EyeGlintFactor;

//////////////////////////////
// Eye Specific Properties //
//////////////////////////////

float _LeftEyeUp;
float _LeftEyeRight;
float _RightEyeUp;
float _RightEyeRight;
half _UVScale;

#endif
