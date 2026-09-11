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
        // IL HOOKS
        // ============================================================

        // Little collectible icons shown on checkpoint polaroids.
        private ILHook chapterPanelCheckpointHook;

        // Chapter Panel / in-level HUD / File Select counter.
        private ILHook strawberriesCounterHook;

        // Journal Progress strawberry-column icon.
        private ILHook journalProgressHook;

        // Epilogue feast result pictures.
        private ILHook endingPortraitHook;


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
            // Gameplay collectible
            // --------------------------------------------------------

            On.Celeste.Strawberry.Added += Strawberry_Added;


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
            // Strawberry counters
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
                        StrawberriesCounter_Render
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


            // --------------------------------------------------------
            // Epilogue feast result pictures
            // --------------------------------------------------------

            MethodInfo endingOnBeginMethod =
                typeof(CS08_Ending).GetMethod(
                    "OnBegin",
                    BindingFlags.Public |
                    BindingFlags.Instance
                );

            if (endingOnBeginMethod != null)
            {
                endingPortraitHook =
                    new ILHook(
                        endingOnBeginMethod,
                        CS08_Ending_OnBegin
                    );
            }
            else
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    "Could not find CS08_Ending.OnBegin."
                );
            }
        }


        // ============================================================
        // UNLOAD
        // ============================================================

        public override void Unload()
        {
            On.Celeste.Strawberry.Added -= Strawberry_Added;


            chapterPanelCheckpointHook?.Dispose();
            chapterPanelCheckpointHook = null;


            strawberriesCounterHook?.Dispose();
            strawberriesCounterHook = null;


            journalProgressHook?.Dispose();
            journalProgressHook = null;


            endingPortraitHook?.Dispose();
            endingPortraitHook = null;
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
            // Let vanilla build the Strawberry normally first.
            //
            // This creates:
            //
            // - sprite
            // - bloom
            // - light
            // - wigglers
            // - follower
            // - etc.
            //
            // We modify only the instance after vanilla is finished.
            orig(self, scene);


            // --------------------------------------------------------
            // Leave special strawberries untouched
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


            // --------------------------------------------------------
            // Find current chapter
            // --------------------------------------------------------

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
            // Replace vanilla strawberry Sprite
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


            self.Remove(
                oldSprite
            );


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


            // Restore Strawberry.OnAnimate so vanilla collection
            // behavior still works with our replacement sprite.
            if (OnAnimateMethod != null)
            {
                newSprite.OnFrameChange =
                    (Action<string>)Delegate.CreateDelegate(
                        typeof(Action<string>),
                        self,
                        OnAnimateMethod
                    );
            }


            // Winged berries need the flap animation.
            if (self.Winged)
            {
                newSprite.Play(
                    "flap"
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
        // STRAWBERRY COUNTER IL HOOK
        // ============================================================

        private static void StrawberriesCounter_Render(
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
            // Total across all chapters = Ingredients
            // --------------------------------------------------------

            if (counter.Entity is OuiFileSelectSlot)
            {
                return "ingredients";
            }


            // --------------------------------------------------------
            // Epilogue ending screen
            //
            // Total ingredients collected = Ingredients
            // --------------------------------------------------------

            if (counter.Entity is CS08_Ending)
            {
                return "ingredients";
            }


            // --------------------------------------------------------
            // Chapter Panel
            //
            // Use chapter-specific food
            // --------------------------------------------------------

            if (counter.Entity is OuiChapterPanel panel)
            {
                string sid =
                    AreaData.Get(panel.Area)?.SID;

                return GetGuiCollectibleTexture(
                    original,
                    sid
                );
            }


            // --------------------------------------------------------
            // In-level HUD
            //
            // Use current chapter's food
            // --------------------------------------------------------

            if (counter.Entity?.Scene is Level level)
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

        // ============================================================
        // EPILOGUE FEAST PORTRAIT IL HOOK
        // ============================================================

        private static void CS08_Ending_OnBegin(
            ILContext il
        )
        {
            int replaced = 0;

            foreach (Instruction instruction in il.Body.Instructions)
            {
                if (instruction.MatchLdstr("final1"))
                {
                    instruction.Operand =
                        "CelesteFeast/final1";

                    replaced++;
                }
                else if (instruction.MatchLdstr("final2"))
                {
                    instruction.Operand =
                        "CelesteFeast/final2";

                    replaced++;
                }
                else if (instruction.MatchLdstr("final3"))
                {
                    instruction.Operand =
                        "CelesteFeast/final3";

                    replaced++;
                }
                else if (instruction.MatchLdstr("final4"))
                {
                    instruction.Operand =
                        "CelesteFeast/final4";

                    replaced++;
                }
                else if (instruction.MatchLdstr("final5"))
                {
                    instruction.Operand =
                        "CelesteFeast/final5";

                    replaced++;
                }
            }

            if (replaced != 5)
            {
                Logger.Log(
                    LogLevel.Warn,
                    nameof(CelesteFeastModule),
                    $"Expected to replace 5 Epilogue portraits, replaced {replaced}."
                );
            }
        }

    }
}