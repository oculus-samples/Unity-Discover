// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.Assertions;

namespace Discover.DroneRage.UI
{
    [DefaultExecutionOrder(-1)]
    public class AutoInteractableUI : MonoBehaviour
    {
        private void Awake()
        {
            var rectTransform = GetComponent<RectTransform>();
            var canvas = GetComponent<Canvas>();

            Assert.IsNotNull(rectTransform, $"{nameof(rectTransform)} cannot be null.");
            Assert.IsNotNull(canvas, $"{nameof(canvas)} cannot be null.");

            var pointableCanvas = gameObject.AddComponent<PointableCanvas>();
            pointableCanvas.InjectAllPointableCanvas(canvas);

            var collider = gameObject.AddComponent<BoxCollider>();
            var colliderSurface = gameObject.AddComponent<ColliderSurface>();
            colliderSurface.InjectAllColliderSurface(collider);

            var autoSizeHitbox = gameObject.AddComponent<AutoSizeRaycastHitbox>();
            autoSizeHitbox.Hitbox = collider;
            autoSizeHitbox.Panel = rectTransform;

            var pointablePlane = gameObject.AddComponent<PlaneSurface>();

            var rayInteractable = gameObject.AddComponent<RayInteractable>();
            rayInteractable.InjectAllRayInteractable(colliderSurface);
            rayInteractable.InjectOptionalPointableElement(pointableCanvas);
            rayInteractable.InjectOptionalSelectSurface(pointablePlane);
        }
    }
}
