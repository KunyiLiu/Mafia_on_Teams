using System.Collections.Generic;

namespace MafiaCore.Players
{
    public class Doctor : Villager
    {
        /// <summary>
        /// The Teams ID of the assigned target the player chose to heal
        /// </summary>
        public string Target
        {
            get; set;
        }

        public Doctor(string id, string name) : base(id, name)
        {

        }

        public Doctor(Player player) : base(player)
        {
            Role = Role.Doctor;
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