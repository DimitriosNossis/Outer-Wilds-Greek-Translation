using HarmonyLib;
using OWML.Common;
using UnityEngine;
using UnityEngine.UI;

namespace GreekTranslation
{
    /// <summary>
    /// Settles one question: is the ~28.9x collapse in Text.preferredWidth the canvas scale
    /// factor reaching a dynamic font?
    ///
    /// Unity's Text.pixelsPerUnit branches on exactly one thing:
    ///     if (!font || font.dynamic) return localCanvas.scaleFactor;
    ///     if (m_FontData.fontSize == 0) return 1;
    ///     return font.fontSize / (float)m_FontData.fontSize;
    ///
    /// VCR_OSD_MONO is non-dynamic and takes the second branch, so the console's world-space
    /// canvas scale never touches it. A dynamic font takes the first branch and Unity then tries
    /// to rasterise glyphs at fontSize * scaleFactor.
    ///
    /// Read-only. Delete after this run.
    /// </summary>
    [HarmonyPatch]
    public static class CockpitScaleProbe
    {
        private static bool _proved;

        private static void Log(string message)
        {
            if (GreekTranslation.Instance == null) return;
            GreekTranslation.Instance.ModHelper.Console.WriteLine("[scale] " + message, MessageType.Info);
        }

        // Runs on PushNotification rather than Awake: by then InitializeFont has swapped
        // VCR_OSD_MONO for the Greek font, which is the state we need to measure.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipNotificationDisplay), nameof(ShipNotificationDisplay.PushNotification))]
        public static void Prove(ShipNotificationDisplay __instance)
        {
            if (_proved) return;
            _proved = true;

            try
            {
                var canvas = __instance.GetComponentInParent<Canvas>();
                Log($"console canvas='{canvas?.name}' renderMode={canvas?.renderMode} " +
                    $"scaleFactor={canvas?.scaleFactor} " +
                    $"referencePixelsPerUnit={canvas?.referencePixelsPerUnit} " +
                    $"lossyScale={canvas?.transform.lossyScale}");

                var scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
                Log($"console scaler={(scaler == null ? "none" : $"scaleFactor={scaler.scaleFactor} " +
                    $"referencePixelsPerUnit={scaler.referencePixelsPerUnit} " +
                    $"dynamicPixelsPerUnit={scaler.dynamicPixelsPerUnit}")}");

                foreach (var text in __instance.GetComponentsInChildren<Text>(true))
                {
                    if (text.font == null) continue;

                    Log($"  {text.name}: font='{text.font.name}' dynamic={text.font.dynamic} " +
                        $"fontSize={text.fontSize} pixelsPerUnit={text.pixelsPerUnit} " +
                        $"preferredWidth={text.preferredWidth} text='{text.text}'");
                }

                // The decisive part: can the font actually produce a glyph at the scaled size?
                var probeText = __instance._testText;
                if (probeText != null && probeText.font != null)
                {
                    var font = probeText.font;
                    var plain = probeText.fontSize;
                    var scaled = Mathf.RoundToInt(probeText.fontSize * probeText.pixelsPerUnit);

                    Log($"glyph test on '{font.name}' dynamic={font.dynamic} " +
                        $"plainSize={plain} scaledSize={scaled}");

                    ReportGlyph(font, 'Φ', plain);
                    ReportGlyph(font, 'Φ', scaled);
                    ReportGlyph(font, 'A', plain);
                    ReportGlyph(font, 'A', scaled);
                }

                // Contrast: canvases that render Greek correctly.
                foreach (var other in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (other == canvas) continue;
                    if (!other.isRootCanvas) continue;

                    Log($"  other canvas='{other.name}' renderMode={other.renderMode} " +
                        $"scaleFactor={other.scaleFactor} refPPU={other.referencePixelsPerUnit}");
                }
            }
            catch (System.Exception ex) { Log("failed: " + ex); }
        }

        private static void ReportGlyph(Font font, char c, int size)
        {
            try
            {
                font.RequestCharactersInTexture(c.ToString(), size, FontStyle.Normal);

                CharacterInfo info;
                var ok = font.GetCharacterInfo(c, out info, size, FontStyle.Normal);

                Log($"   '{c}' @ size {size}: found={ok} advance={info.advance} " +
                    $"glyphWidth={info.glyphWidth} glyphHeight={info.glyphHeight}");
            }
            catch (System.Exception ex) { Log($"   '{c}' @ size {size}: threw {ex.GetType().Name}"); }
        }
    }
}