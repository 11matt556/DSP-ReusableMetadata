using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;

namespace ReusableMetadata
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ReusableMetadataPlugin : BaseUnityPlugin
    {

        public const string pluginGuid = "11matt556.dysonsphereprogram.ReusableMetadata";
        public const string pluginName = "Reusable Metadata";
        public const string pluginVersion = "0.0.1";
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
            topSeedForItem.Add(6001, 0);
            topSeedForItem.Add(6002, 0);
            topSeedForItem.Add(6003, 0);
            topSeedForItem.Add(6004, 0);
            topSeedForItem.Add(6005, 0);
            topSeedForItem.Add(6006, 0);

            useHighestProductionOnly = Config.Bind("Behaviour", "useHighestProductionOnly", false, "High score mode. Only the metadata contributions from your highest production cluster will be available. Metadata from multiple clusters is NOT added together.");
            useVerboseLogging = Config.Bind("Logging", "verboseLogging", false, "For debuging purposes");

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
            long currentClusterSeedKey = GameMain.data.GetClusterSeedKey();
            int maxProductionAmount = 0;
            int num = 0;

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo("Current Seed " + currentClusterSeedKey);

            for (int i = 0; i < __instance.propertyDatas.Count; i++)
            {
                ClusterPropertyData clusterPropertyData = __instance.propertyDatas[i];
                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                    ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + " production=" + clusterPropertyData.GetItemProduction(itemId) + "seed=" + clusterPropertyData.seedKey);

                int production = clusterPropertyData.GetItemProduction(itemId);

                // Use only the maximum value
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
                        ReusableMetadataPlugin.logger.LogInfo("GetItemTotalProperty_Patch ID=" + itemId + "Top seed=" + ReusableMetadataPlugin.topSeedForItem[itemId]);
                }
                //Otherwise, sum production of clusters
                else
                {
                    num += production;
                }

                //Subtract only the consumption of the current cluster
                if (clusterPropertyData.seedKey == currentClusterSeedKey)
                {
                    num -= clusterPropertyData.GetItemConsumption(itemId);
                }
            }

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo("Calculated " + num);
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

        //TODO: Need to handle GetItemAvaliableProperty when useHighestProductionOnly is enabled. Remove or cancel out the subtraction of clusterData.GetItemProduction(itemId);
        [HarmonyPatch(typeof(PropertySystem),
                nameof(PropertySystem.GetItemAvaliableProperty),
                new[] {typeof(long),
                       typeof(int)
                }
              )
        ]
        [HarmonyPostfix]
        public static void GetItemAvaliableProperty_Patch(long seedKey, int itemId, PropertySystem __instance, ref int __result)
        {
            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"ID={itemId} GetItemAvaliableProperty={__result}");

            // In the vanilla game all clusters are always summed together, but you cannot use metadata from your current cluster in that cluster, so trhis metadata is subtracted.
            // But with useHighestProductionOnly the metadata from the current cluster may not have been added unless it is the highest the highest metadata cluster. 
            // But the game will still subtract its contributions from the available metadata. 

            //Re-add the subtracted metadata if the metadata if the current cluster did not contribute to the available metadata in the first place. (In other words, if it it is not thew highest production cluster)
            if (ReusableMetadataPlugin.useHighestProductionOnly.Value && ReusableMetadataPlugin.topSeedForItem[itemId] != GameMain.data.GetClusterSeedKey())
            {
                __result += __instance.GetItemProduction(seedKey, itemId);
            }

        }
    }
}
