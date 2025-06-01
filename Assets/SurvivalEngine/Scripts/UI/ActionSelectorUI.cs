using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{

    /// <summary>
    /// ActionSelectorUI is similar to ActionSelector, but for items in the player's inventory in the UI Canvas.
    /// </summary>

    public class ActionSelectorUI : UISlotPanel
    {
        private Animator animator;
        private ItemSlot slot;

        private UISlotPanel prev_panel = null;

        private static ActionSelectorUI instance;

        protected override void Awake()
        {
            base.Awake();

            instance = this;
            animator = GetComponent<Animator>();
            gameObject.SetActive(false);

        }

        protected override void Start()
        {
            base.Start();

            //PlayerControlsMouse.Get().onClick += OnMouseClick;
            PlayerControlsMouse.Get().onRightClick += OnMouseClick;

            onClickSlot += OnClick;
            onPressAccept += OnAccept;
            onPressCancel += OnCancel;
            onPressUse += OnCancel;
        }

        protected override void Update()
        {
            base.Update();

            //Auto focus
            UISlotPanel focus_panel = UISlotPanel.GetFocusedPanel();
            if (focus_panel != this && IsVisible() && PlayerControls.IsAnyGamePad())
            {
                prev_panel = focus_panel;
                Focus();
            }
        }

        private void RefreshSelector()
        {
            PlayerCharacter character = PlayerCharacter.GetSelf();

            foreach (ActionSelectorButton button in slots)
                button.Hide();

            if (slot != null)
            {
                int index = 0;
                foreach (SAction action in slot.GetItem().actions)
                {
                    if (index < slots.Length && !action.IsAuto() && action.CanDoAction(character, slot.GetSlot()))
                    {
                        ActionSelectorButton button = (ActionSelectorButton) slots[index];
                        button.SetButton(action);
                        index++;
                    }
                }
            }
        }

        public void Show(ItemSlot slot)
        {
            PlayerCharacter character = PlayerCharacter.GetSelf();
            if (slot != null && character != null)
            {
                if (!IsVisible() || this.slot != slot)
                {
                    this.slot = slot;
                    RefreshSelector();
                    //animator.SetTrigger("Show");
                    transform.position = slot.transform.position;
                    gameObject.SetActive(true);
                    animator.Rebind();
                    animator.SetBool("Solo", CountActiveSlots() == 1);
                    selection_index = 0;
                    Show();
                }
            }
        }

        public override void Hide(bool instant = false)
        {
            if (IsVisible())
            {
                base.Hide(instant);
                animator.SetTrigger("Hide");
            }
        }

        private void OnClick(UISlot islot)
        {
            ActionSelectorButton button = (ActionSelectorButton)islot;
            OnClickAction(button.GetAction());
        }

        private void OnAccept(UISlot slot)
        {
            OnClick(slot);
            UISlotPanel.UnfocusAll();
            if (prev_panel != null)
                prev_panel.Focus();
        }

        private void OnCancel(UISlot slot)
        {
            ItemSlotPanel.CancelSelectionAll();
            Hide();
        }

        public void OnClickAction(SAction action)
        {
            if (IsVisible())
            {
                PlayerCharacter character = PlayerCharacter.GetSelf();
                if (action != null && slot != null && character != null)
                {
                    ItemSlot aslot = slot;

                    PlayerUI.Get(character.PlayerID)?.CancelSelection();
                    Hide();

                    character.DoAction(action, aslot.GetSlot());
                }
            }
        }

        private void OnMouseClick(Vector3 pos, Selectable select)
        {
            Hide();
        }

        public static ActionSelectorUI Get()
        {
            return instance;
        }
    }

}