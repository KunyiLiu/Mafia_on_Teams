﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    public class MainDialog : ComponentDialog
    {
        private readonly UserState _userState;
        protected readonly ILogger Logger;

        // Define value names for values tracked inside the dialogs.
        private const string UserInfo = "value-userInfo";

        public MainDialog(UserState userState, ILogger<MainDialog> logger)
            : base(nameof(MainDialog))
        {
            _userState = userState;

            AddDialog(new GameRoundDialog());

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                AssignRoleStepAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
            Logger = logger;
        }

        private async Task<DialogTurnResult> AssignRoleStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var members = await GetPagedMembers(stepContext.Context, cancellationToken);
            if (members.Count <= 0)
            {
                await stepContext.Context.SendActivityAsync("Not enought playes.");
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            await stepContext.Context.SendActivityAsync("The game starts, assigning roles");

            var dict = new Dictionary<string, string> () {
                { "kunyl", "Civilian" },
                { "Supratik", "Civilian" },
                { "Nanhua", "Civilian" },
                { "Yogesh", "Mafia" }
            };

            var userInfo  = new UserProfile() { PlayerCount = 4,  Players = dict };
            // TODO: Create Group Chat for Mafia
            return await stepContext.BeginDialogAsync(nameof(GameRoundDialog), userInfo.Players, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var message = (string)stepContext.Result;

            string status = "The game ends, " + message + "!";

            await stepContext.Context.SendActivityAsync(status);

            var accessor = _userState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userInfo = new UserProfile();
            await accessor.SetAsync(stepContext.Context, userInfo, cancellationToken);

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        private static async Task<List<TeamsChannelAccount>> GetPagedMembers(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var members = new List<TeamsChannelAccount>();
            string continuationToken = null;

            do
            {
                var currentPage = await TeamsInfo.GetPagedMembersAsync(turnContext, 100, continuationToken, cancellationToken);
                continuationToken = currentPage.ContinuationToken;
                members = members.Concat(currentPage.Members).ToList();
            }
            while (continuationToken != null);

            return members;
        }
    }
}