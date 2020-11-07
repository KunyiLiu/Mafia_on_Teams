// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.BotBuilderSamples
{
    // Defines a state property used to track conversation data.
    public class ConversationData
    {
        /// <summary>
        /// Map between user ID to userName
        /// </summary>
        public Dictionary<string, string> UserProfileMap { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, List<string>> RoleToUsers { get; set; } = new Dictionary<string, List<string>>();

        public List<string> ActivePlayers { get; set; } = new List<string>();

        // Is the current round of game started
        public bool IsGameStarted { get; set; } = false;

        public string MafiaTarget { get; set; }
        public string DoctorTarget { get; set; }
    }
}
