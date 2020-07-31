// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MafiaCore;
using MafiaCore.Players;
using AdaptiveCards;
using MafiaCore;
using MafiaCore.Players;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples
{
    public class MainDialog : ComponentDialog
    {
        private readonly UserState _userState;
        protected readonly ILogger Logger;
        private readonly string _appId;
        private readonly string _appPassword;

        public Game MafiaGame { get; set; }
        public List<ConversationReference> IndividualConversations { get; set; }

        public List<ITurnContext> Contexts { get; set; }
        // Define value names for values tracked inside the dialogs.
        private const string UserInfo = "value-userInfo";

        public MainDialog(UserState userState, ILogger<MainDialog> logger, IConfiguration config)
            : base(nameof(MainDialog))
        {
            _userState = userState;
            _appId = config["MicrosoftAppId"];
            _appPassword = config["MicrosoftAppPassword"];

            IndividualConversations = new List<ConversationReference>();
            Contexts = new List<ITurnContext>();

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

            MafiaGame = new Game();

            List<TeamsChannelAccount> members = await GetPagedMembers(stepContext.Context, cancellationToken);
            // TODO: update valid range
            if (members.Count <= 0)
            {
                await stepContext.Context.SendActivityAsync("Not enough players.");
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            foreach (TeamsChannelAccount player in members)
            {
                MafiaGame.AddPlayer(new Player(player.Id, player.Name));
            }

            MafiaGame.InitializeGameBoard();

            await MessageRoleToAllMembersAsync(stepContext.Context, members, cancellationToken);

            var dict = new Dictionary<string, string> () {
                { "kunyl", "Civilian" },
                { "Supratik", "Civilian" },
                { "Nanhua", "Civilian" },
                { "Yogesh", "Mafia" }
            };
            var dict2 = MafiaGame.ActivePlayers.ToDictionary(p => p.Name, p => p.Role.ToString());

            UserProfile userInfo  = new UserProfile() { Game =  MafiaGame, Players = dict2 };
            // TODO: Create Group Chat for Mafia
            return await stepContext.BeginDialogAsync(nameof(GameRoundDialog), dict2, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var message = (string)stepContext.Result;

            string status = "The game ends, " + message + "!";

            await stepContext.Context.SendActivityAsync(status);

            var accessor = _userState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userInfo = new UserProfile();
            // await accessor.SetAsync(stepContext.Context, userInfo, cancellationToken);

            return await stepContext.EndDialogAsync(null, cancellationToken);
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

            if (teamsChannelId != null)
            {
                // TODO: Create a private channel
                return;
            }

            foreach (TeamsChannelAccount teamMember in members)
            {
                // Find player in activeplayers
                Player player = MafiaGame.ActivePlayers.Where(p => p.Id == teamMember.Id.ToString()).First();
                var proactiveMessage = MessageFactory.Text($"Hello {teamMember.Name}, you are {player.Role}.");

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
                        if (!IndividualConversations.Contains(conversationReference))
                        {
                            IndividualConversations.Add(conversationReference);
                        }
                        await ((BotFrameworkAdapter)turnContext.Adapter).ContinueConversationAsync(
                            _appId,
                            conversationReference,
                            async (t2, c2) =>
                            {
                                Contexts.Add(t2);
                                await t2.SendActivityAsync(proactiveMessage, c2);
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
