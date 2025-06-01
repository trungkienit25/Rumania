using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalEngine
{
    /// <summary>
    /// FX that shows an item following the mouse when selected
    /// </summary>

    public class ItemSelectedFX : MonoBehaviour
    {
        public GameObject icon_group;
        public SpriteRenderer icon;
        public Text title;
        public float refresh_rate = 0.2f;

        private ItemSlot current_slot = null;
        private Selectable current_select = null;
        private float timer = 0f;

        private static ItemSelectedFX _instance;

        void Awake()
        {
            _instance = this;
            icon_group.SetActive(false);
            title.enabled = false;
        }

        void Update()
        {
            transform.position = PlayerControlsMouse.Get().GetPointingPos();
            transform.rotation = Quaternion.LookRotation(TheCamera.Get().transform.forward, Vector3.up);

            PlayerCharacter player = PlayerCharacter.GetSelf();
            PlayerControls controls = PlayerControls.Get();

            MAction maction = current_slot != null && current_slot.GetItem() != null ? current_slot.GetItem().FindMergeAction(current_select) : null;
            if (maction == null && current_slot != null && current_select != null)
                maction = current_select.FindMergeAction(current_slot.GetItem());

            title.enabled = maction != null && player != null && maction.CanDoAction(player, current_slot.GetSlot(), current_select);
            title.text = maction != null ? maction.title : "";

            bool active = current_slot != null && controls != null && !controls.IsGamePad();
            if (active != icon_group.activeSelf)
                icon_group.SetActive(active);

            icon.enabled = false;
            if (current_slot != null && current_slot.GetItem())
            {
                icon.sprite = current_slot.GetItem().icon;
                icon.enabled = true;
            }

            timer += Time.deltaTime;
            if (timer > refresh_rate)
            {
                timer = 0f;
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            current_slot = ItemSlotPanel.GetSelectedSlotInAllPanels();
            current_select = Selectable.GetNearestRaycast();
        }

        public static ItemSelectedFX Get()
        {
            return _instance;
        }
    }

}