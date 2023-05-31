#if USING_XR_MANAGEMENT && USING_XR_SDK_OCULUS && !OVRPLUGIN_UNSUPPORTED_PLATFORM
#define USING_XR_SDK
#endif

using System.Collections;
using Oculus.Avatar2;
using UnityEngine;
using UnityEngine.SceneManagement;

/* NOTE: This class will switch scenes in a way that keeps them independent from each other for testing.
 It will ensure the OvrAvatarManager is destroyed when switching scenes,
 allowing each scene to be cleanly tested on its own. In a real app, OvrAvatarManager
 should not be destroyed during scene changes.

 For a real app, simply switch scenes as usual. OvrAvatarManager will be marked DontDestroyOnLoad and carry
 over to the next scene automatically.
 */
public class TestSceneSwitcher : MonoBehaviour
{
#if USING_XR_SDK
    [System.Serializable]
    public struct InputMask
    {
        public OVRInput.Controller controllerMask;
        public OVRInput.Button buttonMask;
    }

    [SerializeField]
    private InputMask _nextSceneInput = new InputMask
    { controllerMask = OVRInput.Controller.RTouch, buttonMask = OVRInput.Button.One };

    [SerializeField]
    private InputMask _prevSceneInput = new InputMask
    { controllerMask = OVRInput.Controller.LTouch, buttonMask = OVRInput.Button.One };
#endif

    private void Awake()
    {
        // Because we destroy the old Avatar Manager for a clean scene change,
        // we need to give the new one the access token again
        if (OvrAvatarEntitlement.AccessTokenIsValid())
        {
            OvrAvatarEntitlement.ResendAccessToken();
        }
    }

    private void Update()
    {
        int sceneChange = 0;

#if USING_XR_SDK
        if (OVRInput.GetActiveController() != OVRInput.Controller.Hands)
        {
            if (OVRInput.GetUp(_nextSceneInput.buttonMask, _nextSceneInput.controllerMask))
            {
                sceneChange = 1;
            }
            else if (OVRInput.GetUp(_prevSceneInput.buttonMask, _prevSceneInput.controllerMask))
            {
                sceneChange = -1;
            }
        }
#endif
        if (sceneChange != 0)
        {
            StartCoroutine(SwitchScene(sceneChange));
        }
    }

#if !USING_XR_SDK
    private void OnGUI()
    {
        float buttonSize = Screen.height * 0.15f;

        var style = new GUIStyle();
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, Screen.height - buttonSize, Screen.width, buttonSize), SceneManager.GetActiveScene().name, style);

        if (GUI.Button(new Rect(0, Screen.height - buttonSize, buttonSize, buttonSize), "Prev"))
        {
            StartCoroutine(SwitchScene(-1));
        }
        if (GUI.Button(new Rect(Screen.width - buttonSize, Screen.height - buttonSize, buttonSize, buttonSize), "Next"))
        {
            StartCoroutine(SwitchScene(1));
        }
    }
#endif

    private IEnumerator SwitchScene(int direction)
    {
        // Wait a frame so OVRInput registers the button up event - otherwise it misses it during the scene switch
        yield return null;

        // Reset OvrAvatarManager so the next scene can create it again with a new configuration
        OvrAvatarManager.ResetInstance();

        // Change scenes
        int activeSceneIdx = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIdx = Wrap(activeSceneIdx + direction, 0, SceneManager.sceneCountInBuildSettings - 1);
        SceneManager.LoadScene(nextSceneIdx, LoadSceneMode.Single);
    }

    // Assumes value is only outside min/max by 1
    private int Wrap(int value, int min, int max)
    {
        if (value < min)
        {
            return max;
        }
        else if (value > max)
        {
            return min;
        }

        return value;
    }
}
