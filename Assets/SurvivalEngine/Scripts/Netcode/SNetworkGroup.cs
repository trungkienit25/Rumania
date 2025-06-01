using UnityEngine;

namespace NetcodePlus
{
    /// <summary>
    /// Group of SNetworkObject, use to Spawn() Despawn() and Destroy() in one call
    /// </summary>

    public class SNetworkGroup : MonoBehaviour
    {
        private SNetworkObject[] object_list;

        protected virtual void Awake()
        {
            object_list = GetComponentsInChildren<SNetworkObject>();
        }

        public virtual void Spawn()
        {
            foreach (SNetworkObject obj in object_list)
                obj.Spawn();
        }

        public virtual void Spawn(ulong owner)
        {
            foreach (SNetworkObject obj in object_list)
                obj.Spawn(owner);
        }

        public virtual void Despawn(bool destroy = false)
        {
            foreach (SNetworkObject obj in object_list)
                obj.Despawn(destroy);
        }

        public virtual void Destroy(float delay = 0f)
        {
            foreach (SNetworkObject obj in object_list)
                obj.Destroy(delay);
        }

        public virtual void ChangeOwner(ulong owner)
        {
            foreach (SNetworkObject obj in object_list)
                obj.ChangeOwner(owner);
        }
    }
}
