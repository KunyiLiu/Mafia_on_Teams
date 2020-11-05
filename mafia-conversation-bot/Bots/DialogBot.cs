// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MafiaCore;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

        public DialogBot(IConfiguration config, ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
        {
            _appId = config["MicrosoftAppId"];
            _appPassword = config["MicrosoftAppPassword"];
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            Logger.LogInformation("======Activity Type: {0}, Text: {1} =========\n", turnContext.Activity.Type, turnContext.Activity.Text);
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            turnContext.Activity.RemoveRecipientMention();

            var userStateAccessor = UserState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userInfo = await userStateAccessor.GetAsync(turnContext, () => new UserProfile());

            var convStateAccessor = ConversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var convInfo = await convStateAccessor.GetAsync(turnContext, () => new ConversationData());

            bool isActiveMafia = false;
            List<string> mafiaIdList = convInfo.RoleToUsers.GetValueOrDefault(Role.Mafia.ToString(), new List<string>());
            if (string.IsNullOrEmpty(userInfo.Id))
            {
                isActiveMafia = mafiaIdList.Contains(turnContext.Activity.From.Id);
                // udpate userState
                userInfo.Id = turnContext.Activity.From.Id;
                userInfo.Name = turnContext.Activity.From.Name;
            }
            else
            {
                isActiveMafia = mafiaIdList.Contains(userInfo.Id);
            }
            userInfo.IsActive = convInfo.ActivePlayers.Contains(userInfo.Id);
            isActiveMafia &= userInfo.IsActive;

            Logger.LogInformation("-----Message Activity. Id: {0}, name: {1}, conversaton: {2} -----\n",
                turnContext.Activity.From.Id, turnContext.Activity.From.Name, Tuple.Create(turnContext.Activity.Conversation.Name, turnContext.Activity.Conversation.Id));
            Logger.LogInformation("-----User Profile. Id: {0}, name: {1}, email: {2}, isActive: {3} -----\n", userInfo.Id, userInfo.Name, userInfo.Email, userInfo.IsActive);
            Logger.LogInformation("-----Converation Data. mafia members: {0} -----\n", string.Join(" + ", mafiaIdList));

            // Run the Dialog with the new message Activity.
            var isMissChose = turnContext.Activity.Value != null && 
                turnContext.Activity.Value.ToString().Contains("kill_choice") &&
                !isActiveMafia;
            if (!userInfo.IsActive && convInfo.IsGameStarted)
            {
                await turnContext.SendActivityAsync("Sorry, you are dead. Please try not doing any operation.");
            }
            else if (isMissChose)
            {
                await turnContext.SendActivityAsync("Only Mafia should decide who to kill!");
            }
            else
            {
                await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
            }
        }
    }
}
