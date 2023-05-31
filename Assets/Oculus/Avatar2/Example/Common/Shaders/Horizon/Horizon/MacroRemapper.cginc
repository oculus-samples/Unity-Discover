
      #ifdef SUN_LIGHT_MODE1
        #define DIRECTIONAL_LIGHT 1    // directional light activates three components: ability to have shader, color of diffuse light, specualar highlight
      #endif
      #ifdef SUN_LIGHT_MODE2
        #define DIRECTIONAL_LIGHT 1    // directional light activates three components: ability to have shader, color of diffuse light, specualar highlight
        #define SHADOWMAP_STATIC_VSM 1 // use variance shadow mapping for the environment/static shadow
      #endif
      #ifdef SUN_LIGHT_MODE3
        #define DIRECTIONAL_LIGHT 1    // directional light activates three components: ability to have shader, color of diffuse light, specualar highlight
        #define SHADOWMAP_STATIC_VSM 1 // use variance shadow mapping for the environment/static shadow
        #define DYNAMIC_SHADOWS 1      // take a seperate sample to combine the shadows of dynamic objects with the static shadows
      #endif