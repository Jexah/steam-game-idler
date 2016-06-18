using Newtonsoft.Json;
using steam_game_idler.Properties;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace steam_game_idler
{
    internal class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if process is child or parent
            if (args.Length > 0)
            {
                // Process is child, start game
                Environment.SetEnvironmentVariable("SteamAppId", args[0], EnvironmentVariableTarget.Process);
                if (!SteamAPI.Init()) return;
                Application.Run();
            }
            else
            {
                // Process is parent, take parent route, initialize "Steam app" (480) to access Steamworks API
                Environment.SetEnvironmentVariable("SteamAppId", "480", EnvironmentVariableTarget.Process);
                while (!SteamAPI.Init()) ;
                var trayReference = new IdlerTray();

                // Clean up child processes before exiting
                Application.ApplicationExit += trayReference.KillChildren;

                // Run tray context
                Application.Run(trayReference);
            }
        }
    }


    public class IdlerTray : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private List<Process> _processes;

        public IdlerTray()
        {
            // Initialize Tray Icon
            _trayIcon = new NotifyIcon()
            {
                Icon = Resources.AppIcon,
                ContextMenu = new ContextMenu(new[] { new MenuItem("Reconnect", Reconnect), new MenuItem("Exit", Exit) }),
                Visible = true
            };

            // Begin loading the games.
            Task.Run(LaunchApp);
        }

        private async Task LaunchApp()
        {
            // Read Steam API key from file
            var apiKey = File.ReadAllText($@"{Application.StartupPath}\apikey.txt");

            // Load owned games list from API
            var games = await new HttpClient().GetStringAsync($"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={SteamUser.GetSteamID().m_SteamID}&format=json");

            // Parse JSON response to dynamic object
            dynamic obj = JsonConvert.DeserializeObject(games);

            // Populate list of games
            var gameIds = new List<string>();
            foreach (var game in obj.response.games)
            {
                gameIds.Add(game.appid.ToString());
            }

            // Start a process for each game
            _processes = gameIds.Select(appId => Process.Start("steam-game-idler.exe", appId)).Cast<Process>().ToList();
        }

        public void KillChildren(object sender = null, EventArgs e = null)
        {
            foreach (var process in _processes) process.Kill();
        }

        private void Reconnect(object sender, EventArgs e)
        {
            // Clean up child processes then relaunch them
            KillChildren();
            Task.Run(LaunchApp);
        }

        private void Exit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }
    }
}
