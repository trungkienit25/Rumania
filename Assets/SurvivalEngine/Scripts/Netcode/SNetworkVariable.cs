using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Events;
using Unity.Collections;

namespace NetcodePlus
{
    //Use to automatically sync variables with clients
    //It is recommanded to have limited number of SNetworkVariable for each object
    //and instead let them contain multiple variables inside a class or struct, syncing the entire class all at once

    public class SNetworkVariable<T> : SNetworkVariableBase where T : INetworkSerializable, new()
    {
        public bool auto_refresh = false;                                 //If true, no need to call Refresh(), it will be done automatically
        public float refresh_rate = 0f;                                   //Refresh rate in seconds, 0 will refresh every network tick
        public bool ignore_authority = false;                             //If true, anyone can trigger a refresh, regardless of owner

        public NetworkActionTarget target = NetworkActionTarget.Clients;          //By default, send refresh to clients only
        public NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced;     //By default, send refresh as UnreliableSequenced
        public ulong single_target = 0;

        public T value;

        public UnityAction<T> onSend;          //Called before a refresh is sent
        public UnityAction<T> onReceive;       //Called after a refresh is received

        private float timer = 0f;
        private bool need_refresh = true;     //Irrelevant if auto_refresh is true, otherwise will refresh the var at next refresh_rate

        public SNetworkVariable() { }                    //Will need to call Init() later after Spawn if using this constructor
        public SNetworkVariable(T val) { value = val; }   //Will need to call Init() later after Spawn if using this constructor

        public SNetworkVariable(SNetworkBehaviour beha, string var_id)
        {
            Link(beha, var_id);
        }

        public SNetworkVariable(SNetworkBehaviour beha, string var_id, T val)
        {
            value = val;
            Link(beha, var_id);
        }

        public SNetworkVariable(ulong custom_id, string var_id)
        {
            Link(custom_id, var_id);
        }

        public SNetworkVariable(ulong custom_id, string var_id, T val)
        {
            value = val;
            Link(custom_id, var_id);
        }

        ~SNetworkVariable()
        {
            Unlink();
        }

        public void Init(SNetworkBehaviour beha, ushort var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            Link(beha, var_id, delivery, target); //Old function name
        }

        public void Init(SNetworkBehaviour beha, string var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            Link(beha, var_id, delivery, target); //Old function name
        }

        public void Init(ulong custom_id, ushort var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            Link(custom_id, var_id, delivery, target); //Old function name
        }

        public void Init(ulong custom_id, string var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            Link(custom_id, var_id, delivery, target); //Old function name
        }

        //Initialize references and Link it (adds this object as reference in the list of all networkactions for syncing)
        public void Link(SNetworkBehaviour beha, ushort var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            netbehaviour = beha;
            custom_net_id = 0;
            variable_id = var_id;
            this.delivery = delivery;
            this.target = target;
            timer = 999f; //refresh now
            Link();
        }

        //Initialize references and Link it (adds this object as reference in the list of all networkactions for syncing)
        public void Link(SNetworkBehaviour beha, string var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            Link(beha, NetworkTool.Hash16(var_id), delivery, target);
        }

        //Link to a custom id without the need to have a SNetworkObject
        public void Link(ulong custom_id, ushort var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            netbehaviour = null;
            custom_net_id = custom_id;
            variable_id = var_id;
            this.delivery = delivery;
            this.target = target;
            timer = 999f; //refresh now
            Link();
        }

        //Link to a custom id without the need to have a SNetworkObject
        public void Link(ulong custom_id, string var_id,
            NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced,
            NetworkActionTarget target = NetworkActionTarget.Clients)
        {
            Init(custom_id, NetworkTool.Hash16(var_id), delivery, target);
        }

        protected override void OnTick()
        {
            if (value != null)
            {
                timer += TheNetwork.Get().DeltaTick;
                if (timer > refresh_rate)
                {
                    if (auto_refresh || need_refresh)
                    {
                        timer = 0f;
                        Refresh();
                    }
                }
            }
        }

        //Toggle on/off auto refresh every refresh_rate
        public void AutoRefresh(bool auto, float ref_rate = 0f)
        {
            auto_refresh = auto;
            refresh_rate = ref_rate;
            need_refresh = need_refresh || auto; //Force refresh next refresh_rate if auto-refresh keep toggle on/off
        }
        
        //Manually refresh NOW
        public void Refresh()
        {
            if (NetObject == null || !IsOnline || !IsConnected || !IsSpawned || value == null)
                return;

            if (!HasAuthority(TheNetwork.Get().ClientID))
                return; //Doesn't have authority, will be rejected by server, save on bandwidth

            need_refresh = false;
            onSend?.Invoke(value);

            if (ShouldSendServer())
                SendRefreshServer();
            else if (ShouldSendClients())
                SendRefreshClients();
            else if (ShouldSendSingle())
                SendRefreshTarget(single_target);
        }

        //Force refresh on the next refresh_rate
        public void RefreshNext()
        {
            need_refresh = true;
        }

        private void SendRefreshServer()
        {
            SendRefreshTarget(TheNetwork.Get().ServerID);
        }

        private void SendRefreshClients()
        {
            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp, TheNetwork.MsgSizeMax);
            writer.WriteValueSafe(NetworkId);
            writer.WriteValueSafe(BehaviourId);
            writer.WriteValueSafe(variable_id);
            writer.WriteValueSafe((ushort)delivery);
            writer.WriteNetworkSerializable(value);
            Messaging.SendAll("variable", writer, delivery);
            writer.Dispose();
        }

