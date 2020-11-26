// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples
{
    public class MainDialog : CancelAndHelpDialog
    {
        private readonly UserState _userState;
        private readonly ConversationState _conversationState;
        protected readonly ILogger Logger;
        private readonly string _appId;
        private readonly string _appPassword;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public MainDialog(UserState userState, ConversationState conversationState,
            ILogger<MainDialog> logger, IConfiguration config, ConcurrentDictionary<string, ConversationReference> conversationReferences)
            : base(nameof(MainDialog), conversationState)
        {
            _userState = userState;
            _conversationState = conversationState;
            _appId = config["MicrosoftAppId"];
            _appPassword = config["MicrosoftAppPassword"];
            _conversationReferences = conversationReferences;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new GameRoundDialog(conversationState, config, conversationReferences));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                StartStepAsync,
                AssignRoleStepAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
            Logger = logger;
        }

        private async Task<DialogTurnResult> StartStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            Console.WriteLine("+++++++StartStepAsync+++++++++++");

            if (stepContext.Context.Activity.Type == ActivityTypes.Event)
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }

            string[] options = new string[] { "New Game: Detective + Doctor + Mafia", "Not Interested" };
            var message = @"Hello, we are a Teams Chat Bot for playing Magia Game, designed and developed by DRAMA team.
                If you are interested in playing with our App, please select one of the role patterns to start a new game.
                ";
            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text(message),
                // RetryPrompt = MessageFactory.Text("Please choose an option from the list."),
                Choices = ChoiceFactory.ToChoices(options),
                Style = ListStyle.HeroCard
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> AssignRoleStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            Console.WriteLine("+++++++AssignRoleStepAsync+++++++++++");
            var convStateAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            ConversationData gameData;

            if (stepContext.Context.Activity.Type == ActivityTypes.Event)
            {
                gameData = await convStateAccessor.GetAsync(stepContext.Context, () => new ConversationData());

                return await stepContext.BeginDialogAsync(nameof(GameRoundDialog), gameData, cancellationToken);
            }

            var result = (FoundChoice)stepContext.Result;
            Logger.LogInformation("What is the result : " + result);
            if (result.Value == "Not Interested")
            {
                await stepContext.Context.SendActivityAsync("Alright. Hope to see you next time.");
                return await stepContext.EndDialogAsync("Manual End", cancellationToken);
            }

            await stepContext.Context.SendActivityAsync("Honored to be your moderator today. Let's start the new game.   \n"
                + "We are now assigning roles. Please wait for a few seconds😊");

            List<TeamsChannelAccount> members = await DialogHelper.GetPagedMembers(stepContext.Context, cancellationToken);
            // TODO: update valid range
            if (members.Count <= 0)
            {
                await stepContext.Context.SendActivityAsync("Not enough players.");
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            // Initialize data in Game
            Game mafiaGame = new Game();
            foreach (TeamsChannelAccount player in members)
            {
                mafiaGame.AddPlayer(new Player(player.Id, player.Name));
            }
            mafiaGame.InitializeGameBoard();

            await MessageRoleToAllMembersAsync(stepContext.Context, mafiaGame, members, cancellationToken);

            // var dict = MafiaGame.ActivePlayers.ToDictionary(p => p.Name, p => p.Role.ToString());
            // await stepContext.Context.SendActivityAsync("For Mafia, you can join the group to discussion with your fellows.");

            gameData = DialogHelper.ConvertConversationState(mafiaGame);
            await convStateAccessor.SetAsync(stepContext.Context, gameData, cancellationToken);

            // await TestProactiveAsync(stepContext.Context, members, cancellationToken);

            return await stepContext.BeginDialogAsync(nameof(GameRoundDialog), gameData, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            Console.WriteLine("+++++++FinalStepAsync+++++++++++");
            var message = (string)stepContext.Result;

            string status = "🎊🎊🎊  The game ends, " + message + "!  🎊🎊🎊";
            await stepContext.Context.SendActivityAsync(status);

            var accessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var gameData = await accessor.GetAsync(stepContext.Context, () => new ConversationData());

            await stepContext.Context.SendActivityAsync("What to know who were the special players?");
            foreach (var pair in gameData.RoleToUsers)
            {
                Logger.LogInformation("The role is {0}", pair.Key);
                if (pair.Key != Role.Villager.ToString())
                {
                    var roleNames = pair.Value.Select(pid => gameData.UserProfileMap.GetValueOrDefault(pid, pid));
                    await stepContext.Context.SendActivityAsync($"{pair.Key}: {string.Join(", ", roleNames)}");
                }
            }
            // await stepContext.Context.SendActivityAsync($"{player.Name}: {player.Role.ToString()}");

            // Clean up the Property Data
            await accessor.SetAsync(stepContext.Context, new ConversationData(), cancellationToken);
            await _conversationState.SaveChangesAsync(stepContext.Context, false, cancellationToken);

            return await stepContext.EndDialogAsync("Official End", cancellationToken);
        }

        private async Task MessageRoleToAllMembersAsync(ITurnContext turnContext, Game mafiaGame, List<TeamsChannelAccount> members, CancellationToken cancellationToken)
        {
            var teamsChannelId = turnContext.Activity.TeamsGetChannelId();
            teamsChannelId ??= "msteams";
            var serviceUrl = turnContext.Activity.ServiceUrl;
            var credentials = new MicrosoftAppCredentials(_appId, _appPassword);

            var mafiaMemberMap = mafiaGame.Mafias.ToDictionary(p => p.Id, p => p.Name, StringComparer.OrdinalIgnoreCase);
            string openUrlForMafia = "https://teams.microsoft.com/l/chat/0/0?users={0}&topicName=Mafia%20Group&message=Hi%2C%20let%27s%20start%20discussing";
            // TODO: will change it later
            openUrlForMafia = String.Format(openUrlForMafia, String.Join(",", mafiaMemberMap.Keys));

            foreach (TeamsChannelAccount teamMember in members)
            {
                // Find player in activeplayers
                Player player = mafiaGame.ActivePlayers.Where(p => p.Id == teamMember.Id.ToString()).First();
                string message = $"Hello {teamMember.Name}, you are <b>{player.Role}</b>.";

                var card = new HeroCard();

                if (player.Role == Role.Mafia && mafiaMemberMap.Count > 1)
                {
                    message += " Your other mafia members: ";
                    message += String.Join(
                        "",
                        mafiaMemberMap.Where(p => p.Key != teamMember.Id).Select(p => $"{p.Value}, ")
                        );
                    message = message.Substring(0, message.Length - 2);

                    card.Buttons = new List<CardAction>{
                        new CardAction
                        {
                            Type = ActionTypes.OpenUrl,
                            Title = "Join the Mafia Group",
                            Text = "MafiaGroupCreateAction",
                            Value = openUrlForMafia
                        }
                    };
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

        private async Task TestProactiveAsync(ITurnContext turnContext, List<TeamsChannelAccount> members, CancellationToken cancellationToken)
        {
            var teamsChannelId = turnContext.Activity.TeamsGetChannelId();
            teamsChannelId ??= "msteams";
            var serviceUrl = turnContext.Activity.ServiceUrl;
            var credentials = new MicrosoftAppCredentials(_appId, _appPassword);

            // var mafias = members.Where(m => MafiaGame.PlayerMapping[m.Id].Role == Role.Mafia).ToArray();
            var conversationParameters = new ConversationParameters
            {
                IsGroup = false,
                Bot = turnContext.Activity.Recipient,
                Members = new List<ChannelAccount> { members.First() },
                TenantId = turnContext.Activity.Conversation.TenantId,
                // TopicName = "Mafia Group"

            };

            var card = new HeroCard()
            {
                Text = "Test for proactive message",
                Buttons = new List<CardAction>
                {
                    new CardAction
                    {
                        Type = ActionTypes.OpenUrl,
                        Title = "Click here",
                        Text = "TestClick",
                        Value = "https://40132ab200ca.ngrok.io/events/sendback"
                    }
                }
            };
            var activity = MessageFactory.Attachment(card.ToAttachment());

            await ((BotFrameworkAdapter)turnContext.Adapter).CreateConversationAsync(
                teamsChannelId,
                serviceUrl,
                credentials,
                conversationParameters,
                async (t1, c1) =>
                {
                    ConversationReference conversationReference = t1.Activity.GetConversationReference();
                    _conversationReferences.AddOrUpdate(conversationReference.Conversation.Id, conversationReference, (key, newValue) => conversationReference);

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
        }
    }
}