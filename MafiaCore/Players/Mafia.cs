using System.Collections.Generic;

namespace MafiaCore.Players
{
    public class Mafia : Player
    {
        /// <summary>
        /// The Teams ID of the assigned target the player chose to kill
        /// </summary>
        public string Target
        {
            get; set;
        }

        public Mafia(string id, string name) : base(id, name)
        {

        }

        public Mafia(Player player) : base(player)
        {

        }

        public override void DoAction(HashSet<Player> activePlayers)
        {
            // with current logic, mafia also doesn't do anything
        }
    }
}