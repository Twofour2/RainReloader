using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Menu;
using Mono.Cecil;
using Mono.Cecil.Pdb;
using Newtonsoft.Json;
using RWCustom;
using UnityEngine;

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
        public static RainReloaderOptions reloaderOptions = new RainReloaderOptions();
        public static Menu.Menu menu;
        public RainWorldGame rainWorldGame;


        public class ReloadModInfo
        {
            public string guid, folder;
            public ReloadModInfo(string guid, string folder)
            {
                this.guid = guid;
                this.folder = folder;
            }
        }

        private void Awake()
        {
            Log = base.Logger;

            On.RainWorld.PreModsInit += RainWorld_PreModsInit;
            On.ModManager.ModApplyer.RequiresRestart += ModApplyer_RequiresRestart;
            On.Menu.Menu.ctor += Menu_ctor;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            
        }

        private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig(self, manager);
            this.rainWorldGame = self;
        }

        private void Update()
        {
            if (this.rainWorldGame != null)
            {
                if(Input.GetKeyDown(reloaderOptions.reloadKeyCode.Value) || shouldReload)
                {
                    if (!Input.GetKey(KeyCode.LeftShift))
                    {
                        this.rainWorldGame.RestartGame();
                    }
                    this.AttemptReload();
                }
            }
            else
            {
                if (Input.GetKeyDown(reloaderOptions.reloadKeyCode.Value) || shouldReload)
                {
                    this.AttemptReload();
                }
            }
        }


        private void Menu_ctor(On.Menu.Menu.orig_ctor orig, Menu.Menu self, ProcessManager manager, ProcessManager.ProcessID ID)
        {
            RainReloader.menu = self;
            orig(self, manager, ID);
        }

        private void RainWorld_PreModsInit(On.RainWorld.orig_PreModsInit orig, RainWorld self)
        {
            MachineConnector.SetRegisteredOI(PLUGIN_GUID, reloaderOptions);
            if (this.EditReloadModsTxtFile())
            {
                // This can happen if enabledMods.txt gets edited manually, or the apply mods function failed.
                // we want to crash the game as EditReloadModsTxtFile has now fixed the file.
                Log.LogFatal("enabledMods.txt contained a folder that is also in reloadMods.txt, this will cause duplicate mod copies to load.\n"+
                    "Mod has been removed from reload mods, the game will now intentionally crash to load correctly.");
                UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.Abort);
                return;
            }
            this.AttemptReload();
            StartFileSystemWatcher();
           
            orig(self);
        }


        private bool ModApplyer_RequiresRestart(On.ModManager.ModApplyer.orig_RequiresRestart orig, ModManager.ModApplyer self)
        {
            if (self.requiresRestart)
            {
                if (menu != null)
                {
                    
                }
                if (this.EditReloadModsTxtFile())
                {
                    menu.PlaySound(SoundID.Thunder, 0, 1.4f, 1.4f);
                }
            }
            return orig(self);
        }



        public List<ReloadModInfo> GetReloadModInfo()
        {
            if (!File.Exists(Path.Combine(Application.streamingAssetsPath, "reloadMods.txt")))
            {
                Log.LogError("Rain Reloader could not find reloadMods.txt file");
                return null;
            }
            // get list of mods to care about
            try
            {
                List<ReloadModInfo> reloadMods = new List<ReloadModInfo>();
                string[] reloadModsLines = File.ReadAllLines(Path.Combine(Application.streamingAssetsPath, "reloadMods.txt"));
                foreach (string line in reloadModsLines)
                {
                    string[] modPairData = line.Split(';');
                    reloadMods.Add(new ReloadModInfo(modPairData[0], modPairData[modPairData.Length - 1]));
                }
                return reloadMods;
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to read reloadMods.txt: {e}");
                return null;
            }
        }

        public bool EditReloadModsTxtFile()
        {
            List<ReloadModInfo> reloadModInfos = GetReloadModInfo();
            if (reloadModInfos == null)
            {
                return false;
            }
            List<string> enabledModsLines = File.ReadAllLines(Path.Combine(Custom.RootFolderDirectory(), "enabledMods.txt")).ToList();
            bool hasRemovedMod = false;
            foreach (ReloadModInfo reloadModInfo in reloadModInfos)
            {
                Log.LogInfo($"Removing {reloadModInfo.guid} from enabledMods.txt");
                if (enabledModsLines.Remove(reloadModInfo.folder))
                {
                    hasRemovedMod = true;
                }
            }
            File.WriteAllLines(Path.Combine(Custom.RootFolderDirectory(), "enabledMods.txt"), enabledModsLines);
            if (hasRemovedMod)
            {
                Log.LogInfo("Edit BepInEx enabledMods.txt file");
            }
            
            return hasRemovedMod;
        }

        public void AttemptReload()
        {
            Log.LogInfo("!!! Rain Reload Started !!!");
            
            
            shouldReload = false;
            List<ReloadModInfo> reloadModInfos = GetReloadModInfo();
            if (reloadModInfos == null)
            {
                return;
            }
            scriptManager = Chainloader.ManagerObject;

            if (scriptManager != null)
            {
                
                foreach (BaseUnityPlugin loadedPlugin in scriptManager.GetComponents<BaseUnityPlugin>())
                {
                    BepInPlugin[] bepInPluginData = MetadataHelper.GetAttributes<BepInPlugin>(loadedPlugin.GetType());
                    foreach (BepInPlugin pluginData in bepInPluginData)
                    {
                        if (reloadModInfos.Any(x => x.guid == pluginData.GUID))
                        {
                            Log.LogWarning($"Removing plugin {pluginData.GUID}");
                            Chainloader.PluginInfos.Remove(pluginData.GUID);
                            Destroy(loadedPlugin); // todo: does not actually destroy our plugin as scriptManager is incorrect
                            

                        }
                    }
                }

            }

            List<string> skippedModFolders = new List<string>();
            List<string> reloadedMods = new List<string>();
            foreach (DirectoryInfo modDirInfo in new DirectoryInfo(modsFolder).GetDirectories())
            {
                if (!reloadModInfos.Any(x => x.folder == modDirInfo.Name))
                {
                    skippedModFolders.Add(modDirInfo.Name);
                    continue;
                }
                reloadedMods.Add(modDirInfo.Name);
                string[] modDllFiles = Directory.GetFiles(modDirInfo.ToString(), "*.dll", SearchOption.AllDirectories);
                foreach (string sourceDllFile in modDllFiles)
                {
                    Log.LogInfo($"Found dll, attempting to load it. {sourceDllFile}");
                    bool tryLoadPdb = false;
                    string tmpFilePath = Path.GetTempFileName();

                    string tmpDllPath = Path.ChangeExtension(tmpFilePath, ".dll");
                    string tmpPdbPath = Path.ChangeExtension(tmpFilePath, ".pdb");
                    File.Copy(sourceDllFile, tmpDllPath, true);
                    string pdbPath = Path.ChangeExtension(sourceDllFile, ".pdb");
                    if (File.Exists(pdbPath))
                    {
                        Log.LogInfo($"Found pdb file, attempting to load it. {pdbPath}");
                        File.Copy(Path.ChangeExtension(sourceDllFile, ".pdb"), tmpPdbPath, true);
                        tryLoadPdb = true;
                    }

                    LoadDLL(tmpDllPath, sourceDllFile, tmpPdbPath, tryLoadPdb, scriptManager);
                }
            }
            Log.LogInfo($"Reloaded mod folders: {reloadedMods.Join()}");
            Log.LogInfo($"Skipped mod folders: {skippedModFolders.Join()}");
            Log.LogInfo("!!! Rain Reload Completed !!!");
        }

        public string GetModFolderNameFromPath(string fullPath)
        {
            DirectoryInfo fullDirInfo = new DirectoryInfo(fullPath);
            List<DirectoryInfo> parentDirectories = new List<DirectoryInfo>();
            GetAllParentDirectories(fullDirInfo, ref parentDirectories);
            foreach (DirectoryInfo dir in parentDirectories) {
                if (dir.Parent.Name == "mods")
                {
                    return dir.Name;
                }
            }
            return null;
        }

        private void GetAllParentDirectories(DirectoryInfo directory, ref List<DirectoryInfo> directories)
        {
            if (directory == null || directory.Name == directory.Root.Name)
            {
                return;
            }
            directories.Add(directory);
            GetAllParentDirectories(directory.Parent, ref directories);
        }

        private void LoadDLL(string tmpDllPath, string sourceDllPath, string pdbFilePath, bool tryLoadPdb, GameObject scriptManager)
        {
            ReaderParameters readerParams = new ReaderParameters();
            ModuleDefinition sourceModuleDefinition = ModuleDefinition.ReadModule(sourceDllPath); 
            var pdbReader = new PdbReaderProvider();
            //if (tryLoadPdb)
            //{
            //    readerParams.ReadSymbols = true;
            //    pdbReader.GetSymbolReader(sourceModuleDefinition, pdbFilePath);
            //    readerParams.SymbolReaderProvider = pdbReader;
            //}


            AssemblyDefinition dll = AssemblyDefinition.ReadAssembly(tmpDllPath, readerParams);
            dll.Name.Name = $"${dll.Name.Name}-{DateTime.Now.Ticks}";

            MemoryStream ms = new MemoryStream();
            dll.Write(ms); // this looks like it does nothing, it does not do nothing.
            

            Assembly assembly = null;

            if (File.Exists (pdbFilePath))
            {
                Log.LogInfo($"Loading assembly with pdb data. Dll: {tmpDllPath} Pdb: {pdbFilePath}");
                assembly = Assembly.Load(ms.ToArray(), File.ReadAllBytes(pdbFilePath));
            }
            else
            {
                assembly = Assembly.Load(ms.ToArray());
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


                    Log.LogInfo($"Loading plugin {pluginMetadata.GUID}");

                    if (Chainloader.PluginInfos.TryGetValue(pluginMetadata.GUID, out PluginInfo existingPluginInfo))
                    {
                        Log.LogError($"A plugin with GUID {pluginMetadata?.GUID} is already loaded! ({existingPluginInfo?.Metadata?.Name ?? "None"} v{existingPluginInfo?.Metadata?.Version ?? new Version(0, 0, 0)})");
                        continue;
                    }


                    TypeDefinition typeDef = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                    PluginInfo typePluginInfo = Chainloader.ToPluginInfo(typeDef);


                    this.StartCoroutine((DelayAction(() =>
                    {
                        // Need to add to PluginInfos first because BaseUnityPlugin constructor (called by AddComponent below)
                        // looks in PluginInfos for an existing PluginInfo and uses it instead of creating a new one.
                        Chainloader.PluginInfos[pluginMetadata.GUID] = typePluginInfo;
                        //
                       // TryRunModuleCtor(pluginInfo, assembly, typeDef);

                        var instance = scriptManager.AddComponent(type);
                        Log.LogInfo("Added component into script manager");

                        // Fill in properties that are normally set by Chainloader
                        var tv = Traverse.Create(typePluginInfo);
                        tv.Property<BaseUnityPlugin>(nameof(typePluginInfo.Instance)).Value = (BaseUnityPlugin)instance;
                        // Loading the assembly from memory causes Location to be lost
                        tv.Property<string>(nameof(typePluginInfo.Location)).Value = tmpDllPath;

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