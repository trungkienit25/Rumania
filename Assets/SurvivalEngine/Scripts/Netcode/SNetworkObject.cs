using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Events;
using Unity.Collections;

namespace NetcodePlus
{
    /// <summary>
    /// Improved version of NetworkObject
    /// Use only with SNetworkBehaviour, do not add both NetworkObject and SNetworkObject on the same object
    /// </summary>

    public class SNetworkObject : MonoBehaviour
    {
        public AutoSpawnType auto_spawn;        //Object will be spawned automatically when client is Ready
        [ReadOnly] public ulong network_id = 0;            //ID to access the instantiated object, this id should match on all clients/server
        [ReadOnly] public ulong prefab_id = 0;             //ID to access the prefab of this object, this id should match on all clients/server
        [ReadOnly] public bool is_scene;                   //Object was placed in the scene and not instantiated, this means it already exists on both client and server

        [ReadOnly] [SerializeField]
        private bool is_spawned = false;        //If the object is spawned or not (spawn means it has been sent to clients for syncing)

        public UnityAction onReady;             //Called after connection was fully established (and all data loaded)
        public UnityAction onBeforeSpawn;       //Called before the object is spawned (on the server only)
        public UnityAction onSpawn;             //Called when this object is spawned (means that the syncing with clients starts)
        public UnityAction onDespawn;           //Called when this object is despawned (means that the syncing with clients stop)
        
        private ulong owner = 0;                //Owner of this object, 0 is usually the server
        private bool is_ready = false;          //If the object is ready (connection was extablished and OnReady was called)
        private bool is_destroying = false;     //Object is being destroyed, prevent spawning

        private SNetworkOptimizer optimizer;    //Can be null, will automatically spawn/despawn this object based on distance to players
        private SNetworkBehaviour[] behaviours; //List of behaviours
        private bool registered = false;        //Prevent registering twice
        private List<SNetworkBehaviour> temp_behaviours = new List<SNetworkBehaviour>(); //Optimization to reuse memory

        private static LinkedList<SNetworkObject> net_objects = new LinkedList<SNetworkObject>();
        private LinkedListNode<SNetworkObject> node; //Reference to node so that Remove() function is O(1)

        public static bool editor_auto_gen_id = true; //Set to off to turn off auto id generation

        protected virtual void Awake()
        {
            node = net_objects.AddLast(this);
            optimizer = GetComponent<SNetworkOptimizer>();
            behaviours = GetComponentsInChildren<SNetworkBehaviour>();

            //Set behaviours id
            for (ushort i = 0; i < behaviours.Length; i++)
                behaviours[i].SetBehaviourId(i);

            if (!is_scene)
                GenerateID(); //Generate ID for newly created objects
            if(network_id == 0)
                Debug.Log("Network ID is 0 " + gameObject.name);
        }

        protected virtual void Start()
        {
            RegisterScene();
            TriggerReady();

            if (is_scene && !is_spawned)
                SetActive(false); //If it has Optimizer, hide the object until in range of player
        }

        public virtual void OnDestroy()
        {
            is_destroying = true;
			if (node.List != null)
				net_objects.Remove(node);
        }

        private void OnValidate()
        {
            if (editor_auto_gen_id)
                GenerateEditorID();
        }

        public void GenerateID()
        {
            network_id = NetworkTool.GenerateRandomUInt64();
        }
        
        public void GenerateEditorID()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return; //Ignore in play mode

            UnityEditor.GlobalObjectId gobjid = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            if (gobjid.targetObjectId == 0 || gobjid.assetGUID.Empty())
                return; //Invalid id, ignore for now

            ulong prev_nid = network_id;
            ulong prev_pid = prefab_id;
            bool pscene = is_scene;

            ulong id = NetworkTool.Hash64(gobjid.ToString());
            network_id = id;
            prefab_id = id;

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            is_scene = gameObject.scene.rootCount > 0 && gameObject.scene == scene;

            if (is_scene)
            {
                GameObject prefab_parent = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                SNetworkObject nprefab = prefab_parent != null ? prefab_parent.GetComponent<SNetworkObject>() : null;
                prefab_id = nprefab != null ? nprefab.prefab_id : 0;          //Load ID from parent prefab
            }

            if (prefab_id != prev_pid || network_id != prev_nid || is_scene != pscene)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public virtual void Spawn()
        {
            Spawn(TheNetwork.Get().ServerID);
        }

