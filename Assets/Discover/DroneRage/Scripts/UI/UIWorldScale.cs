// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;

namespace Discover.DroneRage.UI
{
    /// <summary>
    /// Animates the ellipsis of the given text. Uses maxVisibleCharacters to control the ellipsis visibility instead of appending and removing characters.
    /// Usage: Attach to your text or assign the target text in the inspector. Include the ellipsis in your text.
    /// </summary>
    public class UIWorldScale : MonoBehaviour
    {

        private enum ScaleMode
        {
            WIDTH,
            HEIGHT,
            SEPARATE
        }


        [SerializeField]
        private RectTransform m_targetTransform;


        [SerializeField]
        private ScaleMode m_scaleMode = ScaleMode.WIDTH;


        [SerializeField]
        private float m_worldWidth = 1.0f;

        [SerializeField]
        private float m_worldHeight = 1.0f;

        private void OnEnable()
        {
            ResizeTransform();
        }

        private void ResizeTransform()
        {
            var scale = m_targetTransform.localScale;
            var rect = m_targetTransform.rect;
            switch (m_scaleMode)
            {
                case ScaleMode.WIDTH:
                    scale.x = m_worldWidth / rect.width;
                    scale.y = scale.x;
                    m_worldHeight = m_worldWidth * (rect.height / rect.width);
                    break;
                case ScaleMode.HEIGHT:
                    scale.y = m_worldHeight / rect.height;
                    scale.x = scale.y;
                    m_worldWidth = m_worldHeight * (rect.width / rect.height);
                    break;
                case ScaleMode.SEPARATE:
                    scale.x = m_worldWidth / rect.width;
                    scale.y = m_worldHeight / rect.height;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            m_targetTransform.localScale = scale;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_targetTransform == null)
            {
                m_targetTransform = GetComponentInChildren<RectTransform>();
            }

            if (enabled)
            {
                ResizeTransform();
            }
        }
#endif
    }
}
