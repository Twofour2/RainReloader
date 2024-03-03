using System;
using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Menu.Remix;
using System.Xml.Linq;

namespace RainReloader
{
    public class RainReloaderOptions : OptionInterface
    {

        public readonly Configurable<UnityEngine.KeyCode> reloadKeyCode;
        public readonly Configurable<UnityEngine.KeyCode> setDenKeyCode;
        public readonly Configurable<bool> restartOnFileChange;

        public RainReloaderOptions()
        {
            reloadKeyCode = config.Bind("reloadKeyCode", UnityEngine.KeyCode.Alpha1);
            setDenKeyCode = config.Bind("setDenKeyCode", UnityEngine.KeyCode.Alpha2);
            restartOnFileChange = config.Bind("restartOnFileChange", true);
        }

        public override void Initialize()
        {
            OpTab opTab = new OpTab(this, "Reloader Settings");
            this.Tabs = new OpTab[] { opTab };
            UIelement[] reloadKeyBindElement = new UIelement[]
            {
                new OpLabel(10f, 580f, "Reload Key"),
                new OpKeyBinder(reloadKeyCode, new UnityEngine.Vector2(10f, 550f), new UnityEngine.Vector2(90f, 25f))
            };
            UIelement[] setDenKeyBindElement = new UIelement[]
            {
                new OpLabel(10f, 530f, "Set Den (Save location) Key"),
                new OpKeyBinder(setDenKeyCode, new UnityEngine.Vector2(10f, 500f), new UnityEngine.Vector2(90f, 25f))
            };
            UIelement[] restartOnFileChangeElement = new UIelement[] {
                new OpCheckBox(restartOnFileChange, 10, 460),
                new OpLabel(45f, 460f, "Reload to last save on file change.")
            };

            opTab.AddItems(reloadKeyBindElement);
            opTab.AddItems(setDenKeyBindElement);
            opTab.AddItems(restartOnFileChangeElement);
        }

    }

}
