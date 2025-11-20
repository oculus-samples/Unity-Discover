// Copyright (c) Meta Platforms, Inc. and affiliates.

using Discover.DroneRage.UI.EndScreen;
using Discover.Menus;
using Meta.XR.Samples;
using Oculus.Interaction;
using UnityEngine;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class MenuActiveState : MonoBehaviour, IActiveState
    {
        public bool ActiveWhenMenuUp;
        public bool ActiveWhenMenuDown;

        private static bool IsEndScreenUp() => EndScreenController.Instance != null && EndScreenController.Instance.isActiveAndEnabled;

        public bool Active => MainMenuController.Instance.IsMenuActive() || IsEndScreenUp() ?
            ActiveWhenMenuUp :
            ActiveWhenMenuDown;
    }
}