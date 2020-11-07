// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples
{
    // This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
    // implementation at runtime. Multiple different IBot implementations running at different endpoints can be
    // achieved by specifying a more specific type for the bot constructor argument.
    [ApiController]
    public class BotController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly IBot _bot;
        private readonly ILogger<BotController> _logger;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversations;
        private readonly string _appId;

        public BotController(
            IBotFrameworkHttpAdapter adapter,
            IBot bot, ILogger<BotController> logger,
            ConcurrentDictionary<string, ConversationReference> conversationReferences,
            IConfiguration configuration
            )
        {
            _adapter = adapter;
            _bot = bot;
            _logger = logger;
            _conversations = conversationReferences;
            _appId = configuration["MicrosoftAppId"];
        }

        [HttpPost("api/messages")]
        public async Task PostAsync()
        {
            // Delegate the processing of the HTTP POST to the adapter.
            // The adapter will invoke the bot.
            await _adapter.ProcessAsync(Request, Response, _bot);
        }

        [HttpGet("events/sendback")]
        public async Task<InvokeResponse> GetTest()
        {
            // var conversation = _conversations.Values.Where(c => c.Conversation.Id == "19:f854f14bb6174faebea252e028da8972@thread.v2").FirstOrDefault();
            var conversation = _conversations.Values.FirstOrDefault();

            if (conversation == null)
            {
                _logger.LogInformation("++++++++++++++EVENTS REQUEST Conversation Count: {0} +++++++++++", _conversations.Count);
                return new InvokeResponse{ Status = 404 };
            }

            await ((BotFrameworkAdapter)_adapter).ContinueConversationAsync(_appId, conversation, async (context, token) =>
            {
                context.Activity.Value = new JObject(
                    new JProperty("kill_choice", "No one"));
                context.Activity.ChannelId = conversation.ChannelId;

                await _bot.OnTurnAsync(context, token);
            }, default(CancellationToken));

            _logger.LogInformation("++++++++++++++EVENTS REQUEST +++++++++++");

            return new InvokeResponse{ Status = 200 };
        }
    }
}