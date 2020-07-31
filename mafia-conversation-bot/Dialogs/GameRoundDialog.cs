// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AdaptiveCards;
using Bot.AdaptiveCard.Prompt;
using MafiaCore;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples
{
    public class GameRoundDialog : ComponentDialog
    {
        // Define a "done" response for the company selection prompt.
        private const string DoneOption = "End game";
        private const string NoneOption = "No one";

        // Define value names for values tracked inside the dialogs.
        private const string currentGame = "value-currentGame";
        private const string conversations = "value-conversations";
        static string AdaptivePromptId = "adaptive";
        private const string UserInfo = "value-userInfo";
        private const string contexts = "value-contexts";

        public UserProfile GameData { get; set; }
        public Game MafiaGame { get; set; }

        public GameRoundDialog()
            : base(nameof(GameRoundDialog))
        {
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
            var _livingPeople = stepContext.Options as Dictionary<string, string>;
            stepContext.Values[currentGame] = _livingPeople;
            await stepContext.Context.SendActivityAsync("It's night time.");

            // Create the list of options to choose from.
            List<string> options = new List<string>();
            foreach (string playerName in _livingPeople.Keys)
            {
                options.Add(playerName);
            }
            options.Add(NoneOption);

            // TODO: Prompt the user for a choice to Mafia Group.
            return await PromptWithAdaptiveCardAsync(stepContext, "Who you want to kill? For Mafia only", 
                "kill_choice", options, cancellationToken);
            // return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> NightValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken
            )
        {
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            var dict = stepContext.Values[currentGame] as Dictionary<string, string>;
            string choice = (string)(stepContext.Result as JObject)["kill_choice"];
            await stepContext.Context.SendActivityAsync("You decided to kill " + choice);
            if (dict.ContainsKey(choice)) dict.Remove(choice);
            var livingCivilianCount = GetLivingVillagerCount(dict);
            stepContext.Values[currentGame] = dict;
            if (livingCivilianCount > 0)
            {
                return await stepContext.NextAsync(choice, cancellationToken);
            }
            else
            {
                return await stepContext.EndDialogAsync("Mafia win", cancellationToken);
            }
        }

        private async Task<DialogTurnResult> DayVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var killed = (string)stepContext.Result;
            var _livingPeople = stepContext.Values[currentGame] as Dictionary<string, string>;

            await stepContext.Context.SendActivityAsync("It's daytime now. Last night, " + killed + " was killed.");

            // Create the list of options to choose from.
            List<string> options = new List<string>();
            foreach (string playerName in _livingPeople.Keys)
            {
                options.Add(playerName);
            }
            options.Add(NoneOption);
            options.Add(DoneOption);

            // TODO: Prompt the user for a choice to Mafia Group.
            return await PromptWithAdaptiveCardAsync(stepContext, "Who do you want to vote out?", 
                "vote_choice", options, cancellationToken);
        }

        private async Task<DialogTurnResult> DayValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            var _livingPeople = stepContext.Values[currentGame] as Dictionary<string, string>;
            string choice = (string)(stepContext.Result as JObject)["vote_choice"];

            if (choice == DoneOption)
            {
                // If they're done, exit and return their list.
                return await stepContext.EndDialogAsync("manually ended", cancellationToken);
            }

            await stepContext.Context.SendActivityAsync("You decided to vote out " + choice);
            if (choice != NoneOption)
            {
                _livingPeople.Remove(choice);
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

            var livingCivilianCount = GetLivingVillagerCount(_livingPeople);
            var livingMafia = GetLivingMafiaCount(_livingPeople);

            if (livingCivilianCount <= livingMafia)
            {
                return await stepContext.EndDialogAsync("Mafia win", cancellationToken);
            }
            else if (livingMafia == 0)
            {
                return await stepContext.EndDialogAsync("Villagers win", cancellationToken);
            }
            else
            {
                // Otherwise, repeat this dialog, passing in the list from this iteration.
                return await stepContext.ReplaceDialogAsync(nameof(GameRoundDialog), _livingPeople, cancellationToken);
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

        private async Task<DialogTurnResult> PromptWithAdaptiveCardAsync(
            WaterfallStepContext stepContext,
            string text,
            string id,
            List<string> choices,
            CancellationToken cancellationToken)
        {
            // Create card
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
                        Choices = choices.Select(choice => new AdaptiveChoice
                        {
                            Title = choice,
                            Value = choice,  // This will be a string
                        }).ToList(),
                    }
                },
            };
            card.Actions = new List<AdaptiveAction> { new AdaptiveSubmitAction() };

            // Prompt
            var cardAttachment = new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                // Convert the AdaptiveCard to a JObject
                Content = JObject.FromObject(card)
            };

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
}
