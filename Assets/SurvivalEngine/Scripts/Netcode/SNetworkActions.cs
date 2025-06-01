using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace NetcodePlus
{
    /// <summary>
    /// Allows to call functions on all clients and server, without using RPC calls
    /// This makes the code in other scripts shorter, instead of having to duplicate functions for different targets
    /// Check NetworkAction for more details on available targets
    /// Use Link() when the object spawns, and Unlink() when the object despawn
    /// Use Register() to define a new actions, and use Trigger() to trigger it
    /// </summary>

    public class SNetworkActions
    {
        private SNetworkBehaviour netbehaviour;
        private ulong custom_net_id;   //Use custom ID instead of netbehaviour for managers that dont have a SNetworkObject without the need to spawn/despsawn, only server has authority
        private bool is_linked = false;

        private List<NetworkAction> actions_list = new List<NetworkAction>();

        private static Dictionary<ulong, List<SNetworkActions>> handlers = new Dictionary<ulong, List<SNetworkActions>>();

        public SNetworkActions() { } //If using this contructor, Init() will need to be called

        public SNetworkActions(SNetworkBehaviour netbe) 
        {
            Link(netbe);
        }

        public SNetworkActions(ulong custom_id)
        {
            Link(custom_id); 
        }

        ~SNetworkActions()
        {
            Unlink();
        }

        public void Init(SNetworkBehaviour netbe)
        {
            Link(netbe); //Old function name
        }

        public void Init(ulong custom_id)
        {
            Link(custom_id); //Old function name
        }

        //Initialize references and Link it (adds this object as reference in the list of all networkactions for syncing)
        public void Link(SNetworkBehaviour netbe)
        {
            if (netbe != null)
            {
                this.netbehaviour = netbe;
                this.custom_net_id = 0; //Dont use
                Link();
            }
        }

        //Link to a custom id without the need to have a SNetworkObject
        public void Link(ulong custom_id)
        {
            this.netbehaviour = null; //Dont use
            this.custom_net_id = custom_id;
            Link(); //You should only have 1 handler of each custom id in entire project
        }

        public void Link()
        {
            if (NetworkId == 0)
                return;

            if (!handlers.ContainsKey(NetworkId))
                handlers[NetworkId] = new List<SNetworkActions>();

            List<SNetworkActions> list = handlers[NetworkId];
            if (!list.Contains(this))
                list.Add(this);

            is_linked = true;
        }

        public void Unlink()
        {
            DisposeAll();
            actions_list.Clear();
            is_linked = false;

            if (NetworkId != 0 && handlers.ContainsKey(NetworkId))
            {
                List<SNetworkActions> list = handlers[NetworkId];
                list.Remove(this);
                if (list.Count == 0)
                    handlers.Remove(NetworkId);
            }
        }

        //Remove handle and actions
        public void Clear()
        {
            Unlink();
        }
        
        public void ReceiveAction(ulong client_id, ushort type, FastBufferReader reader, NetworkDelivery delivery)
        {
            NetworkAction action = GetAction(type);
            ReceiveAction(client_id, action, reader, delivery);
        }

        protected void ReceiveAction(ulong client_id, NetworkAction action, FastBufferReader reader, NetworkDelivery delivery)
        {
            if (action != null && action.HasAuthority(netbehaviour, client_id))
            {
                action.RunAction(reader);

                //Server will forward action to other clients
                if (TheNetwork.Get().IsServer && action.IsTargetClients())
                    ForwardAction(client_id, reader, delivery);
            }
        }

        //Forward action to other clients (if the action originally came from origin_client_id)
        protected void ForwardAction(ulong origin_client_id, FastBufferReader reader, NetworkDelivery delivery)
        {
            if (delivery == NetworkDelivery.ReliableFragmentedSequenced)
                return; //Fragmented delivery bug on forwards (need to be investigated), for now just ignore

            Messaging.ForwardAll("action", origin_client_id, reader, delivery);
        }
        
        public void SendActionServer<T>(ushort type, T data, NetworkDelivery delivery) where T : INetworkSerializable
        {
            SendActionTarget(type, TheNetwork.Get().ServerID, data, delivery);
        }

        public void SendActionClients<T>(ushort type, T data, NetworkDelivery delivery) where T : INetworkSerializable
        {
            if (data == null || !IsConnected || !IsOnline)
                return;

            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp, TheNetwork.MsgSizeMax);
            writer.WriteValueSafe(NetworkId);
            writer.WriteValueSafe(BehaviourId);
            writer.WriteValueSafe(type);
            writer.WriteValueSafe((ushort)delivery);
            writer.WriteNetworkSerializable(data);
            Messaging.SendAll("action", writer, delivery);
            writer.Dispose();
        }

        public void SendActionTarget<T>(ushort type, ulong target, T data, NetworkDelivery delivery) where T : INetworkSerializable
        {
            if (data == null || !IsConnected || !IsOnline)
                return;

            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp, TheNetwork.MsgSizeMax);
            writer.WriteValueSafe(NetworkId);
            writer.WriteValueSafe(BehaviourId);
            writer.WriteValueSafe(type);
            writer.WriteValueSafe((ushort)delivery);
            writer.WriteNetworkSerializable(data);
            Messaging.Send("action", target, writer, delivery);
            writer.Dispose();
        }

        //--- Targets and Authority

        public void IgnoreAuthority(ushort type)
        {
            NetworkAction action = GetAction(type);
            if (action != null)
            {
                action.ignore_authority = true;
            }
        }

        public void IgnoreAuthority(string type)
        {
            IgnoreAuthority(NetworkTool.Hash16(type));
        }

        public void SetTarget(ushort type, NetworkActionTarget target, ulong single_target = 0)
        {
            NetworkAction action = GetAction(type);
            if (action != null)
            {
                action.target = target;
                action.single_target = single_target;
            }
        }

        public void SetTarget(string type, NetworkActionTarget target, ulong single_target = 0)
        {
            SetTarget(NetworkTool.Hash16(type), target, single_target);
        }

        //---- NetworkActionSimple ---------

        public void Register(ushort type, UnityAction callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionSimple action = NetworkActionSimple.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        public void Trigger(ushort type)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionSimple)
            {
                NetworkActionSimple naction = (NetworkActionSimple)action;
                naction.TriggerAction(this);
            }
        }

        //---- NetworkActionInt ---------

        public void RegisterInt(ushort type, UnityAction<int> callback, 
            NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionInt action = NetworkActionInt.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        public void Trigger(ushort type, int value)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionInt)
            {
                NetworkActionInt naction = (NetworkActionInt)action;
                naction.TriggerAction(this, value);
            }
        }

        //---- NetworkActionFloat ---------

        public void RegisterFloat(ushort type, UnityAction<float> callback, 
            NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionFloat action = NetworkActionFloat.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        public void Trigger(ushort type, float value)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionFloat)
            {
                NetworkActionFloat naction = (NetworkActionFloat)action;
                naction.TriggerAction(this, value);
            }
        }

        //---- NetworkActionString ---------

        public void RegisterString(ushort type, UnityAction<string> callback,
            NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionString action = NetworkActionString.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        public void Trigger(ushort type, string value)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionString)
            {
                NetworkActionString naction = (NetworkActionString)action;
                naction.TriggerAction(this, value);
            }
        }

        //---- NetworkActionBytes ---------

        public void RegisterBytes(ushort type, UnityAction<byte[]> callback,
            NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionBytes action = NetworkActionBytes.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        public void Trigger(ushort type, byte[] value)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionBytes)
            {
                NetworkActionBytes naction = (NetworkActionBytes)action;
                naction.TriggerAction(this, value);
            }
        }

        //---- NetworkActionVector ---------

        //Define which function to call when action is triggered
        public void RegisterVector(ushort type, UnityAction<Vector3> callback, 
            NetworkDelivery delivery = NetworkDelivery.Reliable, 
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionVector action = NetworkActionVector.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        //Trigger an action on both client and server (or on only the target if target is not set to All)
        public void Trigger(ushort type, Vector3 pos)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionVector)
            {
                NetworkActionVector naction = (NetworkActionVector)action;
                naction.TriggerAction(this, pos);
            }
        }

        //----- NetworkActionObject ----------

        //Define which function to call when action is triggered
        public void RegisterObject(ushort type, UnityAction<SNetworkObject> callback, 
            NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionObject action = NetworkActionObject.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        //Trigger an action on both client and server (or on only the target if target is not set to All)
        public void Trigger(ushort type, SNetworkObject nobj)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionObject)
            {
                NetworkActionObject naction = (NetworkActionObject)action;
                naction.TriggerAction(this, nobj);
            }
        }

        //----- NetworkActionBehaviour ----------

        //Define which function to call when action is triggered
        public void RegisterBehaviour(ushort type, UnityAction<SNetworkBehaviour> callback, 
            NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionBehaviour action = NetworkActionBehaviour.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        //Trigger an action on both client and server (or on only the target if target is not set to All)
        public void Trigger(ushort type, SNetworkBehaviour nobj)
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionBehaviour)
            {
                NetworkActionBehaviour naction = (NetworkActionBehaviour)action;
                naction.TriggerAction(this, nobj);
            }
        }

        //---Serializable

        //Use to send any kind of data (INetworkSerializable)
        //Important to use ReliableFragmentedSequenced for big data, or the data won't fit in a single request
        public void RegisterSerializable(ushort type, UnityAction<SerializedData> callback, 
            NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            NetworkActionSerializable action = NetworkActionSerializable.Create();
            action.type = type;
            action.target = target;
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        //Trigger an action on both client and server (or on only the target if target is not set to All)
        public void Trigger<T>(ushort type, T data) where T : INetworkSerializable
        {
            NetworkAction action = GetAction(type);
            if (action != null && action is NetworkActionSerializable)
            {
                NetworkActionSerializable naction = (NetworkActionSerializable)action;
                naction.TriggerAction(this, data);
            }
        }

        //---Refresh

        //A refresh is always serialized data that is sent from server to the clients (same as RegisterSerializable with Client target)
        //Important to use ReliableFragmentedSequenced for big data refresh, or the data won't fit in a single request
        public void RegisterRefresh(ushort type, UnityAction<SerializedData> callback,
            NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            NetworkActionSerializable action = NetworkActionSerializable.Create();
            action.type = type;
            action.target = NetworkActionTarget.Clients; //Refresh only send to client
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        //Same as Trigger but with additional check if its server
        public void Refresh<T>(ushort type, T data) where T : INetworkSerializable
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server can send refresh
            Trigger(type, data);
        }

        public void RegisterRefresh(ushort type, UnityAction<byte[]> callback,
            NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            NetworkActionBytes action = NetworkActionBytes.Create();
            action.type = type;
            action.target = NetworkActionTarget.Clients; //Refresh only send to client
            action.delivery = delivery;
            action.callback = callback;
            action.ignore_authority = false;
            actions_list.Add(action);
        }

        //Same as Trigger but with additional check if its server
        public void Refresh(ushort type, byte[] data)
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server can send refresh
            Trigger(type, data);
        }

        //------ Unregister -----

        public void Unregister(ushort type)
        {
            NetworkAction to_remove = null;
            foreach (NetworkAction action in actions_list)
            {
                if (action.type == type)
                    to_remove = action;
            }

            if (to_remove != null)
            {
                to_remove.Dispose();
                actions_list.Remove(to_remove);
            }
        }

        public void Unregister(string type)
        {
            Unregister(NetworkTool.Hash16(type));
        }

        public void DisposeAll()
        {
            foreach (NetworkAction action in actions_list)
                action.Dispose();
        }

        //---- String Shortcuts ----

        public void Register(string type, UnityAction callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            Register(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterInt(string type, UnityAction<int> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterInt(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterFloat(string type, UnityAction<float> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterFloat(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterString(string type, UnityAction<string> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterString(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterBytes(string type, UnityAction<byte[]> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterBytes(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterVector(string type, UnityAction<Vector3> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterVector(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterObject(string type, UnityAction<SNetworkObject> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterObject(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterBehaviour(string type, UnityAction<SNetworkBehaviour> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterBehaviour(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterSerializable(string type, UnityAction<SerializedData> callback, NetworkDelivery delivery = NetworkDelivery.Reliable,
            NetworkActionTarget target = NetworkActionTarget.All)
        {
            RegisterSerializable(NetworkTool.Hash16(type), callback, delivery, target);
        }

        public void RegisterRefresh(string type, UnityAction<SerializedData> callback, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            RegisterRefresh(NetworkTool.Hash16(type), callback, delivery);
        }

        public void RegisterRefresh(string type, UnityAction<byte[]> callback, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            RegisterRefresh(NetworkTool.Hash16(type), callback, delivery);
        }

        public void Trigger(string type)
        {
            Trigger(NetworkTool.Hash16(type));
        }

        public void Trigger(string type, int val)
        {
            Trigger(NetworkTool.Hash16(type), val);
        }

        public void Trigger(string type, float val)
        {
            Trigger(NetworkTool.Hash16(type), val);
        }

        public void Trigger(string type, string val)
        {
            Trigger(NetworkTool.Hash16(type), val);
        }

        public void Trigger(string type, byte[] val)
        {
            Trigger(NetworkTool.Hash16(type), val);
        }

        public void Trigger(string type, Vector3 val)
        {
            Trigger(NetworkTool.Hash16(type), val);
        }

        public void Trigger(string type, SNetworkObject obj)
        {
            Trigger(NetworkTool.Hash16(type), obj);
        }

        public void Trigger(string type, SNetworkBehaviour beha)
        {
            Trigger(NetworkTool.Hash16(type), beha);
        }

        public void Trigger<D>(string type, D data) where D : INetworkSerializable
        {
            Trigger(NetworkTool.Hash16(type), data);
        }

        public void Refresh<D>(string type, D data) where D : INetworkSerializable
        {
            Refresh(NetworkTool.Hash16(type), data);
        }

        public void Refresh(string type, byte[] data)
        {
            Refresh(NetworkTool.Hash16(type), data);
        }

        //---------------

        public NetworkAction GetAction(uint type)
        {
            foreach (NetworkAction action in actions_list)
            {
                if (action.type == type)
                    return action;
            }
            return null;
        }

        public bool IsLinked()
        {
            return is_linked;
        }

        public SNetworkObject NetObject { get { return netbehaviour != null ? netbehaviour.NetObject : null; } }
        public SNetworkBehaviour NetBehaviour { get { return netbehaviour; } }

        public ulong NetworkId { get { return netbehaviour != null ? netbehaviour.NetworkId : custom_net_id; } }
        public ushort BehaviourId { get { return netbehaviour != null ? netbehaviour.BehaviourId : (ushort)0; } }

        public bool IsConnected { get { return TheNetwork.Get().IsConnected(); } }
        public bool IsOnline { get { return TheNetwork.Get().IsOnline; } }
        public NetworkMessaging Messaging { get { return TheNetwork.Get().Messaging; } }

        public static SNetworkActions GetHandler(ulong net_id, uint behaviour_id)
        {
            if (handlers.ContainsKey(net_id))
            {
                List<SNetworkActions> list = handlers[net_id];
                foreach (SNetworkActions handler in list)
                {
                    if (handler.NetworkId == net_id
                        && handler.BehaviourId == behaviour_id)
                        return handler;
                }
            }
            return null;
        }

        public static void UnlinkAll()
        {
            handlers.Clear();
        }

        public static void ClearAll()
        {
            foreach (KeyValuePair<ulong, List<SNetworkActions>> item in handlers)
            {
                if (item.Value != null)
                {
                    foreach (SNetworkActions handler in item.Value)
                    {
                        handler.DisposeAll();
                        handler.actions_list.Clear();
                    }
                }
            }
            handlers.Clear();
        }
    }
}
