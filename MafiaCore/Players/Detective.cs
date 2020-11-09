using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaCore.Players
{
    public class Detective : Villager
    {
        /// <summary>
        /// The Teams ID of the assigned target the player chose to inspect
        /// </summary>
        public string Target
        {
            get; set;
        }

        public Detective(string id, string name) : base(id, name)
        {

        }

        public Detective(Player player) : base(player)
        {
            Role = Role.Detective;
        }

        public override void DoAction(HashSet<Player> activePlayers)
        {
            foreach (Player player in activePlayers)
            {
                if (player is Mafia)
                {
                    Mafia mafia = (Mafia)player;
                    if (mafia.Target == Target)
                    {
                        mafia.Target = null; // Reset Mafia target to 0 to nullify the kill
                    }
                }
            }
        }
    }
}