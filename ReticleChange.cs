using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UIExpansionKit.API;
using VRC.UI.Core;

namespace ReticleChange
{
    public class ReticleChange : MelonMod
    {
        public static MelonPreferences_Category ReticleChanger;
        public static MelonPreferences_Entry<string> ReticleSelection;
        public Dictionary<string, Sprite> SpriteStorage = new Dictionary<string, Sprite>();
        private Sprite SelectedSprite;
        private FileSystemWatcher watcher;
        private List<(string, string)> StringEnum = new List<(string, string)>();


        public IEnumerator WaitForUIMan()
        {
            //checks for VR, mod doesnt need to run when in VR
            while (GameObject.Find("UserInterface/UnscaledUI/HudContent/Hud/ReticleParent/Reticle") == null) yield return null;
            if (XRDevice.isPresent == true)
            {
                yield break;
            }
            ChangeReticle();
        }

        private void ChangeReticle()
        {
            if (SelectedSprite == null)
            {
                return;
            }
            //gets the location of the reticle and replaces it
            var Reticle = GameObject.Find("UserInterface/UnscaledUI/HudContent/Hud/ReticleParent/Reticle");
            var ReticleImage = Reticle.GetComponent<Image>();
            ReticleImage.sprite = SelectedSprite;
            ReticleImage.overrideSprite = SelectedSprite;
        }

        private void RefreshStringEnum()
        {
            StringEnum.Clear();
            foreach (string key in SpriteStorage.Keys)
            {
                StringEnum.Add((key, key));
            }
        }

        public override void OnApplicationStart()
        {
            //makes the settings
            ReticleChanger = MelonPreferences.CreateCategory("ReticleChanger");
            ReticleSelection = ReticleChanger.CreateEntry<string>("ReticleSelection", "", "Selected Reticle");
            Directory.CreateDirectory("UserData/ReticleChange");

            foreach (string filename in Directory.GetFiles("UserData/ReticleChange/", "*.png"))
            {
                var FileInfo = new FileInfo(filename);
                GetSprite(FileInfo.Name);
            }

            RefreshStringEnum();
            ExpansionKitApi.RegisterSettingAsStringEnum("ReticleChanger", "ReticleSelection", StringEnum);
            ReticleSelection.OnValueChanged += ReticleSelection_OnValueChanged;

            if (!string.IsNullOrEmpty(ReticleSelection.Value))
            {
                SelectedSprite = GetSprite(ReticleSelection.Value);
                if (SelectedSprite == null)
                {
                    MelonLogger.Error($"File does not exist {ReticleSelection.Value}");
                    ReticleSelection.Value = "";
                }
            }
            else
            {
                MelonLogger.Warning("No Selected Reticle");
            }

            //watches for file changes to update list ingame without needing a restart
            watcher = new FileSystemWatcher("UserData/ReticleChange/");
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Deleted += Watcher_Changed;
            watcher.Renamed += Watcher_Renamed;
            watcher.Filter = "*.png";
            watcher.EnableRaisingEvents = true;

            MelonCoroutines.Start(WaitForUIMan());
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            MelonCoroutines.Start(RenamedCoroutine(e));
        }
        public IEnumerator RenamedCoroutine(RenamedEventArgs e)
        {
            yield return null;
            FileInfo fileInfo = new FileInfo(e.FullPath);
            FileInfo fileInfoOld = new FileInfo(e.OldFullPath);
            SpriteStorage.Remove(fileInfoOld.Name);
            GetSprite(fileInfo.Name);
            RefreshStringEnum();
        }
        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            MelonCoroutines.Start(ChangedCoroutine(e));
        }
        public IEnumerator ChangedCoroutine(FileSystemEventArgs e)
        {
            yield return null;
            FileInfo fileInfo = new FileInfo(e.FullPath);
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                GetSprite(fileInfo.Name);
            }
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                SpriteStorage.Remove(fileInfo.Name);
            }
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                SpriteStorage.Remove(fileInfo.Name);
                GetSprite(fileInfo.Name);
            }
            RefreshStringEnum();
        }

        private void ReticleSelection_OnValueChanged(string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                return;
            }
            SelectedSprite = GetSprite(newValue);
            ChangeReticle();
        }

        public Sprite GetSprite(string filename)
        {
            if (SpriteStorage.ContainsKey(filename))
            {
                return SpriteStorage[filename];
            }
            if (!File.Exists($"UserData/ReticleChange/{filename}"))
            {
                return null;
            }
            //takes the image and creates a byte array
            byte[] ReticleBytes = File.ReadAllBytes($"UserData/ReticleChange/{filename}");

            //this bit is magic to take the byte array to a texture2D, from tex2D to a sprite
            var texture = new Texture2D(1, 1);
            ImageConversion.LoadImage(texture, ReticleBytes);
            texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            texture.wrapMode = TextureWrapMode.Clamp;
            var rect = new Rect(0.0f, 0.0f, texture.width, texture.height);
            var pivot = new Vector2(0.5f, 0.5f);
            var border = Vector4.zero;
            var sprite = Sprite.CreateSprite_Injected(texture, ref rect, ref pivot, 100.0f, 0, SpriteMeshType.Tight, ref border, false);
            sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            sprite.name = $"{filename}";
            SpriteStorage.Add(filename, sprite);
            return sprite;
        }
        //stores the sprites
    }
}
