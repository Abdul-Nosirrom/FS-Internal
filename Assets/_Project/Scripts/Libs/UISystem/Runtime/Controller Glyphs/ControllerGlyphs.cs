using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;
using TMPro;
using UnityEngine;
using ControllerType = FS.Player.ControllerType;

namespace FS.UI
{
    
    [Serializable]
    public struct GlyphKey
    {
        /// <summary>
        /// ID For either the raw-input (gamepad button, axis, keyboard key, mouse button, etc...)
        /// Dropdown for given controller selection then controllerGlyphs will fill out the array with all the keys for the given controller
        /// </summary>
        public string m_physicalInputID;
        /// <summary>
        /// Sprite representing the glyph for the given keyID
        /// </summary>
        public Sprite m_glyph;
    }
    
    /// <summary>
    /// Data object to store key-to-glyph mappings for different controllers.
    /// Then through a seperate class that stores these, we can request glyphs/sprites based on a given input action,
    /// i.e ("GetGlyph("Jump")") to return the correct glyph for the assigned button for the action. Controller hardware
    /// is handled automatically by getting the hardwareTypeGuid for the given player controller.
    /// </summary>
    [CreateAssetMenu(fileName = "ControllerGlyphs", menuName = "FS/UI/Controller Glyphs", order = 0)]
    public class ControllerGlyphs : ScriptableObject
    {
        public TMP_SpriteAsset TextMeshSpriteAsset;
        public ControllerType ControllerType = ControllerType.Gamepad;
        public GlyphKey[] GlyphKeys;

        public bool IsJoystickFallback;
        
        [SerializeField] private string[] m_hardwareSupported;

        public bool IsValidGlyphProvider(string hwdName, ControllerType controllerType)
        {
            return controllerType == ControllerType && (controllerType != ControllerType.Gamepad || IsJoystickFallback || Array.Exists(m_hardwareSupported, item => item == hwdName));
        }
        
        public bool GetGlyphForAction(string actionName, out Sprite sprite, int playerID = 0)
        {
            sprite = null;
            
            var player = ReInput.players.GetPlayer(playerID);
            if (player == null)
            {
                Debug.LogError($"[UI] Could not find player with ID '{playerID}'");
                return false;
            }
            var action = ReInput.mapping.GetAction(actionName);
            if (action == null)
            {
                Debug.LogError($"[UI] Could not find action named '{actionName}'");
                return false;
            }

            ActionElementMap actionElementMap = null;
            
            if (ControllerType == ControllerType.KeyboardAndMouse)
                actionElementMap = player.controllers.maps.GetFirstElementMapWithAction(Rewired.ControllerType.Keyboard, action.id, false)
                    ?? player.controllers.maps.GetFirstElementMapWithAction(Rewired.ControllerType.Mouse, action.id, false);
            else if (ControllerType == ControllerType.Gamepad)
                actionElementMap = player.controllers.maps.GetFirstElementMapWithAction(Rewired.ControllerType.Joystick, action.id, false);
            
            if (actionElementMap == null)
            {
                Debug.LogError($"[UI] Could not find actionElementMap for action '{actionName}' on controller type '{ControllerType}'");
                return false;
            }

            int elementID = actionElementMap.elementIndex;//elementIdentifierId;
            var controller = actionElementMap.controllerMap.controller;
            if (controller != null && controller.templateCount > 0) // This properly gets the correct element ID for any controller by accessing it through the generic template
            {
                List<ControllerTemplateElementTarget> elements = new();
                elementID = controller.Templates[0].GetElementTargets(actionElementMap, elements);
                if (elements.Count > 0)
                    elementID = elements[0].element.id;
            }

            string elementIdentifierName = actionElementMap.elementIdentifierName;
            if (ControllerType == ControllerType.Gamepad && !JoystickElementIdentifiers.TryGetValue(elementID, out elementIdentifierName))
            {
                Debug.LogError($"[UI] Could not find joystick element identifier name for ID '{actionElementMap.elementIdentifierName}/{elementID}'");
                return false;
            }
            
            foreach (var glyphKey in GlyphKeys)
            {
                if (glyphKey.m_physicalInputID.Equals(elementIdentifierName))
                {
                    sprite = glyphKey.m_glyph;
                    return true;
                }
            }
            
            Debug.LogError($"[UI] Could not find glyph for action '{actionName}' with element name '{elementIdentifierName}'");

            return false;
        }
        
#if UNITY_EDITOR
        
