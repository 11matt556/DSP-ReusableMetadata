using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;

// TODO: Try to fix vanilla bug of spent metadata being lost when spent but game is not saved.
// The 'Current Game Utilization' number displayed in the UI does NOT include the metadata lost due to not saving and so should be the more trusted 
// It is not entirely clear to me where this "current game realization' number is derived.
// Presumably 'Current Game Realization' is in the save data while the number being used by the game (I assume GetItemConsumption) is being read from the property/metadata file.
// Looks like 'Current Game Utilization' is in UIPropertyEntrry,UpdateUITexts, this.gamesaveConsText.text. Interestingly, this links back to the same GetItemConsumption function...
// In theory, this.gamesaveConsText.text calls GetItemConsumption and so should == GetItemConsumption used by GetItemTotalProperty but I'm not sure that it does in practice


// TODO: Update UI tooltips to match mod behaviour

namespace ReusableMetadata
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ReusableMetadataPlugin : BaseUnityPlugin
    {

        public const string pluginGuid = "11matt556.dysonsphereprogram.ReusableMetadata";
        public const string pluginName = "Reusable Metadata";
        public const string pluginVersion = "1.0.0";
        public static ManualLogSource logger;
        public static ConfigEntry<bool> useHighestProductionOnly;
        public static ConfigEntry<bool> useVerboseLogging;
        public static IDictionary<int, long> topSeedForItem;

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

            useHighestProductionOnly = Config.Bind("Behaviour", "useHighestProductionOnly", false, "When True, only metadata contributions from your highest production cluster will be available. Otherwise, Metadata production is unaffected. Metadata can be thought of as a 'high score' with this setting enabled.");
            useVerboseLogging = Config.Bind("Logging", "verboseLogging", false, "For debuging.");

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
            long currentClusterSeedKey = GameMain.data.GetClusterSeedKey();
            int maxProductionAmount = 0;
            int num = 0;

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo("Current Seed " + currentClusterSeedKey);

            for (int i = 0; i < __instance.propertyDatas.Count; i++)
            {
                ClusterPropertyData clusterPropertyData = __instance.propertyDatas[i];
                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                    ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " production=" + clusterPropertyData.GetItemProduction(itemId) + " seed=" + clusterPropertyData.seedKey);

                int production = clusterPropertyData.GetItemProduction(itemId);

                // Use only the maximum value for the metadata amount
                if (ReusableMetadataPlugin.useHighestProductionOnly.Value)
                {
                    if (production > maxProductionAmount)
                    {
                        maxProductionAmount = production;
                        num = maxProductionAmount;
                        if (ReusableMetadataPlugin.useVerboseLogging.Value)
                            ReusableMetadataPlugin.topSeedForItem[itemId] = clusterPropertyData.seedKey;
                    }
                    if (ReusableMetadataPlugin.useVerboseLogging.Value)
                        ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " Top seed=" + ReusableMetadataPlugin.topSeedForItem[itemId]);
                }
                //Otherwise, sum production of clusters
                else
                {
                    if (ReusableMetadataPlugin.useVerboseLogging.Value)
                        ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " Production=" + production + " seed=" + clusterPropertyData.seedKey);
                    num += production;
                }

                //Subtract only the consumption of the current cluster
                if (clusterPropertyData.seedKey == currentClusterSeedKey)
                {
                    if (ReusableMetadataPlugin.useVerboseLogging.Value)
                        ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " Consumption=" + clusterPropertyData.GetItemConsumption(itemId) + " seed=" + clusterPropertyData.seedKey);
                }
            }

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " Calculated Total=" + num);
            if (num < 0)
            {
                ReusableMetadataPlugin.logger.LogError("Less than 0 Metadata available. This is probably a bug!");
                __result = 0;
            }
            else
            {
                __result = num;
            }
            return false;
        }

        [HarmonyPatch(typeof(PropertySystem),
                nameof(PropertySystem.GetItemAvaliableProperty),
                new[] {typeof(long),
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
            if (GameMain.gameScenario != null && !GameMain.gameScenario.propertyLogic.isSelfFormalGame)
            {
                availableMetadata = 0;
            }
            if (availableMetadata <= 0)
            {
                __result = 0;
                return false;
            }

            if(GameMain.data.GetClusterSeedKey() == seedKey)
            {
                // Only subtract consumption of current seed, not all seeds.
                availableMetadata -= __instance.GetItemConsumption(seedKey, itemId);

                // Only subtract production of the current seed if it contributed to the available metadata 
                if (ReusableMetadataPlugin.useHighestProductionOnly.Value)
                {
                    // Subtract production if the current seed is a top seed and therefore contributed to the metadata
                    if (ReusableMetadataPlugin.topSeedForItem[itemId] == seedKey)
                    {
                        availableMetadata -= __instance.GetItemProduction(seedKey, itemId);
                    }
                }
                else
                {
                    // Current production contributed if we are not using useHighestProductionOnly and so needs to be subtracted
                    availableMetadata -= __instance.GetItemProduction(seedKey, itemId);
                }
            }

            __result = availableMetadata;
            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"GetItemAvaliableProperty_Patch_Result={__result} ID={itemId} ");
            return false;
        }
    }
}
