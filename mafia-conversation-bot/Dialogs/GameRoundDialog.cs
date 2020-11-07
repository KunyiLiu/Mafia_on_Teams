// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AdaptiveCards;
using Bot.AdaptiveCard.Prompt;
using MafiaCore;
using MafiaCore.Players;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples
{
    public class GameRoundDialog : ComponentDialog
    {
        protected readonly ILogger Logger;

        // Define a "done" response for the company selection prompt.
        private const string DoneOption = "End game";
        private const string NoneOption = "No one";

        // Define value names for values tracked inside the dialogs.
        private const string currentGame = "value-currentGame";
        static string AdaptivePromptId = "adaptive";
        private const string DetectiveChoice = "inspect_choice";
        private const string KillChoice = "kill_choice";
        private const string VoteChoice = "vote_choice";

        public UserProfile GameData { get; set; }

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

        public GameRoundDialog(ILogger<MainDialog> logger)
            : base(nameof(GameRoundDialog))
        {
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new AdaptiveCardPrompt(AdaptivePromptId));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
                {
                    DetectiveVotingStepAsync,
                    MafiaVotingStepAsync,
                    NightValidationStepAsync,
                    DayVotingStepAsync,
                    DayValidationStepAsync,
                    LoopStepAsync,
                }));

            InitialDialogId = nameof(WaterfallDialog);
            Logger = logger;
        }

        private async Task<DialogTurnResult> DetectiveVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            GetGameData(stepContext);

            await stepContext.Context.SendActivityAsync("It's night time.");

            var options = GetActiveUsersOptions();

            DialogTurnResult result = await PromptWithAdaptiveCardAsync(stepContext, "Detective, who do you want to investigate?",
                DetectiveChoice, options, cancellationToken);

            return result;
        }

        private async Task<DialogTurnResult> MafiaVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            string choice = (string)(stepContext.Result as JObject)[DetectiveChoice];
            if (choice != null)
            {
                Logger.LogInformation($"DetectiveChoice={choice}");
                stepContext.Values[DetectiveChoice] = choice;
            }

            GetGameData(stepContext);

            // Create the list of options to choose from.
            var options = GetActiveUsersOptions();

            // TODO: Prompt the user for a choice to Mafia Group.
            DialogTurnResult result = await PromptWithAdaptiveCardAsync(stepContext, "Who you want to kill? For Mafia only",
                KillChoice, options, cancellationToken);

            return result;
        }

        private async Task<DialogTurnResult> NightValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken
            )
        {
            // Continue using the same selection list, if any, from the previous iteration of this dialog.
            string choice = (string)(stepContext.Result as JObject)[KillChoice];
            if (choice == null) return await stepContext.NextAsync(null, cancellationToken);

            // await stepContext.Context.SendActivityAsync("You decided to kill " + choice);

            MafiaGame.AssignTargetToPlayers(choice, Role.Mafia);
            MafiaGame.ExecuteNightPhase();

            if (MafiaGame.CurrentState == GameState.MafiasWon)
            {
                return await stepContext.EndDialogAsync("Mafia win", cancellationToken);
            }
            else if (MafiaGame.CurrentState == GameState.MafiasLost)
            {
                return await stepContext.EndDialogAsync("Villagers win", cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(MafiaGame.Mafias.FirstOrDefault()?.Target, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> DayVotingStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var killed = (string)stepContext.Result;
            var killedPlayerName = !string.IsNullOrEmpty(killed) ? MafiaGame.PlayerMapping[killed].Name : "no one";

            await stepContext.Context.SendActivityAsync("It's daytime now. Last night, " + killedPlayerName + " was killed.");

            // Create the list of options to choose from.
            var options = GetActiveUsersOptions(true);

            return await PromptWithAdaptiveCardAsync(stepContext, "Who do you want to vote out?",
                VoteChoice, options, cancellationToken);
        }

        private async Task<DialogTurnResult> DayValidationStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.
            string choice = (string)(stepContext.Result as JObject)[VoteChoice];

            if (choice == DoneOption)
            {
                // If they're done, exit and return their list.
                return await stepContext.EndDialogAsync("manually ended", cancellationToken);
            }

            var choiceName = choice != null && MafiaGame.PlayerMapping.ContainsKey(choice) ?
                MafiaGame.PlayerMapping[choice].Name :
                "no one";
            await stepContext.Context.SendActivityAsync("You decided to vote out " + choiceName);

            MafiaGame.AssignVoteToPlayers(choice);
            MafiaGame.ExecuteVotingPhase();

            return await stepContext.NextAsync(null, cancellationToken);
        }

            private async Task<DialogTurnResult> LoopStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Retrieve their selection list, the choice they made, and whether they chose to finish.

            if (MafiaGame.CurrentState == GameState.MafiasWon)
            {
                return await stepContext.EndDialogAsync("Mafia win", cancellationToken);
            }
            else if (MafiaGame.CurrentState == GameState.MafiasLost)
            {
                return await stepContext.EndDialogAsync("Villagers win", cancellationToken);
            }
            else
            {
                // Otherwise, repeat this dialog, passing in the list from this iteration.
                return await stepContext.ReplaceDialogAsync(nameof(GameRoundDialog), null, cancellationToken);
            }
        }

        #region Helper Functions
        private void GetGameData(WaterfallStepContext stepContext)
        {
            if (stepContext.Options != null)
            {
                var _gameData = stepContext.Options as ConversationData;
                stepContext.Values[currentGame] = _gameData;
                MafiaGame = new Game(_gameData.UserProfileMap, _gameData.RoleToUsers, _gameData.ActivePlayers);
            }
        }
        #endregion

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
            List<Tuple<string, string>> choices,
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
                        Choices = choices.Select(tup => new AdaptiveChoice
                        {
                            Title = tup.Item1,  // Player Name
                            Value = tup.Item2,  // Player Id
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

        private List<Tuple<string, string>> GetActiveUsersOptions(bool isDayVoting = false)
        {
            List<Tuple<string, string>> options = new List<Tuple<string, string>>();
            foreach (Player player in MafiaGame.ActivePlayers)
            {
                options.Add(Tuple.Create(player.Name, player.Id));
            }
            options.Add(Tuple.Create(NoneOption, NoneOption));

            if (isDayVoting) options.Add(Tuple.Create(DoneOption, DoneOption));
            return options;
        }
    }
}