        private void OnValidate()
        {
            string[] keyIDs;
            switch (ControllerType)
            {
                case ControllerType.KeyboardAndMouse:
                    // Remove keycode for numerics [Alpha0 - Alpha9] as these are the same as the top-row numbers [keypad0-keypad9]. All should just be regular numerics
                    var baseKeyIDs = MouseElements.Concat(Enum.GetNames(typeof(KeyboardKeyCode))).ToList();
                    for (int i = baseKeyIDs.Count - 1; i >= 0; i--)
                    {
                        var keyName = baseKeyIDs[i];
                        if (keyName.Contains("Alpha") || keyName.Contains("Keypad"))
                            baseKeyIDs.RemoveAt(i);
                    }
                    for (int i = 0; i <= 9; i++)
                        baseKeyIDs.Add(i.ToString());
                    keyIDs = baseKeyIDs.ToArray();
                    break;
                case ControllerType.Gamepad:
                    keyIDs = JoystickElementIdentifiers.Values.ToArray(); break;
                default:
                    Debug.LogError("Controller Type Not Supported!"); return;
            }
            
            if (GlyphKeys.Length != keyIDs.Length)
            {
                Array.Resize(ref GlyphKeys, keyIDs.Length);
            }
            
            for (int i = 0; i < keyIDs.Length; i++)
            {
                GlyphKeys[i].m_physicalInputID = keyIDs[i];
            }
        }
        
#endif
        
        public static readonly Dictionary<int, string> JoystickElementIdentifiers = new Dictionary<int, string>()
        {
            { GamepadTemplate.elementId_leftStickX, "Left Stick X" },
            { GamepadTemplate.elementId_leftStickY, "Left Stick Y" },
            { GamepadTemplate.elementId_rightStickX, "Right Stick X" },
            { GamepadTemplate.elementId_rightStickY, "Right Stick Y" },
            { GamepadTemplate.elementId_a, "Button South" }, // Action bottom row 1
            { GamepadTemplate.elementId_b, "Button East" }, // Action bottom row 2
            { GamepadTemplate.elementId_x, "Button West" }, // Action top row 1
            { GamepadTemplate.elementId_y, "Button North" }, // Action top row 2
            { GamepadTemplate.elementId_leftBumper, "Left Shoulder" }, // Left Shoulder 1
            { GamepadTemplate.elementId_leftTrigger, "Left Trigger" }, // Left Shoulder 2
            { GamepadTemplate.elementId_rightBumper, "Right Shoulder" }, // Right Shoulder 1
            { GamepadTemplate.elementId_rightTrigger, "Right Trigger" }, // Right Shoulder 2
            { GamepadTemplate.elementId_back, "Select" }, // Center 1
            { GamepadTemplate.elementId_start, "Start" }, // Center 2
            { GamepadTemplate.elementId_leftStickButton, "Left Stick Button" },
            { GamepadTemplate.elementId_rightStickButton, "Right Stick Button" },
            { GamepadTemplate.elementId_dPadUp, "DPad Up" },
            { GamepadTemplate.elementId_dPadRight, "DPad Right" },
            { GamepadTemplate.elementId_dPadDown, "DPad Down" },
            { GamepadTemplate.elementId_dPadLeft, "DPad Left" },
            { GamepadTemplate.elementId_leftStick, "Left Stick" },
            { GamepadTemplate.elementId_rightStick, "Right Stick" },
            { GamepadTemplate.elementId_dPad, "DPad" }
        };
        
        public static readonly string[] MouseElements = new[]
        {
            "Mouse Horizontal", "Mouse Vertical", "Mouse Wheel", "Mouse Wheel Horizontal",
            "Left Mouse Button", "Right Mouse Button",
            "Mouse Button 3", "Mouse Button 4", "Mouse Button 5", "Mouse Button 6", "Mouse Button 7"
        };
    }
}