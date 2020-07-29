using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaCore.Players
{
    public class Mafia : Player
    {
        /// <summary>
        /// The Teams ID of the assigned target the player chose to kill
        /// </summary>
        public int Target
        {
            get; set;
        }

        public Mafia(int id, string name) : base(id, name)
        {

        }

        public Mafia(Player player) : base (player)
        {

        }
    }
}
