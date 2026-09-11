using System;
using System.Reflection;
using Celeste;
using Monocle;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.CelesteFeast
{
    public class CelesteFeastModule : EverestModule
    {
        public static CelesteFeastModule Instance { get; private set; }

        public override Type SettingsType =>
            typeof(CelesteFeastModuleSettings);

        public static CelesteFeastModuleSettings Settings =>
            (CelesteFeastModuleSettings)Instance._Settings;

        public override Type SessionType =>
            typeof(CelesteFeastModuleSession);

        public static CelesteFeastModuleSession Session =>
            (CelesteFeastModuleSession)Instance._Session;

        public override Type SaveDataType =>
            typeof(CelesteFeastModuleSaveData);

        public static CelesteFeastModuleSaveData SaveData =>
            (CelesteFeastModuleSaveData)Instance._SaveData;


        // ============================================================
        // STRAWBERRY PRIVATE MEMBERS
        // ============================================================

        private static readonly FieldInfo SpriteField =
            typeof(Strawberry).GetField(
                "sprite",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        private static readonly MethodInfo OnAnimateMethod =
            typeof(Strawberry).GetMethod(
                "OnAnimate",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );


        // ============================================================
        // STRAWBERRIES COUNTER PRIVATE DISPLAY FIELDS
        // ============================================================
        //
        // We deliberately modify THESE temporarily instead of
        // counter.Amount / OutOf / ShowOutOf.
        //
        // Setting Amount triggers sounds, wiggles and flashes.
        // These fields are only the cached strings used by Render().
        // ============================================================

        private static readonly FieldInfo CounterAmountStringField =
            typeof(StrawberriesCounter).GetField(
                "sAmount",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        private static readonly FieldInfo CounterOutOfStringField =
            typeof(StrawberriesCounter).GetField(
                "sOutOf",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        private static readonly FieldInfo CounterShowOutOfField =
            typeof(StrawberriesCounter).GetField(
                "showOutOf",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );


        // ============================================================
        // IL HOOKS
        // ============================================================

        // Little collectible icons shown on checkpoint polaroids.
        private ILHook chapterPanelCheckpointHook;

        // Changes the texture used by StrawberriesCounter.
        private ILHook strawberriesCounterHook;

        // Journal Progress strawberry-column icon.
        private ILHook journalProgressHook;


        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public CelesteFeastModule()
        {
            Instance = this;

#if DEBUG
            Logger.SetLogLevel(
                nameof(CelesteFeastModule),
                LogLevel.Verbose
            );
#else
            Logger.SetLogLevel(
                nameof(CelesteFeastModule),
                LogLevel.Info
            );
#endif
        }


        // ============================================================
        // LOAD
        // ============================================================

        public override void Load()
        {
            // --------------------------------------------------------
            // Gameplay collectible sprites
            // --------------------------------------------------------

            On.Celeste.Strawberry.Added +=
                Strawberry_Added;


            // --------------------------------------------------------
            // StrawberriesCounter render wrapper
            //
            // This changes ONLY the displayed chapter count.
            // --------------------------------------------------------

            On.Celeste.StrawberriesCounter.Render +=
                StrawberriesCounter_RenderDetour;


            // --------------------------------------------------------
            // Checkpoint collectible icons
            // --------------------------------------------------------

            MethodInfo drawCheckpointMethod =
                typeof(OuiChapterPanel).GetMethod(
                    "orig_DrawCheckpoint",
                    BindingFlags.NonPublic |
                    BindingFlags.Instance
                );

            if (drawCheckpointMethod != null)
            {
                chapterPanelCheckpointHook =
                    new ILHook(
                        drawCheckpointMethod,
                        OuiChapterPanel_DrawCheckpoint
                    );
            }
            else
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not find OuiChapterPanel.orig_DrawCheckpoint."
                );
            }


            // --------------------------------------------------------
            // StrawberryCounter icon replacement
            // --------------------------------------------------------

            MethodInfo strawberriesRenderMethod =
                typeof(StrawberriesCounter).GetMethod(
                    "Render",
                    BindingFlags.Public |
                    BindingFlags.Instance
                );

            if (strawberriesRenderMethod != null)
            {
                strawberriesCounterHook =
                    new ILHook(
                        strawberriesRenderMethod,
                        StrawberriesCounter_RenderIL
                    );
            }
            else
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not find StrawberriesCounter.Render."
                );
            }


            // --------------------------------------------------------
            // Journal Progress collectible icon
            // --------------------------------------------------------

            ConstructorInfo journalProgressCtor =
                typeof(OuiJournalProgress).GetConstructor(
                    BindingFlags.Public |
                    BindingFlags.Instance,
                    null,
                    new Type[]
                    {
                        typeof(OuiJournal)
                    },
                    null
                );

            if (journalProgressCtor != null)
            {
                journalProgressHook =
                    new ILHook(
                        journalProgressCtor,
                        OuiJournalProgress_Ctor
                    );
            }
            else
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not find OuiJournalProgress constructor."
                );
            }
        }


        // ============================================================
        // UNLOAD
        // ============================================================

        public override void Unload()
        {
            On.Celeste.Strawberry.Added -=
                Strawberry_Added;

            On.Celeste.StrawberriesCounter.Render -=
                StrawberriesCounter_RenderDetour;


            chapterPanelCheckpointHook?.Dispose();
            chapterPanelCheckpointHook = null;


            strawberriesCounterHook?.Dispose();
            strawberriesCounterHook = null;


            journalProgressHook?.Dispose();
            journalProgressHook = null;
        }


        // ============================================================
        // CHAPTER -> GAMEPLAY COLLECTIBLE
        // ============================================================

        private static string GetGameplayCollectibleSprite(
            string sid
        )
        {
            return sid switch
            {
                "Celeste/1-ForsakenCity" =>
                    "orange",

                "Celeste/2-OldSite" =>
                    "icecream",

                "Celeste/3-CelestialResort" =>
                    "banana",

                "Celeste/4-GoldenRidge" =>
                    "chocolate",

                "Celeste/5-MirrorTemple" =>
                    "melon",

                _ => null
            };
        }


        // ============================================================
        // CHAPTER -> GUI COLLECTIBLE
        // ============================================================

        private static string GetGuiCollectibleTexture(
            string original,
            string sid
        )
        {
            return sid switch
            {
                "Celeste/1-ForsakenCity" =>
                    "collectables/orange",

                "Celeste/2-OldSite" =>
                    "collectables/icecream",

                "Celeste/3-CelestialResort" =>
                    "collectables/banana",

                "Celeste/4-GoldenRidge" =>
                    "collectables/chocolate",

                "Celeste/5-MirrorTemple" =>
                    "collectables/melon",

                _ => original
            };
        }


        // ============================================================
        // GAMEPLAY STRAWBERRY REPLACEMENT
        // ============================================================

        private void Strawberry_Added(
            On.Celeste.Strawberry.orig_Added orig,
            Strawberry self,
            Scene scene
        )
        {
            // Let vanilla construct everything normally first.
            orig(self, scene);


            // --------------------------------------------------------
            // Leave special / already-collected berries untouched.
            // --------------------------------------------------------

            if (
                self.Golden ||
                self.Moon ||
                global::Celeste.SaveData.Instance.CheckStrawberry(
                    self.ID
                )
            )
            {
                return;
            }


            Level level =
                scene as Level;

            string sid =
                level?.Session.Area.SID;

            string spriteId =
                GetGameplayCollectibleSprite(
                    sid
                );

            if (spriteId == null)
            {
                return;
            }


            // --------------------------------------------------------
            // Get vanilla Sprite.
            // --------------------------------------------------------

            if (
                SpriteField?.GetValue(self)
                is not Sprite oldSprite
            )
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not access Strawberry.sprite."
                );

                return;
            }


            // --------------------------------------------------------
            // Remove vanilla berry Sprite.
            // --------------------------------------------------------

            self.Remove(
                oldSprite
            );


            // --------------------------------------------------------
            // Create food Sprite.
            // --------------------------------------------------------

            Sprite newSprite =
                GFX.SpriteBank.Create(
                    spriteId
                );


            SpriteField.SetValue(
                self,
                newSprite
            );


            self.Add(
                newSprite
            );


            // --------------------------------------------------------
            // Restore vanilla Strawberry.OnAnimate.
            // --------------------------------------------------------

            if (OnAnimateMethod != null)
            {
                newSprite.OnFrameChange =
                    (Action<string>)Delegate.CreateDelegate(
                        typeof(Action<string>),
                        self,
                        OnAnimateMethod
                    );
            }


            if (self.Winged)
            {
                newSprite.Play(
                    "flap"
                );
            }
        }


        // ============================================================
        // GAMEPLAY HUD COUNTER DISPLAY
        // ============================================================
        //
        // IMPORTANT:
        //
        // We DO NOT change:
        //
        //     counter.Amount
        //     counter.OutOf
        //     counter.ShowOutOf
        //
        // because Amount's setter triggers sound / wiggle / flash.
        //
        // Instead, immediately before vanilla Render(), we temporarily
        // change:
        //
        //     sAmount
        //     sOutOf
        //     showOutOf
        //
        // Then after Render() finishes, the original values are put
        // straight back.
        //
        // Vanilla therefore remains fully responsible for:
        //
        //     - when the counter appears
        //     - when it disappears
        //     - collection sound
        //     - wiggle
        //     - flash
        //     - animation / timer
        //
        // ============================================================

        private static void StrawberriesCounter_RenderDetour(
            On.Celeste.StrawberriesCounter.orig_Render orig,
            StrawberriesCounter self
        )
        {
            // --------------------------------------------------------
            // Only counters inside gameplay Levels can be candidates.
            // --------------------------------------------------------

            if (
                self.Entity?.Scene
                is not Level level
            )
            {
                orig(self);
                return;
            }


            // --------------------------------------------------------
            // The Epilogue results counter also exists inside a Level.
            // It must stay a GLOBAL Ingredients counter.
            // --------------------------------------------------------

            if (self.Entity is CS08_Ending)
            {
                orig(self);
                return;
            }


            string sid =
                level.Session.Area.SID;


            // --------------------------------------------------------
            // Only alter chapters handled by CelesteFeast.
            // --------------------------------------------------------

            if (
                GetGameplayCollectibleSprite(
                    sid
                ) == null
            )
            {
                orig(self);
                return;
            }


            // --------------------------------------------------------
            // Make sure reflection succeeded.
            // --------------------------------------------------------

            if (
                CounterAmountStringField == null ||
                CounterOutOfStringField == null ||
                CounterShowOutOfField == null
            )
            {
                orig(self);
                return;
            }


            AreaKey area =
                level.Session.Area;

            AreaData areaData =
                AreaData.Get(
                    area
                );

            global::Celeste.SaveData saveData =
                global::Celeste.SaveData.Instance;


            if (
                areaData == null ||
                saveData == null
            )
            {
                orig(self);
                return;
            }


            // --------------------------------------------------------
            // Validate AreaStats.
            // --------------------------------------------------------

            if (
                area.ID < 0 ||
                area.ID >= saveData.Areas_Safe.Count
            )
            {
                orig(self);
                return;
            }


            AreaStats areaStats =
                saveData.Areas_Safe[
                    area.ID
                ];


            if (
                areaStats == null ||
                areaStats.Modes == null ||
                (int)area.Mode < 0 ||
                (int)area.Mode >= areaStats.Modes.Length
            )
            {
                orig(self);
                return;
            }


            AreaModeStats modeStats =
                areaStats.Modes[
                    (int)area.Mode
                ];


            if (
                modeStats == null ||
                areaData.Mode == null ||
                areaData.Mode.Length == 0 ||
                areaData.Mode[0] == null
            )
            {
                orig(self);
                return;
            }


            // --------------------------------------------------------
            // Save vanilla display strings.
            // --------------------------------------------------------

            string originalAmount =
                CounterAmountStringField.GetValue(
                    self
                ) as string;

            string originalOutOf =
                CounterOutOfStringField.GetValue(
                    self
                ) as string;

            bool originalShowOutOf =
                (bool)CounterShowOutOfField.GetValue(
                    self
                );


            // --------------------------------------------------------
            // Build CelesteFeast display.
            //
            // Example:
            //
            // Chapter 1:
            //
            //     3 / 20
            //
            // If Golden is collected after all 20:
            //
            //     21 / 20
            // --------------------------------------------------------

            string chapterAmount =
                modeStats.TotalStrawberries
                    .ToString();

            string chapterOutOf =
                "/" +
                areaData.Mode[0]
                    .TotalStrawberries;


            try
            {
                // ----------------------------------------------------
                // Temporarily replace DISPLAY ONLY.
                //
                // These are direct FieldInfo writes, therefore the
                // Amount property setter is never called.
                // ----------------------------------------------------

                CounterAmountStringField.SetValue(
                    self,
                    chapterAmount
                );

                CounterOutOfStringField.SetValue(
                    self,
                    chapterOutOf
                );

                CounterShowOutOfField.SetValue(
                    self,
                    true
                );


                // Let vanilla draw the counter normally.
                orig(self);
            }
            finally
            {
                // ----------------------------------------------------
                // Restore vanilla state immediately after rendering.
                // ----------------------------------------------------

                CounterAmountStringField.SetValue(
                    self,
                    originalAmount
                );

                CounterOutOfStringField.SetValue(
                    self,
                    originalOutOf
                );

                CounterShowOutOfField.SetValue(
                    self,
                    originalShowOutOf
                );
            }
        }


        // ============================================================
        // IL MATCHERS
        // ============================================================

        private static bool MatchCollectablesStrawberry(
            Instruction instruction
        )
        {
            return instruction.MatchLdstr(
                "collectables/strawberry"
            );
        }


        private static bool MatchJournalStrawberry(
            Instruction instruction
        )
        {
            return instruction.MatchLdstr(
                "strawberry"
            );
        }


        // ============================================================
        // CHECKPOINT POLAROID IL HOOK
        // ============================================================

        private static void OuiChapterPanel_DrawCheckpoint(
            ILContext il
        )
        {
            ILCursor cursor =
                new ILCursor(il);


            if (
                cursor.TryGotoNext(
                    MoveType.After,
                    MatchCollectablesStrawberry
                )
            )
            {
                // Push OuiChapterPanel "this".
                cursor.Emit(
                    OpCodes.Ldarg_0
                );


                cursor.EmitDelegate<
                    Func<
                        string,
                        OuiChapterPanel,
                        string
                    >
                >(
                    ReplaceCheckpointCollectibleTexture
                );
            }
            else
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not find checkpoint strawberry GUI texture."
                );
            }
        }


        // ============================================================
        // CHECKPOINT POLAROID TEXTURE
        // ============================================================

        private static string ReplaceCheckpointCollectibleTexture(
            string original,
            OuiChapterPanel panel
        )
        {
            string sid =
                AreaData.Get(
                    panel.Area
                )?.SID;


            return GetGuiCollectibleTexture(
                original,
                sid
            );
        }


        // ============================================================
        // STRAWBERRY COUNTER ICON IL HOOK
        // ============================================================
        //
        // This hook ONLY changes which icon texture is drawn.
        //
        // It does not touch amounts or counter behavior.
        // ============================================================

        private static void StrawberriesCounter_RenderIL(
            ILContext il
        )
        {
            ILCursor cursor =
                new ILCursor(il);


            if (
                cursor.TryGotoNext(
                    MoveType.After,
                    MatchCollectablesStrawberry
                )
            )
            {
                // Push StrawberriesCounter "this".
                cursor.Emit(
                    OpCodes.Ldarg_0
                );


                cursor.EmitDelegate<
                    Func<
                        string,
                        StrawberriesCounter,
                        string
                    >
                >(
                    ReplaceStrawberriesCounterTexture
                );
            }
            else
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not find StrawberriesCounter strawberry GUI texture."
                );
            }
        }


        // ============================================================
        // STRAWBERRY COUNTER TEXTURE
        // ============================================================
        //
        // Handles:
        //
        // - File Select
        // - Epilogue
        // - Chapter Panel
        // - In-level HUD
        //
        // ============================================================

        private static string ReplaceStrawberriesCounterTexture(
            string original,
            StrawberriesCounter counter
        )
        {
            // --------------------------------------------------------
            // File Select
            //
            // Global total = Ingredients
            // --------------------------------------------------------

            if (counter.Entity is OuiFileSelectSlot)
            {
                return "ingredients";
            }


            // --------------------------------------------------------
            // Epilogue
            //
            // Global total = Ingredients
            // --------------------------------------------------------

            if (counter.Entity is CS08_Ending)
            {
                return "ingredients";
            }


            // --------------------------------------------------------
            // Chapter Panel
            //
            // Chapter-specific food.
            // --------------------------------------------------------

            if (counter.Entity is OuiChapterPanel panel)
            {
                string sid =
                    AreaData.Get(
                        panel.Area
                    )?.SID;


                return GetGuiCollectibleTexture(
                    original,
                    sid
                );
            }


            // --------------------------------------------------------
            // In-level HUD
            //
            // Chapter-specific food.
            // --------------------------------------------------------

            if (
                counter.Entity?.Scene
                is Level level
            )
            {
                string sid =
                    level.Session.Area.SID;


                return GetGuiCollectibleTexture(
                    original,
                    sid
                );
            }


            return original;
        }


        // ============================================================
        // JOURNAL PROGRESS IL HOOK
        // ============================================================

        private static void OuiJournalProgress_Ctor(
            ILContext il
        )
        {
            ILCursor cursor =
                new ILCursor(il);


            if (
                cursor.TryGotoNext(
                    MoveType.After,
                    MatchJournalStrawberry
                )
            )
            {
                cursor.EmitDelegate<
                    Func<
                        string,
                        string
                    >
                >(
                    ReplaceJournalCollectibleIcon
                );
            }
            else
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not find journal strawberry icon."
                );
            }
        }


        // ============================================================
        // JOURNAL COLLECTIBLE ICON
        // ============================================================

        private static string ReplaceJournalCollectibleIcon(
            string original
        )
        {
            return "ingredients";
        }
    }
}