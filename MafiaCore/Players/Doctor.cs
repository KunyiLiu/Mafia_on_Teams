using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaCore.Players
{
    public class Doctor : Villager
    {
        /// <summary>
        /// The Teams ID of the assigned target the player chose to kill
        /// </summary>
        public int Target
        {
            get; set;
        }

        public Doctor(int id, string name) : base(id, name)
        {

        }

        public Doctor(Player player) : base(player)
        {

        }

        public override void DoAction(List<Player> activePlayers)
        {
            foreach (Player player in activePlayers)
            {
                if (player is Mafia)
                {
                    Mafia mafia = (Mafia)player;
                    if (mafia.Target == Target)
                    {
                        mafia.Target = 0; // Reset Mafia target to 0 to nullify the kill
                    }
                }
            }
        }
    }
}
