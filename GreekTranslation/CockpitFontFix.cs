using HarmonyLib;
using OWML.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GreekTranslation
{
    /// <summary>
    /// Makes the ship's cockpit console render Greek.
    ///
    /// PROVEN CAUSE. ConsoleDisplay is a world-space canvas with scaleFactor 370. Unity's
    /// Text.pixelsPerUnit returns canvas.scaleFactor when the font is DYNAMIC, and
    /// font.fontSize / fontData.fontSize when it is not. Vanilla puts the non-dynamic
    /// VCR_OSD_MONO on this canvas, so it never sees the 370. Our Greek font is dynamic, so the
    /// text generator gets asked for 39 * 370 px glyphs and returns degenerate ones.
    /// Evidence: every collapsed Text.preferredWidth in the probe log is an exact integer over
    /// 370 (3474/370, 786/370, 10980/370, 7985/370, 4579/370, 6019/370, 8535/370, 5999/370,
    /// 10163/370), while every healthy one is a plain integer.
    ///
    /// Two fixes here:
    ///   - ConsolePixelsPerUnit forces 1 for the console's texts, reproducing what a non-dynamic
    ///     font gets. Works today, but the glyphs rasterise at 39px and are magnified onto the
    ///     screen mesh, so they look softer than vanilla.
    ///   - BakedGetFont returns a pre-baked non-dynamic font for GetFont(false) once one exists
    ///     in the bundle. That is the real fix and matches what the game itself does. It stays
    ///     inert until the font is there, and once it is, ConsolePixelsPerUnit self-disables
    ///     because it only acts on dynamic fonts.
    ///
    /// SECONDARY. TextStyleApplier - a BaseMeshEffect on every console text and on nothing else
    /// in the game - classifies characters with a hardcoded LatinCharactersRegexPattern = "[0-z]"
    /// (ASCII 0x30-0x7A). No Greek character matches, so they fall through to fixedWidth, which
    /// is 0 on this prefab. Disabling it costs only its letter spacing of 6.
    /// </summary>
    [HarmonyPatch]
    public static class CockpitFontFix
    {
        private const bool LogDiagnostics = false;

        /// Set this to the asset path of the baked font once it is in the bundle.
        private const string BakedFontPath = "Assets/Comfortaa-Regular - Greek Baked.ttf";
        private const string BundleName = "greekfont";

        /// Instance IDs of the console's texts, so the pixelsPerUnit prefix stays a hash lookup
        /// rather than a hierarchy walk inside a property getter that runs during layout.
        private static readonly HashSet<int> ConsoleTextIds = new HashSet<int>();

        private static Font _bakedFont;
        private static bool _bakedSearched;

        private static bool GreekActive()
        {
            var table = TextTranslation.Get();
            if (table == null) return false;

            var language = table.GetLanguage();
            if ((int)language <= (int)TextTranslation.Language.TOTAL) return false;

            return language.ToString() == GreekTranslation.LanguageName;
        }

        private static void Log(string message)
        {
            if (!LogDiagnostics || GreekTranslation.Instance == null) return;
            GreekTranslation.Instance.ModHelper.Console.WriteLine("[cockpit] " + message, MessageType.Info);
        }

        /// <summary>
        /// Interplanetary Polyglot already loaded the bundle, so pull the second font out of the
        /// live AssetBundle rather than loading the file again, which Unity refuses.
        /// </summary>
        private static Font BakedFont()
        {
            if (_bakedSearched) return _bakedFont;
            _bakedSearched = true;

            try
            {
                foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (bundle == null || bundle.name != BundleName) continue;

                    _bakedFont = bundle.LoadAsset<Font>(BakedFontPath);
                    break;
                }

                Log(_bakedFont == null
                    ? $"no baked font at '{BakedFontPath}' - falling back to the pixelsPerUnit patch"
                    : $"baked font '{_bakedFont.name}' loaded, dynamic={_bakedFont.dynamic} size={_bakedFont.fontSize}");
            }
            catch (System.Exception ex) { Log("baked font lookup failed: " + ex); }

            return _bakedFont;
        }

        // ------------------------------------------------------------------
        // Font selection
        // ------------------------------------------------------------------

        /// <summary>
        /// Interplanetary Polyglot's GetFont patch ignores the dynamicFont argument and hands back
        /// the same dynamic font either way. GetFont(false) is the game asking for a non-dynamic
        /// font, which is exactly what the console needs. Priority.First so this runs before it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(TextTranslation), nameof(TextTranslation.GetFont))]
        public static bool BakedGetFont(bool dynamicFont, ref Font __result)
        {
            if (dynamicFont || !GreekActive()) return true;

            var baked = BakedFont();
            if (baked == null) return true;

            __result = baked;
            return false;
        }

        // m_gameOverFonts is indexed by language and our enum value (13) is past the end of it.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TextTranslation), nameof(TextTranslation.GetGameOverFont))]
        public static bool SafeGameOverFont(ref Font __result)
        {
            if (!GreekActive()) return true;

            __result = TextTranslation.GetFont(true);
            return false;
        }

        // ------------------------------------------------------------------
        // The scale-factor fix
        // ------------------------------------------------------------------

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Text), nameof(Text.pixelsPerUnit), MethodType.Getter)]
        public static bool ConsolePixelsPerUnit(Text __instance, ref float __result)
        {
            if (ConsoleTextIds.Count == 0) return true;
            if (!ConsoleTextIds.Contains(__instance.GetInstanceID())) return true;
            if (__instance.font == null || !__instance.font.dynamic) return true;
            if (!GreekActive()) return true;

            __result = 1f;
            return false;
        }

        // ------------------------------------------------------------------
        // Console hookup
        // ------------------------------------------------------------------

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipNotificationDisplay), nameof(ShipNotificationDisplay.Awake))]
        public static void ShipConsoleAwake(ShipNotificationDisplay __instance) => Repair(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipNotificationDisplay), nameof(ShipNotificationDisplay.ExpandPool))]
        public static void ShipConsoleExpandPool(ShipNotificationDisplay __instance) => Repair(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipNotificationDisplay), nameof(ShipNotificationDisplay.PushNotification))]
        public static void ShipConsolePush(ShipNotificationDisplay __instance) => Repair(__instance);

        // The Large UI setting swaps in a different object (ShipCockpitUI._largeUiDisplay).
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerCockpitNotificationDisplay), nameof(PlayerCockpitNotificationDisplay.Awake))]
        public static void LargeConsoleAwake(PlayerCockpitNotificationDisplay __instance) => Repair(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerCockpitNotificationDisplay), nameof(PlayerCockpitNotificationDisplay.PushNotification))]
        public static void LargeConsolePush(PlayerCockpitNotificationDisplay __instance) => Repair(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NotificationDisplayTextLayout), nameof(NotificationDisplayTextLayout.ExpandPool))]
        public static void LayoutExpandPool(NotificationDisplayTextLayout __instance) => Repair(__instance);

        /// <summary>Idempotent - safe to call on every notification.</summary>
        private static void Repair(NotificationDisplayTextLayout display)
        {
            if (display == null || !GreekActive()) return;

            try
            {
                var disabled = 0;
                foreach (var applier in display.GetComponentsInChildren<TextStyleApplier>(true))
                {
                    if (!applier.enabled) continue;
                    applier.enabled = false;
                    disabled++;
                }
                if (disabled > 0) Log($"disabled {disabled} TextStyleApplier on '{display.name}'");

                var texts = display.GetComponentsInChildren<Text>(true);
                var added = 0;
                foreach (var text in texts)
                    if (ConsoleTextIds.Add(text.GetInstanceID())) added++;

                if (added > 0)
                {
                    foreach (var text in texts) text.SetAllDirty();
                    Log($"tracking {added} new console texts (total {ConsoleTextIds.Count})");
                }
            }
            catch (System.Exception ex) { Log("repair failed: " + ex); }
        }
    }
}