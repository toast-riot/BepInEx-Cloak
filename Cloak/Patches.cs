using System.Collections.Generic;
using HarmonyLib;
using BepInEx;
using BepInEx.Bootstrap;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;
using System.Linq;

namespace Cloak
{
    static class Patches
    {
        static bool MatchInstruction(CodeInstruction instruction, OpCode opcode) => instruction.opcode == opcode;
        static bool MatchInstruction<T>(CodeInstruction instruction, OpCode opcode, Func<T, bool> predicate)
        {
            return instruction.opcode == opcode && instruction.operand is T operand && predicate(operand);
        }

        public static void Chainloader_Initialize_Postfix()
        {
            Cloak.harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Start)),
                transpiler: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Chainloader_Start_Transpiler))),
                postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Chainloader_Start_Postfix)))
            );
        }

        public static void Chainloader_Start_Postfix()
        {
            Common.PostChainloaderChecks();
            Common.LogDebugInfo();
        }

        public static IEnumerable<CodeInstruction> PatchManager_GetPatchedMethods_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                // Hide patches owned by hidden plugins from GetPatchedMethods
                // enumerable = PatchManager.PatchInfos.Keys.ToList<MethodBase>();
                if (MatchInstruction(instruction, OpCodes.Call, static (MethodInfo method) => method.Name == "ToList"))
                {
                    Cloak.logger.Log("PatchManager.GetPatchedMethods transpiler matched");
                    yield return instruction;
                    yield return Transpilers.EmitDelegate(static (IEnumerable<MethodBase> patchedMethods) =>
                        patchedMethods.Where(static method =>
                            !Harmony.GetPatchInfo(method).Owners.Any(static owner =>
                                Utils.IsHiddenPatch(owner) || owner == Cloak.HarmonyID
                            )
                        )
                    );
                    continue;
                }
                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> BaseUnityPlugin_Ctor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo get_PluginInfos = typeof(Chainloader).GetProperty(nameof(Chainloader.PluginInfos)).GetMethod;

            foreach (CodeInstruction instruction in instructions)
            {
                // Restore real PluginInfos
                // if (!Chainloader.IsEditor && Chainloader.PluginInfos.TryGetValue(metadata.GUID, out pluginInfo))
                if (MatchInstruction(instruction, OpCodes.Call, (MethodInfo method) => method == get_PluginInfos))
                {
                    Cloak.logger.Log("BaseUnityPlugin.Ctor transpiler matched");
                    yield return Transpilers.EmitDelegate(() => Cloak.RealPluginInfos);
                    continue;
                }
                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> Harmony_Ctor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                // When an instance of Harmony is created, determine the plugin that owns the patch
                if (MatchInstruction(instruction, OpCodes.Ret))
                {
                    Cloak.logger.Log("Harmony.Ctor transpiler matched");
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AccessTools), nameof(AccessTools.GetOutsideCaller)));
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return Transpilers.EmitDelegate<Action<MethodBase, string>>(
                        static (method, id) =>
                        {
                            List<PluginInfo> plugins = Utils.PluginsInAssembly(method.DeclaringType.Assembly);

                            // If multiple plugins are (for some reason) present, assume the patch belongs to all of them
                            foreach (PluginInfo plugin in plugins)
                            {
                                if (!Cloak.PluginPatches.TryGetValue(plugin.Metadata.GUID, out HashSet<string> set))
                                    Cloak.PluginPatches[plugin.Metadata.GUID] = set = [];
                                set.Add(id);
                            }
                        }
                    );
                }
                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> Chainloader_Start_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo PluginInfoInstanceSet = typeof(PluginInfo).GetProperty(nameof(PluginInfo.Instance)).SetMethod;
            MethodInfo PluginDictSet = typeof(Dictionary<string, PluginInfo>).GetProperty("Item").SetMethod;
            MethodInfo PluginDictRemove = typeof(Dictionary<string, PluginInfo>).GetMethod("Remove", [typeof(string)]);
            MethodInfo get_PluginInfos = typeof(Chainloader).GetProperty(nameof(Chainloader.PluginInfos)).GetMethod;

            List<CodeInstruction> code = [.. instructions];
            for (int i = 0; i < code.Count; i++)
            {
                // Add HideAndDontSave to the BaseUnityPlugin components of hidden plugins
                // pluginInfo5.Instance = (BaseUnityPlugin)Chainloader.ManagerObject.AddComponent(assembly.GetType(pluginInfo5.TypeName));
                if (Settings.Patches_HideAndDontSave.Value &&
                    MatchInstruction(code[i], OpCodes.Callvirt, (MethodInfo method) => method == PluginInfoInstanceSet))
                {
                    Cloak.logger.Log("Chainloader.Start plugin instance transpiler matched");
                    yield return Transpilers.EmitDelegate<Func<BaseUnityPlugin, BaseUnityPlugin>>(
                        static (plugin) =>
                        {
                            if (Utils.IsHiddenPlugin(plugin.Info.Metadata.GUID))
                                plugin.hideFlags = HideFlags.HideAndDontSave;
                            return plugin;
                        }
                    );
                }

                // Prevent hidden plugins being added to Chainloader.PluginInfos
                // Chainloader.PluginInfos[text3] = pluginInfo5;
                if (
                    i >= 3 &&
                    MatchInstruction(code[i - 3], OpCodes.Call, (MethodInfo method) => method == get_PluginInfos) &&
                    MatchInstruction(code[i], OpCodes.Callvirt, (MethodInfo method) => method == PluginDictSet))
                {
                    Cloak.logger.Log("Chainloader.Start PluginInfos transpiler matched");
                    yield return Transpilers.EmitDelegate<Action<Dictionary<string, PluginInfo>, string, PluginInfo>>(
                        static (pluginInfos, key, value) =>
                        {
                            Cloak.RealPluginInfos[key] = value;
                            if (Settings.Patches_Chainloader_PluginInfos.Value && Utils.IsHiddenPlugin(key)) return;
                            pluginInfos[key] = value;
                        }
                    );
                    continue;
                }

                // Restore real PluginInfos
                // Chainloader.PluginInfos.Remove(text3);
                if (i >= 1 &&
                    Settings.Patches_Chainloader_PluginInfos.Value &&
                    MatchInstruction(code[i - 1], OpCodes.Ldloc_S) &&
                    MatchInstruction(code[i], OpCodes.Callvirt, (MethodInfo method) => method == PluginDictRemove))
                {
                    Cloak.logger.Log("Chainloader.Start PluginInfos removal transpiler matched");
                    yield return Transpilers.EmitDelegate<Func<Dictionary<string, PluginInfo>, string, bool>>(
                        static (pluginInfos, key) =>
                        {
                            pluginInfos.Remove(key);
                            return Cloak.RealPluginInfos.Remove(key);
                        }
                    );
                    continue;
                }
                yield return code[i];
            }
        }
    }
}