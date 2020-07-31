using MafiaCore.Players;
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

        public HashSet<Mafia> Mafias { get; set; }

        public HashSet<Doctor> Doctors { get; set; }

        public Dictionary<string, Player> PlayerMapping { get; set; }

        public Dictionary<Role, int> RolesToAssign { get; set; }

        public Game()
        {
            CurrentState = GameState.Unassigned;
            NumPlayers = 0;
            ActivePlayers = new HashSet<Player>();
            Doctors = new HashSet<Doctor>();
            Mafias = new HashSet<Mafia>();
            PlayerMapping = new Dictionary<string, Player>();
            RolesToAssign = new Dictionary<Role, int>
            {
                { Role.Doctor, 1 },
                { Role.Mafia, 1 },
                //{ Role.Sheriff, 1 }
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
            FillRolesToAssign();

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
                    inactivePlayers.Remove(playerToModify);

                    if (role == Role.Mafia)
                    {
                        Mafia mafia = new Mafia(playerToModify);
                        Mafias.Add(mafia);
                        PlayerMapping[playerToModify.Id] = mafia;
                        ActivePlayers.Add(mafia);
                    }
                    else if (role == Role.Doctor)
                    {
                        Doctor doctor = new Doctor(playerToModify);
                        Doctors.Add(doctor);
                        PlayerMapping[playerToModify.Id] = doctor;
                        ActivePlayers.Add(doctor);
                    }
                }
            }

            foreach (Player player in inactivePlayers)
            {
                player.Role = Role.Civilian;
                player.Active = true;
                Villager villager = new Villager(player);
                ActivePlayers.Add(villager);
            }

            ShowRolesToPlayers();
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

            // TODO: async method to get all the input, and after it finishes, execute the following

            ExecutePlayerActions();

            Player eliminatedPlayer = PlayerKilledByMafia();
            ShowNightPhaseResults(eliminatedPlayer);

            ChangeGameState();
        }

        public void ExecuteVotingPhase()
        {
            string playerIdToEliminate = GetVotingResult();
            EliminatePlayer(PlayerMapping[playerIdToEliminate]);
            ChangeGameState();
        }

        private void FillRolesToAssign()
        {
            int numTotalPlayers = PlayerMapping.Count;
            // If less than 8 players, just have 1 mafia and 1 doctor
            if (numTotalPlayers < 4)
            {
                return;
            }
            int numMafiasAndDoctors = numTotalPlayers / 3;
            RolesToAssign[Role.Doctor] = numMafiasAndDoctors;
            RolesToAssign[Role.Mafia] = numMafiasAndDoctors;
        }

        public void ExecuteMafiasWonPhase()
        {
            // TODO: show mafias won and end the game
        }

        public void ExecuteMafiasLostPhase()
        {
            // TODO: show mafias lost and end the game
        }

        public void SerializeToDatabase()
        {

        }

        public void DeserializeFromDatabase()
        {

        }

        private void ExecutePlayerActions()
        {
            foreach (Player player in ActivePlayers)
            {
                player.DoAction(ActivePlayers);
            }
        }

        private Player PlayerKilledByMafia()
        {
            if (Mafias.Count == 0)
            {
                return null;
            }
            // All mafias have the same target
            string mafiasTarget = Mafias.First().Target;
            if (string.IsNullOrEmpty(mafiasTarget))
            {
                return null;
            }
            Player playerToEliminate = PlayerMapping[mafiasTarget];
            EliminatePlayer(playerToEliminate);
            return playerToEliminate;
        }

        private void ShowNightPhaseResults(Player playerKilledByMafia)
        {
            if (playerKilledByMafia == null)
            {
                // TODO: show no one killed
                return;
            }
            // TODO: show player killed
        }

        private void EliminatePlayer(Player playerToEliminate)
        {
            playerToEliminate.Active = false;
            ActivePlayers.Remove(playerToEliminate);

            if (playerToEliminate.Role == Role.Mafia)
            {
                Mafias.Remove((Mafia)playerToEliminate);
                return;
            }
            if (playerToEliminate.Role == Role.Doctor)
            {
                Doctors.Remove((Doctor)playerToEliminate);
            }
        }

        internal bool AllMafiasCaught()
        {
            return Mafias.Count == 0;
        }

        internal bool AllCivilliansEliminated()
        {
            return Mafias.Count >= ActivePlayers.Where(player => player.Role != Role.Mafia).ToList().Count;
        }

        // After Night and Voting phases, change to the appropriate game state
        private void ChangeGameState()
        {
            if (AllMafiasCaught())
            {
                this.CurrentState = GameState.MafiasLost;
            }
            else if (AllCivilliansEliminated())
            {
                this.CurrentState = GameState.MafiasWon;
            }
            else // cycle between night and voting phases
            {
                this.CurrentState = this.CurrentState == GameState.Night
                    ? GameState.Voting
                    : GameState.Night;
            }
        }

        // TODO : implement this method
        private string GetVotingResult()
        {
            return null;
        }

        // TODO: call this function after roles have been assigned to relay information to players
        private void ShowRolesToPlayers()
        {

        }
    }
}