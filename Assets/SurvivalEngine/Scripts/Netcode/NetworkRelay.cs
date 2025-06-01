using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;

namespace NetcodePlus
{

    public class NetworkRelay
    {
        public static async Task<RelayConnectData> HostGame(int maxConn)
        {
            if (!Authenticator.Get().IsSignedIn())
                return null; //Can't use relay if not logged in

            //If auth system is NOT unity services, need to login anymously to unity services
            if (!Authenticator.Get().IsUnityServices())
                await LoginUnity();

            Allocation allocation;
            try
            {
                //Ask Unity Services to allocate a Relay server
                allocation = await RelayService.Instance.CreateAllocationAsync(maxConn);
            }
            catch (Exception e)
            {
                Debug.Log("Create Allocation Error: " + e);
                return null;
            }

            RelayServerEndpoint endpoint = GetEndpoint(allocation.ServerEndpoints, "udp");
            RelayConnectData data = new RelayConnectData
            {
                url = endpoint.Host,
                port = (ushort)endpoint.Port,
                alloc_id = allocation.AllocationIdBytes,
                alloc_key = allocation.Key,
                connect_data = allocation.ConnectionData,
            };

            try
            {
                //Retrieve the Relay join code for our clients to join our party
                data.join_code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                return data;
            }
            catch (Exception e)
            {
                Debug.Log("Get Join Code Error: " + e);
                return null;
            }
        }

        public static async Task<RelayConnectData> JoinGame(string joinCode)
        {
            if (!Authenticator.Get().IsSignedIn())
                return null; //Can't use relay if not logged in

            //If auth system is NOT unity services, need to login anymously to unity services
            if (!Authenticator.Get().IsUnityServices())
                await LoginUnity();

            //Ask Unity Services for allocation data based on a join code
            JoinAllocation allocation;
            try
            {
                allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            }
            catch (Exception e)
            {
                Debug.Log("Join Allocation Error: " + e);
                return null;
            }

            RelayServerEndpoint endpoint = GetEndpoint(allocation.ServerEndpoints, "udp");
            RelayConnectData data = new RelayConnectData
            {
                url = endpoint.Host,
                port = (ushort)endpoint.Port,
                alloc_id = allocation.AllocationIdBytes,
                alloc_key = allocation.Key,
                connect_data = allocation.ConnectionData,
                host_connect_data = allocation.HostConnectionData,
                join_code = joinCode,
            };
            return data;
        }

        //Force login to Unity Services if the engine auth system isn't already Unity Services
        private static async Task<bool> LoginUnity()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsAuthorized && NetworkData.Get().auth_auto_logout)
                    AuthenticationService.Instance.ClearSessionToken();

                if (!AuthenticationService.Instance.IsAuthorized)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (AuthenticationException ex) { Debug.LogException(ex); }
            catch (RequestFailedException ex) { Debug.LogException(ex); }
            return AuthenticationService.Instance.IsAuthorized;
        }

        private static RelayServerEndpoint GetEndpoint(List<RelayServerEndpoint> list, string type = "udp")
        {
            foreach (RelayServerEndpoint end in list)
            {
                if (end.ConnectionType == type)
                    return end;
            }
            return null;
        }
    }

    public class RelayConnectData
    {
        public string url;
        public ushort port;
        public byte[] alloc_id;
        public byte[] alloc_key;
        public byte[] connect_data;
        public byte[] host_connect_data;
        public string join_code;
    }

}