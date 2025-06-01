using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

namespace NetcodePlus
{
    public enum NetworkActionTarget
    {
        All = 0, //Action will be executed on all clients and server
        Clients = 10, //Action will be executed on all clients only (not the host)
        Server = 20, //Action will be executed on server only
        ServerAndSelf = 30, //Action will be executed both locally and on the server (but will not be forwarded to other clients)
        Single = 40, //Single Target, use SetTarget(client_id) to choose the target before triggering, must be triggered from server if targeting a client other than self
        
        //All actions, no matter the target, can only be triggered by either the owner or the server
        //If an action is triggered by a non-owner non-server, it may still be executed locally (if target matches) but not remotely
    }

    public class NetworkAction 
    {
        public ushort type;                //Each action within a SNetworkBehaviour, should have a unique type
        public NetworkActionTarget target; //Target is who will execute the action (not who can trigger it)
        public NetworkDelivery delivery; //Unreliable mode (UDP) or Reliable mode (TCP), or ReliableFragmentedSequenced (Big Request)
        public ulong single_target; //when target is set to Single, will send to this target
        public bool ignore_authority = false; //If true, anyone can trigger this action, regardless of owner

        //Action can only be executed remotely if the one who call Trigger() has authority
        public bool HasAuthority(SNetworkBehaviour obj, ulong client_id) {
            bool owner = obj != null && client_id == obj.OwnerId;
            bool server = client_id == TheNetwork.Get().ServerID;
            return owner || server || ignore_authority;
        }

        public bool IsServer() { return TheNetwork.Get().IsServer; }
        public bool IsHost() { return TheNetwork.Get().IsHost; }
        public bool IsTargetSelf() { return target == NetworkActionTarget.ServerAndSelf; }
        public bool IsTargetClients() { return target == NetworkActionTarget.Clients || target == NetworkActionTarget.All; }
        public bool IsTargetServer() { return target == NetworkActionTarget.Server || target == NetworkActionTarget.ServerAndSelf || target == NetworkActionTarget.All; }
        public bool IsTargetSingle() { return target == NetworkActionTarget.Single; }

        public bool ShouldSendClients() { return IsTargetClients() && IsServer(); }
        public bool ShouldSendServer() { return IsTargetServer() && !IsServer(); }
        public bool ShouldSendSingle() { return IsTargetSingle() && single_target != TheNetwork.Get().ClientID; }

        public bool ShouldRun() { 
            return IsTargetSelf()
                || (IsTargetServer() && IsServer()) 
                || (IsTargetClients() && !IsServer()) 
                || (IsTargetSingle() && single_target == TheNetwork.Get().ClientID); 
        }

        public void SendToTarget<T>(SNetworkActions handler, T data) where T : INetworkSerializable
        {
            if (handler.NetObject != null && !handler.NetObject.IsSpawned)
                return; //Don't send unspawned, target won't know which object it is

            if (handler.NetworkId == 0)
                return; //Not initialized yet, dont send

            if (!HasAuthority(handler.NetBehaviour, TheNetwork.Get().ClientID))
                return; //Doesn't have authority, will be rejected by server, save on bandwidth

            if (IsServer() && handler.Messaging.ClientCount == 0)
                return; //No clients to send

            if (IsHost() && handler.Messaging.ClientCount <= 1)
                return; //No clients to send

            if (ShouldSendServer())
                handler.SendActionServer(type, data, delivery);
            else if (ShouldSendClients())
                handler.SendActionClients(type, data, delivery);
            else if (ShouldSendSingle())
                handler.SendActionTarget(type, single_target, data, delivery);
        }

        public virtual void RunAction(FastBufferReader reader)
        {
            //This will be called on all targets when NetworkActionHandler calls Trigger(...)
            //Override this
        }

        public virtual void Dispose()
        {
            //Overwrite to dispose from pool
        }

        public static void ClearAll()
        {
            NetworkActionBehaviour.Clear();
            NetworkActionBytes.Clear();
            NetworkActionFloat.Clear();
            NetworkActionInt.Clear();
            NetworkActionObject.Clear();
            NetworkActionSerializable.Clear();
            NetworkActionSimple.Clear();
            NetworkActionString.Clear();
            NetworkActionVector.Clear();
        }
    }
}
