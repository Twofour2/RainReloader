using System;
using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Menu.Remix;

namespace RainReloader
{
    public class RainReloaderOptions : OptionInterface
    {

        public readonly Configurable<UnityEngine.KeyCode> reloadKeyCode;
        public readonly Configurable<bool> reloadOnRestart;

        public RainReloaderOptions()
        {
            reloadKeyCode = config.Bind("reloadKeyCode", UnityEngine.KeyCode.Alpha1);
            reloadOnRestart = config.Bind("reloadOnRestart", false);
        }

        public override void Initialize()
        {
            OpTab opTab = new OpTab(this, "Reloader Settings");
            this.Tabs = new OpTab[] { opTab };
            UIelement[] keyBindElement = new UIelement[]
            {
                new OpLabel(10f, 580f, "Reload Key"),
                new OpKeyBinder(reloadKeyCode, new UnityEngine.Vector2(10f, 540f), new UnityEngine.Vector2(90f, 25f))
            };
            
            opTab.AddItems(keyBindElement);
        }

    }

}
