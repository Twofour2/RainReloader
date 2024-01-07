# RainReloader
Live reloads and re-injects BepInEx plugins into rainworld  

This only barely works and isn't easy to setup. You need a good idea on how to setup a rainworld code mod to use this.  

Enable your mod in rainworld and remove it from "Rain World\RainWorld_Data\StreamingAssets\enabledMods.txt"  
Create a new file called "reloadMods.txt" in the same StreamingAssets folder, then add your mod GUID to it.  

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

Mostly adapted from: https://github.com/BepInEx/BepInEx.Debug/blob/master/src/ScriptEngine/ScriptEngine.cs