        public virtual void Spawn(ulong owner)
        {
            if (is_destroying)
                return; //Object already being destroyed

            if ((network_id == 0 || network_id == prefab_id) && IsServer)
                GenerateID();

            if (IsServer && !IsSpawned)
            {
                //Debug.Log("Spawn " +gameObject.name);
                this.owner = owner;
                is_spawned = true;
                onBeforeSpawn?.Invoke();
                NetworkSpawner.Get().Spawn(this);
                onSpawn?.Invoke();
            }
        }

        public virtual void Despawn(bool destroy = false)
        {
            if (is_destroying)
                return; //Object being destroyed

            if (IsServer && IsSpawned)
            {
                //Debug.Log("Despawn " + gameObject.name);
                is_spawned = false;
                is_destroying = destroy;
                NetworkSpawner.Get().Despawn(this, destroy);
                onDespawn?.Invoke();

                if (destroy)
                    GameObject.Destroy(gameObject);
            }
        }

        public virtual void DestroyUnspawned()
        {
            if (!IsSpawned && !is_destroying)
            {
                is_destroying = true;
                NetworkSpawner.Get().DestroyScene(this);
                GameObject.Destroy(gameObject);
            }
        }

        public virtual void Destroy()
        {
            if (is_destroying)
                return; //Object already being destroyed

            if (IsServer && IsSpawned)
                Despawn(true);
            else if (IsSpawned)
                DespawnLocal(true);
            else
                DestroyUnspawned();
        }

        public virtual void Destroy(float delay)
        {
            if (delay > 0.01f)
                TimeTool.WaitFor(delay, Destroy);
            else
                Destroy();
        }

        public void AutoSpawn()
        {
            if (IsServer && !IsSpawned && IsActive())
            {
                bool should_spawn = auto_spawn == AutoSpawnType.Always
                    || (is_scene && auto_spawn == AutoSpawnType.SceneOnly);

                if (should_spawn)
                    Spawn();
            }
        }

        public void SpawnLocal(ulong owner, byte[] extra)
        {
			if (is_destroying)
                return; //Object being destroyed
			
            if (!is_spawned)
            {
                is_spawned = true;
                this.owner = owner;
                ReadBehaviorSpawnData(extra);
                SetActive(true);
                onSpawn?.Invoke();
            }
        }

        public void DespawnLocal(bool destroy = false)
        {
			if (is_destroying)
                return; //Object being destroyed
			
            if (is_spawned)
            {
                is_spawned = false;
                SetActive(false);
                onDespawn?.Invoke();
            }
			
			is_destroying = destroy;
			if (destroy)
                GameObject.Destroy(gameObject);
        }

        public void ChangeOwner(ulong owner)
        {
            if (IsSpawned && this.owner != owner)
            {
                this.owner = owner;
                NetworkSpawner.Get().ChangeOwner(this);
            }
        }

        public void SetActive(bool active)
        {
            if (optimizer != null)
                optimizer.SetActive(active);
        }

        public void SetOptimizerEnabled(bool enable)
        {
            if (optimizer != null)
                optimizer.enabled = enable;
        }

        public void RegisterScene()
        {
            if (is_scene && !registered)
            {
                registered = true;
                NetworkGame.Get().Spawner.RegisterSceneObject(this);
            }
        }

        public void TriggerReady()
        {
            if (!is_ready && TheNetwork.Get().IsReady())
            {
                is_ready = true;
                onReady?.Invoke();
                AutoSpawn();
            }
        }

        public byte[] WriteBehaviorSpawnData()
        {
            //Check if any behavior have spawn data
            temp_behaviours.Clear();
            foreach (SNetworkBehaviour beha in behaviours)
            {
                if (beha.GetSpawnData().Length > 0)
                    temp_behaviours.Add(beha);
            }

            if (temp_behaviours.Count == 0)
                return new byte[0];

            //Write the ones with data
            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp, TheNetwork.MsgSize);
            writer.WriteValueSafe((ushort)temp_behaviours.Count);
            foreach (SNetworkBehaviour beha in temp_behaviours)
            {
                byte[] bextra = beha.GetSpawnData();
                writer.TryBeginWrite(4 + bextra.Length);
                writer.WriteValue(beha.BehaviourId);
                writer.WriteValue((ushort)bextra.Length);
                writer.WriteBytes(bextra);
            }
            byte[] wextra = writer.ToArray();
            writer.Dispose();
            return wextra;
        }

