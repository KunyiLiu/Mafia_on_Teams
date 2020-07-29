﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MafiaCore.Players
{
    public class Villager : Player
    {
        public Villager(int id, string name) : base(id, name)
        {

        }

        public override void DoAction(List<Player> activePlayers)
        {
            // Villager does nothing
        }
    }
}
