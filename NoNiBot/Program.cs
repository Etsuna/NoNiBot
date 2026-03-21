using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NoNiDev.NoNiBot
{
    public class Program
    {
        private const string TOKEN = "Token";
        private DiscordSocketClient _client;
        private readonly HttpClient _httpClient = new HttpClient();

        // Le point d'entrée classique de C#
        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            // On initialise le client ici
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync; // On attache une méthode pour gérer les logs
            _client.MessageReceived += OnMessageReceived; // On attache une méthode pour gérer les messages reçus
            _client.Ready += Client_Ready; // On attache une méthode pour gérer l'événement "Ready" (lorsque le bot est prêt)
            _client.SlashCommandExecuted += SlashCommandHandler; // On attache une méthode pour gérer les commandes slash

            // On se connecte avec le Token
            await _client.LoginAsync(TokenType.Bot, TOKEN);
            await _client.StartAsync();

            // Cette ligne est vitale : elle empêche la console de se fermer
            await Task.Delay(-1);
        }
        private async Task OnMessageReceived(SocketMessage message)
        {
            // 1. On vérifie que ce n'est pas le bot lui-même qui parle (pour éviter les boucles infinies)
            if (message.Author.IsBot) return;

            // Commande simple : !ping
            if (message.Content.ToLower() == "!ping")
            {
                await message.Channel.SendMessageAsync("Pong ! 🏓");
            }

            // Commande : !hello
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
                // Premier paramètre : la Seed (obligatoire)
                .AddOption("spoiler", ApplicationCommandOptionType.Attachment, "Le spoiler", isRequired: true)
                // Deuxième paramètre : la difficulté (optionnel avec des choix fixes)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("games")
                    .WithDescription("Jeux")
                    .WithType(ApplicationCommandOptionType.String)
                    .AddChoice("SOH", "Ship Of Harkinian")
                    .AddChoice("OW", "Outer Wilds")
                    .AddChoice("All", "All")
                )
                .AddOption("config", ApplicationCommandOptionType.Attachment, "Le fichier de config", isRequired: false);

            // On l'envoie à Discord (ici pour un serveur spécifique pour que ce soit instantané)
            // Remplace ID_DE_TON_SERVEUR par l'ID de ton serveur de test
            await _client.GetGuild(1454155787302604853).CreateApplicationCommandAsync(guildCommand.Build());
        }
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (command.Data.Name == "parse")
            {
                string arguments = string.Empty;
                // On récupère les valeurs saisies par l'utilisateur
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

                // On informe l'utilisateur que le bot travaille (indispensable pour les slash commands)
                await command.DeferAsync();

                // 2. Création d'un dossier temporaire pour cette exécution
                string runId = Guid.NewGuid().ToString().Substring(0, 8);
                string tempPath = Path.Combine(Path.GetTempPath(), $"bot_parse_{runId}");
                Directory.CreateDirectory(tempPath);

                try
                {
                    // 3. Téléchargement des fichiers
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
                    // 4. Préparation des arguments pour ton CLI
                    // On passe les chemins complets des fichiers téléchargés
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
                    // On appelle une méthode de nettoyage qui ne bloque pas la réponse Discord
                    _ = Task.Run(async () => {
                        await Task.Delay(2000); // On attend 2 secondes pour laisser le temps au CLI de libérer les fichiers
                        try
                        {
                            if (Directory.Exists(tempPath))
                            {
                                Directory.Delete(tempPath, true); // Le 'true' signifie : supprime aussi tout le contenu
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
            

            // 2. Configuration du processus
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

                    // Lecture asynchrone pour éviter de bloquer si la sortie est longue
                    string result = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    // On attend maximum 30 secondes que le processus finisse (sécurité timeout)
                    if (process.WaitForExit(30000))
                    {
                        return string.IsNullOrEmpty(error) ? result : $"Erreur : {error}";
                    }
                    else
                    {
                        process.Kill(); // On force l'arrêt si c'est trop long
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