using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;

namespace GreekTranslation;

public class GreekTranslation : ModBehaviour
{
	public static GreekTranslation Instance;

	// Single source of truth - used both to register the language and to detect it at runtime.
	// Confirmed working: the log shows language=Ελληνικά asInt=13 TOTAL=12.
	public const string LanguageName = "Ελληνικά";

	public void Awake()
	{
		Instance = this;
	}

	public void Start()
	{
		new Harmony("Dimitrios.GreekTranslation").PatchAll(Assembly.GetExecutingAssembly());

		var api = ModHelper.Interaction.TryGetModApi<ILocalizationAPI>("xen.LocalizationUtility");
		if (api == null)
		{
			ModHelper.Console.WriteLine(
				"Interplanetary Polyglot (xen.LocalizationUtility) not found - enable it in the mod manager.",
				MessageType.Error);
			return;
		}

		// The label shown in Options > Language.
		api.RegisterLanguage(this, LanguageName, "assets/Translation.xml");

		api.AddLanguageFont(this, LanguageName, "assets/greekfont", "Assets/Comfortaa-Regular - Greek.ttf");
		api.AddLanguageFixer(LanguageName, s => s);
		api.SetLanguageDefaultFontSpacing(LanguageName, 1.05f);
		api.SetLanguageFontSizeModifier(LanguageName, 0.85f);

		ModHelper.Console.WriteLine("Greek translation registered.", MessageType.Success);
	}
}