# RainReloader
Live reloads and re-injects BepInEx plugins into rainworld  

Create a new file called "reloadMods.txt" in the same StreamingAssets folder, then add your mod GUID to it along with the name of the mod folder seperated by a colon ";"   

The mod GUID must match the one used in your BepInPlugin, specifically PLUGIN_GUID
```
[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
```

StreamingAssets/reloadMods.txt
```
twofour2.iteratorKit;IteratorKit
anotherSample.myMod;SampleMod
```


You'll also need to add an OnDisable() method to your mod that removes all registered delegates. Otherwise they will remain running.  
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
If you wish to manually reload you can press the "1" key, this will restart your rain cycle so that things get re-initialized.  

## Set save location
Press the "2" key to force your save point to the current room, this means you wont have to keep returning to the same room just to test a feature.

## Mod Remix Options
This mod allows you to change the keybinds for the manual reload and save location features.

You can also turn off the auto game restart on build feature in case it is getting in the way or causing issues.

## Working with other mods
RainReloader will sometimes break other mods like Slugbase that don't expect a mod to re-initialise itself, any features you have setup will be wiped and return the wrong value.  
I suggest temporarily adding `|| true` to any features to bypass this while writing code.
```
 if (YourMod.YourFeature.TryGet(self, out bool canDoFeature) && canDoFeature || true)
```

Mostly adapted from: https://github.com/BepInEx/BepInEx.Debug/blob/master/src/ScriptEngine/ScriptEngine.cs
