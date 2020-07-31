using System.Collections.Generic;

namespace MafiaCore.Players
{
    public class Villager : Player
    {
        public Villager(string id, string name) : base(id, name)
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