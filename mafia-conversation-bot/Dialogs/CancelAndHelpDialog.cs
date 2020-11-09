// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    public class CancelAndHelpDialog : ComponentDialog
    {
        private const string HelpMsgText = "This is the help info for MafiaGame Bot. If you have any question, please contact kunyl@micorosoft.com";
        private const string CancelMsgText = "Manually Cancel the whole game.";
        // protected readonly ILogger Logger;
        private readonly ConversationState _conversationState;

        public CancelAndHelpDialog(string id, ConversationState conversationState)
            : base(id)
        {
            _conversationState = conversationState;
        }

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            var convStateAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var convInfo = await convStateAccessor.GetAsync(innerDc.Context, () => new ConversationData());

            DialogSet dialogs = innerDc.Dialogs;
            if (innerDc.Context.Activity.Type == ActivityTypes.Event)
            {
                // var dc = await dialogs.CreateContextAsync(innerDc.Context);
                await innerDc.CancelAllDialogsAsync(cancellationToken);
                Console.WriteLine("++++++++++++++Begin new stack++++++++++" + innerDc.Context.Activity.Value.ToString());
                return await innerDc.BeginDialogAsync(nameof(MainDialog));
            }

            // TODO: Find a btter way to resolve the Diaglog stack issue
            Console.WriteLine("++++++++++++++++++++++Continue++++++++++" + convInfo.IsGameStarted);
            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        private async Task<DialogTurnResult> InterruptAsync(DialogContext innerDc, CancellationToken cancellationToken)
        {
            if (innerDc.Context.Activity.Type == ActivityTypes.Message && innerDc.Context.Activity.Text != null)
            {
                var convStateAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
                var convInfo = await convStateAccessor.GetAsync(innerDc.Context, () => new ConversationData());
                /*
                if (!convInfo.IsGameStarted)
                {
                    await innerDc.CancelAllDialogsAsync(cancellationToken);
                    Console.WriteLine("++++++++++++++++++++++InterruptAsync++++++++++");
                    return await innerDc.BeginDialogAsync(nameof(MainDialog));
                }
                */

                var text = innerDc.Context.Activity.Text.ToLowerInvariant();
                switch (text)
                {
                    case "help":
                    case "?":
                        var helpMessage = MessageFactory.Text(HelpMsgText, HelpMsgText, InputHints.ExpectingInput);
                        await innerDc.Context.SendActivityAsync(helpMessage, cancellationToken);
                        return new DialogTurnResult(DialogTurnStatus.Waiting);

                    case "cancel":
                    case "quit":
                        var cancelMessage = MessageFactory.Text(CancelMsgText, CancelMsgText, InputHints.IgnoringInput);
                        await innerDc.Context.SendActivityAsync(cancelMessage, cancellationToken);
                        return await innerDc.CancelAllDialogsAsync(cancellationToken);
                }
            }

            return null;
        }
    }
}