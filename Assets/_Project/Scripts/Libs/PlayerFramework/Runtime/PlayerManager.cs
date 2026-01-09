using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FS.GameService.Attributes;
using FS.Extensions;
using Rewired;
using UnityEngine.Assertions;
using FS.GameService;
using UnityEditor;


namespace FS.Player
{
    public static class PlayerUtils
    {
        public static T GetPlayerSystem<T>(this GameObject playerObject) where T : PlayerSystem => PlayerManager.GetSystem<T>(playerObject);
        public static T GetPlayerSystem<T>(this MonoBehaviour playerComp) where T : PlayerSystem => PlayerManager.GetSystem<T>(playerComp.gameObject);
    }
    
    
    /// <summary>
    /// Manages the lifecycle, operation, and interaction of player subsystems.
    /// Responsible for adding and removing players, initializing subsystems for specific players, and
    /// providing access to specific types of subsystems associated with a player.
    /// This class is auto-registered during game startup using an early registration order and
    /// implements the ISubsystem interface.
    /// </summary>
    [AutoRegisteredService(EGameServiceRegistrationOrder.Early)]
    public sealed class PlayerManager : IGameService
    {
        public static PlayerManager Instance => GameServiceLocator.Get<PlayerManager>();
        public static T GetSystem<T>(int playerIndex) where T : PlayerSystem => Instance.GetSubsystem<T>(playerIndex);
        public static T GetSystem<T>(GameObject playerObject) where T : PlayerSystem => Instance.GetSubsystem<T>(playerObject);
        
        public delegate void PlayerCountDelegate(int playerID);

        public event PlayerCountDelegate OnPlayerAdded;
        public event PlayerCountDelegate OnPlayerRemoved;
        
        // NOTE: 05/01/2024 We might not need to keep an integer player ID, instead GetPlayer(int i) [i] is the 2nd player in the list. If player 2 is removed, the list isn't shrunk but that element is just made to null
        private readonly List<PlayerData> m_players = new();
        private readonly HashSet<Type> m_subsystemTypes = new();
        
        public PlayerData[] Players => m_players.ToArray();
        
        public int PlayerCount => m_players.Count;
        public int ActivePlayerCount => m_players.Count(p => p != null);
        
        public void Initialize()
        {
            Shutdown();
            
            // Cache all PlayerSubsystem types through reflection, happens only once on game startup
            var subsystemTypes = ReflectionUtility.GetAllDerivedTypes<PlayerSystem>();
            m_subsystemTypes.UnionWith(subsystemTypes);
        }

        public void Shutdown()
        {
            for (int i = PlayerCount - 1; i >= 0; i--)
            {
                RemovePlayer(i);
            }
            
            m_players.Clear();
        }
        
        public PlayerData AddPlayer(GameObject playerInstance, string sceneName = null)
        {
            int playerIndex = m_players.FindIndex(p => p == null);
            if (playerIndex == -1) playerIndex = PlayerCount;
            
            // Do we have enough controllers for another player?
            Assert.IsTrue(ReInput.players.playerCount >= PlayerCount, "Player count exceeds available controllers.");
            
            // Create a unique scene name if none provided
            sceneName ??= $"Player_{playerIndex}";
            
            // Instantiate player in this new scene then create the subsystems in that scene
            var playerData = PlayerData.CreatePlayer(playerIndex, sceneName, playerInstance, m_subsystemTypes);
            
            playerData.Initialize();
            
            m_players.Insert(playerIndex, playerData);
                
            OnPlayerAdded?.Invoke(PlayerCount - 1);
            
#if UNITY_EDITOR
            if (PlayerSpawnConfig.instance.m_spawnAtCamera)
            {
                var pos = PlayerSpawnConfig.instance.m_position;
                var rot = PlayerSpawnConfig.instance.m_rotation;
                var physics = playerInstance.GetComponentInChildren<PhysicsController>();
                if (physics != null)
                    physics.SetPositionAndRotation(pos, rot);
                else
                    playerInstance.transform.SetPositionAndRotation(pos, rot);
            }
#endif            

            return playerData;
        }

        public bool RemovePlayer(int playerIndex)
        {
            if (!GetPlayer(playerIndex, out var playerData)) return false;
            
            playerData.Shutdown();

            m_players[playerIndex] = null;
        
            
            OnPlayerRemoved?.Invoke(playerIndex);

            return true;
        }

        public bool GetPlayer(int playerIndex, out PlayerData playerData)
        {
            Assert.IsFalse(playerIndex < 0 || playerIndex >= PlayerCount, $"Attempted to get player {playerIndex} which does not exist.");

            playerData = m_players[playerIndex];

            return playerData != null;
        }

        public bool GetPlayer(GameObject playerObject, out PlayerData playerData)
        {
            Assert.IsNotNull(playerObject, "Attempted to get player from null GameObject");
            
            playerData = null;
            while (playerObject != null)
            {
                playerData = m_players.FirstOrDefault(p => p.Instance == playerObject);
                if (playerData != null) return true;
                
                playerObject = playerObject.transform.parent?.gameObject;
            }

            return false;
        }

        public T GetSubsystem<T>(int playerIndex) where T : PlayerSystem
        {
            if (!GetPlayer(playerIndex, out var playerData)) return null;

            return playerData.Subsystems.TryGetValue(typeof(T), out var subsystem) 
                ? subsystem as T 
                : null;
        }

        public T GetSubsystem<T>(GameObject playerObject) where T : PlayerSystem
        {
            if (!GetPlayer(playerObject, out var playerData)) return null;
            
            return playerData.Subsystems.TryGetValue(typeof(T), out var subsystem) 
                ? subsystem as T 
                : null;
        }
    }
    
    public abstract class PlayerSystem : MonoBehaviour
    { 
        protected PlayerData m_playerData { get; private set; }

        internal void InitializeInternal(PlayerData playerData)
        {
            m_playerData = playerData;
        }
        
        public abstract void Initialize();
        public abstract void Shutdown();
    }
}