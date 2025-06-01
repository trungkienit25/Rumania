using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{
    [RequireComponent(typeof(Selectable))]
    public class Door : SNetworkBehaviour
    {
        private Selectable select;
        private Animator animator;
        private Collider collide;

        private SNetworkActions actions;
        private bool opened = false;
        private float toggle_timer = 0f;

        void Start()
        {
            select = GetComponent<Selectable>();
            animator = GetComponentInChildren<Animator>();
            collide = GetComponentInChildren<Collider>();
            select.onUse += OnUse;
        }

        protected override void OnBeforeSpawn()
        {
            DoorState state = new DoorState();
            state.opened = opened;
            SetSpawnData(state);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            actions = new SNetworkActions(this);
            actions.Register("open", DoOpen, NetworkDelivery.Reliable, NetworkActionTarget.Server);
            actions.Register("close", DoClose, NetworkDelivery.Reliable, NetworkActionTarget.Server);
            actions.RegisterSerializable("refresh", DoRefresh);
            actions.IgnoreAuthority("open");
            actions.IgnoreAuthority("close");

            DoorState state = GetSpawnData<DoorState>();
            opened = state.opened;
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        private void Update()
        {
            toggle_timer += Time.deltaTime;
        }

        private void OnUse(PlayerCharacter character)
        {
            Toggle();
        }

        public void Toggle()
        {
            if (toggle_timer < 1f)
                return;

            if (!opened)
                Open();
            else
                Close();
        }

        public void Open()
        {
            actions?.Trigger("open"); // DoOpen()
        }

        public void Close()
        {
            actions?.Trigger("close"); // DoClose()
        }

        private void DoOpen()
        {
            opened = true;
            toggle_timer = 0f;
            Refresh();
        }

        private void DoClose()
        {
            opened = false;
            toggle_timer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            DoorState state = new DoorState();
            state.opened = opened;
            actions?.Trigger("refresh", state); // DoRefresh()
        }

        private void DoRefresh(SerializedData sdata)
        {
            DoorState state = sdata.Get<DoorState>();
            toggle_timer = 0f;

            if (!IsServer)
                opened = state.opened;

            if (collide != null)
                collide.isTrigger = opened;

            if (animator != null)
                animator.SetBool("Open", opened);
        }
    }

    public class DoorState : INetworkSerializable
    {
        public bool opened;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref opened);
        }
    }
}
