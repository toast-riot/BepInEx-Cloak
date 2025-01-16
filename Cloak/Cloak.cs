using System.Collections.Generic;
using HarmonyLib;
using Mono.Cecil;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using System.Reflection;
using System;
using BepInEx.Configuration;
using System.Text.RegularExpressions;
using System.IO;
using HarmonyLib.Public.Patching;
using System.Linq;

namespace Cloak
{
    static class Settings
    {
        // General
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> CloakedPlugins;

        // Patches
        internal static ConfigEntry<bool> Patches_Chainloader_PluginInfos;
        internal static ConfigEntry<bool> Patches_HideAndDontSave;
        internal static ConfigEntry<bool> Patches_PatchManager_GetPatchedMethods;

        // Debug
        internal static ConfigEntry<bool> Logging;

        internal static void Init(ConfigFile config)
        {
            string section = "1. General";
            Enabled = config.Bind(section, "Enabled", true);
            CloakedPlugins = config.Bind(section, "Plugins to hide", "", "Comma-separated list of GUIDs");

            section = "2. Patches";
            Patches_Chainloader_PluginInfos = config.Bind(section, "Chainloader.PluginInfos", true, "Don't add the plugin to Chainloader.PluginInfos");
            Patches_HideAndDontSave = config.Bind(section, "HideAndDontSave", true, "Set the plugin's component's HideFlags to HideAndDontSave");
            Patches_PatchManager_GetPatchedMethods = config.Bind(section, "PatchManager.GetPatchedMethods", true, "Hide the plugin from PatchManager.GetPatchedMethods");

            section = "3. Debug";
            Logging = config.Bind(section, "Enable logging", true);
        }
    }

    static class Cloak
    {
        public static IEnumerable<string> TargetDLLs { get; } = [];
        public static void Patch(AssemblyDefinition ad) { }

        internal static Utils.CustomLogger logger;
        internal static Harmony harmony;
        internal static readonly string GUID = Guid.NewGuid().ToString();
        internal static readonly string HarmonyID = GUID;
        static readonly string ConfigPath = Path.Combine(Paths.ConfigPath, Assembly.GetExecutingAssembly().GetName().Name);

        internal static string[] CloakedPlugins = [];
        internal static readonly Dictionary<string, PluginInfo> RealPluginInfos = [];
        internal static readonly Dictionary<string, HashSet<string>> PluginPatches = [];

        static void Finish()
        {
            Settings.Init(new ConfigFile($"{ConfigPath}.cfg", true));
            CloakedPlugins = Regex.Split(Settings.CloakedPlugins.Value, @"\s*,\s*").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            logger = new(Logger.CreateLogSource(nameof(Cloak)))
            { IsLoggingEnabled = Settings.Logging.Value };

            if (Settings.Enabled.Value)
            {
                harmony = new(HarmonyID);

                harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Initialize)),
                    postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.Chainloader_Initialize_Postfix))));

                if (Settings.Patches_Chainloader_PluginInfos.Value)
                    harmony.Patch(typeof(BaseUnityPlugin).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null),
                        transpiler: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.BaseUnityPlugin_Ctor_Transpiler))));

                if (Settings.Patches_PatchManager_GetPatchedMethods.Value)
                    harmony.Patch(typeof(PatchManager).GetMethod(nameof(PatchManager.GetPatchedMethods)),
                        transpiler: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.PatchManager_GetPatchedMethods_Transpiler))));

                harmony.Patch(typeof(Harmony).GetConstructor([typeof(string)]),
                    transpiler: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.Harmony_Ctor_Transpiler))));
            }

            logger.Log("Initialized");
        }
    }
}