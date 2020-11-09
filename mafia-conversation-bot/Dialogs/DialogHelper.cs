using MafiaCore;
using MafiaCore.Players;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema.Teams;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples
{

    public class DialogHelper
    {
        public static async Task<List<TeamsChannelAccount>> GetPagedMembers(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            List<TeamsChannelAccount> members = new List<TeamsChannelAccount>();
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

        public static ConversationData ConvertConversationState(Game mafiaGame)
        {
            ConversationData gameData = new ConversationData();

            gameData.ActivePlayers = mafiaGame.ActivePlayers.Select(p => p.Id).ToList();
            foreach (KeyValuePair<string, Player> pair in mafiaGame.PlayerMapping)
            {
                gameData.UserProfileMap.Add(pair.Key, pair.Value.Name);

                var roleName = pair.Value.Role.ToString();
                if (!gameData.RoleToUsers.ContainsKey(roleName))
                {
                    gameData.RoleToUsers[roleName] = new List<string>();
                }
                if (gameData.ActivePlayers.Contains(pair.Key))
                    gameData.RoleToUsers[roleName].Add(pair.Key);
            }
            gameData.IsGameStarted = true;
            gameData.VoteTarget = mafiaGame.ActivePlayers.FirstOrDefault()?.Vote;
            gameData.MafiaTarget = mafiaGame.Mafias.FirstOrDefault()?.Target;
            gameData.DoctorTarget = mafiaGame.Doctors.FirstOrDefault()?.Target;
            gameData.CurrentState = mafiaGame.CurrentState;

            return gameData;
        }
    }
}
