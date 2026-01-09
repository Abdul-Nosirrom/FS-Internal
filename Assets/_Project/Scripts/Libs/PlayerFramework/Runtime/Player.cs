using System;
using UnityEngine;

namespace FS.Player
{
    /// <summary>
    /// Essentially a marker class for the player prefab. Must be on the root component of a player
    /// Late execution order to ensure that components have had their Awake methods called before systems are initialized.
    /// Perhaps later we can change it so systems are initialized before components, but some stuff like the CameraController
    /// initializes its camera state in Awake, and the player camera system fetches it during Initialize.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class Player : MonoBehaviour
    {
        public PlayerData PlayerData { get; private set; }
        private void Awake()
        {
            PlayerData = PlayerManager.Instance.AddPlayer(gameObject);
        }
    }

    // Player Only Attribute, disallowing a component from being added to anything other than a player
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class PlayerOnlyAttribute : Attribute
    {}
}