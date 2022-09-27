using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.IO;

// Note to self: Property... classes are actually 'lower' than the Game... objects. Generally, Property... objects seem to be called first (and by the Game... objects) to read and write the property file.

// TODO: Update UI tooltips to match mod behaviour
// TODO: See if there is a way to fix the inaccurate Realization value

namespace ReusableMetadata
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ReusableMetadataPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "11matt556.dysonsphereprogram.ReusableMetadata";
        public const string pluginName = "Reusable Metadata";
        public const string pluginVersion = "1.1.0";

        public static ConfigEntry<bool> useHighestProductionOnly;
        public static ConfigEntry<bool> useVerboseLogging;
        public static ConfigEntry<bool> useSandboxCheat;
        public static ConfigEntry<bool> useSeparatePropertyDirectory;

        public static IDictionary<int, long> topSeedForItem;
        public static ManualLogSource logger;

        public void Awake()
        {
            logger = Logger;
            topSeedForItem = new Dictionary<int, long>();
            Harmony harmony = new Harmony(pluginGuid);

            // Why is this needed here? Adding to dict later does not work...
            topSeedForItem.Add(6001, -1);
            topSeedForItem.Add(6002, -1);
            topSeedForItem.Add(6003, -1);
            topSeedForItem.Add(6004, -1);
            topSeedForItem.Add(6005, -1);
            topSeedForItem.Add(6006, -1);

            useHighestProductionOnly = Config.Bind("Behaviour", "useHighestProductionOnly", false, "When True, only metadata contributions from your highest production cluster will be available. Metadata can be thought of as a 'high score' with this setting enabled. When false, Metadata production is unchanged unchanged from Vanilla.");
            useVerboseLogging = Config.Bind("Debugging", "verboseLogging", false, "For debugging.");
            useSandboxCheat = Config.Bind("Debugging", "enableSandboxCheat", false, "Bumps sandbox metadata from None to 4x multipler. Use at your own risk.");
            useSandboxCheat = Config.Bind("Behaviour", "useSeparatePropertyDirectory", true, "Use a separate file to track the metadata when using this mod. Reccomended to leave this enabled unless you have manually backed up your vanilla property directory.");


            harmony.PatchAll();
            logger.LogInfo(pluginName + " " + pluginVersion + " " + "Patch successful");
        }
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(PropertySystem), nameof(PropertySystem.GetItemTotalProperty))]
        [HarmonyPrefix]
        public static bool GetItemTotalProperty_Patch(int itemId, PropertySystem __instance, ref int __result)
        {
            //GetItemTotalProperty corresponds to the large 'Amount' number at the very top of the Metadata panel

            //GameMain.history.GetPropertyItemComsumption(itemId); 
            long currentClusterSeedKey = GameMain.data.GetClusterSeedKey();
            int productionHighScore = 0;
            int netTotalMetadata = 0;

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo("Current Seed " + currentClusterSeedKey);


            for (int i = 0; i < __instance.propertyDatas.Count; i++)
            {
                ClusterPropertyData clusterPropertyData = __instance.propertyDatas[i];
                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                    ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " production=" + clusterPropertyData.GetItemProduction(itemId) + " seed=" + clusterPropertyData.seedKey);

                int production = clusterPropertyData.GetItemProduction(itemId);

                // If useHighestProductionOnly is set, find the highest metadata value out of all clusters and ignore the others.
                if (ReusableMetadataPlugin.useHighestProductionOnly.Value)
                {
                    if (production > productionHighScore)
                    {
                        productionHighScore = production;
                        netTotalMetadata = productionHighScore;
                        ReusableMetadataPlugin.topSeedForItem[itemId] = clusterPropertyData.seedKey;
                    }
                }
                // Otherwise, just add up the production of all clusters. This is what the Vanilla game does.
                else
                {
                    if (ReusableMetadataPlugin.useVerboseLogging.Value)
                        ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " Production=" + production + " seed=" + clusterPropertyData.seedKey);
                    netTotalMetadata += production;
                }
            }

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " Calculated Total=" + netTotalMetadata);

            __result = netTotalMetadata;

            return false;
        }

        [HarmonyPatch(
            typeof(PropertySystem),
            nameof(PropertySystem.GetItemAvaliableProperty),
            new[]
            {
                typeof(long),
                typeof(int)
            }
            )
         ]
        [HarmonyPrefix]
        public static bool GetItemAvaliableProperty_Patch(long seedKey, int itemId, PropertySystem __instance, ref int __result)
        {
            //ClusterPropertyData clusterData = __instance.GetClusterData(seedKey);
            int availableMetadata = __instance.GetItemTotalProperty(itemId); //Start with all metadata
            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch_Start={availableMetadata} ID={itemId} ");

            // Sanity check to make sure we are acting on the current seed.
            if (GameMain.data.GetClusterSeedKey() == seedKey)
            {
                // Only subtract metadata consumed of current seed, not all seeds. This one line is really the main point of this whole mod...
                // Important thing to remember! 
                // GameMain.history.GetPropertyItemComsumption reads the game data itself. It will OVERRULE whatever is in the property file
                // PropertySystem.GetItemConsumption reads from the property file itself! 
                // I use GameMain.history here because it seems to prtevent the vanilla bug where metadata is lost when realizing and exiting without saving.

                //availableMetadata -= ReusableMetadataPlugin.gameSaveConsTextDict[itemId];
                availableMetadata -= GameMain.history.GetPropertyItemComsumption(itemId);
                //availableMetadata -= __instance.GetItemConsumption(seedKey, itemId);

                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                {
                    ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch ID={itemId} PropertySystem.GetItemConsumption={__instance.GetItemConsumption(seedKey, itemId)} seed={seedKey}");
                    ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch ID={itemId} GameMain.history.GetPropertyItemComsumption={GameMain.history.GetPropertyItemComsumption(itemId)} seed={seedKey}");
                }

                // Make sure we can't spend metadata from the current seed.
                if (ReusableMetadataPlugin.useHighestProductionOnly.Value && seedKey != ReusableMetadataPlugin.topSeedForItem[itemId])
                {
                    // Do nothing because we did not actually contribute any metadata to the total.
                }
                else
                {
                    // This production contributed to the total metadata and so needs to be removed from the available metadata.
                    availableMetadata -= __instance.GetItemProduction(seedKey, itemId);
                }
            }

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch_Result={__result} ID={itemId} ");

            __result = availableMetadata;

            return false;
        }

        [HarmonyPatch(typeof(GameDesc))]
        [HarmonyPatch("propertyMultiplier", MethodType.Getter)]
        [HarmonyPostfix]
        public static void PropertyMultiplier_Patch(GameDesc __instance, ref float __result)
        {
            // Give 4x multiplier in sandbox mode if useSandboxCheat is set.
            // Note: Not really tested this.
            if (__instance.isSandboxMode && ReusableMetadataPlugin.useSandboxCheat.Value)
            {
                __result = 4f;
            }
        }

        [HarmonyPatch(typeof(GameConfig))]
        [HarmonyPatch("propertyFolder", MethodType.Getter)]
        [HarmonyPostfix]
        public static void PropertyFolder_Patch(ref string __result)
        {

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"propertyFolder_Patch: __result={__result}");

            string propertyFileName = $"{AccountData.me.userId}"; // Same as Vanilla
            string baseDir = Path.Combine(Paths.ConfigPath, ReusableMetadataPlugin.pluginName.Replace(" ", ""));
            string myPropertyDir = Path.Combine(baseDir, "Property");

            // Create property the custom directory if it doesnt exist
            if (!Directory.Exists(myPropertyDir))
            {
                Directory.CreateDirectory(myPropertyDir);
            }

            string myPropertyPath = Path.Combine(myPropertyDir, propertyFileName);
            string vanillaPropertyPath = Path.Combine(__result, propertyFileName);

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
            {
                ReusableMetadataPlugin.logger.LogInfo($"propertyFolder_Patch: baseDir={baseDir}");
                ReusableMetadataPlugin.logger.LogInfo($"propertyFolder_Patch: vanillaPropertyPath={vanillaPropertyPath}");
                ReusableMetadataPlugin.logger.LogInfo($"propertyFolder_Patch: myPropertyDir={myPropertyDir}");
                ReusableMetadataPlugin.logger.LogInfo($"propertyFolder_Patch: myPropertyPath={myPropertyPath}");
                ReusableMetadataPlugin.logger.LogInfo($"propertyFolder_Patch: vanillaPropertyPath={vanillaPropertyPath}");
            }


            // If the custom property file doesn't exist, copy the existing property file as a starting point
            if (!File.Exists(myPropertyPath))
            {
                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                    ReusableMetadataPlugin.logger.LogInfo($"Copying from {vanillaPropertyPath} to {myPropertyPath}");

                File.Copy(vanillaPropertyPath, myPropertyPath);
            }



            //TODO: Check that propertyFileName exists in the default directory. If it doesn't then throw an error that is visible to user. (Should only happen if an update breaks things)
            // Finally, set the rsult so that the new property file is used
            //__result = myPropertyDir;

        }
    }
}