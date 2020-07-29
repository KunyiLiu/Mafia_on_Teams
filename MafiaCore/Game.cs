using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaCore
{
    public class Game
    {
        public int NumPlayers { get; set; }

        public GameState CurrentState { get; set; }

        public HashSet<Player> ActivePlayers { get; set; }

        public Dictionary<int, Player> PlayerMapping { get; set; }

        public Dictionary<Role, int> RolesToAssign { get; set; }

        public Game()
        {
            CurrentState = GameState.Unassigned;
            NumPlayers = 0;
            ActivePlayers = new HashSet<Player>();
            PlayerMapping = new Dictionary<int, Player>();
            RolesToAssign = new Dictionary<Role, int>
            {
                { Role.Doctor, 1 },
                { Role.Mafia, 1 },
                { Role.Sheriff, 1 }
            };
        }

        /// <summary>
        /// API called to add a new player to the game
        /// </summary>
        /// <param name="player"></param>
        public void AddPlayer(Player player)
        {
            PlayerMapping.Add(player.Id, player);
            NumPlayers++;
        }

        public void InitializeGameBoard()
        {
            Random random = new Random();
            List<Player> inactivePlayers = PlayerMapping.Select(p => p.Value).ToList();

            // TODO: Add some validation to make sure aggregate roles and role counts do not exceed number of players
            foreach (Role role in RolesToAssign.Keys)
            {
                for (int i = 0; i < RolesToAssign[role]; i++)
                {
                    Player playerToModify = inactivePlayers[random.Next(inactivePlayers.Count)];
                    playerToModify.Role = role;
                    playerToModify.Active = true;
                    ActivePlayers.Add(playerToModify);
                    inactivePlayers.Remove(playerToModify);
                }
            }

            foreach (Player player in inactivePlayers)
            {
                player.Role = Role.Civilian;
                player.Active = true;
                ActivePlayers.Add(player);
            }

            CurrentState = GameState.Night;
        }

        public void ExecuteNightPhase()
        {
            foreach (Player player in ActivePlayers)
            {
                if (player.Role != Role.Civilian)
                {
                    player.State = PlayerState.WaitingForPlayerInput;
                }
            }
        }

        public void ExecuteVotingPhase()
        {

        }

        public void SerializeToDatabase()
        {

        }

        public void DeserializeFromDatabase()
        {

        }
    }
}
