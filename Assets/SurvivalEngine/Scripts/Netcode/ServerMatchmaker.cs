using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NetcodePlus
{
    /// <summary>
    /// Example server script for matchmaking, currently only works with a Dedicated server
    /// </summary>

    public class ServerMatchmaker
    {
        public UnityAction<LobbyGame> onMatchmaking;               //Whenever a match is found and starts, use to change game settings before its sent to players

        private const ulong matchmaking_force_start = 5; //After this time, will start the game even if not full
        private const ulong matchmaking_expiration = 8; //Client must refresh within this time or will be canceled
        private const ulong matched_expiration = 120;   //A started match will stay in array for this time

        private Dictionary<ulong, MatchmakingItem> matchmaking_players = new Dictionary<ulong, MatchmakingItem>();
        private Dictionary<ulong, LobbyGame> matched_players = new Dictionary<ulong, LobbyGame>();
        private ServerLobbyConfig config;

        public ServerMatchmaker(ServerLobbyConfig config)
        {
            this.config = config;
        }

        public void SlowUpdate(ulong server_tick)
        {
            //Remove expired
            List<ulong> expired_matchmaking = new List<ulong>();
            foreach (KeyValuePair<ulong, MatchmakingItem> pair in matchmaking_players)
            {
                ulong expiration = pair.Value.last_tick + matchmaking_expiration;
                if (server_tick > expiration)
                    expired_matchmaking.Add(pair.Key);
            }

            //Remove expired
            List<ulong> expired_matched = new List<ulong>();
            foreach (KeyValuePair<ulong, LobbyGame> pair in matched_players)
            {
                ulong expiration = pair.Value.last_update + matched_expiration;
                if (server_tick > expiration)
                    expired_matched.Add(pair.Key);
            }

            //Remove
            foreach (ulong id in expired_matchmaking)
                matchmaking_players.Remove(id);
            foreach (ulong id in expired_matched)
                matched_players.Remove(id);
        }

        public LobbyGame FindMatchmaking(LobbyPlayer player, MatchmakingRequest req, ulong server_tick, string host_ip)
        {
            LobbyGame result = new LobbyGame();
            if (player == null || req == null)
                return result;

            if (req.group == null || req.scene == null)
                return result;

            //Already matched, return saved value
            if (!req.is_new && matched_players.ContainsKey(player.client_id))
                return matched_players[player.client_id];

            //Cancel previous
            if (req.is_new)
                CancelMatchmaking(player);

            if (req.is_new)
                Debug.Log("Matchmaking: " + player.username);

            //Add to list
            MatchmakingItem item;
            if (!matchmaking_players.ContainsKey(player.client_id))
            {
                item = new MatchmakingItem();
                item.client_id = player.client_id;
                item.username = player.username;
                item.host_ip = host_ip;
                item.group = req.group;
                item.start_tick = server_tick;
                item.last_tick = server_tick;
                matchmaking_players[player.client_id] = item;
            }
            else
            {
                item = matchmaking_players[player.client_id];
                item.group = req.group;
                item.last_tick = server_tick;
            }

            int group_count = CountInGroup(req.group);
            result.players_found = group_count;
            result.players_max = req.players;

            int players_search = Mathf.Clamp(req.players, 1, config.PlayersMax);
            ulong force_tick = item.start_tick + matchmaking_force_start;
            if (group_count >= players_search || server_tick > force_tick)
            {
                //Match success!
                List<MatchmakingItem> list = SelectPlayers(item, req.group, players_search);
                LobbyGame game = StartGame(req, item, list);
                if (game != null)
                {
                    result = game;
                }
            }

            return result;
        }

        public void CancelMatchmaking(LobbyPlayer player)
        {
            if (player != null)
            {
                if (matchmaking_players.ContainsKey(player.client_id) || matched_players.ContainsKey(player.client_id))
                {
                    matchmaking_players.Remove(player.client_id);
                    matched_players.Remove(player.client_id);
                    Debug.Log("Cancel Matchmaking: " + player.username);
                }
            }
        }

        private int CountInGroup(string group)
        {
            int count = 0;
            foreach (KeyValuePair<ulong, MatchmakingItem> pair in matchmaking_players)
            {
                if (pair.Value.group == group)
                    count++;
            }
            return count;
        }

        private MatchmakingItem PickInGroup(string group)
        {
            foreach (KeyValuePair<ulong, MatchmakingItem> pair in matchmaking_players)
            {
                if (pair.Value.group == group)
                    return pair.Value;
            }
            return null;
        }

        private List<MatchmakingItem> SelectPlayers(MatchmakingItem current, string group, int max)
        {
            List<MatchmakingItem> players = new List<MatchmakingItem>();
            players.Add(current); //Add current player
            matchmaking_players.Remove(current.client_id);

            int to_find = max - 1; //-1, current already added
            int available = CountInGroup(group);
            while (to_find > 0 && available > 0)
            {
                MatchmakingItem player = PickInGroup(group);
                if (player != null)
                {
                    players.Add(player); //Add current player
                    matchmaking_players.Remove(player.client_id);
                    to_find -= 1;
                }
                available = CountInGroup(group);
            }
            return players;
        }

        private LobbyGame StartGame(MatchmakingRequest req, MatchmakingItem host_item, List<MatchmakingItem> items)
        {
            if (items.Count == 0)
                return null;

            string file = NetworkTool.GenerateRandomID(); //placeholder filename
            string scene = req.scene;

            CreateGameData create = new CreateGameData(file, file, scene);
            create.players_max = req.players;
            create.hidden = true; //Matchmaking games are hidden from list
            create.extra = req.extra;

            LobbyGame game = ServerLobby.Get().CreateGame(create, host_item.host_ip);
            if (game != null)
            {
                //Add players
                foreach (MatchmakingItem item in items)
                {
                    LobbyPlayer player = ServerLobby.Get().GetPlayer(item.client_id);
                    if (player != null)
                    {
                        matched_players[item.client_id] = game;
                        game.AddPlayer(player);
                    }
                }

                game.players_found = game.players.Count;

                if (game.players.Count > 0)
                {
                    onMatchmaking?.Invoke(game);
                    ServerLobby.Get().StartGame(game);
                    return game;
                }
            }
            return null;
        }

        public static ServerMatchmaker Get()
        {
            return ServerLobby.Get().Matchmaker;
        }
    }

    [System.Serializable]
    public class MatchmakingRequest
    {
        public string group;
        public string scene;
        public int players;
        public bool is_new;
        public byte[] extra;
    }

    [System.Serializable]
    public class MatchmakingItem
    {
        public ulong client_id;
        public string username;
        public string host_ip;
        public ulong start_tick;
        public ulong last_tick;
        public string group;
    }

}