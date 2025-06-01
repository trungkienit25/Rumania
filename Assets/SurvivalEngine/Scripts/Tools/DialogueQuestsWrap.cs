using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

#if DIALOGUE_QUESTS
using DialogueQuests;
#endif

namespace SurvivalEngine
{
    /// <summary>
    /// Wrapper class for integrating DialogueQuests
    /// </summary>

    public class DialogueQuestsWrap : MonoBehaviour
    {

#if DIALOGUE_QUESTS

        private HashSet<PlayerCharacter> inited_players = new HashSet<PlayerCharacter>();
        private HashSet<Actor> inited_actors = new HashSet<Actor>();
        private float timer = 1f;

        static DialogueQuestsWrap()
        {
            NarrativeData.getData += GetData;
            NarrativeData.getDataAll += GetDataAll;
            NarrativeData.overrideData += OverrideData;
        }

        void Awake()
        {
            NarrativeManager narrative = FindObjectOfType<NarrativeManager>();
            if (narrative != null)
            {
                narrative.onPauseGameplay += OnPauseGameplay;
                narrative.onUnpauseGameplay += OnUnpauseGameplay;
                narrative.onPlaySFX += OnPlaySFX;
                narrative.onPlayMusic += OnPlayMusic;
                narrative.onStopMusic += OnStopMusic;
                narrative.getTimestamp += GetTimestamp;
                narrative.use_custom_audio = true;
            }
            else
            {
                Debug.LogError("Dialogue Quests: Integration failed - Make sure to add the DQManager to the scene");
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer > 1f)
            {
                timer = 0f;
                SlowUpdate();
            }
        }

        private void SlowUpdate()
        {
            foreach (PlayerCharacter player in PlayerCharacter.GetAll())
            {
                if (player != null && !inited_players.Contains(player) && player.IsSpawned)
                {
                    inited_players.Add(player);
                    InitPlayer(player);
                }
            }
            foreach (Actor actor in Actor.GetAll())
            {
                if (actor != null && !inited_actors.Contains(actor))
                {
                    inited_actors.Add(actor);
                    InitActor(actor);
                }
            }
        }

        private void InitPlayer(PlayerCharacter player)
        {
            Actor actor = player.GetComponent<Actor>();
            if (actor != null && actor.IsPlayer())
            {
                actor.player_id = player.player_id;
            }

            if (actor == null || !actor.IsPlayer())
                Debug.LogError("Dialogue Quests: Integration failed - Make sure to add the Actor script on all PlayerCharacter, with an ActorData that has is_player to true ");
        }

        private void InitActor(Actor actor)
        {
            Selectable select = actor.GetComponent<Selectable>();
            if (select != null)
            {
                actor.auto_interact_enabled = false;
                select.onUse += (PlayerCharacter character) =>
                {
                    character.StopMove();
                    character.FaceTorward(actor.transform.position);
                    actor.Interact(character.GetComponent<Actor>());
                };
            }
        }

        private void OnPauseGameplay()
        {
            if(!TheNetwork.Get().IsOnline)
                TheGame.Get().PauseScripts();
        }

        private void OnUnpauseGameplay()
        {
            if (!TheNetwork.Get().IsOnline)
                TheGame.Get().UnpauseScripts();
        }

        private void OnPlaySFX(string channel, AudioClip clip, float vol = 0.8f)
        {
            TheAudio.Get().PlaySFX(channel, clip, vol);
        }

        private void OnPlayMusic(string channel, AudioClip clip, float vol = 0.4f)
        {
            TheAudio.Get().PlayMusic(channel, clip, vol);
        }

        private void OnStopMusic(string channel)
        {
            TheAudio.Get().StopMusic(channel);
        }

        private float GetTimestamp()
        {
            return TheGame.Get().GetTimestamp();
        }

        private static void OverrideData(int player_id, NarrativeData ndata)
        {
            if (player_id >= 0)
                PlayerData.Get(player_id)?.OverrideNarrativeData(ndata);
            else
                WorldData.Get()?.OverrideNarrativeData(ndata);
        }

        private static NarrativeData GetData(int player_id)
        {
            if (player_id >= 0 && PlayerData.Get(player_id) != null)
                return PlayerData.Get(player_id).narrative_data;
            return WorldData.Get().narrative_data;
        }

        public static List<NarrativeData> GetDataAll()
        {
            List<NarrativeData> list = new List<NarrativeData>();
            list.Add(WorldData.Get().narrative_data);
            foreach(PlayerData pdata in PlayerData.GetAll())
                list.Add(pdata.narrative_data);
            return list;
        }

#endif

    }
}

