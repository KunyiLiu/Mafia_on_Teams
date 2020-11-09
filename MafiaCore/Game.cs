using MafiaCore.Players;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MafiaCore
{
    public class Game
    {
        public int NumPlayers { get; set; } = 0;

        public GameState CurrentState { get; set; } = GameState.Unassigned;

        public HashSet<Player> ActivePlayers { get; set; } = new HashSet<Player>();

        /// <summary>
        /// Active Mafias
        /// </summary>
        public HashSet<Mafia> Mafias { get; set; } = new HashSet<Mafia>();

        /// <summary>
        /// Active Doctors
        /// </summary>
        public HashSet<Doctor> Doctors { get; set; } = new HashSet<Doctor>();

        public HashSet<Detective> Detectives { get; set; } = new HashSet<Detective>();

        public Dictionary<string, Player> PlayerMapping { get; set; } = new Dictionary<string, Player>();

        private Dictionary<Role, int> _rolesToAssign;
        public Dictionary<Role, int> RolesToAssign 
        {
            get 
            {
                return _rolesToAssign ?? (_rolesToAssign =
                    new Dictionary<Role, int>
                    {
                        { Role.Mafia, 1 },
                        { Role.Doctor, 1 },
                        { Role.Detective, 1 }
                    });
            }
            set
            {
                _rolesToAssign = value;
            }
        }

        public Game()
        {
        }

        public Game(
            Dictionary<string, string> userProfileMap,
            Dictionary<string, List<string>> roleToUsers,
            List<string> activePlayers,
            string mafiaTarget,
            string doctorTarget,
            string voteTarget,
            GameState currentState
            )
        {
            CurrentState = currentState;
            List<string> mafiaIdList;  // all mafias
            List<string> doctorIdList;
            roleToUsers.TryGetValue(Role.Mafia.ToString(), out mafiaIdList);
            roleToUsers.TryGetValue(Role.Doctor.ToString(), out doctorIdList);

            foreach (KeyValuePair<string, string> idWithName in userProfileMap)
            {
                Player newPlayer = new Player(idWithName.Key, idWithName.Value);
                AddPlayer(newPlayer);
                newPlayer.Vote = voteTarget;
                newPlayer.Active = true;

                if (activePlayers.Contains(newPlayer.Id))
                {
                    // TODO: Sheriff
                    if (mafiaIdList != null && mafiaIdList.Contains(newPlayer.Id))
                    {
                        var mafia = new Mafia(newPlayer);
                        mafia.Target = mafiaTarget;
                        Mafias.Add(mafia);
                        PlayerMapping[newPlayer.Id] = mafia;
                        ActivePlayers.Add(mafia);
                    }
                    else if (doctorIdList != null && doctorIdList.Contains(newPlayer.Id))
                    {
                        var doctor = new Doctor(newPlayer);
                        doctor.Target = doctorTarget;
                        Doctors.Add(doctor);
                        PlayerMapping[newPlayer.Id] = doctor;
                        ActivePlayers.Add(doctor);
                    }
                    else
                    {
                        var village = new Villager(newPlayer);
                        PlayerMapping[newPlayer.Id] = village;
                        ActivePlayers.Add(village);
                    }
                }
                else
                {
                    newPlayer.Active = false;
                    PlayerMapping[newPlayer.Id] = newPlayer;
                }
            }
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
                if (inactivePlayers.Count == 0) break;
                for (int i = 0; i < RolesToAssign[role]; i++)
                {
                    //remove later
                    if (role == Role.Mafia && i == 0)
                    {
                        foreach (Player p in inactivePlayers)
                        {
                            if (p.Name == "Kunyi Liu")
                            {
                                p.Role = role;
                                p.Active = true;
                                inactivePlayers.Remove(p);

                                Mafia mafia = new Mafia(p);
                                Mafias.Add(mafia);
                                PlayerMapping[p.Id] = mafia;
                                ActivePlayers.Add(mafia);
                                break;
                            }
                        }
                        continue;
                    }
                    if (role == Role.Detective)
                    {
                        foreach (Player p in inactivePlayers)
                        {
                            if (p.Name == "Nanhua Jin")
                            {
                                p.Role = role;
                                p.Active = true;
                                inactivePlayers.Remove(p);

                                Detective detective = new Detective(p);
                                Detectives.Add(detective);
                                PlayerMapping[p.Id] = detective;
                                ActivePlayers.Add(detective);
                                break;
                            }
                        }
                        continue;
                    }
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
                    else if (role == Role.Detective)
                    {
                        Detective detective = new Detective(playerToModify);
                        Detectives.Add(detective);
                        PlayerMapping[playerToModify.Id] = detective;
                        ActivePlayers.Add(detective);
                    }
                }
            }

            foreach (Player player in inactivePlayers)
            {
                player.Role = Role.Villager;
                player.Active = true;
                Villager villager = new Villager(player);
                PlayerMapping[player.Id] = villager;
                ActivePlayers.Add(villager);
            }

            CurrentState = GameState.Night;
        }

        public void ExecuteNightPhase()
        {
            //  PlayerState can be removed?
            foreach (Player player in ActivePlayers)
            {
                if (player.Role != Role.Villager)
                {
                    player.State = PlayerState.WaitingForPlayerInput;
                }
            }
            // TODO: async method to get all the input, and after it finishes, execute the following

            ExecutePlayerActions();

            Player eliminatedPlayer = PlayerKilledByMafia();
            ShowNightPhaseResults(eliminatedPlayer);

            ChangeGameState();
            AssignTargetToPlayers(null, Role.Mafia);
            AssignTargetToPlayers(null, Role.Doctor);
            AssignTargetToPlayers(null, Role.Detective);
        }

        public void ExecuteVotingPhase()
        {
            VoteOutPlayer();
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
            RolesToAssign[Role.Doctor] = 0;
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

        public void AssignTargetToPlayers(string targetId, Role playerRole) 
        {
            if (!ActivePlayers.Select(p => p.Id).Contains(targetId)) targetId = null;

            if (playerRole == Role.Mafia)
            {
                foreach (Mafia mafia in Mafias) mafia.Target = targetId;
            }
            else if (playerRole == Role.Doctor)
            {
                foreach (Doctor doctor in Doctors) doctor.Target = targetId;
            }
            else if (playerRole == Role.Detective)
            {
                foreach (Detective detective in Detectives) detective.Target = targetId;
            }
        }

        public void AssignVoteToPlayers(string voteId)
        {
            // If the vote is not within active players, assign null to the Vote property
            if (!ActivePlayers.Select(p => p.Id).Contains(voteId)) voteId = null;

            foreach (Player player in ActivePlayers)
            {
                player.Vote = voteId;
            }
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

        private void VoteOutPlayer()
        {
            // All players have the same vote
            string playerVote = ActivePlayers.FirstOrDefault()?.Vote;
            if (string.IsNullOrEmpty(playerVote)) return;

            Player playerToEliminate = PlayerMapping[playerVote];
            EliminatePlayer(playerToEliminate);
        }
    }
}