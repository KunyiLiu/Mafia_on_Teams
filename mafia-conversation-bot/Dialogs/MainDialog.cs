// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MafiaCore;
using MafiaCore.Players;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    public class MainDialog : ComponentDialog
    {
        private readonly UserState _userState;
        private readonly ConversationState _conversationState;
        protected readonly ILogger Logger;
        private readonly string _appId;
        private readonly string _appPassword;

        private Game _mafiaGame;
        public Game MafiaGame
        {
            get
            {
                return _mafiaGame ??= new Game();
            }
            set
            {
                _mafiaGame = value;
            } 
        }

        private ConversationData _gameData;
        public ConversationData GameData
        {
            get
            {
                return _gameData ??= new ConversationData();
            }
            set
            {
                _gameData = value;
            }
        }
        
        public MainDialog(UserState userState, ConversationState conversationState,
            ILogger<MainDialog> logger, IConfiguration config)
            : base(nameof(MainDialog))
        {
            _userState = userState;
            _conversationState = conversationState;
            _appId = config["MicrosoftAppId"];
            _appPassword = config["MicrosoftAppPassword"];

            AddDialog(new GameRoundDialog());

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                AssignRoleStepAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
            Logger = logger;
        }

        private async Task<DialogTurnResult> AssignRoleStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync("The game starts, assigning roles.");

            List<TeamsChannelAccount> members = await GetPagedMembers(stepContext.Context, cancellationToken);
            // TODO: update valid range
            if (members.Count <= 0)
            {
                await stepContext.Context.SendActivityAsync("Not enough players.");
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            // Initialize data in Game
            foreach (TeamsChannelAccount player in members)
            {
                MafiaGame.AddPlayer(new Player(player.Id, player.Name));
            }

            MafiaGame.InitializeGameBoard();

            await MessageRoleToAllMembersAsync(stepContext.Context, members, cancellationToken);

            // var dict = MafiaGame.ActivePlayers.ToDictionary(p => p.Name, p => p.Role.ToString());
            await stepContext.Context.SendActivityAsync("For Mafia, you can join the group to discussion with your fellows.");

            UpdateConversationState();
            var convStateAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            await convStateAccessor.SetAsync(stepContext.Context, GameData, cancellationToken);

            return await stepContext.BeginDialogAsync(nameof(GameRoundDialog), GameData, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var message = (string)stepContext.Result;

            string status = "The game ends, " + message + "!";
            await stepContext.Context.SendActivityAsync(status);

            var roleToPlayers = new Dictionary<string, List<string>> ();
            foreach (Player player in MafiaGame.PlayerMapping.Values)
            {
                if (player.Role == Role.Villager) continue;
                var roleName = player.Role.ToString();
                if (!roleToPlayers.ContainsKey(roleName))
                {
                    roleToPlayers[roleName] = new List<string>();
                }
                roleToPlayers[roleName].Add(player.Name);
            }
            // TODO: refactor

            await stepContext.Context.SendActivityAsync("Who were the special players?");
            foreach (var pair in roleToPlayers)
            {
                Logger.LogInformation("The role is {0}", pair.Key);
                await stepContext.Context.SendActivityAsync($"{pair.Key}: {string.Join(", ", pair.Value)}");
            }
            // await stepContext.Context.SendActivityAsync($"{player.Name}: {player.Role.ToString()}");

            var accessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            // Clean up the Property Data
            GameData = new ConversationData();
            MafiaGame = new Game();
            await accessor.SetAsync(stepContext.Context, GameData, cancellationToken);

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        private void UpdateConversationState()
        {
            foreach (KeyValuePair<string, Player> pair in MafiaGame.PlayerMapping)
            {
                GameData.UserProfileMap.Add(pair.Key, pair.Value.Name);

                var roleName = pair.Value.Role.ToString();
                if (!GameData.RoleToUsers.ContainsKey(roleName))
                {
                    GameData.RoleToUsers[roleName] = new List<string>();
                }
                GameData.RoleToUsers[roleName].Add(pair.Key);
            }
            GameData.ActivePlayers = MafiaGame.ActivePlayers.Select(p => p.Id).ToList();
            GameData.IsGameStarted = true;

            Logger.LogInformation(
                "UserProfileMap: {0}, RoleToUsers: {1}, ActivePlayers:{2}",
                String.Join(" , ", GameData.UserProfileMap),
                String.Join(" , ", GameData.RoleToUsers),
                String.Join(" , ", GameData.ActivePlayers)
                );
        }

        private static async Task<List<TeamsChannelAccount>> GetPagedMembers(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            List<TeamsChannelAccount> members = new List<TeamsChannelAccount>();
            string continuationToken = null;

            do
            {
                var currentPage = await TeamsInfo.GetPagedMembersAsync(turnContext, 100, continuationToken, cancellationToken);
                continuationToken = currentPage.ContinuationToken;
                members = members.Concat(currentPage.Members).ToList();
            }
            while (continuationToken != null);

            return members;
        }

        private async Task MessageRoleToAllMembersAsync(ITurnContext turnContext, List<TeamsChannelAccount> members, CancellationToken cancellationToken)
        {
            var teamsChannelId = turnContext.Activity.TeamsGetChannelId();
            var serviceUrl = turnContext.Activity.ServiceUrl;
            var credentials = new MicrosoftAppCredentials(_appId, _appPassword);
            var mafiaMemberMap = members.ToDictionary(p => p.UserPrincipalName, p => p.Name, StringComparer.OrdinalIgnoreCase);
            string openUrlForMafia = "https://teams.microsoft.com/l/chat/0/0?users={0}&topicName=Mafia%20Group&message=Hi%2C%20let%27s%20start%20discussing";
            // TODO: will change it later
            openUrlForMafia = mafiaMemberMap.Count <= 1 ?
                String.Format(openUrlForMafia, "nanhua.jin@microsoft.com,supratik.neupane@microsoft.com") :
                String.Format(openUrlForMafia, String.Join(",", mafiaMemberMap.Keys));

            if (teamsChannelId != null)
            {
                // TODO: Create a private channel
                return;
            }
            IEnumerable<Player> mafiaMembers = MafiaGame.ActivePlayers.Where(p => p.Role == Role.Mafia);
            bool isGroupChatCreated = false; 

            foreach (TeamsChannelAccount teamMember in members)
            {
                // Find player in activeplayers
                Player player = MafiaGame.ActivePlayers.Where(p => p.Id == teamMember.Id.ToString()).First();
                string message = $"Hello {teamMember.Name}, you are {player.Role}.";

                var card = new HeroCard();

                if (player.Role == Role.Mafia && mafiaMembers.Count() > 1)
                {
                    message += " Your other mafia members: ";
                    message += String.Join(
                        "",
                        mafiaMemberMap.Where(m => m.Key != teamMember.Name).Select(m => $"{m.Value}, ")
                        );
                    message = message.Substring(0, message.Length - 2);
                }
                if (player.Role == Role.Mafia && !isGroupChatCreated)
                {
                    card.Buttons = new List<CardAction>{
                        new CardAction
                        {
                            Type = ActionTypes.OpenUrl,
                            Title = "Join the Mafia Group",
                            Text = "MafiaGroupCreateAction",
                            Value = openUrlForMafia
                        } 
                    };
                    isGroupChatCreated = true;
                }
                // Test: var proactiveMessage = MessageFactory.Text(message);
                card.Text = message;
                var activity = MessageFactory.Attachment(card.ToAttachment());

                var conversationParameters = new ConversationParameters
                {
                    IsGroup = false,
                    Bot = turnContext.Activity.Recipient,
                    Members = new ChannelAccount[] { teamMember },
                    TenantId = turnContext.Activity.Conversation.TenantId,
                };

                Task conv = ((BotFrameworkAdapter)turnContext.Adapter).CreateConversationAsync(
                    teamsChannelId,
                    serviceUrl,
                    credentials,
                    conversationParameters,
                    async (t1, c1) =>
                    {
                        ConversationReference conversationReference = t1.Activity.GetConversationReference();

                        await ((BotFrameworkAdapter)turnContext.Adapter).ContinueConversationAsync(
                            _appId,
                            conversationReference,
                            async (t2, c2) =>
                            {
                                await t2.SendActivityAsync(activity, c2);
                            },
                            cancellationToken);
                    },
                    cancellationToken);

                conv.Wait();

            }

            await turnContext.SendActivityAsync(MessageFactory.Text("Roles are assigned. Please don't reveal your identity to others."), cancellationToken);
        }
    }
}