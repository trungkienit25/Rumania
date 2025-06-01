using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NetcodePlus;

namespace SurvivalEngine
{

    public class PlayerName : MonoBehaviour
    {
        public Text name_text;

        private PlayerCharacter character;

        void Start()
        {
            character = GetComponentInParent<PlayerCharacter>();
            character.NetObject.onSpawn += onSpawn;
            name_text.text = "";
            if (character.NetObject.IsSpawned)
                onSpawn();
        }

        void onSpawn()
        {
            int pid = character.GetSpawnDataInt32();
            PlayerData pdata = WorldData.Get().GetPlayer(pid);
            if(pdata != null)
                name_text.text = pdata.username;
        }

        private void Update()
        {
            TheCamera cam = TheCamera.Get();
            Vector3 dir = cam.transform.position - transform.position;
            transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }
    }
}
