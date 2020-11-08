// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MafiaCore;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples
{
    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class DialogBot<T> : TeamsActivityHandler where T : Dialog
    {
        private readonly string _appId;
        private readonly string _appPassword;

        protected readonly BotState ConversationState;
        protected readonly Dialog Dialog;
        protected readonly ILogger Logger;
        protected readonly BotState UserState;
        protected readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public DialogBot(
            IConfiguration config,
            ConversationState conversationState,
            UserState userState,
            T dialog,
            ILogger<DialogBot<T>> logger,
            ConcurrentDictionary<string, ConversationReference> conversationReferences
            )
        {
            _appId = config["MicrosoftAppId"];
            _appPassword = config["MicrosoftAppPassword"];
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
            _conversationReferences = conversationReferences;
        }

        private void AddConversationReference(Activity activity)
        {
            var conversationReference = activity.GetConversationReference();
            _conversationReferences.AddOrUpdate(conversationReference.Conversation.Id, conversationReference, (key, newValue) => conversationReference);
        }

        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            AddConversationReference(turnContext.Activity as Activity);
            Logger.LogInformation("-----ConverSation ADD. Id: {0}, name: {1}, conversaton: {2} -----\n",
                turnContext.Activity.From.Id, turnContext.Activity.From.Name, Tuple.Create(turnContext.Activity.Conversation.Name, turnContext.Activity.Conversation.Id));

            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            Logger.LogInformation("======Activity Type: {0}, Text: {1}, Channel: {2}=========\n", turnContext.Activity.Type, turnContext.Activity.Text, turnContext.Activity.ChannelId);
            await base.OnTurnAsync(turnContext, cancellationToken);

            if (turnContext.Activity.Type == ActivityTypes.Event)
            {
                var convStateAccessor = ConversationState.CreateProperty<ConversationData>(nameof(ConversationData));
                var convInfo = await convStateAccessor.GetAsync(turnContext, () => new ConversationData());

                // Exclude the scenario where players start the game by clicking the choiceprompt afterwards
                if (!convInfo.IsGameStarted && turnContext.Activity.Value != null)
                {
                    return;
                }

                bool isNightChoiceIncomplete = true;
                if (turnContext.Activity.Value != null &&
                    turnContext.Activity.Value is JObject)
                {
                    var activityValue = turnContext.Activity.Value;

                    if (activityValue.ToString().Contains("kill_choice"))
                        convInfo.MafiaTarget = (string)(activityValue as JObject)["kill_choice"];
                    else if (activityValue.ToString().Contains("doctor_choice"))
                        convInfo.DoctorTarget = (string)(activityValue as JObject)["doctor_choice"];

                    List<string> doctorIdList = convInfo.RoleToUsers.GetValueOrDefault(Role.Doctor.ToString(), new List<string>());
                    if (doctorIdList.Any())
                        isNightChoiceIncomplete &= !(convInfo.MafiaTarget != null && convInfo.DoctorTarget != null);
                    else
                    {
                        isNightChoiceIncomplete &= !(convInfo.MafiaTarget != null);
                    }
                }

                if (isNightChoiceIncomplete)
                {
                    await turnContext.SendActivityAsync("Still waiting for some role to choose!");
                    // convInfo.DoctorTarget = "No one";
                    // await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
                }
                else
                {
                    Logger.LogInformation("-----Converation Data. mafia target: {0}, doctor target: {1} -----\n",
                        convInfo.MafiaTarget, convInfo.DoctorTarget);
                    await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
                }
            }

            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            turnContext.Activity.RemoveRecipientMention();
            AddConversationReference(turnContext.Activity as Activity);

            var userStateAccessor = UserState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userInfo = await userStateAccessor.GetAsync(turnContext, () => new UserProfile());

            var convStateAccessor = ConversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var convInfo = await convStateAccessor.GetAsync(turnContext, () => new ConversationData());

            // Exclude the scenario where players start the game by clicking the choiceprompt afterwards
            if (!convInfo.IsGameStarted && turnContext.Activity.Value != null)
            {
                return;
            }

            //bool isActiveMafia = false;
            // bool isNightChoiceIncomplete = false;
            List<string> mafiaIdList = convInfo.RoleToUsers.GetValueOrDefault(Role.Mafia.ToString(), new List<string>());
            if (string.IsNullOrEmpty(userInfo.Id))
            {
               //  isActiveMafia = mafiaIdList.Contains(turnContext.Activity.From.Id);
                // udpate userState
                userInfo.Id = turnContext.Activity.From.Id;
                userInfo.Name = turnContext.Activity.From.Name;
            }
            // else
            // {
            //    isActiveMafia = mafiaIdList.Contains(userInfo.Id);
            // }
            userInfo.IsActive = convInfo.ActivePlayers.Contains(userInfo.Id);
            // isActiveMafia &= userInfo.IsActive;
            // var isMissChose = turnContext.Activity.Value != null &&
            //    turnContext.Activity.Value.ToString().Contains("kill_choice") &&
            //    !isActiveMafia;

            // update the killtarget and other targets in conversation state
            // It's not used for vote_choice in Daytime currently
            // If doctor or detective get killed, just update the 
            /*
            if (turnContext.Activity.Value != null &&
                turnContext.Activity.Value is JObject)
            {
                var activityValue = turnContext.Activity.Value;
                if (activityValue.ToString().Contains("SendbackTo"))
                    isNightChoiceIncomplete = true;

                if (activityValue.ToString().Contains("kill_choice"))
                    convInfo.MafiaTarget = (string)(activityValue as JObject)["kill_choice"];
                else if (activityValue.ToString().Contains("doctor_choice"))
                    convInfo.DoctorTarget = (string)(activityValue as JObject)["doctor_choice"];

                isNightChoiceIncomplete &= !(convInfo.MafiaTarget != null && convInfo.DoctorTarget != null);
            }
            */

            Logger.LogInformation("-----Message Activity. Id: {0}, name: {1}, conversaton: {2}, \t------ conversation count: {3} -----\n",
                turnContext.Activity.From.Id,
                turnContext.Activity.From.Name,
                Tuple.Create(turnContext.Activity.Conversation.Name,
                turnContext.Activity.Conversation.Id),
                string.Join(", ", _conversationReferences.Select(c => $"[{c.Key} -*- {c.Value.Conversation.Name}]")));
            Logger.LogInformation("-----User Profile. Id: {0}, name: {1}, email: {2}, isActive: {3} -----\n", userInfo.Id, userInfo.Name, userInfo.Email, userInfo.IsActive);
            Logger.LogInformation("-----Converation Data. mafia members: {0}, mafia target: {1}, doctor target: {2} -----\n",
                string.Join(" + ", mafiaIdList), convInfo.MafiaTarget, convInfo.DoctorTarget);

            // Logic of deciding when to start the dialog
            if (!userInfo.IsActive && convInfo.IsGameStarted)
            {
                await turnContext.SendActivityAsync("Sorry, you are dead. Please try not doing any operation.");
            }
            // else if (isMissChose)
            // {
            //    await turnContext.SendActivityAsync("Only Mafia should decide who to kill!");
            // }
            // else if (isNightChoiceIncomplete)
            // {
            //    await turnContext.SendActivityAsync("Still waiting for some role to choose!");
            // }
            else
            {
                await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
            }
        }
    }
}
