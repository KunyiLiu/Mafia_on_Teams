// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            turnContext.Activity.RemoveRecipientMention();

            var accessor = UserState.CreateProperty<Dictionary<string, string>>("Mafia-Group");
            var userInfo = await accessor.GetAsync(turnContext, () => new Dictionary<string, string>());
            Logger.LogInformation("----------------Running dialog with Message Activity. Old Id: {0}, new Id: {1}, test: {2}-----------",
    turnContext.Activity.ReplyToId, turnContext.Activity.Id, string.Join(" + ", userInfo));

            userInfo.TryGetValue(turnContext.Activity.From.Name, out string role);
            // Run the Dialog with the new message Activity.
            var check = turnContext.Activity.Value != null && 
                turnContext.Activity.Value.ToString().Contains("kill_choice") &&
                role != "Mafia";
            if (!check)
            {
                await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
            } else
            {
                await turnContext.SendActivityAsync("Only Mafia should decide who to kill!");
            }
        }
    }
}
