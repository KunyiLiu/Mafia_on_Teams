// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MafiaCore.Players;
using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using MafiaCore;

namespace Microsoft.BotBuilderSamples
{
    public class GameRoundDialog : ComponentDialog
    {
        // Define a "done" response for the company selection prompt.
        private const string DoneOption = "done";
        private const string NoneOption = "No one";

        // Define value names for values tracked inside the dialogs.
        private const string currentGame = "value-currentGame";
        private const string conversations = "value-conversations";
        private const string UserInfo = "value-userInfo";
        private const string contexts = "value-contexts";

        public UserProfile GameData { get; set; }
        public Game MafiaGame { get; set; }

        public GameRoundDialog()
            : base(nameof(GameRoundDialog))
        {
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));

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
            UserProfile userProfile = stepContext.Options as UserProfile;
            GameData = userProfile;
            MafiaGame = userProfile.Game;
            stepContext.Values[currentGame] = userProfile.Game;
            stepContext.Values[conversations] = userProfile.IndividualConversations;
            stepContext.Values[contexts] = userProfile.Contexts;
            await stepContext.Context.SendActivityAsync("It's night time.");

            // Create the list of options to choose from.
            List<string> options = new List<string>();
            foreach (Player player in ((Game)stepContext.Values[currentGame]).ActivePlayers)
            {
                options.Add(player.Name);
            }
            options.Add(NoneOption);

            var activity = MessageFactory.Text("Who you want to kill?");
            var promptOptions = new PromptOptions
            {
                Prompt = activity,
                RetryPrompt = MessageFactory.Text("Please choose an option from the list."),
                Choices = ChoiceFactory.ToChoices(options),
            };

            // TODO: Prompt the user for a choice to Mafia Group.
            // await PromptWithAdaptiveCardAsync(stepContext, options, cancellationToken);
            return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> NightValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken
            )
        {
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            //Game game = stepContext.Values[currentGame] as Game;
            var result = stepContext.Result;
            FoundChoice choice = (FoundChoice)result;


            await stepContext.Context.SendActivityAsync("You decided to kill " + choice.Value);

            HashSet<Player> activePlayers = MafiaGame.ActivePlayers;

            foreach (Player player in activePlayers)
            {
                if (player is Mafia)
                {
                    ((Mafia)player).Target = choice.Value;
                }
            }

            // Kill civilian

            // Count living civilians
            int livingCivilianCount = activePlayers.Where(a => a is Villager).Count();

            if (livingCivilianCount > 0)
            {
                return await stepContext.NextAsync(choice.Value, cancellationToken);
            } else
            {
                return await stepContext.EndDialogAsync("Mafia Win", cancellationToken);
            }    
        }

        private async Task<DialogTurnResult> DayVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var killed = (string)stepContext.Result;
            var _livingPeople = stepContext.Values[currentGame] as Dictionary<string, string>;

            await stepContext.Context.SendActivityAsync("It's daytime now. Last Night, " + killed + " was killed.");

            // Create the list of options to choose from.
            var options = _livingPeople.Keys.ToList();
            options.Add(NoneOption);
            options.Add(DoneOption);

            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text("Who you want to vote out"),
                RetryPrompt = MessageFactory.Text("Please choose an option from the list."),
                Choices = ChoiceFactory.ToChoices(options),
            };

            // TODO: Prompt the user for a choice to Mafia Group.
            return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> DayValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            var _livingPeople = stepContext.Values[currentGame] as Dictionary<string, string>;
            var choice = (FoundChoice)stepContext.Result;
            var done = choice.Value == DoneOption;

            if (done)
            {
                // If they're done, exit and return their list.
                return await stepContext.EndDialogAsync("Manually End", cancellationToken);
            }

            await stepContext.Context.SendActivityAsync("You decided to vote out " + choice.Value);
            if (choice.Value != NoneOption)
            {
                _livingPeople.Remove(choice.Value);
            }
            stepContext.Values[currentGame] = _livingPeople;
            return await stepContext.NextAsync(null, cancellationToken);
        }

            private async Task<DialogTurnResult> LoopStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            var _livingPeople = stepContext.Values[currentGame] as Dictionary<string, string>;

            var livingCivilianCount = GetLivingCivilianCount(_livingPeople);
            var livingMafia = GetLivingMafiaCount(_livingPeople);

            if (livingCivilianCount <= livingMafia)
            {
                return await stepContext.EndDialogAsync("Mafia Win", cancellationToken);
            }
            else if (livingMafia == 0)
            {
                return await stepContext.EndDialogAsync("Civilian Win", cancellationToken);
            }
            else
            {
                // Otherwise, repeat this dialog, passing in the list from this iteration.
                return await stepContext.ReplaceDialogAsync(nameof(GameRoundDialog), _livingPeople, cancellationToken);
            }
        }

        private int GetLivingCivilianCount(Dictionary<string, string> dict)
        {
            return dict.Where(pair => pair.Value == "Civilian").Count();
        }
        private int GetLivingMafiaCount(Dictionary<string, string> dict)
        {
            return dict.Where(pair => pair.Value == "Mafia").Count();
        }

        private async Task PromptWithAdaptiveCardAsync(
            WaterfallStepContext stepContext,
            List<string> choices,
            CancellationToken cancellationToken)
        {
            // Create card
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                // Use LINQ to turn the choices into submit actions
                Actions = choices.Select(choice => new AdaptiveSubmitAction
                {
                    Title = choice,
                    Data = choice,  // This will be a string
                }).ToList<AdaptiveAction>(),
            };
            // Prompt
            var activity = (Activity)MessageFactory.Attachment(new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                // Convert the AdaptiveCard to a JObject
                Content = JObject.FromObject(card),
            });
            activity.ReplyToId = stepContext.Context.Activity.Id;

            var _appId = "e0de0d3f-8ed6-47dc-afa7-caebdd9c6f43";
            var teamsChannelId = stepContext.Context.Activity.TeamsGetChannelId();
            var serviceUrl = stepContext.Context.Activity.ServiceUrl;
            var credentials = new MicrosoftAppCredentials(_appId, "OX~jD7eP2E~3l7sdMK-Q84.SdL4GvbSKq~");
            ConversationReference conversationReference = null;

            var teamMember = await TeamsInfo.GetMemberAsync(stepContext.Context, stepContext.Context.Activity.From.Id, cancellationToken);
            var conversationParameters = new ConversationParameters
            {
                IsGroup = false,
                Bot = stepContext.Context.Activity.Recipient,
                Members = new ChannelAccount[] { teamMember },
                TenantId = stepContext.Context.Activity.Conversation.TenantId,
            };

            await ((BotFrameworkAdapter)stepContext.Context.Adapter).CreateConversationAsync(
                teamsChannelId,
                serviceUrl,
                credentials,
                conversationParameters,
                async (t1, c1) =>
                {
                    conversationReference = t1.Activity.GetConversationReference();
                    await ((BotFrameworkAdapter)stepContext.Context.Adapter).ContinueConversationAsync(
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
