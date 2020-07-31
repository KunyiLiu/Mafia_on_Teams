﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MafiaCore.Players;
using AdaptiveCards;
using Bot.AdaptiveCard.Prompt;
using MafiaCore;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        static string AdaptivePromptId = "adaptive";
        private const string UserInfo = "value-userInfo";
        private const string contexts = "value-contexts";

        public UserProfile GameData { get; set; }
        //public Game MafiaGame { get; set; }

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
            var _livingPeople = MainDialog.MafiaGame.GetActivePlayerNames();
            await stepContext.Context.SendActivityAsync("It's night time.");

            // Create the list of options to choose from.
            List<string> options = new List<string>();
            foreach (string playerName in _livingPeople)
            {
                options.Add(playerName);
            }
            options.Add(NoneOption);

            // TODO: Prompt the user for a choice to Mafia Group.
            return await PromptWithAdaptiveCardAsync(stepContext, options, cancellationToken);
            // return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> NightValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken
            )
        {
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            var choice = (String)(stepContext.Result as JObject)["kill_choice"];
            // await stepContext.Context.SendActivityAsync("You decided to kill " + choice);
            if (MainDialog.MafiaGame.PlayerMapping.ContainsKey(choice))
                MainDialog.MafiaGame.EliminatePlayer(MainDialog.MafiaGame.PlayerMapping[choice]);
            stepContext.Values[currentGame] = MainDialog.MafiaGame.ActivePlayers
                .Select(player => player.Name).ToHashSet();
            if (!MainDialog.MafiaGame.AllCivilliansEliminated())
            {
                return await stepContext.NextAsync(choice, cancellationToken);
            }
            else if (MainDialog.MafiaGame.AllCivilliansEliminated())
            {
                return await stepContext.EndDialogAsync("Mafia Win", cancellationToken);
            }
                return await stepContext.EndDialogAsync("Villagers win", cancellationToken);
        }

        private async Task<DialogTurnResult> DayVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var killed = (string)stepContext.Result;
            var _livingPeople = MainDialog.MafiaGame.ActivePlayers;

            await stepContext.Context.SendActivityAsync("It's daytime now. Last Night, " + killed + " was killed.");

            // Create the list of options to choose from.
            var options = _livingPeople.Select(people => people.Name).ToList();
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
            var _livingPeople = MainDialog.MafiaGame.ActivePlayers;
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
                MainDialog.MafiaGame.EliminatePlayer(MainDialog.MafiaGame.PlayerMapping[choice.Value]);
            }
            stepContext.Values[currentGame] = MainDialog.MafiaGame.ActivePlayers
                .Select(player => player.Name).ToHashSet(); ;

            if (!MainDialog.MafiaGame.AllCivilliansEliminated())
            {
                return await stepContext.NextAsync(choice, cancellationToken);
            }
            else if (MainDialog.MafiaGame.AllCivilliansEliminated())
            {
                return await stepContext.EndDialogAsync("Mafia Win", cancellationToken);
            }
            return await stepContext.EndDialogAsync("Villagers win", cancellationToken);
        }

            private async Task<DialogTurnResult> LoopStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            var _livingPeople = stepContext.Values[currentGame] as Dictionary<string, string>;

            var livingCivilianCount = GetLivingCivilianCount(_livingPeople);
            var livingMafia = GetLivingMafiaCount(_livingPeople);

            if (MainDialog.MafiaGame.AllCivilliansEliminated())
            {
                return await stepContext.EndDialogAsync("Mafia Win", cancellationToken);
            }
            else if (MainDialog.MafiaGame.AllMafiasCaught())
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

        private async Task<DialogTurnResult> PromptWithAdaptiveCardAsync(
            WaterfallStepContext stepContext,
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
                        Text = "Who you want to kill? For mafia only",
                        Weight = AdaptiveTextWeight.Bolder
                    },
                    new AdaptiveChoiceSetInput()
                    {
                        Id = "kill_choice",
                        Style = AdaptiveChoiceInputStyle.Expanded,
                        Choices = choices.Select(choice => new AdaptiveChoice
                        {
                            Title = choice,
                            Value = choice,  // This will be a string
                        }).ToList<AdaptiveChoice>(),
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
