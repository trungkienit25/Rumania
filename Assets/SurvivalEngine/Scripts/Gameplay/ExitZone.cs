using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine
{
    /// <summary>
    /// Zone to change scene when you enter this zone, make sure there is also a trigger collider
    /// </summary>

    public class ExitZone : MonoBehaviour
    {
        [Header("Exit")]
        public string scene;                //Leave empty to teleport inside the scene instead of changing scene
        public string go_to_entry;

        [Header("Entrance")]
        public string entry;
        public Vector3 entry_offset;

        private float timer = 0f;

        private static List<ExitZone> exit_list = new List<ExitZone>();

        void Awake()
        {
            exit_list.Add(this);
        }

        private void OnDestroy()
        {
            exit_list.Remove(this);
        }

        void Update()
        {
            if (!TheNetwork.Get().IsReady())
                return;

            timer += Time.deltaTime;
        }

        public void EnterZone(PlayerCharacter character)
        {
            if (!TheNetwork.Get().IsServer)
                return; //Only server

            ResetTimer();

            if (string.IsNullOrEmpty(scene) || scene == SceneNav.GetCurrentScene())
            {
                ExitZone zone = Get(go_to_entry);
                TheGame.Get().TeleportToZone(character, zone);
            }
            else
            {
                TheGame.Get().TransitionToScene(scene, go_to_entry);
            }
        }

        public void ResetTimer()
        {
            timer = 0f;
        }

        public Vector3 GetSpawnPos()
        {
            return transform.position + entry_offset;
        }

        public Vector3 GetRandomPosition(float range = 2f)
        {
            if (range > 0.01f)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float rad = Random.Range(0f, range);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rad;
                return GetSpawnPos() + offset;
            }
            return GetSpawnPos();
        }

        private void OnTriggerEnter(Collider collision)
        {
            PlayerCharacter character = collision.GetComponent<PlayerCharacter>();
            if (timer > 0.1f && character != null)
            {
                EnterZone(character);
            }
        }

        public static ExitZone Get(string id)
        {
            foreach (ExitZone exit in exit_list)
            {
                if (id == exit.entry)
                    return exit;
            }
            return null;
        }
    }
}
