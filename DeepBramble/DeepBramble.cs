﻿using OWML.ModHelper;
using OWML.Common;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine.Events;
using System;
using System.Reflection;
using DeepBramble.BaseInheritors;
using DeepBramble.MiscBehaviours;
using DeepBramble.Ditylum;
using DeepBramble.Helpers;
using NewHorizons.Utility;
using NewHorizons.Handlers;

namespace DeepBramble
{
    /**
     * The main class of the mod, everything used in the mod branches from this class.
     */
    public class DeepBramble : ModBehaviour {
        //Miscellanious variables
        public INewHorizons NewHorizonsAPI = null;
        public IAchievements AchievementsAPI = null;
        public static float recallTimer = -999;
        public static Material textMat = null;
        public static GameObject signalBodyObject= null;

        //Only needed for debug
        public static Transform relBody = null;
        public static DeepBramble instance;
        private static bool manualLoadEnd = false;
        private static bool useDebugKeybinds = false;

        /**
         * Do NH setup stuff and patch certain methods
         */
        private void Start()
        {
            instance = this;

            //NH setup stuff
            NewHorizonsAPI = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
            NewHorizonsAPI.LoadConfigs(this);

            //Load assetbundles
            TitleScreenHelper.titleBundle = ModHelper.Assets.LoadBundle("assetbundles/titlescreeneffects");
            PostCreditsHelper.leviathanBundle = ModHelper.Assets.LoadBundle("assetbundles/end_bundle");
            textMat = ModHelper.Assets.LoadBundle("assetbundles/text_bundle").LoadAsset<Material>("Assets/Materials/dree_text.mat");
            signalBodyObject= ModHelper.Assets.LoadBundle("assetbundles/signal_body").LoadAsset<GameObject>("Assets/Prefabs/props/signal_body.prefab");
            signalBodyObject.DontDestroyOnLoad<GameObject>();
            signalBodyObject.AddComponent<SignalBody>();

            //Do title screen stuff
            TitleScreenHelper.FirstTimeTitleEdits();
            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                debugPrint("Detecting scene load");
                debugPrint("Scene: " + scene + " | Loadscene: " + loadScene);
                if (loadScene == OWScene.PostCreditsScene)
                    PostCreditsHelper.LoadEndingAdditions();
                else
                    manualLoadEnd = false;
                if (loadScene == OWScene.TitleScreen)
                {
                    TitleScreenHelper.FirstTimeTitleEdits();
                    ForgottenLocator.inBrambleSystem = false;
                }
            };

            //Make a bunch of achievements
            AchievementsAPI = ModHelper.Interaction.TryGetModApi<IAchievements>("xen.AchievementTracker");
            if (AchievementsAPI != null)
            {
                AchievementsAPI.RegisterAchievement("FC.JUKE_FISH", true, this);
                AchievementsAPI.RegisterAchievement("FC.DOUBLE_WARP", true, this);
                AchievementsAPI.RegisterAchievement("FC.ERNESTO", false, this);
                AchievementsAPI.RegisterAchievement("FC.GEYSER", true, this);
                AchievementsAPI.RegisterAchievement("FC.MARSHMALLOW", false, this);
                AchievementsAPI.RegisterAchievement("FC.SCROLL_HAUL", true, this);
                AchievementsAPI.RegisterAchievement("FC.PET", true, this);
                AchievementsAPI.RegisterAchievement("FC.BABY_BITE", true, this);
                AchievementsAPI.RegisterAchievement("FC.BABY_TAXI", true, this);

                AchievementsAPI.RegisterTranslationsFromFiles(this, "achievements");
            }

            //Do stuff when the system starts to load
            UnityEvent<String> startLoadEvent = NewHorizonsAPI.GetChangeStarSystemEvent();
            startLoadEvent.AddListener(UpdateSystemFlag);

            //Do stuff when the system finishes loading
            UnityEvent<string> loadCompleteEvent = NewHorizonsAPI.GetStarSystemLoadedEvent();
            loadCompleteEvent.AddListener(PrepSystem);

            //Make all of the patches
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        /**
         * Updates the inBrambleSystem flag
         * 
         * @param s The name of the loading system
         */
        private void UpdateSystemFlag(String s)
        {
            ForgottenLocator.inBrambleSystem = s.Equals("DeepBramble") && TitleScreenHelper.titleEffectsObject == null;
        }

