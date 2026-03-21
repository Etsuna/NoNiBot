using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NoNiDev.NoNiBot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private readonly HttpClient _httpClient = new HttpClient();

        // Le point d'entrée classique de C#
        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            DotNetEnv.Env.Load();
            string token = Environment.GetEnvironmentVariable("TOKEN_DISCORD");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Erreur : La variable DISCORD_TOKEN est introuvable !");
                return;
            }
            // On initialise le client ici
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync;                                // On attache une méthode pour gérer les logs
            _client.MessageReceived += OnMessageReceived;           // On attache une méthode pour gérer les messages reçus
            _client.Ready += Client_Ready;                          // On attache une méthode pour gérer l'événement "Ready" (lorsque le bot est prêt)
            _client.SlashCommandExecuted += SlashCommandHandler;    // On attache une méthode pour gérer les commandes slash

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }
        private async Task OnMessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot) return;
     
            if (message.Content.ToLower() == "!ping")
            {
                await message.Channel.SendMessageAsync("Pong ! 🏓");
            }

            if (message.Content.ToLower() == "!hello")
            {
                await message.Channel.SendMessageAsync($"Salut {message.Author.Mention} ! Ravi de te voir.");
            }
        }

        public async Task Client_Ready()
        {
            // On définit la commande "parse"
            //ApplicationConsoleReader.exe [-a] [-g GameName] [-c "path\config.json"] "Path\Spoiler.txt"
            var guildCommand = new SlashCommandBuilder()
                .WithName("parse")
                .WithDescription("Lance le parser de randomizer")
                .AddOption("spoiler", ApplicationCommandOptionType.Attachment, "Le spoiler", isRequired: true)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("games")
                    .WithDescription("Jeux")
                    .WithType(ApplicationCommandOptionType.String)
                    .AddChoice("SOH", "Ship Of Harkinian")
                    .AddChoice("OW", "Outer Wilds")
                    .AddChoice("All", "All")
                )
                .AddOption("config", ApplicationCommandOptionType.Attachment, "Le fichier de config", isRequired: false);


            await _client.GetGuild(1454155787302604853).CreateApplicationCommandAsync(guildCommand.Build());
        }
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (command.Data.Name == "parse")
            {
                string arguments = string.Empty;
                var spoilerFile = (IAttachment)command.Data.Options.First(x => x.Name == "spoiler").Value;
                if (!IsValidExtension(spoilerFile.Filename, new[] { ".txt" }))
                {
                    await command.FollowupAsync("Le fichier spoiler doit être un .txt !");
                    return;
                }

                var games = (string)command.Data.Options.FirstOrDefault(x => x.Name == "games")?.Value;
                if (games == "Ship Of Harkinian")
                {
                    arguments += "-g \"Ship of Harkinian\" ";
                }
                else if (games == "Outer Wilds")
                {
                    arguments += "-g \"Outer Wilds\" ";
                }
                else if (games == "All")
                {
                    arguments += "-g \"Ship of Harkinian\" -g \"Outer Wilds\" ";
                }
                var configFile = (IAttachment)command.Data.Options.First(x => x.Name == "config").Value;
                if (!IsValidExtension(configFile.Filename, new[] { ".json" }))
                {
                    await command.FollowupAsync("Le fichier config doit être un .json !");
                    return;
                }

                await command.DeferAsync();

                string runId = Guid.NewGuid().ToString().Substring(0, 8);
                string tempPath = Path.Combine(Path.GetTempPath(), $"bot_parse_{runId}");
                Directory.CreateDirectory(tempPath);

                try
                {
                    string spoilerFilePath = Path.Combine(tempPath, spoilerFile.Filename);
                    string configFilePath = Path.Combine(tempPath, configFile.Filename);
                    try
                    {
                        await DownloadFile(spoilerFile.Url, spoilerFilePath);
                        await DownloadFile(configFile.Url, configFilePath);
                    }
                    catch (HttpRequestException)
                    {
                        await command.FollowupAsync("Impossible de récupérer le fichier depuis les serveurs Discord.");
                        return;
                    }
                    arguments += $"-c \"{configFilePath}\" \"{spoilerFilePath}\"";

                    // 5. Exécution
                    string result = await ExecuteParser(arguments);

                    await command.FollowupAsync($"✅ Parsing réussi !\nArguments utilisés : `{arguments}`\n```\n{result}\n```");
                }
                catch (Exception ex)
                {
                    await command.FollowupAsync($"❌ Une erreur est survenue : {ex.Message}");
                }
                finally
                {
                    _ = Task.Run(async () => {
                        await Task.Delay(2000); 
                        try
                        {
                            if (Directory.Exists(tempPath))
                            {
                                Directory.Delete(tempPath, true); 
                                Console.WriteLine($"[Cleanup] Dossier temporaire supprimé : {tempPath}");
                            }
                        }
                        catch (IOException ioEx)
                        {
                            Console.WriteLine($"[Cleanup Error] Impossible de supprimer le dossier : {ioEx.Message}");
                        }
                    });
                }
            }
        }
        private async Task DownloadFile(string url, string outputPath)
        {
            var data = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(outputPath, data);
        }

        public Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
        private bool IsValidExtension(string fileName, string[] allowedExtensions)
        {
            string ext = Path.GetExtension(fileName).ToLower();
            return allowedExtensions.Contains(ext);
        }
        private async Task<string> ExecuteParser(string inputArgs)
        {
            
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "E:\\Dev\\git\\Source\\Repos\\SpoilerArchipelagoParser\\ApplicationConsoleReader\\bin\\Release\\net10.0\\publish\\win-x86\\ApplicationConsoleReader.exe", // Assure-toi que le chemin est correct
                Arguments = inputArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // On récupère aussi les erreurs du CLI
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = new Process { StartInfo = startInfo })
                {
                    StringBuilder output = new StringBuilder();
                    process.Start();

                    string result = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    if (process.WaitForExit(30000))
                    {
                        return string.IsNullOrEmpty(error) ? result : $"Erreur : {error}";
                    }
                    else
                    {
                        process.Kill();
                        return "Erreur : Le parsing a pris trop de temps (Timeout).";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Erreur système : {ex.Message}";
            }
        }
    }
}