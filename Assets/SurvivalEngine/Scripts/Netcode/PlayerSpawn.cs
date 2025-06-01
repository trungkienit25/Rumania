using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetcodePlus
{
    public class PlayerSpawn : MonoBehaviour
    {
        public string id;
        public int player_id;
        public float radius = 1f;

        private static List<PlayerSpawn> list = new List<PlayerSpawn>();

        void Awake()
        {
            list.Add(this);
        }

        private void OnDestroy()
        {
            list.Remove(this);
        }

        public Vector3 GetRandomPosition() 
        {
            if (radius > 0.01f)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float rad = Random.Range(0f, radius);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rad;
                return transform.position + offset;
            }
            return transform.position;
        }

        public static PlayerSpawn GetNearest(Vector3 pos, float range = 999f)
        {
            PlayerSpawn nearest = null;
            float min_dist = range;
            foreach (PlayerSpawn spawn in list)
            {
                float dist = (spawn.transform.position - pos).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = spawn;
                }
            }
            return nearest;
        }

        public static PlayerSpawn GetNearest(Vector3 pos, string id, float range = 999f)
        {
            PlayerSpawn nearest = null;
            float min_dist = range;
            foreach (PlayerSpawn spawn in list)
            {
                if (spawn.id == id)
                {
                    float dist = (spawn.transform.position - pos).magnitude;
                    if (dist < min_dist)
                    {
                        min_dist = dist;
                        nearest = spawn;
                    }
                }
            }
            return nearest;
        }

        public static PlayerSpawn Get(int player_id)
        {
            foreach (PlayerSpawn spawn in list)
            {
                if (spawn.player_id == player_id)
                    return spawn;
            }
            return null;
        }

        public static PlayerSpawn Get(string id = "")
        {
            foreach (PlayerSpawn spawn in list)
            {
                if (spawn.id == id)
                    return spawn;
            }
            return null; 
        }

        public static List<PlayerSpawn> GetAll()
        {
            return list;
        }
    }
}
