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
using System.Security;
using Mono.Cecil.Pdb;

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

        private static string modsFolder = Path.Combine(Application.streamingAssetsPath, "mods");
        private FileSystemWatcher fileSystemWatcher;
        private bool shouldReload = false;

        public GameObject scriptManager;

        private void Awake()
        {
            Log = base.Logger;
            Log.LogInfo("RainReloader is active");
            On.RainWorld.PreModsInit += RainWorld_PreModsInit;
        }

        private void RainWorld_PreModsInit(On.RainWorld.orig_PreModsInit orig, RainWorld self)
        {
            this.AttemptReload();
            StartFileSystemWatcher();
            orig(self);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || shouldReload)
            {
                this.AttemptReload();
            }
        }

        public void AttemptReload()
        {
            Log.LogInfo("ATTEMPING RELOAD");
            shouldReload = false;

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

            //string modsFolder = Path.Combine(Application.streamingAssetsPath, "mods");
            Log.LogWarning(modsFolder);

            string[] modDllFiles = Directory.GetFiles(modsFolder, "*.dll", SearchOption.AllDirectories);
            foreach (string file in modDllFiles)
            {
                Log.LogInfo($"Found dll, attempting to load it. {file}");
                bool tryLoadPdb = false;
                string tmpFilePath = Path.GetTempFileName();

                string tmpDllPath = Path.ChangeExtension(tmpFilePath, ".dll");
                string tmpPdbPath = Path.ChangeExtension(tmpFilePath, ".pdb");
                File.Copy(file, tmpDllPath, true);
                string pdbPath = Path.ChangeExtension(file, ".pdb");
                if (File.Exists(pdbPath))
                {
                    Log.LogInfo($"Found pdb file, attempting to load it. {pdbPath}");
                    File.Copy(Path.ChangeExtension(file, ".pdb"), tmpPdbPath, true);
                    tryLoadPdb = true;
                }
                
                LoadDLL(tmpDllPath, file, tmpPdbPath, tryLoadPdb, scriptManager, reloadModsTxt);
            }
        }


        private void LoadDLL(string path, string sourcePath, string pdbFilePath, bool tryLoadPdb, GameObject scriptManager, string[] reloadMods)
        {
            ReaderParameters readerParams = new ReaderParameters();
            ModuleDefinition sourceModuleDefinition = ModuleDefinition.ReadModule(sourcePath);
            var pdbReader = new PdbReaderProvider();
            if (tryLoadPdb)
            {
                readerParams.ReadSymbols = true;
                pdbReader.GetSymbolReader(sourceModuleDefinition, pdbFilePath);
                readerParams.SymbolReaderProvider = pdbReader;
            }


            AssemblyDefinition dll = AssemblyDefinition.ReadAssembly(path, readerParams);
            dll.Name.Name = $"${dll.Name.Name}-{DateTime.Now.Ticks}";

            Assembly assembly = null;

            if (File.Exists (pdbFilePath))
            {
                Log.LogInfo($"Loading assembly with pdb data. {pdbFilePath}");
                assembly = Assembly.Load(File.ReadAllBytes(path), File.ReadAllBytes(pdbFilePath));
            }
            else
            {
                assembly = Assembly.Load(File.ReadAllBytes(path));
            }
            
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


                    TypeDefinition typeDef = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                    foreach (var stupid in typeDef.Methods)
                    {
                        Log.LogWarning(stupid.HasCustomDebugInformations);
                    }
                    Log.LogWarning(typeDef.Methods.First().HasCustomDebugInformations);
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

        private void StartFileSystemWatcher()
        {
            fileSystemWatcher = new FileSystemWatcher(modsFolder)
            {
                IncludeSubdirectories = true
            };
            fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            fileSystemWatcher.Filter = "*.dll";
            fileSystemWatcher.Changed += FileChangedEventHandler;
            fileSystemWatcher.Deleted += FileChangedEventHandler;
            fileSystemWatcher.Created += FileChangedEventHandler;
            fileSystemWatcher.Renamed += FileChangedEventHandler;
            fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void FileChangedEventHandler(object sender, FileSystemEventArgs args)
        {
            Log.LogInfo($"File {Path.GetFileName(args.Name)} changed. Delayed recompiling...");
            shouldReload = true;
        }


    }
}