        /**
         * When a star system finishes loading, check if it's the bramble system
         * If it is the bramble system, do a series of setup steps
         * 
         * @param s The string that's the name of the loaded system? I think? Not used in this method.
         */
        private void PrepSystem(String s)
        {
            //Do this stuff no matter where we are
            ForgottenLocator.blockableSockets = new List<BlockableQuantumSocket>();
            DomesticFishController.Reset();
            ForgottenLocator.inBrambleSystem = s.Equals("DeepBramble") && TitleScreenHelper.titleEffectsObject == null;

            //Do this stuff if we're in the bramble system
            if (NewHorizonsAPI.GetCurrentStarSystem().Equals("DeepBramble"))
            {
                DBSystemHelper.FixDeepBramble();
            }

            //Do this stuff if we're in the hearthian system
            else if(NewHorizonsAPI.GetCurrentStarSystem().Equals("SolarSystem"))
            {
                BaseSystemHelper.FixBaseSystem();
            }

            //Do this stuff if we're in the eye system
            else if (NewHorizonsAPI.GetCurrentStarSystem().Equals("EyeOfTheUniverse"))
            {
                EyeSystemHelper.FixEyeSystem();
            }
        }

        /**
         * Do certain things every frame:
         * -Any debug presses
         * -Fix the ship drift
         * -Send the ship far, far away
         */
        private void Update()
        {
            //Stop forbidding unlocks this frame
            Patches.forbidUnlock = false;

            //Ensure that the starting dimension gets rendered
            if(DBSystemHelper.ensureStarterLoad && ForgottenLocator.startDimensionObject.GetComponent<NewHorizons.Components.Sectored.BrambleSectorController>() != null)
            {
                ForgottenLocator.startDimensionObject.GetComponent<NewHorizons.Components.Sectored.BrambleSectorController>().Invoke("EnableRenderers", 0);
                debugPrint("Start dimension renderers manually enabled");
                DBSystemHelper.ensureStarterLoad = false;
            }

            //Lower the damage audio if the player is hurt only by the hot node
            if (ForgottenLocator.playerAudioController != null)
            {
                if (Patches.DamagedByAmbientHeatOnly())
                    ForgottenLocator.playerAudioController._damageAudioSource.SetMaxVolume(0.15f);
                else
                    ForgottenLocator.playerAudioController._damageAudioSource.SetMaxVolume(0.7f);
            }

            //If needed, count down to the probe being recalled
            if(recallTimer > 0)
            {
                recallTimer -= Time.deltaTime;
            }
            //Otherwise, warp it if needed
            else if(recallTimer <= 0 && recallTimer > -999)
            {
                Locator.GetProbe().ExternalRetrieve(silent: true);
                NotificationData data = new NotificationData(NotificationTarget.All, TranslationHandler.GetTranslation("SCOUT RECALL COMPLETED", TranslationHandler.TextType.UI));
                NotificationManager.SharedInstance.PostNotification(data);
                recallTimer = -999;
            }

            //Lock onto the body that a signal is attached to
            if (OWInput.IsNewlyPressed(InputLibrary.lockOn))
            {
                //Go through each audio signal
                foreach (AudioSignal i in Component.FindObjectsOfType<AudioSignal>())
                {
                    //If it's known and strong enough, try to lock onto it's parent body
                    if (i.GetSignalStrength() == 1 && PlayerData.KnowsSignal(i._name))
                    {
                        //Find the OWRigidBody just under the signal
                        OWRigidbody body = i.GetComponentInChildren<OWRigidbody>();

                        //If this parent is lockable, lock onto it
                        if (body != null && body.IsTargetable())
                        {
                            Locator.GetPlayerBody().gameObject.GetComponent<ReferenceFrameTracker>().TargetReferenceFrame(body.GetReferenceFrame());
                            Patches.forbidUnlock = true;
                            break;
                        }
                    }
                }
            }

            //Print the player's absolute and relative positions when k is pressed
            if (useDebugKeybinds && Keyboard.current[Key.K].wasPressedThisFrame)
            {
                Transform absCenter = null;
                foreach (AstroObject i in Component.FindObjectsOfType<AstroObject>())
                {
                    //Find the center
                    if (i._name == AstroObject.Name.CustomString && i._customName.Equals("The First Dimension"))
                    {
                        absCenter = i.transform;
                    }
                }
                //Finish calculating the absolute position
                GameObject absObject = new GameObject("noname1");
                absObject.transform.position = new Vector3(Locator.GetPlayerCamera().transform.position.x, Locator.GetPlayerCamera().transform.position.y, Locator.GetPlayerCamera().transform.position.z);
                absObject.transform.SetParent(absCenter, true);

                //Finish calculating the relative position
                if (relBody != null) { 
                    GameObject relObject = new GameObject("noname2");
                    relObject.transform.position = new Vector3(Locator.GetPlayerCamera().transform.position.x, Locator.GetPlayerCamera().transform.position.y, Locator.GetPlayerCamera().transform.position.z);
                    relObject.transform.SetParent(relBody, true);
                    debugPrint("Relative position to " + relBody.name + ": " + relObject.transform.localPosition);
                    Destroy(relObject);
                }

                //Print stuff
                debugPrint("Absolute position: " + absObject.transform.localPosition);
            }

            //Tell the speed of the player and ship
            if (useDebugKeybinds && Keyboard.current[Key.L].wasPressedThisFrame)
            {
                string msg = "";
                msg += "Player speed: " + Locator.GetPlayerBody().GetVelocity().magnitude.ToString();
                msg += "\nShip speed: " + Locator.GetShipBody().GetVelocity().magnitude.ToString();
                DeepBramble.debugPrint(msg);
            }
            
            
            //Teleport to a specific point when n is pressed
            if (useDebugKeybinds && Keyboard.current[Key.N].wasPressedThisFrame)
            {
                //Vector3 point = new Vector3(18.1f, -108.8f, 28770.3f); //Graviton's Folly
                Vector3 point = new Vector3(9968.0f, -7.1f, -158.7f); //Dree planet
                //Vector3 point = new Vector3(9559.7f, 9920.6f, -99.4f); //Language Dimension
                //Vector3 point = new Vector3(-24.7f, 10043.4f, -244.6f); //Lava planet start
                //Vector3 point = new Vector3(-257.3f, 9950.4f, 39.4f); //Quantum cave
                //Vector3 point = new Vector3(85.7f, -3.3f, -9960.4f); //Poison planet
                //Vector3 point = new Vector3(349.8f, -322.1f, 31738.1f); //Dilation node
                //Vector3 point = new Vector3(1296.0f, -235.1f, 30832.0f); //Campsite
                //Vector3 point = new Vector3(-10031.3f, 10.4f, -10035.9f); //Domestic dimension
                //Vector3 point = new Vector3(-9974.5f, -121.3f, 3.3f); //Heart
                Transform absCenter = null;
                foreach (AstroObject i in Component.FindObjectsOfType<AstroObject>())
                {
                    //Find the center
                    if (i._name == AstroObject.Name.CustomString && i._customName.Equals("The First Dimension"))
                    {
                        absCenter = i.transform;
                    }
                }

                point = point + absCenter.position;

                FogWarpDetector shipDetector = Locator.GetShipDetector().GetComponent<FogWarpDetector>();
                if (shipDetector.GetOuterFogWarpVolume() != null) {
                    Patches.fogRepositionHandled = true;
                    shipDetector.GetOuterFogWarpVolume().WarpDetector(shipDetector, GameObject.Find("DreeDimension_Body/Sector/OuterWarp").GetComponent<OuterFogWarpVolume>());
                }

                Locator._shipBody.SetPosition(point);
            }

            /*
            //Load the post-credit scene
            if (!manualLoadEnd && Keyboard.current[Key.Backslash].wasPressedThisFrame)
            {
                LoadManager.LoadScene(OWScene.PostCreditsScene);
                manualLoadEnd = true;
            }

            //Clear some useful persistent conditions
                /*if (Keyboard.current[Key.V].wasPressedThisFrame)
                {
                    PlayerData._currentGameSave.SetPersistentCondition("DeepBrambleFound", false);
                    PlayerData._currentGameSave.SetPersistentCondition("LockableSignalFound", false);
                    PlayerData._currentGameSave.SetPersistentCondition("ShipWarpTold", false);
                    PlayerData._currentGameSave.SetPersistentCondition("SignalLockTold", false);
                }
                */
        }

        /**
         * When the player exits a lava geyser, start the timer
         */
        public static void OnGeyserExit(GameObject other)
        {
            if (other.CompareTag("PlayerDetector") && !Locator.GetDeathManager().IsPlayerDying() && !Locator.GetDeathManager().IsPlayerDead())
                AchievementHelper.GrantAchievement("FC.GEYSER");
        }

        /**
         * Retrieve settings for the mod
         */
        public override void Configure(IModConfig config)
        {
            //Whether to use the vanilla title screen or not
            TitleScreenHelper.SetVanillaTitle(config.GetSettingsValue<bool>("Vanilla Title Screen"));

            //Whether to use the vanilla title screen or not
            useDebugKeybinds = config.GetSettingsValue<bool>("Debug Keybinds (DON'T ENABLE)");
        }

        public static void debugPrint(string str)
        {
            instance.ModHelper.Console.WriteLine(str);
        }
    }
}
