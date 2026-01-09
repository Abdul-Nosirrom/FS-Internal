using System.Text.RegularExpressions;
using FS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FS.UI
{
    // TODO: Figure out TMP sprite asset assignment as right now we've got to manually assign it, in addition, figure out player owner
    // of this - it just gets player 0. Consider multiple players, this breaks as if player 0 is keyboard and 1 is gamepad, itll
    // show keyboard glyphs for both.
    /// <summary>
    /// Helper component to replace custom <input="ActionName"> tags in a TextMeshPro text field with the associated sprite
    /// for the input glyph and ensuring it updates if the controller changes.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Free Skies/UI/Controller Glyph Text")]
    public class ControllerGlyphText : MonoBehaviour
    {
        private TMP_Text m_text;
        private string m_originalText;
        private static readonly Regex inputTagRegex = new Regex(
            @"<input\s*=\s*""([^""]+)""([^>]*)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        private void Start()
        {
            m_text = GetComponent<TMP_Text>();
            if (m_text)
            {
                m_originalText = m_text.text;
                UpdateText();
            }
        }

        private void OnEnable() => PlayerInputSystem.OnControllerChanged += UpdateText;
        private void OnDisable() => PlayerInputSystem.OnControllerChanged -= UpdateText;
        
        private void UpdateText()
        {
            var newText = inputTagRegex.Replace(m_originalText, match =>
            {
                string actionName = match.Groups[1].Value;
                string spriteSettings = match.Groups[2].Value; // Not used currently
                if (InputGlyphsUtility.GetInputGlyph(actionName, out var sprite, out var assetName))
                {
                    //Debug.LogError($"Retrieved Sprite: {sprite.name}", this);
                    return $"<sprite=\"{assetName}\" name=\"{sprite.name}\"{spriteSettings}>";
                }
                return $"<color=\"red\">[(ERROR) INPUT GLYPH NOT FOUND: {actionName}]</color>";
            });

            m_text.text = newText;
        }
    }
}