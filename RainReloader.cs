using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RWCustom;
using System.Text;
using On.Menu;
using Menu;
using MoreSlugcats;
using System.Runtime.ExceptionServices;
using BepInEx.Bootstrap;
using Mono.Cecil;
using System.Reflection;
using UnityEngine.Profiling.Memory.Experimental;
using UnityEngine;
using System.Collections;
using HarmonyLib;

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

        public static GameObject scriptManager;

        private void Awake()
        {
            Log = base.Logger;
            Log.LogWarning("BepInPlugin has awoken! ");
           // scriptManager = this.gameObject;
           // this.AttemptReload();
        }

        //private void RainWorldGame_RawUpdate(On.RainWorldGame.orig_RawUpdate orig, RainWorldGame self, float dt)
        //{
        //    orig(self, dt);
            
        //    if (self.devToolsActive) {
                
                
        //    }
        //}

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
           // 
            Log.LogWarning(scriptManager);
            if (scriptManager != null)
            {
                Log.LogWarning(scriptManager.activeInHierarchy);



                foreach (BaseUnityPlugin loadedPlugin in scriptManager.GetComponents<BaseUnityPlugin>())
                {
                    Log.LogInfo(loadedPlugin);
                    string metaGuid = loadedPlugin?.Info?.Metadata?.GUID ?? "";
                    Log.LogInfo($"BepInPlugin: {metaGuid}");
                    if (Chainloader.PluginInfos.ContainsKey(metaGuid))
                    {
                        Log.LogWarning($"Removing plugin ${metaGuid}");
                        Chainloader.PluginInfos.Remove(metaGuid);
                    }
                }
                Destroy(scriptManager);
            }

            // now create now?
            scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            DontDestroyOnLoad(scriptManager);
            
            foreach (ModManager.Mod mod in ModManager.ActiveMods)
            {
                Log.LogWarning(mod.path);
                string[] modDllFiles = Directory.GetFiles(mod.path, "*.dll", SearchOption.AllDirectories);
                foreach (string file in modDllFiles)
                {
                    string tmpFilePath = Path.GetTempFileName();
                    File.Copy(file,tmpFilePath, true);
                    Log.LogInfo($"Found DLL: {file}");
                    LoadDLL(tmpFilePath, scriptManager);
                }

            }

            

        }


        private void LoadDLL(string path, GameObject scriptManager)
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

                    Log.LogInfo($"Loading plugin {pluginMetadata.GUID}");

                    if (Chainloader.PluginInfos.TryGetValue(pluginMetadata.GUID, out var existingPluginInfo))
                    {
                        Log.LogError($"A plugin with GUID {pluginMetadata.GUID} is already loaded! ({existingPluginInfo.Metadata.Name} v{existingPluginInfo.Metadata.Version})");
                        continue;
                    }

                    var typeDef = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                    var pluginInfo = Chainloader.ToPluginInfo(typeDef);

                    this.StartCoroutine((DelayAction(() =>
                    {
                        // Need to add to PluginInfos first because BaseUnityPlugin constructor (called by AddComponent below)
                        // looks in PluginInfos for an existing PluginInfo and uses it instead of creating a new one.
                        Chainloader.PluginInfos[pluginMetadata.GUID] = pluginInfo;

                        var instance = scriptManager.AddComponent(type);

                        // Fill in properties that are normally set by Chainloader
                        var tv = Traverse.Create(pluginInfo);
                        tv.Property<BaseUnityPlugin>(nameof(pluginInfo.Instance)).Value = (BaseUnityPlugin)instance;
                        // Loading the assembly from memory causes Location to be lost
                        tv.Property<string>(nameof(pluginInfo.Location)).Value = path;

                        Log.LogInfo($"Loaded plugin {pluginMetadata.GUID}");
                    })));


                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to load plugin {type.Name} because of exception: {e}");
                }
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