        public void ReadBehaviorSpawnData(byte[] bextra)
        {
            if (bextra.Length == 0)
                return;

            FastBufferReader reader = new FastBufferReader(bextra, Allocator.Temp);
            reader.ReadValueSafe(out ushort count);
            for(int i=0; i<count; i++)
            {
                reader.TryBeginRead(4);
                reader.ReadValue(out ushort beha_id);
                reader.ReadValue(out ushort byte_length);
                byte[] beha_extra = new byte[byte_length];
                reader.ReadBytesSafe(ref beha_extra, byte_length);

                if (beha_id >= 0 && beha_id < behaviours.Length)
                    behaviours[beha_id].SetSpawnData(beha_extra);
            }
            reader.Dispose();
        }

        public SNetworkBehaviour GetBehaviour(ushort id)
        {
            foreach (SNetworkBehaviour beha in behaviours)
            {
                if (beha.BehaviourId == id)
                    return beha;
            }
            return null;
        }

        public float GetPlayerRange(float range_max = 99999f)
        {
            //Useful for optimization purpose, check how far the nearest player is
            SNetworkPlayer player = SNetworkPlayer.GetNearest(transform.position, range_max);
            if (player != null)
                return (transform.position - player.transform.position).magnitude;
            return range_max;
        }

        public float GetActiveRange()
        {
            if (optimizer != null)
                return optimizer.active_range;
            return 99999f;
        }

        public float GetRangePercent()
        {
            float arange = GetActiveRange();
            float prange = GetPlayerRange(arange);
            return Mathf.Clamp01(prange / arange); //Return from 0 to 1, represent how far from player 
        }

        public bool IsActive()
        {
            if(optimizer != null)
                return optimizer.IsActive() && gameObject.activeSelf;
            return gameObject.activeSelf;
        }

        public ulong NetworkId { get { return network_id; } }
        public ulong PrefabId { get { return prefab_id; } }
        public ulong OwnerId { get { return owner; } }

        public bool IsServer { get { return TheNetwork.Get().IsServer; } }
        public bool IsClient { get { return TheNetwork.Get().IsClient; } }
        public bool IsOwner { get { return TheNetwork.Get().ClientID == owner; } }

        public bool IsSpawned { get { return is_spawned; } }
        public bool IsReady { get { return is_ready; } }
        public bool IsSceneObject { get { return is_scene; } }
        public bool IsDestroyed { get { return is_destroying; } }

        public static LinkedList<SNetworkObject> GetAll()
        {
            return net_objects;
        }

        public static void ClearAll()
        {
            net_objects.Clear();
        }

        public static void GenerateAllInScene()
        {
#if UNITY_EDITOR
            GenerateAll(FindObjectsOfType<SNetworkObject>());
#endif
        }

        public static void GenerateAll(SNetworkObject[] objs)
        {
#if UNITY_EDITOR
			if (!Application.isPlaying)
            {
				editor_auto_gen_id = false;
				foreach (SNetworkObject nobj in objs)
					nobj.GenerateEditorID();
				editor_auto_gen_id = true;
			}
#endif
        }

        public static void SpawnAll(SNetworkObject[] objs)
        {
            foreach (SNetworkObject nobj in objs)
                nobj.Spawn();
        }

        public static SNetworkObject Create(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (!TheNetwork.Get().IsServer)
                return null;

            GameObject obj = Instantiate(prefab, pos, rot);
            SNetworkObject nobj = obj.GetComponent<SNetworkObject>();
            nobj?.Spawn();
            return nobj;
        }
    }

    [System.Serializable]
    public struct SNetworkObjectRef : INetworkSerializable
    {
        public ulong net_id;

        public SNetworkObjectRef(SNetworkObject behaviour)
        {
            if (behaviour != null)
                net_id = behaviour.NetworkId;
            else
                net_id = 0;
        }

        public SNetworkObject Get()
        {
            return NetworkSpawner.Get().GetSpawnedObject(net_id);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref net_id);
        }
    }

    public enum AutoSpawnType
    {
        SceneOnly = 0,  //Default, only scene object will be auto spawned, script Instantiated object need to call Spawn()
        Always = 10,    //All object will be autospawned in Start() or when the client is ready
        Never = 20,     //Never auto spawn, Spawn() must be called for each object, even the ones in the scene
    }
}
