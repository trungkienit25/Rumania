using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace NetcodePlus
{
    /// <summary>
    /// Takes care of starting and stopping new game servers, usually called by the Lobby
    /// </summary>

    public class ServerLauncher
    {
        public UnityAction<string> onServerStart;
        public UnityAction<string> onServerEnd;

        private Dictionary<string, ExecGame> exec_games = new Dictionary<string, ExecGame>();
        private List<string> remove_list = new List<string>();
        private ServerLobbyConfig config;

        private static bool debug = false;

        public ServerLauncher(ServerLobbyConfig config)
        {
            this.config = config;
            string fullpath = config.GamePath;
            ServerType type = config.GameServerType;
            if (!File.Exists(fullpath) && type == ServerType.DedicatedServer)
            {
                UnityEngine.Debug.LogError("Game Server can't be found, Lobby won't be able to start it: " + fullpath);
            }
        }

        public void SlowUpdate()
        {
            //Deleted process that ended
            foreach (KeyValuePair<string, ExecGame> game in exec_games)
            {
                Process process = game.Value.process;
                if (process == null || process.HasExited)
                {
                    process?.Close(); //Unlink process from this one
                    remove_list.Add(game.Key);
                }
            }
            foreach (string id in remove_list)
            {
                exec_games.Remove(id);
                onServerEnd?.Invoke(id);
            }

            if (remove_list.Count > 0)
                remove_list.Clear();
        }

        public void StartGame(LobbyGame game)
        {
            if (game == null)
                return;

            StopGame(game.game_id); //Stop existing

            ExecGame exec = new ExecGame();
            exec.game_id = game.game_id;
            exec.url = game.server_host;
            exec.port = game.server_port;
            exec.save = game.save;
            exec.scene = game.scene;
            exec_games[game.game_id] = exec;
            StartExec(exec);
        }

        public void StopGame(string game_uid)
        {
            ExecGame exec = GetGame(game_uid);
            if (exec != null && exec.process != null)
            {
                exec.process.Kill();
            }
        }

        public void StopAllGames()
        {
            foreach (KeyValuePair<string, ExecGame> game in exec_games)
            {
                Process process = game.Value.process;
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
        }

        private void StartExec(ExecGame game)
        {
            string fullpath = config.GamePath;
            if (File.Exists(fullpath) && game.process == null)
            {
                UnityEngine.Debug.Log("Starting Game: " + game.game_id + "\n" + GetArguments(game));
                Process process = new Process();
                game.process = process;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = fullpath;
                process.StartInfo.Arguments = GetArguments(game);
                process.EnableRaisingEvents = true;

                if (debug)
                {
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.OutputDataReceived += new DataReceivedEventHandler(ReceiveOutput);
                    process.ErrorDataReceived += new DataReceivedEventHandler(ReceiveError);
                }

                process.Start();

                if (debug)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                onServerStart?.Invoke(game.game_id);
            }
            else
            {
                UnityEngine.Debug.LogError("Can't find game server executable: " + fullpath);
            }
        }

        private void ReceiveOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log(e.Data);
        }

        private void ReceiveError(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log(e.Data);
        }

        private string GetArguments(ExecGame game)
        {
            string args = "-batchmode -nographics ";
            if (!string.IsNullOrEmpty(game.game_id))
                args += ServerGame.game_id_id + game.game_id + " ";
            if (!string.IsNullOrEmpty(game.url))
                args += ServerGame.url_id + "\"" + game.url + "\" ";
            if (game.port != 0)
                args += ServerGame.port_id + game.port + " ";
            if (!string.IsNullOrEmpty(game.scene))
                args += ServerGame.scene_id + "\"" + game.scene + "\" ";
            if (!string.IsNullOrEmpty(game.save))
                args += ServerGame.save_id + "\"" + game.save + "\" ";
            args += ServerGame.permanent_id + "false" + " ";
            args += ServerGame.wait_id + config.WaitSave;
            return args;
        }

        private ExecGame GetGame(string game_id)
        {
            if (exec_games.ContainsKey(game_id))
                return exec_games[game_id];
            return null;
        }
    }

    public class ExecGame
    {
        public string game_id;
        public string url;
        public ushort port;
        public string save;
        public string scene;
        public Process process;
    }
}
