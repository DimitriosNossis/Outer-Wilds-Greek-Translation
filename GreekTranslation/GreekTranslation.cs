using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;

namespace GreekTranslation;

public class GreekTranslation : ModBehaviour
{
	public static GreekTranslation Instance;

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
		api.RegisterLanguage(this, "Ελληνικά", "assets/Translation.xml");

		// Uncomment once the Greek font bundle exists. Build it in Unity 2019.4.27f1
		// as a TMP SDF asset covering Basic Latin + Greek and Coptic (U+0370-U+03FF).
		api.AddLanguageFont(this, "Ελληνικά", "assets/greekfont", "RobotoMono-Light");

		ModHelper.Console.WriteLine("Greek translation registered.", MessageType.Success);
	}
}
