// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.BotBuilderSamples
{
    using MafiaCore.Players;
    using System.Collections.Generic;

    /// <summary>Contains information about a user.</summary>
    public class UserProfile
    {
        public int PlayerCount { get; set; }

        // The list of companies the user wants to review.
        public Dictionary<string, Player> Players { get; set; } = new Dictionary<string, Player>();
    }
}
