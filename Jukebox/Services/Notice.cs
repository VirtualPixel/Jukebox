using UnityEngine;

namespace Jukebox.Services
{
    // One line on the HUD in the game's own focus text, white with the yellow flash like "Buy
    // stuff in the shop". The HUD only exists in the truck, a level and the shop, so a line
    // that comes up in a menu or a loading screen waits for it.
    internal static class Notice
    {
        private static string? waiting;

        public static void Show(string line)
        {
            waiting = line;
            Tick();
        }

        public static void Tick()
        {
            if (waiting == null || !MissionUI.instance)
            {
                return;
            }
            Color flash = AssetManager.instance ? AssetManager.instance.colorYellow : new Color(1f, 0.55f, 0f);
            SemiFunc.UIFocusText(waiting, Color.white, flash);
            waiting = null;
        }
    }
}
