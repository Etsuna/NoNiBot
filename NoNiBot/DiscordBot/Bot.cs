using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NoNiDev.NoNiBot.DiscordBot
{
    public class Bot
    {
        private readonly string TOKEN;
        private DiscordSocketClient? _client;
        private readonly HttpClient _httpClient = new HttpClient();
        public static string BasePath = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException("Environment.ProcessPath is null.");
        public static CancellationTokenSource Cts = new CancellationTokenSource();

        public Bot(string token)
        {
            TOKEN = token;

        }
        public async Task InitBot()
        {
            // On initialise le client ici
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
                UseInteractionSnowflakeDate = false,
                ResponseInternalTimeCheck = false
            };

            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync;                                // On attache une méthode pour gérer les logs
            _client.MessageReceived += OnMessageReceived;           // On attache une méthode pour gérer les messages reçus
            _client.Ready += Client_Ready;                          // On attache une méthode pour gérer l'événement "Ready" (lorsque le bot est prêt)
            _client.SlashCommandExecuted += SlashCommandHandler;    // On attache une méthode pour gérer les commandes slash
            _client.JoinedGuild += OnGuildJoined;                   // On attache une méthode pour gérer l'événement "JoinedGuild" (lorsque le bot rejoint un serveur)
            _client.Disconnected += OnDisconnected;

            await _client.LoginAsync(TokenType.Bot, TOKEN);
            await _client.StartAsync();
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
        static Task OnDisconnected(Exception _)
        {
            Cts?.Cancel();
            return Task.CompletedTask;
        }

        private async Task OnGuildJoined(SocketGuild guild)
        {
            if (_client is null)
            {
                Console.WriteLine("Erreur : Le client Discord n'est pas initialisé !");
                return;
            }

            Console.WriteLine($"Bot a rejoint le serveur : {guild.Name} (ID: {guild.Id})");
            await CommandBulk(guild.Id);
        }

        public async Task Client_Ready()
        {
            if (_client is null)
            {
                Console.WriteLine("Erreur : Le client Discord n'est pas initialisé !");
                return;
            }

            foreach (var g in _client.Guilds)
            {
                Console.WriteLine($"Bot a rejoint le serveur : {g.Name} (ID: {g.Id})");
                await CommandBulk(g.Id);
            }
        }

        public async Task CommandBulk(ulong channelId)
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
                .AddOption("config", ApplicationCommandOptionType.Attachment, "Le fichier de config", isRequired: true);


            if (_client is null)
            {
                Console.WriteLine("Erreur : Le client Discord n'est pas initialisé !");
                return;
            }

            await _client.GetGuild(channelId).CreateApplicationCommandAsync(guildCommand.Build());
            // Exemple de commande supplémentaire (non implémentée pour l'instant)
            var guildCommand2 = new SlashCommandBuilder()
                .WithName("add-archipel")
                .WithDescription("Ajoute un archipel")
                .AddOption("spoiler", ApplicationCommandOptionType.Attachment, "Le spoiler", isRequired: true)
                .AddOption("name", ApplicationCommandOptionType.String, "Le nom de l'archipel", isRequired: true)
                .AddOption("url", ApplicationCommandOptionType.String, "lL'url de la room", isRequired: true)
                .AddOption("config", ApplicationCommandOptionType.Attachment, "Le fichier de config", isRequired: true);


            if (_client is null)
            {
                Console.WriteLine("Erreur : Le client Discord n'est pas initialisé !");
                return;
            }

            await _client.GetGuild(channelId).CreateApplicationCommandAsync(guildCommand2.Build());
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (command.Data.Name != "parse" && command.Data.Name != "add-archipel")
                return;

            await command.DeferAsync();

            switch (command.Data.Name)
            {
                case "parse":
                    _ = Task.Run(async () =>
                    {
                        await HandleParseCommandAsync(command);
                    });
                    break;
                case "add-archipel":
                    _ = Task.Run(async () =>
                    {
                        await HandleCreateArchCommandAsync(command);
                    });
                    break;
                default:
                    await command.FollowupAsync("Commande inconnue.");
                    break;
            }
           
        }

        private async Task HandleCreateArchCommandAsync(SocketSlashCommand command)
        {
            string arguments = string.Empty;

            try
            {
                arguments += "-a "; // Argument pour indiquer qu'on veut ajouter un archipel

                var name = command.Data.Options.FirstOrDefault(x => x.Name == "name")?.Value as string;
                arguments += $"-n \"{name}\" ";

                var url = command.Data.Options.FirstOrDefault(x => x.Name == "url")?.Value as string;
                arguments += $"-u \"{url}\" ";

                string result = await DownloadConfigAndSpoilerFIleAndExecute(command, arguments);
                

                await command.FollowupAsync(
                    $"✅ Ajout d'archipel --.\nArguments utilisés : `{arguments}`\n```\n{result}\n```");

            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"❌ Une erreur est survenue : {ex.Message}");
            }
        }
        private async Task HandleParseCommandAsync(SocketSlashCommand command)
        {
            string arguments = string.Empty;

            try
            {
                var games = command.Data.Options.FirstOrDefault(x => x.Name == "games")?.Value as string;

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

                string result = await DownloadConfigAndSpoilerFIleAndExecute(command, arguments);
                

                await command.FollowupAsync(
                    $"✅ Parsing terminé.\nArguments utilisés : `{arguments}`\n```\n{result}\n```");

            }
            catch (Exception ex)
            {
                await command.FollowupAsync($"❌ Une erreur est survenue : {ex.Message}");
            }
        }
        private async Task<string> DownloadConfigAndSpoilerFIleAndExecute(SocketSlashCommand command, string arguments)
        {
            string result = "";
            string argumentToReturn = "";
            var spoilerOption = command.Data.Options.FirstOrDefault(x => x.Name == "spoiler");
            var spoilerFile = spoilerOption?.Value as IAttachment;

            if (spoilerFile is null)
            {
                await command.FollowupAsync("Le fichier spoiler est obligatoire.");
                return "" ;
            }

            if (!IsValidExtension(spoilerFile.Filename, new[] { ".txt" }))
            {
                await command.FollowupAsync("Le fichier spoiler doit être un .txt !");
                return "" ;
            }



            var configOption = command.Data.Options.FirstOrDefault(x => x.Name == "config");
            var configFile = configOption?.Value as IAttachment;

            if (configFile is not null)
            {
                if (!IsValidExtension(configFile.Filename, new[] { ".json" }))
                {
                    await command.FollowupAsync("Le fichier config doit être un .json !");
                    return "";
                }
            }

            string runId = Guid.NewGuid().ToString("N")[..8];
            string tempPath = Path.Combine(Path.GetTempPath(), $"bot_parse_{runId}");
            Directory.CreateDirectory(tempPath);

            try
            {
                string spoilerFilePath = Path.Combine(tempPath, spoilerFile.Filename);
                await DownloadFile(spoilerFile.Url, spoilerFilePath);

                if (configFile is not null)
                {
                    string configFilePath = Path.Combine(tempPath, configFile.Filename);
                    await DownloadFile(configFile.Url, configFilePath);
                    argumentToReturn += $"-c \"{configFilePath}\" ";
                }

                argumentToReturn += $"\"{spoilerFilePath}\"";
                arguments += " " + argumentToReturn;
                result = await ExecuteConsoleReader(arguments);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempPath))
                        Directory.Delete(tempPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Cleanup Error] {ex.Message}");
                }
            }
            return result;
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

        private async Task<string> ExecuteConsoleReader(string inputArgs)
        {
            string progName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                progName = "ApplicationConsoleReader.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                progName = "ApplicationConsoleReader";
            else
                return "Erreur système : OS non supporté.";

            string fullPath = Path.Combine(BasePath, progName);

            if (!File.Exists(fullPath))
                return $"Erreur système : exécutable introuvable ({fullPath}).";

            var startInfo = new ProcessStartInfo
            {
                FileName = fullPath,
                WorkingDirectory = BasePath,
                Arguments = inputArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };

                process.Start();

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return "Erreur : Le parsing a pris trop de temps (Timeout).";
                }

                string result = await outputTask;
                string error = await errorTask;

                return string.IsNullOrWhiteSpace(error) ? result : $"Erreur : {error}";
            }
            catch (Exception ex)
            {
                return $"Erreur système : {ex.Message}";
            }
        }
    }
}
