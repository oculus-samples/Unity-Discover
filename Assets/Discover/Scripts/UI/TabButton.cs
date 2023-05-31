// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Discover.UI
{
    [RequireComponent(typeof(Image))]
    public class TabButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
    {
        [SerializeField] private TabGroup m_tabGroup;
        [SerializeField] private bool m_activeWhenSubscribed;

        [AutoSet]
        [SerializeField] private Image m_background;

        public UnityEvent OnTabSelected;
        public UnityEvent OnTabDeselected;

        private void Start()
        {
            m_tabGroup.Subscribe(this);
            if (m_activeWhenSubscribed)
            {
                m_tabGroup.OnTabSelected(this);
            }
        }

        private void OnDestroy()
        {
            if (m_tabGroup != null)
            {
                m_tabGroup.Unsubscribe(this);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            m_tabGroup.OnTabSelected(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_tabGroup.OnTabEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_tabGroup.OnTabExit(this);
        }

        public void Select()
        {
            OnTabSelected?.Invoke();
        }

        public void Deselect()
        {
            OnTabDeselected?.Invoke();
        }

        public void SetColor(Color color)
        {
            m_background.color = color;
        }
    }
}
