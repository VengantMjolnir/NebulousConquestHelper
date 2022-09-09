using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace NebulousConquestHelper
{
    public class Bot
    {
        public readonly EventId BotEventId = new EventId(42, "Nebulous-QB");

        public DiscordClient Client { get; set; }
        public CommandsNextExtension Commands { get; set; }

		public Dictionary<string, Func<DiscordInteraction, Task>> BotCallbacks;
		public Dictionary<string, CreateOrderSession> ActiveUserInteractions;
		
		public GameInfo Game { get; set; }	

		public Bot(GameInfo game)
        {
			Game = game;

			BotCallbacks = new Dictionary<string, Func<DiscordInteraction, Task>>()
			{
				{"map", HandleMapRequest },
				{"fleets", HandleFleetRequest },
				{"order", HandleCreateOrderRequest },
				{"assign", HandleTeamAssignmentRequest }
			};

			ActiveUserInteractions = new Dictionary<string, CreateOrderSession>();
        }


        public async Task RunBotAsync()
		{
			String configFileName = "src/data/config.json";
			String json = "";
			if (!File.Exists(configFileName))
			{
				Console.WriteLine("config.json is missing. Copy config.example.json and put in the discord bot token");
				return;
			}
			using (FileStream fs = File.OpenRead(configFileName))
			using (StreamReader sr = new StreamReader(fs, new UTF8Encoding(false)))
				json = await sr.ReadToEndAsync();

			BotConfig cfgjson = JsonConvert.DeserializeObject<BotConfig>(json);
			if (string.IsNullOrEmpty(cfgjson.Token))
			{
				Console.WriteLine("Missing token in config");
				return;
			}
			DiscordConfiguration cfg = new DiscordConfiguration
			{
				Token = cfgjson.Token,
				TokenType = TokenType.Bot,
				Intents = DiscordIntents.AllUnprivileged 
					| DiscordIntents.DirectMessages 
					| DiscordIntents.DirectMessageTyping 
					| DiscordIntents.GuildMessages 
					| DiscordIntents.GuildMessageTyping,

				AutoReconnect = true,
				MinimumLogLevel = LogLevel.Debug,
			};

			Client = new DiscordClient(cfg);

			Client.Ready += Client_Ready;
			Client.GuildAvailable += Client_GuildAvailable;
			Client.ClientErrored += Client_ClientErrored;
            Client.InteractionCreated += Client_InteractionCreated;


			var ccfg = new CommandsNextConfiguration
			{
				StringPrefixes = new[] { cfgjson.CommandPrefix },
				EnableDms = true,
				EnableMentionPrefix = true
			};

			Commands = Client.UseCommandsNext(ccfg);

			Commands.CommandExecuted += Commands_CommandExecuted;
			Commands.CommandErrored += Commands_CommandErrored;

			Commands.RegisterCommands<BotCommands>();

			Commands.SetHelpFormatter<BotHelpFormatter>();

			Client.UseInteractivity(new InteractivityConfiguration()
			{
				PollBehaviour = PollBehaviour.KeepEmojis,
				Timeout = TimeSpan.FromSeconds(30)
			});

			await Client.ConnectAsync();

			await Task.Delay(-1);
		}

        private async Task Client_InteractionCreated(DiscordClient sender, InteractionCreateEventArgs e)
        {
			Client.Logger.LogInformation(BotEventId, $"{e.Interaction.User.Username} sent an interaction: {e.Interaction.Data.Name}");

            try
            {
				await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
			}
            catch (Exception ex)
            {
				Client.Logger.LogError(BotEventId, "Exception when responding to interaction");
                throw;
            }

            try
            {
				if ( BotCallbacks.ContainsKey(e.Interaction.Data.Name))
				{
					var callback = BotCallbacks[e.Interaction.Data.Name];
					await callback(e.Interaction);
				}
			}
            catch (Exception ex)
            {
				await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
					.WithContent("Failed to retrieve map"));
            }
        }

        #region Discord Client Handlers
        private async Task Commands_CommandErrored(CommandsNextExtension sender, CommandErrorEventArgs e)
		{
			// Log details
			e.Context.Client.Logger.LogError(BotEventId, $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}", DateTime.Now);

			// Check for lacking permissions and respond
			if (e.Exception is ChecksFailedException ex)
			{
				var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

				var embed = new DiscordEmbedBuilder
				{
					Title = "Access denied",
					Description = $"{emoji} You do not have the permissions required to execute this command.",
					Color = new DiscordColor(0xFF0000) // red
				};
				await e.Context.RespondAsync(embed);
			}
		}

		private Task Commands_CommandExecuted(CommandsNextExtension sender, CommandExecutionEventArgs e)
		{
			e.Context.Client.Logger.LogInformation(BotEventId, $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'");

			return Task.CompletedTask;
		}

		private Task Client_ClientErrored(DiscordClient sender, ClientErrorEventArgs e)
		{
			sender.Logger.LogError(BotEventId, e.Exception, "Exception occured");

			return Task.CompletedTask;
		}

		private Task Client_GuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
		{
			sender.Logger.LogInformation(BotEventId, $"Guild available: {e.Guild.Name}");

			return Task.CompletedTask;
		}

		private Task Client_Ready(DiscordClient sender, ReadyEventArgs e)
		{
			sender.Logger.LogInformation(BotEventId, "Client is ready to process events.");

			return Task.CompletedTask;
		}
        #endregion


        #region Bot Callbacks
        private async Task HandleMapRequest(DiscordInteraction Interaction)
		{
			Mapping.CreateSystemMap(Game.System);
			using (FileStream fs = new FileStream(Helper.DATA_FOLDER_PATH + "SystemMap.png", FileMode.Open, FileAccess.Read))
			{
				await Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
					.WithContent("Here is the map for the current game state:")
					.AddFile("SystemMap.png", fs));
			}
		}

		private async Task HandleFleetRequest(DiscordInteraction Interaction)
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendLine("Fleet Info:");
			foreach (FleetInfo fleet in Game.Fleets)
            {
                builder.Append("  ").AppendLine($"{fleet.Fleet.Name} at {fleet.LocationName}");
			}
            await Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
				.WithContent(builder.ToString()));
		}

		private async Task HandleCreateOrderRequest(DiscordInteraction Interaction)
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendLine("Order Info:");
			foreach (FleetInfo fleet in Game.Fleets)
			{
				builder.Append("  ").AppendLine($"{fleet.Fleet.Name} at {fleet.LocationName}");
			}
			await Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
				.WithContent(builder.ToString()));
		}

		private async Task HandleTeamAssignmentRequest(DiscordInteraction Interaction)
		{
			StringBuilder builder = new StringBuilder();
			DiscordUser user = null;
			TeamInfo team = null;
			foreach (var option in Interaction.Data.Options)
            {
				if (option.Name == "user")
                {
					ulong userid = Convert.ToUInt64(option.Value);
					user = await Client.GetUserAsync(userid);
					Client.Logger.LogInformation(BotEventId, "user option is: " + option.Value.ToString());
                }
				if (option.Name == "team")
                {
					var team_enum = Enum.Parse<GameInfo.ConquestTeam>(option.Value as string);
					team = Game.GetTeam(team_enum);
                }
			}

			if (team != null && user != null)
			{
				team.RegisterPlayer(user.Username, user.Id, "Admiral");
				builder.AppendLine($"User {user.Username} has been assigned to the {team.ShortName}");
				Game.SaveGame();
			}

			await Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
				.WithContent(builder.ToString()));
		}
		#endregion
	}

	public class CreateOrderSession
    {
		public string SelectedFleet { get; set; }
		public string UserName { get; set; }
    }
}
