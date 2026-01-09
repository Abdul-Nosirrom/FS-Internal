using System;
using System.Collections.Generic;
using FS.RuntimeDebug;
using Rewired;
using Sirenix.Utilities;
using TimeUtils;
using UnityEngine.Assertions;
using UnityEngine;
using Object = UnityEngine.Object;


namespace FS.Player
{
    public enum ControllerType
    {
        KeyboardAndMouse,
        Gamepad,
    }

    public enum InputMode
    {
        Game,
        UI
    }
    
#region Input Bootstrapper
    /// <summary>
    /// Initializes the game input system by finding the input manager prefab in the project and instantiating it on game boot
    /// </summary>
    internal static class GameInputInitializer
    {
        private static Rewired.InputManager s_inputManager;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitializeInput()
        {
            var inputManager = Resources.Load<GameObject>("InputConfiguration");
            if (inputManager != null && inputManager.GetComponent<Rewired.InputManager>() != null)
            {
                // Instantiate Input Manager
                var inputManagerObj = Object.Instantiate(inputManager);
                inputManagerObj.name = inputManager.name;
                s_inputManager = inputManagerObj.GetComponent<Rewired.InputManager>();
                Object.DontDestroyOnLoad(inputManagerObj);
                return;
            }
            Assert.IsTrue(false,"Input Manager Prefab not found!");
        }
    }
#endregion

#region Player Input System

    [DefaultExecutionOrder(-500)]
    public class PlayerInputSystem : PlayerSystem
    {
        public static event Action OnControllerChanged;
        
        private Rewired.Player m_inputController;
        public int InputID => m_inputController.id;

        private bool m_isInputEnabled = true;

        public bool IsInputEnabled
        {
            get => m_isInputEnabled && !DebugManager.Instance.DebugFreeCamEnabled;
            set => m_isInputEnabled = value;
        }
        
        public bool IsMoveInputEnabled = true;
        
        private class InputState
        {
            public bool bConsumed;
            public bool bValid;
            public TimeSince m_timeSincePressed;
        }
        
        private readonly List<GameInput> m_buttonActions = new();
        private readonly Dictionary<GameInput, InputState> m_buttonActionRegisters = new();
        
        PlayerActiveControllerChangedDelegate m_activeControllerChangedDelegate;
        
        public override void Initialize()
        {
            m_inputController = ReInput.players.GetPlayer(m_playerData.ID);

            m_buttonActions.Clear();
            var allActions = ReInput.mapping.Actions;
            foreach (var action in allActions)
            {
                if (action.type == InputActionType.Button)
                {
                    m_buttonActions.Add((GameInput)action.id);
                    m_buttonActionRegisters.Add((GameInput)action.id, new InputState());
                }
            }
            
            m_inputController.controllers.AddLastActiveControllerChangedDelegate(OnControllerChangedEvent);
            
            SetInputMode(InputMode.Game);
        }

        public override void Shutdown()
        {
            if (m_inputController != null)
                m_inputController.controllers.RemoveLastActiveControllerChangedDelegate(OnControllerChangedEvent);
        }

        private void Update()
        {
            foreach (GameInput actionID in m_buttonActions)
            {
                // Is this button valid according to rewired? First frame to BufferDown duration
                if (m_inputController.GetButtonDown((int)actionID))
                {
                    // This is the first frame the button was pressed, here's where we want to set its valid state
                    if (m_inputController.GetButton((int)actionID) && m_inputController.GetButtonTimePressed((int)actionID) == 0)
                    {
                        m_buttonActionRegisters[actionID].m_timeSincePressed = 0f;
                        m_buttonActionRegisters[actionID].bValid = true;
                        m_buttonActionRegisters[actionID].bConsumed = false;
                    }
                }
                else
                {
                    m_buttonActionRegisters[actionID].bValid = false;
                }
            }
        }

        private void OnControllerChangedEvent(Rewired.Player player, Controller controller)
        {
            // Ensure we dont trigger an update when going from keyboard <--> mouse as they use theyre the same in our eyes (TODO: Right now, we do)
            OnControllerChanged?.Invoke();
        }

        public InputMode InputMode { get; private set; } = InputMode.Game;
        
        public void SetInputMode(InputMode mode)
        {
            InputMode = mode;

            if (InputMode == InputMode.Game)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (InputMode == InputMode.UI)
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }

            m_inputController.controllers.maps.GetAllMapsInCategory("Default").ForEach(map => map.enabled = mode == InputMode.Game);
            m_inputController.controllers.maps.GetAllMapsInCategory("UI").ForEach(map => map.enabled = mode == InputMode.UI);
        }

        public string GetControllerPlatform()
        {
            // Can be null for first frame before any input is recieved
            var controller = m_inputController.controllers.GetLastActiveController();
            return controller == null ? "NONE" : controller.name;
        }
        
        public ControllerType GetControllerType()
        {
            // Can be null for first frame before any input is recieved
            var controller = m_inputController.controllers.GetLastActiveController();
            if (controller == null) return ControllerType.KeyboardAndMouse;
            return controller.type == Rewired.ControllerType.Joystick ? ControllerType.Gamepad : ControllerType.KeyboardAndMouse;
        }

        public bool IsController => GetControllerType() == ControllerType.Gamepad;
        
        public bool IsValidInput(GameInput actionID) => IsInputEnabled && m_buttonActions.Contains(actionID);

        public void ConsumeInput(GameInput id)
        {
            if (IsValidInput(id)) m_buttonActionRegisters[id].bConsumed = true;
        }
        
        public bool IsConsumed(GameInput id) => IsValidInput(id) && m_buttonActionRegisters[id].bConsumed;

        public bool GetButton(GameInput id) => IsValidInput(id) && m_buttonActionRegisters[id].bValid && !m_buttonActionRegisters[id].bConsumed;
        public bool GetButtonRelease(GameInput id) => IsValidInput(id) && m_inputController.GetButtonUp((int)id);
        
        public double TimeHeld(GameInput id) => IsValidInput(id) ? m_inputController.GetButtonTimePressed((int)id) : 0f;
        
        public Vector2 MoveVector => IsMoveInputEnabled && IsInputEnabled ? m_inputController.GetAxis2D("Move Horizontal", "Move Vertical") : Vector2.zero;
        public Vector2 LookVector => IsInputEnabled ? m_inputController.GetAxis2D("Look Horizontal", "Look Vertical") : Vector2.zero;
    }
#endregion    
}