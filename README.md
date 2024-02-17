# RainReloader
Live reloads and re-injects BepInEx plugins into rainworld  

This mod is very early stages, isn't complete, barely works and isn't easy to setup. You need a good idea on how to setup a rainworld code mod to use this.  
I wrote this for my own use, as such it only does what I need it to.  

This mod can also load in .pdb files for debugging, make sure to enable it first in `Solution Properties > Build > General > Debug Symbols`

Enable your mod in rainworld and remove it from "Rain World\RainWorld_Data\StreamingAssets\enabledMods.txt"  
Create a new file called "reloadMods.txt" in the same StreamingAssets folder, then add your mod GUID to it.   

You will need to remove your mod from enableMods.txt every time you add or remove a mod.

You'll also need to add an OnDisable() method to your mod that removes all registered delegates  
```
[BepInPlugin(PLUGIN_GUID, "Mod Name", "0.1.0")]
    class YourMod : BaseUnityPlugin
    {
        public void OnEnable()
        {
            On.Player.Jump += Player_Jump;
        }
        
        // add this to do the opposite of whatever you did in OnEnable()
        // otherwise you'll wind up with two methods being called
        public void OnDisable()
        {
            On.Player.Jump -= Player_Jump;
        }
    }
```

Build your new file and watch it reload :D  

## Working with other mods
Currently this breaks mods like Slugbase, any features you have setup will be wiped and return the wrong value.  
Personally I just add `|| true` to any features to bypass this while writing code.
```
 if (YourMod.YourFeature.TryGet(self, out bool canDoFeature) && canDoFeature || true)
```

Mostly adapted from: https://github.com/BepInEx/BepInEx.Debug/blob/master/src/ScriptEngine/ScriptEngine.cs
