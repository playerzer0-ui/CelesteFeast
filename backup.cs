using System;
using System.Reflection;
using Celeste;
using Monocle;

namespace Celeste.Mod.CelesteFeast
{
    public class CelesteFeastModule : EverestModule
    {
        public static CelesteFeastModule Instance { get; private set; }

        public override Type SettingsType => typeof(CelesteFeastModuleSettings);
        public static CelesteFeastModuleSettings Settings => (CelesteFeastModuleSettings)Instance._Settings;

        public override Type SessionType => typeof(CelesteFeastModuleSession);
        public static CelesteFeastModuleSession Session => (CelesteFeastModuleSession)Instance._Session;

        public override Type SaveDataType => typeof(CelesteFeastModuleSaveData);
        public static CelesteFeastModuleSaveData SaveData => (CelesteFeastModuleSaveData)Instance._SaveData;

        private static readonly FieldInfo SpriteField =
            typeof(Strawberry).GetField("sprite", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo OnAnimateMethod =
            typeof(Strawberry).GetMethod("OnAnimate", BindingFlags.NonPublic | BindingFlags.Instance);

        public CelesteFeastModule()
        {
            Instance = this;
#if DEBUG
            Logger.SetLogLevel(nameof(CelesteFeastModule), LogLevel.Verbose);
#else
            Logger.SetLogLevel(nameof(CelesteFeastModule), LogLevel.Info);
#endif
        }

        public override void Load()
        {
            On.Celeste.Strawberry.Added += Strawberry_Added;
        }

        public override void Unload()
        {
            On.Celeste.Strawberry.Added -= Strawberry_Added;
        }

        private void Strawberry_Added(On.Celeste.Strawberry.orig_Added orig, Strawberry self, Scene scene)
        {
            orig(self, scene);

            if (self.Golden || self.Moon || global::Celeste.SaveData.Instance.CheckStrawberry(self.ID))
                return;

            Level level = scene as Level;
            string sid = level?.Session.Area.SID;

            string spriteId = sid switch
            {
                "Celeste/1-ForsakenCity"    => "orange",
                "Celeste/2-OldSite"         => "icecream",
                "Celeste/3-CelestialResort" => "banana",
                _ => null
            };

            if (spriteId == null) return;

            Sprite oldSprite = (Sprite)SpriteField.GetValue(self);
            self.Remove(oldSprite);

            Sprite newSprite = GFX.SpriteBank.Create(spriteId);
            SpriteField.SetValue(self, newSprite);
            self.Add(newSprite);

            newSprite.OnFrameChange = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), self, OnAnimateMethod);

            if (self.Winged) newSprite.Play("flap");
        }
    }
}