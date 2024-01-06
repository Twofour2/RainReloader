using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Bootstrap;
using Mono.Cecil;
using System.Reflection;
using System.Collections;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace RainReloader
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class RainReloader : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "twofour2.rainReloader";
        public const string PLUGIN_NAME = "rainreloader";
        public const string PLUGIN_DESC = "";
        public const string PLUGIN_VERSION = "0.2.9";
        public static ManualLogSource Log { get; private set; }

        public GameObject scriptManager;

        private void Awake()
        {
            Log = base.Logger;
            Log.LogInfo("RainReloader is active");
            On.RainWorld.PreModsInit += RainWorld_PreModsInit;
           // this.AttemptReload();
            
            
        }

        private void RainWorld_PreModsInit(On.RainWorld.orig_PreModsInit orig, RainWorld self)
        {
            this.AttemptReload();
            orig(self);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                this.AttemptReload();
            }
        }

        public void AttemptReload()
        {
            Log.LogInfo("ATTEMPING RELOAD");

            // get list of mods to care about
            string[] reloadModsTxt = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "reloadMods.txt")).Split(new string[] {Environment.NewLine}, StringSplitOptions.None);
            Log.LogInfo(reloadModsTxt);
            scriptManager = Chainloader.ManagerObject;

            Log.LogWarning(scriptManager);
            if (scriptManager != null)
            {
                Log.LogWarning(scriptManager.activeInHierarchy);
                
                foreach (BaseUnityPlugin loadedPlugin in scriptManager.GetComponents<BaseUnityPlugin>())
                {
                    BepInPlugin[] bepInPluginData = MetadataHelper.GetAttributes<BepInPlugin>(loadedPlugin.GetType());
                    foreach (BepInPlugin pluginData in bepInPluginData)
                    {
                        Log.LogWarning($"Check plugin {pluginData.GUID}");
                        //if (pluginData.GUID == metaGuid)
                        
                        if (reloadModsTxt.Contains(pluginData.GUID))
                        {
                            Log.LogWarning($"Removing plugin {pluginData.GUID}");
                            Chainloader.PluginInfos.Remove(pluginData.GUID);
                            Destroy(loadedPlugin); // todo: does not actually destroy our plugin as scriptManager is incorrect
                        }
                        
                    }
                }
              //  Destroy(scriptManager);

            }
            // now create now?
            //scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            //DontDestroyOnLoad(scriptManager);

            string modsFolder = Path.Combine(Application.streamingAssetsPath, "mods");
            Log.LogWarning(modsFolder);

            string[] modDllFiles = Directory.GetFiles(modsFolder, "*.dll", SearchOption.AllDirectories);
            foreach (string file in modDllFiles)
            {
                Log.LogWarning(file);
                string tmpFilePath = Path.GetTempFileName();
                File.Copy(file, tmpFilePath, true);
                Log.LogInfo($"Found DLL: {file}");
                LoadDLL(tmpFilePath, scriptManager, reloadModsTxt);
            }
        }


        private void LoadDLL(string path, GameObject scriptManager, string[] reloadMods)
        {
            AssemblyDefinition dll = AssemblyDefinition.ReadAssembly(path);
            dll.Name.Name = $"${dll.Name.Name}-{DateTime.Now.Ticks}";

            MemoryStream ms = new MemoryStream();
            dll.Write(ms);
            Assembly assembly = Assembly.Load(ms.ToArray());
            foreach (Type type in GetTypesSafe(assembly))
            {
                try
                {
                    if (!typeof(BaseUnityPlugin).IsAssignableFrom(type)) continue;

                    BepInPlugin pluginMetadata = MetadataHelper.GetMetadata(type);

                    if (pluginMetadata == null)
                    {
                        Log.LogWarning($"Plugin metadata not found? {type.Name}");
                        continue;
                    }

                    if (!reloadMods.Contains(pluginMetadata.GUID))
                    {
                        Log.LogWarning($"{pluginMetadata.GUID} is not in reload mods list");
                        continue;
                    }

                    Log.LogInfo($"Loading plugin {pluginMetadata.GUID}");

                    if (Chainloader.PluginInfos.TryGetValue(pluginMetadata.GUID, out PluginInfo existingPluginInfo))
                    {
                        Log.LogError($"A plugin with GUID {pluginMetadata?.GUID} is already loaded! ({existingPluginInfo?.Metadata?.Name ?? "None"} v{existingPluginInfo?.Metadata?.Version ?? new Version(0, 0, 0)})");
                        continue;
                    }


                    var typeDef = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                    var pluginInfo = Chainloader.ToPluginInfo(typeDef);

                    this.StartCoroutine((DelayAction(() =>
                    {
                        // Need to add to PluginInfos first because BaseUnityPlugin constructor (called by AddComponent below)
                        // looks in PluginInfos for an existing PluginInfo and uses it instead of creating a new one.
                        Chainloader.PluginInfos[pluginMetadata.GUID] = pluginInfo;
                        //
                       // TryRunModuleCtor(pluginInfo, assembly, typeDef);

                        var instance = scriptManager.AddComponent(type);

                        // Fill in properties that are normally set by Chainloader
                        var tv = Traverse.Create(pluginInfo);
                        tv.Property<BaseUnityPlugin>(nameof(pluginInfo.Instance)).Value = (BaseUnityPlugin)instance;
                        // Loading the assembly from memory causes Location to be lost
                        tv.Property<string>(nameof(pluginInfo.Location)).Value = path;

                        Log.LogInfo($"Loaded plugin of type {type} with GUID {pluginMetadata.GUID}");
                    })));


                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to load plugin {type.Name} because of exception: {e}");
                }
            }

        }

        private static void TryRunModuleCtor(PluginInfo plugin, Assembly assembly, TypeDefinition typeDef)
        {
            try
            {
                RuntimeHelpers.RunModuleConstructor(assembly.GetType(typeDef.Name).Module.ModuleHandle);
            }
            catch (Exception e)
            {
                Log.Log(LogLevel.Warning,
                           $"Couldn't run Module constructor for {assembly.FullName}::{plugin}: {e}");
            }
        }

        private IEnumerable<Type> GetTypesSafe(Assembly ass)
        {
            try
            {
                return ass.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var sbMessage = new StringBuilder();
                sbMessage.AppendLine("\r\n-- LoaderExceptions --");
                foreach (var l in ex.LoaderExceptions)
                    sbMessage.AppendLine(l.ToString());
                sbMessage.AppendLine("\r\n-- StackTrace --");
                sbMessage.AppendLine(ex.StackTrace);
                Logger.LogError(sbMessage.ToString());
                return ex.Types.Where(x => x != null);
            }
        }

        private static IEnumerator DelayAction(Action action)
        {
            yield return null;
            action();
        }


    }
}