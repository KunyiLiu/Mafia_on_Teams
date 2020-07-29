using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaCore.Players
{
    public class Villager : Player
    {
        public Villager(int id, string name) : base(id, name)
        {

        }

        public Villager(Player player) : base(player)
        {

        }

        public override void DoAction(HashSet<Player> activePlayers)
        {
            // Villager does nothing
        }
    }
}
