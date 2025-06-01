using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Object will be spawned/despawned based on distance to SNetworkPlayer
    /// </summary>

    [RequireComponent(typeof(SNetworkObject))]
    public class SNetworkOptimizer : MonoBehaviour
    {
        [Header("Optimization")]
        public float active_range = 50f; //If farther than this, will be disabled for optim
        public bool always_run_scripts = false; //Set to true to have other scripts still run when this one is not active
        public bool turn_off_gameobject = false; //Set to true if you want the gameobject to be SetActive(false) when far away

        private SNetworkObject nobj;
        private Transform transf;
        private bool is_active = true;

        private List<MonoBehaviour> scripts = new List<MonoBehaviour>();
        private List<Animator> animators = new List<Animator>();

        private static LinkedList<SNetworkOptimizer> opt_list = new LinkedList<SNetworkOptimizer>();
        private LinkedListNode<SNetworkOptimizer> node; //Reference to node so that Remove() function is O(1)

        void Awake()
        {
            node = opt_list.AddLast(this);
            transf = transform;
            nobj = GetComponent<SNetworkObject>();
            scripts.AddRange(GetComponentsInChildren<MonoBehaviour>(true));
            animators.AddRange(GetComponentsInChildren<Animator>(true));
        }

        void OnDestroy()
        {
			if (node.List != null)
				opt_list.Remove(node);
        }

        public void SetActive(bool visible)
        {
            if (is_active != visible)
            {
                is_active = visible;

                if (!always_run_scripts && !turn_off_gameobject)
                {
                    foreach (MonoBehaviour script in scripts)
                    {
                        if (script != null && script != this && script != nobj)
                            script.enabled = visible;
                    }

                    foreach (Animator anim in animators)
                    {
                        if (anim != null)
                            anim.enabled = visible;
                    }
                }

                if (visible)
                    NetObject.Spawn();
                else
                    NetObject.Despawn();

                if (turn_off_gameobject)
                    gameObject.SetActive(visible);
            }
        }

        public Vector3 GetPos()
        {
            return transf.position;
        }

        public bool IsActive()
        {
            return is_active;
        }

        public SNetworkObject NetObject { get { return nobj; } }

        public static void ClearAll()
        {
            opt_list.Clear();
        }

        public static LinkedList<SNetworkOptimizer> GetAll()
        {
            return opt_list;
        }
    }
}
