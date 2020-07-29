// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;

namespace Microsoft.BotBuilderSamples
{
    public class GameRoundDialog : ComponentDialog
    {
        // Define a "done" response for the company selection prompt.
        private const string DoneOption = "done";
        private const string NoneOption = "No one";

        // Define value names for values tracked inside the dialogs.
        private const string currentAttendants = "value-currentPlayers";
        private const string UserInfo = "value-userInfo";

        public GameRoundDialog()
            : base(nameof(GameRoundDialog))
        {
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
                {
                    NightVotingStepAsync,
                    NightValidationStepAsync,
                    DayVotingStepAsync,
                    LoopStepAsync,
                }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> NightVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var _livingPeople = stepContext.Options as Dictionary<string, string> ?? new Dictionary<string, string>();
            stepContext.Values[currentAttendants] = _livingPeople;

            // Create the list of options to choose from.
            var options = _livingPeople.Keys.ToList();
            options.Add(NoneOption);

            var promptOptions = new PromptOptions
            {
                Prompt = MessageFactory.Text("Who you want to kill"),
                RetryPrompt = MessageFactory.Text("Please choose an option from the list."),
                Choices = ChoiceFactory.ToChoices(options),
            };

            // TODO: Prompt the user for a choice to Mafia Group.
            return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> NightValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken
            )
        {
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            var dict = stepContext.Values[currentAttendants] as Dictionary<string, string>;
            var choice = (FoundChoice)stepContext.Result;

            await stepContext.Context.SendActivityAsync("You decided to kill " + choice.Value);
            if (dict.ContainsKey(choice.Value)) dict.Remove(choice.Value);

            var livingCivilianCount = GetLivingCivilianCount(dict);
            stepContext.Values[currentAttendants] = dict;

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
            var _livingPeople = stepContext.Values[currentAttendants] as Dictionary<string, string>;

            await stepContext.Context.SendActivityAsync("Last Night, " + killed + " was killed.");

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


        private async Task<DialogTurnResult> LoopStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            var _livingPeople = stepContext.Values[currentAttendants] as Dictionary<string, string>;
            var choice = (FoundChoice)stepContext.Result;
            var done = choice.Value == DoneOption;

            if (!done || choice.Value != NoneOption)
            {
                // If they chose a company, add it to the list.
                await stepContext.Context.SendActivityAsync("You decided to vote out " + choice.Value);
                _livingPeople.Remove(choice.Value);
            }

            var livingCivilianCount = GetLivingCivilianCount(_livingPeople);
            var livingMafia = GetLivingMafiaCount(_livingPeople);
            if (done)
            {
                // If they're done, exit and return their list.
                return await stepContext.EndDialogAsync("Manually End", cancellationToken);
            }
            else if (livingCivilianCount == 0)
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
    }
}
