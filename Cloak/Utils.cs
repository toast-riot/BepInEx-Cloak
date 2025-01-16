using Mono.Cecil;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using System;
using BepInEx.Logging;

namespace Cloak
{
    static class Utils
    {
        internal class CustomLogger(ManualLogSource logger)
        {
            readonly ManualLogSource _logger = logger;
            public bool IsLoggingEnabled = true;

            public void Log(string message, LogLevel level = LogLevel.Debug)
            {
                if (IsLoggingEnabled) _logger.Log(level, message);
            }
        }

        internal static List<PluginInfo> PluginsInAssembly(Assembly assembly)
        {
            ModuleDefinition mainModule;

            if (!string.IsNullOrEmpty(assembly.Location))
            {
                mainModule = AssemblyDefinition.ReadAssembly(assembly.Location, TypeLoader.ReaderParameters)?.MainModule;
            }
            else if (Utility.TryResolveDllAssembly(assembly.GetName(), Paths.GameRootPath, TypeLoader.ReaderParameters, out AssemblyDefinition resolvedAssembly))
            {
                mainModule = resolvedAssembly.MainModule;
            }
            else
            {
                Cloak.logger.Log($"Unable to read module for assembly {assembly.FullName}");
                return [];
            }

            if (mainModule.GetTypeReferences().All(static r => r.FullName != typeof(BepInPlugin).FullName)) return [];
            return mainModule.Types.Select(Chainloader.ToPluginInfo).Where(static t => t != null).ToList();
        }

        internal static bool IsHiddenPlugin(string GUID)
        {
            return Array.Exists(Cloak.CloakedPlugins, plugin => plugin == GUID);
        }

        internal static HashSet<string> GetHiddenPatches()
        {
            return [.. Cloak.PluginPatches
                .Where(static plugin => IsHiddenPlugin(plugin.Key))
                .SelectMany(static plugin => plugin.Value)
            ];
        }

        internal static bool IsHiddenPatch(string ID)
        {
            return GetHiddenPatches().Contains(ID);
        }
    }
}