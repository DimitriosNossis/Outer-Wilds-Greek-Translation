using HarmonyLib;
using UnityEngine;

namespace GreekTranslation
{
    [HarmonyPatch]
    public static class CockpitFontFix
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TextTranslation), nameof(TextTranslation.GetGameOverFont))]
        public static bool SafeGameOverFont(ref Font __result)
        {
            __result = TextTranslation.GetFont(true);
            return false;   // skip the original, which indexes past the font array
        }
    }
}