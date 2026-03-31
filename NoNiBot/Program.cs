using Discord;
using Discord.WebSocket;
using NoNiDev.NoNiBot.DiscordBot;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NoNiDev.NoNiBot
{
    public class Program
    {
       
       
        public static string token = Environment.GetEnvironmentVariable("TOKEN_DISCORD") ?? string.Empty;
       

        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            DotNetEnv.Env.Load();

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Erreur : La variable DISCORD_TOKEN est introuvable !");
                return;
            }
            Bot discordBot = new Bot(token);
            await discordBot.InitBot();

            await Task.Delay(-1);
        }
       
    }
}