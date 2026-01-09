using UnityEditor;
using UnityEngine;

namespace FS.Player
{
#if UNITY_EDITOR    
    /// <summary>
    /// Settings object for editor player spawn data. Holds transient spawn location,
    /// prefab overrides, and other settings related to spawn mode overrides for when
    /// entering play mode.
    /// </summary>
    [FilePath("FreeSkies/PlayerSpawnConfig.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class PlayerSpawnConfig : ScriptableSingleton<PlayerSpawnConfig>
    {
        [SerializeField] public Vector3 m_position;
        [SerializeField] public Quaternion m_rotation;
        
        [SerializeField] public bool m_spawnAtCamera;

        protected PlayerSpawnConfig()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DoSave;
            AssemblyReloadEvents.beforeAssemblyReload += DoSave;

            EditorApplication.playModeStateChanged -= OnPlaymodeStateChanged;
            EditorApplication.playModeStateChanged += OnPlaymodeStateChanged;
        }

        private void OnPlaymodeStateChanged(PlayModeStateChange newState)
        {
            if (newState == PlayModeStateChange.ExitingPlayMode)
            {
                m_spawnAtCamera = false;
                DoSave();
            }
        }

        private void DoSave() => Save(true);
    }
#endif    
}