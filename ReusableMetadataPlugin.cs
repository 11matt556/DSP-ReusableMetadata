using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;
using System;

// TODO: Try to fix vanilla bug of spent metadata being lost when spent but game is not saved.
// The 'Current Game Utilization' number displayed in the UI does NOT include the metadata lost due to not saving and so should be the more trusted 
// It is not entirely clear to me where this "current game realization' number is derived.
// Presumably 'Current Game Realization' is in the save data while the number being used by the game (I assume GetItemConsumption) is being read from the property/metadata file.
// Looks like 'Current Game Realization' is in UIPropertyEntry,UpdateUITexts, this.gamesaveConsText.text. Interestingly, this links back to the same GetItemConsumption function...
// In theory, this.gamesaveConsText.text calls GetItemConsumption and so should == GetItemConsumption used by GetItemTotalProperty but I'm not sure that it does in practice
// this.gamesaveConsText.text != GetItemConsumption in GetItemTotalProperty when Metadata has been lost. this.gamesaveConsText.text is the more accurate value.
// Maybe ClusterPropertyData used by PropertySystem loads from the property file, but the ClusterPropertyData in GameHistoryData (used by UIPropertyEntry) is loaded from the save.
// I don't see where this happens in the code, but that seems to be the behavior.

// TODO: Update UI tooltips to match mod behaviour

namespace ReusableMetadata
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ReusableMetadataPlugin : BaseUnityPlugin
    {

        public const string pluginGuid = "11matt556.dysonsphereprogram.ReusableMetadata";
        public const string pluginName = "Reusable Metadata";
        public const string pluginVersion = "1.0.2";
        public static ManualLogSource logger;
        public static ConfigEntry<bool> useHighestProductionOnly;
        public static ConfigEntry<bool> useVerboseLogging;
        public static IDictionary<int, long> topSeedForItem;
        public static IDictionary<int, int> gameSaveConsTextDict;


        public void Awake()
        {
            logger = Logger;
            topSeedForItem = new Dictionary<int, long>();
            gameSaveConsTextDict = new Dictionary<int, int>();
            Harmony harmony = new Harmony(pluginGuid);

            // Why is this needed here? Adding to dict later does not work...
            topSeedForItem.Add(6001, -1);
            topSeedForItem.Add(6002, -1);
            topSeedForItem.Add(6003, -1);
            topSeedForItem.Add(6004, -1);
            topSeedForItem.Add(6005, -1);
            topSeedForItem.Add(6006, -1);

            gameSaveConsTextDict.Add(6001, -1);
            gameSaveConsTextDict.Add(6002, -1);
            gameSaveConsTextDict.Add(6003, -1);
            gameSaveConsTextDict.Add(6004, -1);
            gameSaveConsTextDict.Add(6005, -1);
            gameSaveConsTextDict.Add(6006, -1);

            useHighestProductionOnly = Config.Bind("Behaviour", "useHighestProductionOnly", false, "When True, only metadata contributions from your highest production cluster will be available. Otherwise, Metadata production is unaffected. Metadata can be thought of as a 'high score' with this setting enabled.");
            useVerboseLogging = Config.Bind("Logging", "verboseLogging", false, "For debugging.");

            harmony.PatchAll();
            logger.LogInfo(pluginName + " " + pluginVersion + " " + "Patch successful");

        }
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(PropertySystem), nameof(PropertySystem.GetItemTotalProperty))]
        [HarmonyPrefix]
        public static bool GetItemTotalProperty_Patch(int itemId, PropertySystem __instance, ref int __result) //Corresponds to the 'Net Amount' number at the top of the Metadata panel
        {
            //GameMain.history.GetPropertyItemComsumption(itemId); 
            long currentClusterSeedKey = GameMain.data.GetClusterSeedKey();
            int maxProductionAmount = 0;
            int netTotalMetadata = 0;

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo("Current Seed " + currentClusterSeedKey);

            for (int i = 0; i < __instance.propertyDatas.Count; i++)
            {
                ClusterPropertyData clusterPropertyData = __instance.propertyDatas[i];
                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                    ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " production=" + clusterPropertyData.GetItemProduction(itemId) + " seed=" + clusterPropertyData.seedKey);

                int production = clusterPropertyData.GetItemProduction(itemId);

                // Use only the maximum value for the metadata amount, Otherwise, sum production of clusters like Vanilla does
                if (ReusableMetadataPlugin.useHighestProductionOnly.Value)
                {
                    if (production > maxProductionAmount)
                    {
                        maxProductionAmount = production;
                        netTotalMetadata = maxProductionAmount;
                        ReusableMetadataPlugin.topSeedForItem[itemId] = clusterPropertyData.seedKey;
                    }
                }
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

        [HarmonyPatch(  typeof(PropertySystem),
                        nameof(PropertySystem.GetItemAvaliableProperty),
                        new[] {typeof(long),typeof(int)}
        )]
        [HarmonyPrefix]
        public static bool GetItemAvaliableProperty_Patch(long seedKey, int itemId, PropertySystem __instance, ref int __result)
        {
            //ClusterPropertyData clusterData = __instance.GetClusterData(seedKey);
            int availableMetadata = __instance.GetItemTotalProperty(itemId); //Start with all metadata
            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch_Start={availableMetadata} ID={itemId} ");
            
            if (GameMain.gameScenario != null && !GameMain.gameScenario.propertyLogic.isSelfFormalGame)
            {
                availableMetadata = 0;
            }

            //Make sure we only run calculations on current seed
            if (GameMain.data.GetClusterSeedKey() == seedKey)
            {
                // Only subtract consumption of current seed, not all seeds.
                availableMetadata -= ReusableMetadataPlugin.gameSaveConsTextDict[itemId];

                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                {
                    ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch ID={itemId} GetItemConsumption={__instance.GetItemConsumption(seedKey, itemId)} seed={seedKey}");
                    ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch ID={itemId} gameSaveConsText={ReusableMetadataPlugin.gameSaveConsTextDict[itemId]} seed={seedKey}");
                }

                // Prevent Metadata generated on current seed from being spent on the current seed (Vanilla) by subtracting contributed production
                // If we are top seed we definitely contributed
                if (ReusableMetadataPlugin.topSeedForItem[itemId] == seedKey)
                {
                    availableMetadata -= __instance.GetItemProduction(seedKey, itemId);
                }
                // If we are not top seed we only contributed if useHighestProductionOnly is false
                else if (!ReusableMetadataPlugin.useHighestProductionOnly.Value)
                {
                    availableMetadata -= __instance.GetItemProduction(seedKey, itemId);
                }
            }

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch_Result={__result} ID={itemId} ");

            __result = availableMetadata;

            return false;
        }

        [HarmonyPatch(typeof(UIPropertyEntry), nameof(UIPropertyEntry.UpdateUITexts))]
        [HarmonyPostfix]
        public static void UpdateUITexts_Patch(UIPropertyEntry __instance)
        {
            ReusableMetadataPlugin.gameSaveConsTextDict[__instance.itemId] = Int32.Parse(__instance.gamesaveConsText.text);

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"UpdateUITexts_Patch={__instance.gamesaveConsText.text} ID={__instance.itemId} ");
        }
    }


}
