// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.DroneRage.UI
{
    public class AutoSizeRaycastHitbox : MonoBehaviour
    {


        [SerializeField]
        private RectTransform m_panel = null;
        public RectTransform Panel
        {
            get => m_panel;
            set
            {
                m_panel = value;
                ResizeHitbox();
            }
        }


        [SerializeField]
        private BoxCollider m_hitbox = null;
        public BoxCollider Hitbox
        {
            get => m_hitbox;
            set
            {
                m_hitbox = value;
                ResizeHitbox();
            }
        }


        [SerializeField]
        private bool m_resizeOnUpdate = false;

        private void OnEnable()
        {
            ResizeHitbox();
        }

        private void Update()
        {
            if (m_resizeOnUpdate)
            {
                ResizeHitbox();
            }
        }

        private void ResizeHitbox()
        {
            if (m_panel == null || m_hitbox == null)
            {
                return;
            }

            m_hitbox.size = new Vector3(m_panel.rect.width, m_panel.rect.height, 0.01f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_panel == null)
            {
                m_panel = GetComponent<RectTransform>();
            }
            if (m_hitbox == null)
            {
                m_hitbox = GetComponent<BoxCollider>();
            }
        }
#endif
    }
}
