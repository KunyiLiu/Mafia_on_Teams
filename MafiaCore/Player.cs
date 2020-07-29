using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaCore
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
        /// The Teams ID of the assigned target the player chose
        /// </summary>
        public int Target
        {
            get; set;
        }

        public Player(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
