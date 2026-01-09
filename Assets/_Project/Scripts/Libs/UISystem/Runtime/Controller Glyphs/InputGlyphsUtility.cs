using FS.Patterns;
using UnityEngine;
using FS.Player;
using TMPro;

namespace FS.UI
{
    /// <summary>
    /// Utility class for fetching input glyphs for actions
    /// </summary>
    public static class InputGlyphsUtility
    {
        private static ControllerGlyphs[] s_glyphsMap;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            s_glyphsMap = Resources.LoadAll<ControllerGlyphs>("");
        }

        public static bool GetInputGlyph(string actionName, out Sprite glyph, out string spriteAssetName, GameObject player) =>
            GetInputGlyph(actionName, out glyph, out spriteAssetName, PlayerManager.GetSystem<PlayerInputSystem>(player));

        public static bool GetInputGlyph(string actionName, out Sprite glyph, out string spriteAssetName, int playerID = 0) =>
            GetInputGlyph(actionName, out glyph, out spriteAssetName, PlayerManager.GetSystem<PlayerInputSystem>(playerID));

        private static bool GetInputGlyph(string actionName, out Sprite glyph, out string spriteAssetName, PlayerInputSystem playerInput)
        {
            glyph = null;
            spriteAssetName = null;

            if (playerInput == null) return false;
            
            ControllerType controllerType = playerInput.GetControllerType();
            string hardwareType = playerInput.GetControllerPlatform();

            ControllerGlyphs bestGlyphOption = null;
            foreach (var glyphs in s_glyphsMap)
            {
                if (!glyphs.IsValidGlyphProvider(hardwareType, controllerType))
                {
                    //Debug.LogError($"[UI] GlyphsManager: ControllerType {controllerType} with hardware {hardwareType} is not supported by {glyphs.name}");
                    continue;
                }
                bestGlyphOption = glyphs;
            }

            if (bestGlyphOption)
            {
                spriteAssetName = bestGlyphOption.TextMeshSpriteAsset.name;
                return bestGlyphOption.GetGlyphForAction(actionName, out glyph, playerInput.InputID);
            }
            return false;
        }
    }
}