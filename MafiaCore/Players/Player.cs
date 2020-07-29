using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaCore.Players
{
    public class Player
    {
        public Role Role
        {
            get; set;
        }

        /// <summary>
        /// The Teams ID of the player
        /// </summary>
        public int Id
        {
            get; set;
        }

        /// <summary>
        /// The friendly name of the player
        /// </summary>
        public string Name
        {
            get; set;
        }

        /// <summary>
        /// Whether the player is still active or not
        /// </summary>
        public bool Active
        {
            get; set;
        }

        /// <summary>
        /// State of the current player, whether waiting for input or game to continue
        /// </summary>
        public PlayerState State
        {
            get; set;
        }

        public int Vote
        {
            get; set;
        }

        public Player(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public Player(Player player)
        {
            Id = player.Id;
            Name = player.Name;
            Role = player.Role;
            Active = player.Active;
            State = player.State;
            Vote = player.Vote;
        }

        public virtual void DoAction(HashSet<Player> activePlayers)
        {
            // Generic player, aka civilian will do nothing
        }
    }
}
