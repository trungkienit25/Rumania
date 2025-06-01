using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace NetcodePlus
{
    /// <summary>
    /// A component for syncing transforms.
    /// NetworkTransform will read the underlying transform and replicate it to clients.
    /// The replicated value will be automatically be interpolated (if active) and applied to the underlying GameObject's transform.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Netcode/" + nameof(SNetworkTransform))]
    [DefaultExecutionOrder(100000)] // this is needed to catch the update time after the transform was updated by user scripts
    public class SNetworkTransform : SNetworkBehaviour
    {
        public const float PositionThresholdDefault = 0.001f;
        public const float RotAngleThresholdDefault = 0.01f;
        public const float ScaleThresholdDefault = 0.01f;

        public bool SyncPositionX = true;
        public bool SyncPositionY = true;
        public bool SyncPositionZ = true;
        public bool SyncRotAngleX = true;
        public bool SyncRotAngleY = true;
        public bool SyncRotAngleZ = true;
        public bool SyncScaleX = true;
        public bool SyncScaleY = true;
        public bool SyncScaleZ = true;

        public float PositionThreshold = PositionThresholdDefault;
        [Range(0.001f, 360.0f)]
        public float RotAngleThreshold = RotAngleThresholdDefault;
        public float ScaleThreshold = ScaleThresholdDefault;

        public bool InLocalSpace = false;           //Use Local space instead of World space
        public bool Interpolate = true;             //Interpolate movement for more smooth sync
        public bool AlwaysSync = false;             //Always sync the object, even when not moving

        private Transform m_Transform;
        private SNetworkTransformState network_state;
        private bool teleporting = false;
        private bool last_sent = false;
        private int last_tick = 0;
        private bool was_owner = false;

        private const ushort TypeRefresh = 1;
        private const ushort TypeTeleport = 2;
        private SNetworkActions actions = new SNetworkActions();

        private BufferedLinearInterpolator<float> m_PositionXInterpolator;
        private BufferedLinearInterpolator<float> m_PositionYInterpolator; 
        private BufferedLinearInterpolator<float> m_PositionZInterpolator; 
        private BufferedLinearInterpolator<Quaternion> m_RotationInterpolator; 
        private BufferedLinearInterpolator<float> m_ScaleXInterpolator; 
        private BufferedLinearInterpolator<float> m_ScaleYInterpolator; 
        private BufferedLinearInterpolator<float> m_ScaleZInterpolator;
        private readonly List<BufferedLinearInterpolator<float>> m_AllFloatInterpolators = new List<BufferedLinearInterpolator<float>>(6);

        protected override void Awake()
        {
            base.Awake();
            m_Transform = transform;
            network_state = new SNetworkTransformState();
            
            m_PositionXInterpolator = new BufferedLinearInterpolatorFloat();
            m_PositionYInterpolator = new BufferedLinearInterpolatorFloat();
            m_PositionZInterpolator = new BufferedLinearInterpolatorFloat();
            m_RotationInterpolator = new BufferedLinearInterpolatorQuaternion(); // rotation is a single Quaternion since each euler axis will affect the quaternion's final value
            m_ScaleXInterpolator = new BufferedLinearInterpolatorFloat();
            m_ScaleYInterpolator = new BufferedLinearInterpolatorFloat();
            m_ScaleZInterpolator = new BufferedLinearInterpolatorFloat();

            if (m_AllFloatInterpolators.Count == 0)
            {
                m_AllFloatInterpolators.Add(m_PositionXInterpolator);
                m_AllFloatInterpolators.Add(m_PositionYInterpolator);
                m_AllFloatInterpolators.Add(m_PositionZInterpolator);
                m_AllFloatInterpolators.Add(m_ScaleXInterpolator);
                m_AllFloatInterpolators.Add(m_ScaleYInterpolator);
                m_AllFloatInterpolators.Add(m_ScaleZInterpolator);
            }
        }

        protected virtual void Update()
        {
            if (!TheNetwork.Get().IsConnected())
                return;

            if (!IsSpawned)
                return;

            SendTransform();
            UpdateInterpolate();
            UpdateApplyTransform();
        }

        protected override void OnBeforeSpawn()
        {
            SetSpawnData(network_state);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            actions.Link(this);
            actions.RegisterSerializable(TypeRefresh, OnRefresh, NetworkDelivery.Unreliable, NetworkActionTarget.All);
            actions.RegisterSerializable(TypeTeleport, OnTeleport, NetworkDelivery.Reliable, NetworkActionTarget.All);
            was_owner = IsOwner;

            if (!IsServer)
            {
                network_state = GetSpawnData<SNetworkTransformState>();
                ApplyInterpolatedNetworkStateToTransform(network_state, transform, true);
            }

            ResetInterpolatedState(network_state);
        }

        protected override void OnDespawn()
        {
            actions.Clear();
        }

        private void SendTransform(bool force_send = false)
        {
            if (IsOwner && LocalTime.Tick > last_tick)
            {
                bool dirty = ApplyTransformToNetworkState(ref network_state, LocalTime.Time, m_Transform);
                if (dirty || last_sent || force_send || AlwaysSync)
                {
                    actions.Trigger(TypeRefresh, network_state);
                    last_tick = LocalTime.Tick;
                }
                last_sent = dirty || force_send; //Send one more frame to complete interpolation (it uses last 2 frames)
            }
        }

        private void UpdateInterpolate()
        {
            if (Interpolate)
            {
                if (was_owner != IsOwner)
                {
                    was_owner = IsOwner;
                    ResetInterpolatedState(network_state);
                }

                NetworkTime serverTime = IsOwner ? LocalTime : ServerTime;
                double cachedServerTime = serverTime.Time;
                double cachedRenderTime = serverTime.TimeTicksAgo(1).Time;

                foreach (var interpolator in m_AllFloatInterpolators)
                {
                    interpolator.Update(Time.deltaTime, cachedRenderTime, cachedServerTime);
                }

                m_RotationInterpolator.Update(Time.deltaTime, cachedRenderTime, cachedServerTime);
            }
        }

        private void UpdateApplyTransform()
        {
            if (!IsOwner)
            {
                ApplyInterpolatedNetworkStateToTransform(network_state, m_Transform, teleporting);
                teleporting = false;
            }
        }

        private void OnRefresh(SerializedData sdata)
        {
            if (!IsOwner)
            {
                network_state = sdata.Get<SNetworkTransformState>();
                AddInterpolatedState(network_state); //Interpolate received data
            }
        }

        private void OnTeleport(SerializedData sdata)
        {
            if (!IsOwner)
            {
                network_state = sdata.Get<SNetworkTransformState>();
                AddInterpolatedState(network_state, true); //Reset interpolation after teleport
                teleporting = true;
            }
        }

        public void Teleport(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            if (!IsOwner)
                return;

            Vector3 newRotationEuler = newRotation.eulerAngles;
            SNetworkTransformState stateToSend = network_state;
            stateToSend.Position = newPosition;
            stateToSend.Rotation = newRotationEuler;
            stateToSend.Scale = newScale;
            ApplyInterpolatedNetworkStateToTransform(stateToSend, transform, true);
            actions?.Trigger(TypeTeleport, stateToSend);
        }

        private bool ApplyTransformToNetworkState(ref SNetworkTransformState networkState, double netTime, Transform transformToUse)
        {
            return ApplyTransformToNetworkStateWithInfo(ref networkState, netTime, transformToUse).isDirty;
        }

        private (bool isDirty, bool isPositionDirty, bool isRotationDirty, bool isScaleDirty) ApplyTransformToNetworkStateWithInfo(ref SNetworkTransformState networkState, double netTime, Transform transformToUse)
        {
            var position = InLocalSpace ? transformToUse.localPosition : transformToUse.position;
            var rotAngles = InLocalSpace ? transformToUse.localEulerAngles : transformToUse.eulerAngles;
            var scale = transformToUse.localScale;
            return ApplyTransformToNetworkStateWithInfo(ref networkState, netTime, position, rotAngles, scale);
        }

        private (bool isDirty, bool isPositionDirty, bool isRotationDirty, bool isScaleDirty) ApplyTransformToNetworkStateWithInfo(ref SNetworkTransformState networkState, double netTime, Vector3 position, Vector3 rotAngles, Vector3 scale)
        {
            var isDirty = false;
            var isPositionDirty = false;
            var isRotationDirty = false;
            var isScaleDirty = false;

            if (InLocalSpace != networkState.InLocalSpace)
            {
                networkState.InLocalSpace = InLocalSpace;
                isDirty = true;
            }

            if (SyncPositionX &&
                Mathf.Abs(networkState.PositionX - position.x) > PositionThreshold)
            {
                networkState.PositionX = position.x;
                networkState.HasPositionX = true;
                isPositionDirty = true;
            }

            if (SyncPositionY &&
                Mathf.Abs(networkState.PositionY - position.y) > PositionThreshold)
            {
                networkState.PositionY = position.y;
                networkState.HasPositionY = true;
                isPositionDirty = true;
            }

            if (SyncPositionZ &&
                Mathf.Abs(networkState.PositionZ - position.z) > PositionThreshold)
            {
                networkState.PositionZ = position.z;
                networkState.HasPositionZ = true;
                isPositionDirty = true;
            }

            if (SyncRotAngleX &&
                Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleX, rotAngles.x)) > RotAngleThreshold)
            {
                networkState.RotAngleX = rotAngles.x;
                networkState.HasRotAngleX = true;
                isRotationDirty = true;
            }

            if (SyncRotAngleY &&
                Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleY, rotAngles.y)) > RotAngleThreshold)
            {
                networkState.RotAngleY = rotAngles.y;
                networkState.HasRotAngleY = true;
                isRotationDirty = true;
            }

            if (SyncRotAngleZ &&
                Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleZ, rotAngles.z)) > RotAngleThreshold)
            {
                networkState.RotAngleZ = rotAngles.z;
                networkState.HasRotAngleZ = true;
                isRotationDirty = true;
            }

            if (SyncScaleX &&
                Mathf.Abs(networkState.ScaleX - scale.x) > ScaleThreshold)
            {
                networkState.ScaleX = scale.x;
                networkState.HasScaleX = true;
                isScaleDirty = true;
            }

            if (SyncScaleY &&
                Mathf.Abs(networkState.ScaleY - scale.y) > ScaleThreshold)
            {
                networkState.ScaleY = scale.y;
                networkState.HasScaleY = true;
                isScaleDirty = true;
            }

            if (SyncScaleZ &&
                Mathf.Abs(networkState.ScaleZ - scale.z) > ScaleThreshold)
            {
                networkState.ScaleZ = scale.z;
                networkState.HasScaleZ = true;
                isScaleDirty = true;
            }

            isDirty |= isPositionDirty || isRotationDirty || isScaleDirty;
            networkState.SentTime = netTime;

            return (isDirty, isPositionDirty, isRotationDirty, isScaleDirty);
        }

        private void ApplyInterpolatedNetworkStateToTransform(SNetworkTransformState networkState, Transform transformToUpdate, bool teleport = false)
        {
            var interpolatedPosition = InLocalSpace ? transformToUpdate.localPosition : transformToUpdate.position;

            var interpolatedRotAngles = InLocalSpace ? transformToUpdate.localEulerAngles : transformToUpdate.eulerAngles;
            var interpolatedScale = transformToUpdate.localScale;

            InLocalSpace = networkState.InLocalSpace;
            if (SyncPositionX)
            {
                interpolatedPosition.x = teleport || !Interpolate ? networkState.Position.x : m_PositionXInterpolator.GetInterpolatedValue();
            }

            if (SyncPositionY)
            {
                interpolatedPosition.y = teleport || !Interpolate ? networkState.Position.y : m_PositionYInterpolator.GetInterpolatedValue();
            }

            if (SyncPositionZ)
            {
                interpolatedPosition.z = teleport || !Interpolate ? networkState.Position.z : m_PositionZInterpolator.GetInterpolatedValue();
            }

            if (SyncRotAngleX || SyncRotAngleY || SyncRotAngleZ)
            {
                var eulerAngles = new Vector3();
                if (Interpolate)
                {
                    eulerAngles = m_RotationInterpolator.GetInterpolatedValue().eulerAngles;
                }

                if (SyncRotAngleX)
                {
                    interpolatedRotAngles.x = teleport || !Interpolate ? networkState.Rotation.x : eulerAngles.x;
                }

                if (SyncRotAngleY)
                {
                    interpolatedRotAngles.y = teleport || !Interpolate ? networkState.Rotation.y : eulerAngles.y;
                }

                if (SyncRotAngleZ)
                {
                    interpolatedRotAngles.z = teleport || !Interpolate ? networkState.Rotation.z : eulerAngles.z;
                }
            }

            // Scale Read
            if (SyncScaleX)
            {
                interpolatedScale.x = teleport || !Interpolate ? networkState.Scale.x : m_ScaleXInterpolator.GetInterpolatedValue();
            }

            if (SyncScaleY)
            {
                interpolatedScale.y = teleport || !Interpolate ? networkState.Scale.y : m_ScaleYInterpolator.GetInterpolatedValue();
            }

            if (SyncScaleZ)
            {
                interpolatedScale.z = teleport || !Interpolate ? networkState.Scale.z : m_ScaleZInterpolator.GetInterpolatedValue();
            }

            // Position Apply
            if (SyncPositionX || SyncPositionY || SyncPositionZ)
            {
                if (InLocalSpace)
                {
                    transformToUpdate.localPosition = interpolatedPosition;
                }
                else
                {
                    transformToUpdate.position = interpolatedPosition;
                }
            }

            // RotAngles Apply
            if (SyncRotAngleX || SyncRotAngleY || SyncRotAngleZ)
            {
                if (InLocalSpace)
                {
                    transformToUpdate.localRotation = Quaternion.Euler(interpolatedRotAngles);
                }
                else
                {
                    transformToUpdate.rotation = Quaternion.Euler(interpolatedRotAngles);
                }
            }

            // Scale Apply
            if (SyncScaleX || SyncScaleY || SyncScaleZ)
            {
                transformToUpdate.localScale = interpolatedScale;
            }
        }

        private void AddInterpolatedState(SNetworkTransformState newState, bool teleport = false)
        {
            if (!Interpolate)
                return;

            var sentTime = newState.SentTime;

            if (teleport)
            {
                if (newState.HasPositionX)
                {
                    m_PositionXInterpolator.ResetTo(newState.PositionX, sentTime);
                }

                if (newState.HasPositionY)
                {
                    m_PositionYInterpolator.ResetTo(newState.PositionY, sentTime);
                }

                if (newState.HasPositionZ)
                {
                    m_PositionZInterpolator.ResetTo(newState.PositionZ, sentTime);
                }

                m_RotationInterpolator.ResetTo(Quaternion.Euler(newState.Rotation), sentTime);

                if (newState.HasScaleX)
                {
                    m_ScaleXInterpolator.ResetTo(newState.ScaleX, sentTime);
                }

                if (newState.HasScaleY)
                {
                    m_ScaleYInterpolator.ResetTo(newState.ScaleY, sentTime);
                }

                if (newState.HasScaleZ)
                {
                    m_ScaleZInterpolator.ResetTo(newState.ScaleZ, sentTime);
                }

                return;
            }
            if (newState.HasPositionX)
            {
                m_PositionXInterpolator.AddMeasurement(newState.PositionX, sentTime);
            }

            if (newState.HasPositionY)
            {
                m_PositionYInterpolator.AddMeasurement(newState.PositionY, sentTime);
            }

            if (newState.HasPositionZ)
            {
                m_PositionZInterpolator.AddMeasurement(newState.PositionZ, sentTime);
            }

            m_RotationInterpolator.AddMeasurement(Quaternion.Euler(newState.Rotation), sentTime);

            if (newState.HasScaleX)
            {
                m_ScaleXInterpolator.AddMeasurement(newState.ScaleX, sentTime);
            }

            if (newState.HasScaleY)
            {
                m_ScaleYInterpolator.AddMeasurement(newState.ScaleY, sentTime);
            }

            if (newState.HasScaleZ)
            {
                m_ScaleZInterpolator.AddMeasurement(newState.ScaleZ, sentTime);
            }
        }

        private void ResetInterpolatedState(SNetworkTransformState newState)
        {
            var serverTime = ServerTime.Time;
            m_PositionXInterpolator.ResetTo(newState.PositionX, serverTime);
            m_PositionYInterpolator.ResetTo(newState.PositionY, serverTime);
            m_PositionZInterpolator.ResetTo(newState.PositionZ, serverTime);

            m_RotationInterpolator.ResetTo(Quaternion.Euler(newState.Rotation), serverTime);

            m_ScaleXInterpolator.ResetTo(newState.ScaleX, serverTime);
            m_ScaleYInterpolator.ResetTo(newState.ScaleY, serverTime);
            m_ScaleZInterpolator.ResetTo(newState.ScaleZ, serverTime);
        }

        public NetworkTime LocalTime { get { return TheNetwork.Get().LocalTime; } }
        public NetworkTime ServerTime { get { return TheNetwork.Get().ServerTime; } }
    }

    internal struct SNetworkTransformState : INetworkSerializable
    {
        private const int k_InLocalSpaceBit = 0;
        private const int k_PositionXBit = 1;
        private const int k_PositionYBit = 2;
        private const int k_PositionZBit = 3;
        private const int k_RotAngleXBit = 4;
        private const int k_RotAngleYBit = 5;
        private const int k_RotAngleZBit = 6;
        private const int k_ScaleXBit = 7;
        private const int k_ScaleYBit = 8;
        private const int k_ScaleZBit = 9;
        //private const int k_TeleportingBit = 10;

        // 11-15: <unused>
        private ushort m_Bitset;

        internal bool InLocalSpace
        {
            get => (m_Bitset & (1 << k_InLocalSpaceBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_InLocalSpaceBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_InLocalSpaceBit)); }
            }
        }

        // Position
        internal bool HasPositionX
        {
            get => (m_Bitset & (1 << k_PositionXBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionXBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionXBit)); }
            }
        }

        internal bool HasPositionY
        {
            get => (m_Bitset & (1 << k_PositionYBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionYBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionYBit)); }
            }
        }

        internal bool HasPositionZ
        {
            get => (m_Bitset & (1 << k_PositionZBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionZBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionZBit)); }
            }
        }

        // RotAngles
        internal bool HasRotAngleX
        {
            get => (m_Bitset & (1 << k_RotAngleXBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleXBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleXBit)); }
            }
        }

        internal bool HasRotAngleY
        {
            get => (m_Bitset & (1 << k_RotAngleYBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleYBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleYBit)); }
            }
        }

        internal bool HasRotAngleZ
        {
            get => (m_Bitset & (1 << k_RotAngleZBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleZBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleZBit)); }
            }
        }

        // Scale
        internal bool HasScaleX
        {
            get => (m_Bitset & (1 << k_ScaleXBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_ScaleXBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_ScaleXBit)); }
            }
        }

        internal bool HasScaleY
        {
            get => (m_Bitset & (1 << k_ScaleYBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_ScaleYBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_ScaleYBit)); }
            }
        }

        internal bool HasScaleZ
        {
            get => (m_Bitset & (1 << k_ScaleZBit)) != 0;
            set
            {
                if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_ScaleZBit)); }
                else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_ScaleZBit)); }
            }
        }

        internal float PositionX, PositionY, PositionZ;
        internal float RotAngleX, RotAngleY, RotAngleZ;
        internal float ScaleX, ScaleY, ScaleZ;
        internal double SentTime;

        internal Vector3 Position
        {
            get { return new Vector3(PositionX, PositionY, PositionZ); }
            set
            {
                PositionX = value.x;
                PositionY = value.y;
                PositionZ = value.z;
            }
        }

        internal Vector3 Rotation
        {
            get { return new Vector3(RotAngleX, RotAngleY, RotAngleZ); }
            set
            {
                RotAngleX = value.x;
                RotAngleY = value.y;
                RotAngleZ = value.z;
            }
        }

        internal Vector3 Scale
        {
            get { return new Vector3(ScaleX, ScaleY, ScaleZ); }
            set
            {
                ScaleX = value.x;
                ScaleY = value.y;
                ScaleZ = value.z;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SentTime);
            // InLocalSpace + HasXXX Bits
            serializer.SerializeValue(ref m_Bitset);
            // Position Values
            if (HasPositionX)
            {
                serializer.SerializeValue(ref PositionX);
            }

            if (HasPositionY)
            {
                serializer.SerializeValue(ref PositionY);
            }

            if (HasPositionZ)
            {
                serializer.SerializeValue(ref PositionZ);
            }

            // RotAngle Values
            if (HasRotAngleX)
            {
                serializer.SerializeValue(ref RotAngleX);
            }

            if (HasRotAngleY)
            {
                serializer.SerializeValue(ref RotAngleY);
            }

            if (HasRotAngleZ)
            {
                serializer.SerializeValue(ref RotAngleZ);
            }

            // Scale Values
            if (HasScaleX)
            {
                serializer.SerializeValue(ref ScaleX);
            }

            if (HasScaleY)
            {
                serializer.SerializeValue(ref ScaleY);
            }

            if (HasScaleZ)
            {
                serializer.SerializeValue(ref ScaleZ);
            }
        }
    }
}
