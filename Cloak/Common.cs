using BepInEx.Bootstrap;
using HarmonyLib;
using System.Linq;
using BepInEx.Logging;

namespace Cloak
{
    static class Common
    {
        internal static void PostChainloaderChecks()
        {
            if (!Settings.Logging.Value) return;

            foreach (string plugin in Cloak.CloakedPlugins.Except(Cloak.RealPluginInfos.Keys).ToList())
            {
                Cloak.logger.Log($"Did not find plugin with GUID {plugin}", LogLevel.Warning);
            }

            foreach (var pluginData in Cloak.RealPluginInfos)
            {
                string status = Utils.IsHiddenPlugin(pluginData.Key) ? "HIDE" : "SHOW";
                Cloak.logger.Log($"[{status}] {pluginData.Value.Metadata.Name} ({pluginData.Key})", LogLevel.Info);
            }
        }

        internal static void LogDebugInfo()
        {
            if (!Settings.Logging.Value) return;

            Cloak.logger.Log($"Cloaked plugins: {string.Join(", ", Cloak.CloakedPlugins)}");
            Cloak.logger.Log($"Associated Harmony instances:");
            foreach (var plugin in Cloak.PluginPatches)
            {
                Cloak.logger.Log($"{plugin.Key}: {string.Join(", ", plugin.Value)}");
            }

            foreach (var plugin in Chainloader.PluginInfos)
            {
                Cloak.logger.Log($"Detectable plugin: {plugin.Key}");
            }
            foreach (var version in Harmony.VersionInfo(out _))
            {
                Cloak.logger.Log($"Detectable Harmony instance: {version.Key}");
            }
        }
    }
}