        private void SendRefreshTarget(ulong target_id)
        {
            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp, TheNetwork.MsgSizeMax);
            writer.WriteValueSafe(NetworkId);
            writer.WriteValueSafe(BehaviourId);
            writer.WriteValueSafe(variable_id);
            writer.WriteValueSafe((ushort)delivery);
            writer.WriteNetworkSerializable(value);
            Messaging.Send("variable", target_id, writer, delivery);
            writer.Dispose();
        }

        public override void ReceiveVariable(ulong client_id, FastBufferReader reader, NetworkDelivery delivery)
        {
            if (HasAuthority(client_id))
            {
                reader.ReadNetworkSerializable(out value);
                onReceive?.Invoke(value);

                if (TheNetwork.Get().IsServer)
                    ForwardVariable(client_id, reader, delivery);
            }
        }

        private void ForwardVariable(ulong origin_client_id, FastBufferReader reader, NetworkDelivery delivery)
        {
            if (delivery == NetworkDelivery.ReliableFragmentedSequenced)
                return; //Fragmented delivery bug on forwards (need to be investigated), for now just ignore

            Messaging.ForwardAll("variable", origin_client_id, reader, delivery);
        }

        //Refresh() can only be executed remotely from a client with authority
        public bool HasAuthority(ulong client_id)
        {
            bool owner = netbehaviour != null && client_id == netbehaviour.OwnerId;
            bool server = client_id == TheNetwork.Get().ServerID;
            return owner || server || ignore_authority;
        }

        public bool IsTargetClients() { return target == NetworkActionTarget.Clients || target == NetworkActionTarget.All; }
        public bool IsTargetServer() { return target == NetworkActionTarget.Server || target == NetworkActionTarget.ServerAndSelf || target == NetworkActionTarget.All; }
        public bool IsTargetSingle() { return target == NetworkActionTarget.Single; }

        public bool ShouldSendClients() { return IsTargetClients() && IsServer; }
        public bool ShouldSendServer() { return IsTargetServer() && !IsServer; }
        public bool ShouldSendSingle() { return IsTargetSingle() && single_target != TheNetwork.Get().ClientID; }
    }

    public class SNetworkVariableBase
    {
        protected SNetworkBehaviour netbehaviour;
        protected ushort variable_id;
        protected ulong custom_net_id;
        protected bool is_linked = false;

        private static Dictionary<ulong, List<SNetworkVariableBase>> variables = new Dictionary<ulong, List<SNetworkVariableBase>>();

        public SNetworkObject NetObject { get { return netbehaviour != null ? netbehaviour.NetObject : null; } }
        public ulong NetworkId { get { return netbehaviour != null ? netbehaviour.NetworkId : custom_net_id; } }
        public ushort BehaviourId { get { return netbehaviour != null ? netbehaviour.BehaviourId : (ushort)0; } }
        public ushort VariableId { get { return variable_id; } }

        public bool IsConnected { get { return TheNetwork.Get().IsConnected(); } }
        public bool IsOnline { get { return TheNetwork.Get().IsOnline; } }
        public bool IsSpawned { get { return netbehaviour != null ? netbehaviour.IsSpawned : false; } }
        public bool IsServer { get { return netbehaviour != null ? netbehaviour.IsServer : false; } }
        public bool IsOwner { get { return netbehaviour != null ? netbehaviour.IsOwner : false; } }

        public NetworkMessaging Messaging { get { return TheNetwork.Get().Messaging; } }

        public virtual void Link()
        {
            if (NetworkId == 0)
                return;

            if (!variables.ContainsKey(NetworkId))
                variables[NetworkId] = new List<SNetworkVariableBase>();

            List<SNetworkVariableBase> list = variables[NetworkId];
            if (!list.Contains(this))
                list.Add(this);

            is_linked = true;
        }

        public virtual void Unlink()
        {
            is_linked = false;

            if (NetworkId != 0 && variables.ContainsKey(NetworkId))
            {
                List<SNetworkVariableBase> list = variables[NetworkId];
                list.Remove(this);
            }
        }

        protected virtual void OnTick()
        {
            //Override this
        }

        public virtual void ReceiveVariable(ulong client_id, FastBufferReader reader, NetworkDelivery delivery)
        {
            //Override this
        }

        public virtual void Clear()
        {
            Unlink();
        }

        public virtual bool IsLinked()
        {
            return is_linked;
        }

        public static void TickAll()
        {
            foreach (KeyValuePair<ulong, List<SNetworkVariableBase>> item in variables)
            {
                if (item.Value != null)
                {
                    foreach (SNetworkVariableBase variable in item.Value)
                    {
                        variable.OnTick();
                    }
                }
            }
        }

        public static SNetworkVariableBase GetVariable(ulong net_id, ushort behaviour_id, ushort vid)
        {
            if (variables.ContainsKey(net_id))
            {
                List<SNetworkVariableBase> list = variables[net_id];
                foreach (SNetworkVariableBase variable in list)
                {
                    if (variable.NetworkId == net_id
                        && variable.BehaviourId == behaviour_id && vid == variable.VariableId)
                        return variable;
                }
            }
            return null;
        }

        public static void UnlinkAll()
        {
            variables.Clear();
        }

        public static void ClearAll()
        {
            variables.Clear(); //Same than UnlinkAll, but kept both names for consistency with SNetworkActions
        }
    }
}
