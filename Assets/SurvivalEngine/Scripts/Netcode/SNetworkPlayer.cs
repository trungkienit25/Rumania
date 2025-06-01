using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Base script for player character script, stores player_id and used by SNetworkOptimizer to know which objects are in player range
    /// </summary>

    [RequireComponent(typeof(SNetworkObject))]
    public class SNetworkPlayer : SNetworkBehaviour
    {
        protected int player_id;
        protected Transform transf;

        protected static List<SNetworkPlayer> player_list = new List<SNetworkPlayer>();

        protected override void Awake()
        {
            base.Awake();
            player_list.Add(this);
            transf = transform;
        }

        protected virtual void OnDestroy()
        {
            player_list.Remove(this);
        }

        protected override void OnBeforeSpawn()
        {
            ClientData client = TheNetwork.Get().GetClient(OwnerId);
            SetSpawnData(client.player_id); //Send player ID with the spawn
        }

        protected override void OnSpawn()
        {
            player_id = GetSpawnDataInt32();
        }

        public Vector3 GetPos()
        {
            return transf.position;
        }

        public int PlayerID { get { return player_id; } }

        public static SNetworkPlayer GetSelf()
        {
            return Get(TheNetwork.Get().PlayerID);
        }

        public static SNetworkPlayer Get(int player_id)
        {
            foreach (SNetworkPlayer player in player_list)
            {
                if (player.player_id == player_id)
                {
                    return player;
                }
            }
            return null;
        }

        public static SNetworkPlayer GetNearest(Vector3 pos, float range = 999f)
        {
            SNetworkPlayer nearest = null;
            float min_dist = range;
            foreach (SNetworkPlayer player in player_list)
            {
                float dist = (player.transform.position - pos).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = player;
                }
            }
            return nearest;
        }

        public static List<SNetworkPlayer> GetAll()
        {
            return player_list;
        }
    }
}
