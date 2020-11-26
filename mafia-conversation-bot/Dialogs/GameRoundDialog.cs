// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AdaptiveCards;
using Bot.AdaptiveCard.Prompt;
using MafiaCore;
using MafiaCore.Players;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples
{
    public class GameRoundDialog : CancelAndHelpDialog
    {
        // Define a "done" response for the company selection prompt.
        private const string DoneOption = "End game";
        private const string NoneOption = "No one";

        // Define value names for values tracked inside the dialogs.
        private const string currentGame = "value-currentGame";
        static string AdaptivePromptId = "adaptive";
        private const string KillChoice = "kill_choice";
        private const string DoctorChoice = "doctor_choice";
        private const string DetectiveChoice = "inspect_choice";
        private const string VoteChoice = "vote_choice";

        private readonly ConversationState _conversationState;
        private readonly string _appId;
        private readonly string _appPassword;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public GameRoundDialog(
            ConversationState conversationState,
            IConfiguration config,
            ConcurrentDictionary<string, ConversationReference> conversationReferences
            )
            : base(nameof(GameRoundDialog), conversationState)
        {
            _conversationState = conversationState;
            _appId = config["MicrosoftAppId"];
            _appPassword = config["MicrosoftAppPassword"];
            _conversationReferences = conversationReferences;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new AdaptiveCardPrompt(AdaptivePromptId));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
                {
                    NightVotingStepAsync,
                    NightValidationStepAsync,
                    DayVotingStepAsync,
                    DayValidationStepAsync,
                    LoopStepAsync,
                }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> NightVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("+++++++NightVotingStepAsync+++++++++++");
            var _gameData = stepContext.Options as ConversationData ?? new ConversationData();
            stepContext.Values[currentGame] = _gameData;  

            if (_gameData.MafiaTarget != null || _gameData.DoctorTarget != null || _gameData.DetectiveTarget != null)
            {
                var stepContextResult = new JObject
                    (
                    new JProperty("kill_choice", _gameData.MafiaTarget),
                    new JProperty("doctor_choice", _gameData.DoctorTarget),
                    new JProperty(DetectiveChoice, _gameData.DetectiveTarget)
                    );
                return await stepContext.NextAsync(stepContextResult, cancellationToken);
            }

            var msg = "┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈🌜🌜 It's night time 🌛🌛┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈   \n Special roles, it's time for you to take actions now.";
            await stepContext.Context.SendActivityAsync(msg);

            // Create the list of options to choose from.
            var options = CreatePromptOptions(_gameData);

            // TODO: Prompt the user for a choice to Mafia Group.
            return await PromptWithAdaptiveCardAsync(stepContext, _gameData, options, true, cancellationToken);
        }

        private async Task<DialogTurnResult> NightValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken
            )
        {
            Console.WriteLine("+++++++NightValidationStepAsync+++++++++++");
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            string kill_choice;
            string doctor_choice;
            string detective_choice;
            var convStateAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var convInfo = await convStateAccessor.GetAsync(stepContext.Context, () => new ConversationData());

            if (stepContext.Result != null)
            {
                kill_choice = (string)(stepContext.Result as JObject)[KillChoice];
                doctor_choice = (string)(stepContext.Result as JObject)[DoctorChoice];
                detective_choice = (string)(stepContext.Result as JObject)[DetectiveChoice];
            }
            else
            {
                kill_choice = convInfo.MafiaTarget;
                doctor_choice = convInfo.DoctorTarget;
                detective_choice = convInfo.DetectiveTarget;
            }
            // if (choice == null) return await stepContext.NextAsync(null, cancellationToken);

            // await stepContext.Context.SendActivityAsync("You decided to kill " + choice);
            var _gameData = stepContext.Values[currentGame] as ConversationData;
            var mafiaGame = new Game(_gameData.UserProfileMap, _gameData.RoleToUsers, _gameData.ActivePlayers,
                _gameData.MafiaTarget, _gameData.DoctorTarget, _gameData.DetectiveTarget, _gameData.VoteTarget, _gameData.CurrentState);
            mafiaGame.AssignTargetToPlayers(kill_choice, Role.Mafia);
            mafiaGame.AssignTargetToPlayers(doctor_choice, Role.Doctor);
            mafiaGame.AssignTargetToPlayers(detective_choice, Role.Detective);
            mafiaGame.ExecuteNightPhase();

            HashSet<Detective> detectives = mafiaGame.Detectives;
            List<Detective> aliveDetectives = detectives.Where(a => _gameData.ActivePlayers.Contains(a.Id)).ToList();
            await SendDetectiveResponses(stepContext.Context, _gameData, cancellationToken, aliveDetectives);

            _gameData = DialogHelper.ConvertConversationState(mafiaGame);
            // clean up and save the latest gameData to ConversationState
            _gameData.MafiaTarget = null;
            _gameData.DoctorTarget = null;
            _gameData.DetectiveTarget = null;
            stepContext.Values[currentGame] = _gameData;
            await convStateAccessor.SetAsync(stepContext.Context, _gameData, cancellationToken);

            // return await stepContext.NextAsync(mafiaGame.Mafias.FirstOrDefault()?.Target, cancellationToken);
            if (mafiaGame.CurrentState == GameState.MafiasWon)
            {
                return await stepContext.EndDialogAsync("**Mafias** win", cancellationToken);
            }
            else if (mafiaGame.CurrentState == GameState.MafiasLost)
            {
                return await stepContext.EndDialogAsync("**Villagers** win", cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(mafiaGame.Mafias.FirstOrDefault()?.Target, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> DayVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("+++++++DayVotingStepAsync+++++++++++");
            var killed = (string)stepContext.Result;
            var _gameData = stepContext.Values[currentGame] as ConversationData;
            var killedPlayerName = !string.IsNullOrEmpty(killed) ? _gameData.UserProfileMap[killed] : "no one";

            var msg = $"┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈🌞🌞 It's daytime now 🌞🌞┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈   \n  " +
                $"Last night, {killedPlayerName} was killed.";
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg));

            // Create the list of options to choose from.
            var options = CreatePromptOptions(_gameData, true);

            return await PromptWithAdaptiveCardAsync(stepContext, _gameData, options, false, cancellationToken);
        }

        private async Task<DialogTurnResult> DayValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("+++++++DayValidationStepAsync+++++++++++");
            var convStateAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            string choice = (string)(stepContext.Result as JObject)[VoteChoice];

            if (choice == DoneOption)
            {
                // If they're done, exit and return their list.
                return await stepContext.EndDialogAsync("manually ended", cancellationToken);
            }

            var _gameData = stepContext.Values[currentGame] as ConversationData;
            var mafiaGame = new Game(_gameData.UserProfileMap, _gameData.RoleToUsers, _gameData.ActivePlayers,
                _gameData.MafiaTarget, _gameData.DoctorTarget, _gameData.DetectiveTarget, _gameData.VoteTarget, _gameData.CurrentState);
            var choiceName = choice != null && mafiaGame.PlayerMapping.ContainsKey(choice) ?
                mafiaGame.PlayerMapping[choice].Name :
                "no one";
            await stepContext.Context.SendActivityAsync("You decided to vote out " + choiceName);

            mafiaGame.AssignVoteToPlayers(choice);
            mafiaGame.ExecuteVotingPhase();

            _gameData = DialogHelper.ConvertConversationState(mafiaGame);
            stepContext.Values[currentGame] = _gameData;
            await convStateAccessor.SetAsync(stepContext.Context, _gameData, cancellationToken);

            return await stepContext.NextAsync(null, cancellationToken);
        }

            private async Task<DialogTurnResult> LoopStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            var _gameData = stepContext.Values[currentGame] as ConversationData;

            // return await stepContext.ReplaceDialogAsync(nameof(GameRoundDialog), _gameData, cancellationToken);
            if (_gameData.CurrentState == GameState.MafiasWon)
            {
                return await stepContext.EndDialogAsync("**Mafias** win", cancellationToken);
            }
            else if (_gameData.CurrentState == GameState.MafiasLost)
            {
                return await stepContext.EndDialogAsync("**Villagers** win", cancellationToken);
            }
            else
            {
                // Otherwise, repeat this dialog, passing in the list from this iteration.
                return await stepContext.ReplaceDialogAsync(nameof(GameRoundDialog), _gameData, cancellationToken);
            }
        }

        private int GetLivingVillagerCount(Dictionary<string, string> dict)
        {
            return dict.Where(pair => pair.Value == "Villager").Count();
        }
        private int GetLivingMafiaCount(Dictionary<string, string> dict)
        {
            return dict.Where(pair => pair.Value == "Mafia").Count();
        }

        private async Task SendDetectiveResponses(ITurnContext context, ConversationData gameData, CancellationToken cancellationToken, List<Detective> activeDetectives)
        {
            if (activeDetectives == null || activeDetectives.Count == 0)
            {
                return;
            }

            List<TeamsChannelAccount> members = await DialogHelper.GetPagedMembers(context, cancellationToken);

            var teamsChannelId = context.Activity.TeamsGetChannelId();
            teamsChannelId ??= "msteams";
            var serviceUrl = context.Activity.ServiceUrl;
            var credentials = new MicrosoftAppCredentials(_appId, _appPassword);

            gameData.RoleToUsers.TryGetValue(Role.Detective.ToString(), out List<string> activeDetectiveIds);

            foreach (TeamsChannelAccount member in members)
            {
                foreach (Detective detective in activeDetectives)
                {
                    if (detective.Id == member.Id)
                    {
                        string message = $"The team member you inspected is a <b>{detective.TargetRole}</b>.";

                        var conversationParameters = new ConversationParameters
                        {
                            IsGroup = false,
                            Bot = context.Activity.Recipient,
                            Members = new ChannelAccount[] { member },
                            TenantId = context.Activity.Conversation.TenantId,
                        };

                        Task conv = ((BotFrameworkAdapter)context.Adapter).CreateConversationAsync(
                            teamsChannelId,
                            serviceUrl,
                            credentials,
                            conversationParameters,
                            async (t1, c1) =>
                            {
                                ConversationReference conversationReference = t1.Activity.GetConversationReference();

                                await ((BotFrameworkAdapter)context.Adapter).ContinueConversationAsync(
                                    _appId,
                                    conversationReference,
                                    async (t2, c2) =>
                                    {
                                        await t2.SendActivityAsync(message);
                                    },
                                    cancellationToken);
                            },
                            cancellationToken);

                        conv.Wait();
                    }
                }
            }
        }

        private async Task<DialogTurnResult> PromptWithAdaptiveCardAsync(
            WaterfallStepContext stepContext,
            ConversationData gameData,
            List<Tuple<string, string>> choices,
            bool isFakePrompt,
            CancellationToken cancellationToken
            )
        {
            // PureCard Sent
            if (isFakePrompt)
            {
                //TODO: You can add more options for different roles here, please refactor the code
                var currectRef = stepContext.Context.Activity.GetConversationReference();
                var sendBackData = new Dictionary<string, string> { { "SendbackTo", currectRef.Conversation.Id } };
                var mcardAttachment = MakeAdaptiveCard("Who you want to kill? For Mafia only", KillChoice, choices, sendBackData);
                var dcardAttachment = MakeAdaptiveCard("Who you want to heal? For Doctor only", DoctorChoice, choices, sendBackData);
                var detcardAttachment = MakeAdaptiveCard("Who you want to inspect? For Detective only", DetectiveChoice, choices, sendBackData);

                List<TeamsChannelAccount> members = await DialogHelper.GetPagedMembers(stepContext.Context, cancellationToken);
                var activeMafiaIds = gameData.RoleToUsers.GetValueOrDefault(Role.Mafia.ToString(), new List<string>());
                activeMafiaIds = activeMafiaIds.Where(id => gameData.ActivePlayers.Contains(id)).ToList();
                var activeDoctorIds = gameData.RoleToUsers.GetValueOrDefault(Role.Doctor.ToString(), new List<string>());
                activeDoctorIds = activeDoctorIds.Where(id => gameData.ActivePlayers.Contains(id)).ToList();
                var activeDetectiveIds = gameData.RoleToUsers.GetValueOrDefault(Role.Detective.ToString(), new List<string>());
                activeDetectiveIds = activeDetectiveIds.Where(id => gameData.ActivePlayers.Contains(id)).ToList();

                var mafias = new List<TeamsChannelAccount>();
                var doctors = new List<TeamsChannelAccount>();
                var detectives = new List<TeamsChannelAccount>();
                foreach (var member in members)
                {
                    if (activeMafiaIds != null && activeMafiaIds.Contains(member.Id))
                    {
                        mafias.Add(member);
                    }
                    else if (activeDoctorIds != null && activeDoctorIds.Contains(member.Id))
                    {
                        doctors.Add(member);
                    }
                    else if (activeDetectiveIds != null && activeDetectiveIds.Contains(member.Id))
                    {
                        detectives.Add(member);
                    }
                }
                // await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(mcardAttachment), cancellationToken);
                await SendtProactiveMsgAsync(stepContext.Context, mafias, mcardAttachment, cancellationToken);
                await SendtProactiveMsgAsync(stepContext.Context, doctors, dcardAttachment, cancellationToken);
                await SendtProactiveMsgAsync(stepContext.Context, detectives, detcardAttachment, cancellationToken);

                return new DialogTurnResult(DialogTurnStatus.Waiting);
            }
            else
            {
                var cardAttachment = MakeAdaptiveCard("Who do you want to vote out?", VoteChoice, choices);
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Attachments = new List<Attachment>() { cardAttachment },
                        Type = ActivityTypes.Message,
                    },
                };
                return await stepContext.PromptAsync(AdaptivePromptId, opts, cancellationToken);
            }
        }
        private async Task SendtProactiveMsgAsync(ITurnContext turnContext, List<TeamsChannelAccount> members, Attachment card, CancellationToken cancellationToken)
        {
            if (!members.Any()) return;

            var teamsChannelId = turnContext.Activity.TeamsGetChannelId();
            teamsChannelId ??= "msteams";
            var serviceUrl = turnContext.Activity.ServiceUrl;
            var credentials = new MicrosoftAppCredentials(_appId, _appPassword);

            var conversationParameters = new ConversationParameters
            {
                IsGroup = false,
                Bot = turnContext.Activity.Recipient,
                Members = new List<ChannelAccount> { members.First() },
                TenantId = turnContext.Activity.Conversation.TenantId,
                // TopicName = "Mafia Group"

            };

            var activity = MessageFactory.Attachment(card);

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

        private Attachment MakeAdaptiveCard(
            string text,
            string id,
            List<Tuple<string, string>> choices,
            Dictionary<string, string> requestData = null
            )
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                Body = new List<AdaptiveElement>()
                {
                    new AdaptiveTextBlock()
                    {
                        Text = text,
                        Weight = AdaptiveTextWeight.Bolder
                    },
                    new AdaptiveChoiceSetInput()
                    {
                        Id = id,
                        Style = AdaptiveChoiceInputStyle.Expanded,
                        Choices = choices.Select(tup => new AdaptiveChoice
                        {
                            Title = tup.Item1,  // Player Name
                            Value = tup.Item2,  // Player Id
                        }).ToList(),
                    }
                },
            };

            var submitAction = new AdaptiveSubmitAction();
            if (requestData != null)
                submitAction.Data = requestData;
            card.Actions = new List<AdaptiveAction> { submitAction };

            // Prompt
            var cardAttachment = new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                // Convert the AdaptiveCard to a JObject
                Content = JObject.FromObject(card)
            };

            return cardAttachment;
        }

        private List<Tuple<string, string>> CreatePromptOptions(ConversationData gameData, bool isDayVoting = false)
        {
            List<Tuple<string, string>> options = new List<Tuple<string, string>>();
            foreach (string playerId in gameData.ActivePlayers)
            {
                options.Add(Tuple.Create(gameData.UserProfileMap.GetValueOrDefault(playerId, "Null"), playerId));
            }
            options.Add(Tuple.Create(NoneOption, NoneOption));

            if (isDayVoting) options.Add(Tuple.Create(DoneOption, DoneOption));
            return options;
        }
    }
}
