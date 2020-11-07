// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
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

        public CancelAndHelpDialog(string id)
            : base(id)
        {
        }

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            if (innerDc.Context.Activity.Type == ActivityTypes.Event)
            {
                Console.WriteLine("++++++++++++++Begin new stack++++++++++");
                var dialogs = innerDc.Dialogs;
                var dc = await dialogs.CreateContextAsync(innerDc.Context);

                DialogSet test = dc.Dialogs;
                if (dc.ActiveDialog == null)
                {
                    await dc.BeginDialogAsync(nameof(MainDialog), cancellationToken);
                }
                else
                {
                    await dc.ContinueDialogAsync(cancellationToken);
                }

                return await innerDc.BeginDialogAsync(nameof(MainDialog));
            }
            

            Console.WriteLine("++++++++++++++++++++++Continue++++++++++");
            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        private async Task<DialogTurnResult> InterruptAsync(DialogContext innerDc, CancellationToken cancellationToken)
        {
            if (innerDc.Context.Activity.Type == ActivityTypes.Message && innerDc.Context.Activity.Text != null)
            {
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