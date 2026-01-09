using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace FS.Player
{
    public class PlayerData
    {
        /// <summary>
        /// Unique integer identifier for this player
        /// </summary>
        public readonly int ID;

        /// <summary>
        /// Scene where the player's GameObject and subsystems are located
        /// </summary>
        public readonly Scene Scene;
        
        /// <summary>
        /// Instance of the player prefab that was spawned
        /// </summary>
        public readonly GameObject Instance;
        
        /// <summary>
        /// Collection of all PlayerSystems associated with this player
        /// </summary>
        public Dictionary<Type, PlayerSystem> Subsystems { get; }
            
        //public bool IsInitialized { get; private set; }

        internal static PlayerData CreatePlayer(int playerID, string sceneName, GameObject playerInstance, 
            HashSet<Type> subsystemTypes)
        {
            var scene = SceneManager.CreateScene(sceneName);

            SceneManager.MoveGameObjectToScene(playerInstance, scene);
            
            var playerData = new PlayerData(playerID, scene, playerInstance, subsystemTypes);
            return playerData;
        }
        
        
            
        private PlayerData(int playerID, Scene scene, GameObject instance, HashSet<Type> subsystemTypes)
        {
            ID = playerID;
            Scene = scene;
            Instance = instance;
            Subsystems = new Dictionary<Type, PlayerSystem>();
            
            foreach (var subsystemType in subsystemTypes)
            {
                CreatePlayerSystem(subsystemType);
            }
            
        }

        internal void Initialize()
        {
            foreach (var subsystem in Subsystems)
            {
                subsystem.Value.Initialize();
            }
        }

        internal void Shutdown()
        {
            foreach (var subsystem in Subsystems)
            {
                subsystem.Value.Shutdown();
            }
            
            SceneManager.UnloadSceneAsync(Scene);
        }

        private void CreatePlayerSystem(Type t)
        {
            var subsystemObj = new GameObject(t.Name);
            var subsystem = subsystemObj.AddComponent(t) as PlayerSystem;
                
            Assert.IsNotNull(subsystem, $"Failed to add subsystem of type {t.Name} to player {ID}");
            subsystem.InitializeInternal(this);
                
            Subsystems.Add(t, subsystem);
            SceneManager.MoveGameObjectToScene(subsystemObj, Scene);
        }
        
    }
}