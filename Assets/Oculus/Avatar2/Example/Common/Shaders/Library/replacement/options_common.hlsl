
#ifdef HAS_NORMAL_MAP_ON
static const bool enableNormalMapping = true;
#else
static const bool enableNormalMapping = false;
#endif

#ifdef SKIN_ON
static const bool enableSkin = true;
#else
static const bool enableSkin = false;
#endif

#ifdef EYE_GLINTS_ON
static const bool enableEyeGlint = true;
#else
static const bool enableEyeGlint = false;
#endif

#ifdef ENABLE_HAIR_ON
static const bool enableHair = true;
#else
static const bool enableHair = false;
#endif

#ifdef ENABLE_RIM_LIGHT_ON
static const bool enableRimLight = true;
#else
static const bool enableRimLight = false;
#endif

#ifdef ENABLE_ALPHA_TO_COVERAGE
static const bool enableAlphaToCoverage = true;
#else
static const bool enableAlphaToCoverage = false;
#endif

#ifdef ENABLE_PREVIEW_COLOR_RAMP_ON
static const bool enablePreviewColorRamp = true;
#else
static const bool enablePreviewColorRamp = false;
#endif

#if ENABLE_DEBUG_RENDER_ON
static const bool enableDebugRender = true;
#else
static const bool enableDebugRender = false;
#endif
