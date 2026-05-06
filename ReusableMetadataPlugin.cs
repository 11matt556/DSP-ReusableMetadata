using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;
using WinAPI;
using System.IO;
using System.Reflection;
using Steamworks;

/* ~~~~~~~~~~~~~~~~ */
/*      NOTES       */
/* ~~~~~~~~~~~~~~~~ */

/*

UIPropertyEntry

long clusterSeedKey = this.gameData.GetClusterSeedKey();

.totalText              = "value-1"     = "Meta Amount" (Top number)     = DSPGame.propertySystem.GetItemTotalProperty(this.itemId);
.avaliableText          = "value-2"     = "Current Available Amount"     = DSPGame.propertySystem.GetItemAvaliableProperty(clusterSeedKey, this.itemId);
.gamesaveProductionText = "value-3"     = "Current Game Contribution"    = this.gameData.history.GetPropertyItemProduction(this.itemId);
.clusterProductionText  = "value-4"     = "Current Cluster Contribution" = DSPGame.propertySystem.GetItemProduction(clusterSeedKey, this.itemId);
.gamesaveConsText       = "value-5"     = "Current Game Instantiation"   = this.gameData.history.GetPropertyItemComsumption(this.itemId);
.clusterConsText        = "value-6"     = "Total Instantiation"          = DSPGame.propertySystem.GetItemTotalConsumption(this.itemId);

*/

/* 

In Game Descriptions 
"Meta Amount" (Top number) = "Equals the total amount of metadata obtained from every cluster, minus the total amount of used Metadata in every cluster. All metadata can be used for rebuilding icarus."
"Current Available Amount" = "Current immediately instantiable metadata. You can't instantiate metadata contributed by the current cluster address, so it equals to the remaining amount minus contribution from current cluster address"

*/

namespace ReusableMetadata
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ReusableMetadataPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "11matt556.dysonsphereprogram.ReusableMetadata";
        public const string pluginName = "Reusable Metadata";
        public const string pluginVersion = "1.0.8";

        public static ManualLogSource logger;
        public static ConfigEntry<bool> useHighestProductionOnly;
        public static ConfigEntry<bool> useVerboseLogging;
        public static ConfigEntry<bool> useSandboxCheat;
        public static ConfigEntry<float> sandboxMultiplier;
        public static IDictionary<int, long> topSeedForItem;

        public void Awake()
        {
            logger = Logger;
            topSeedForItem = new Dictionary<int, long>();

            topSeedForItem.Add(6001, -1);
            topSeedForItem.Add(6002, -1);
            topSeedForItem.Add(6003, -1);
            topSeedForItem.Add(6004, -1);
            topSeedForItem.Add(6005, -1);
            topSeedForItem.Add(6006, -1);

            useHighestProductionOnly = Config.Bind(
                "Behaviour",
                "useHighestProductionOnly",
                false,
                "When true, only metadata contributions from your highest production cluster will be available. Metadata can be thought of as a high score with this setting enabled. When false, metadata production is summed across clusters."
            );

            useVerboseLogging = Config.Bind(
                "Debugging",
                "verboseLogging",
                false,
                "For debugging."
            );

            useSandboxCheat = Config.Bind(
                "Debugging",
                "enableSandboxCheat",
                false,
                "Sets sandbox metadata multiplier to sandboxMultiplier. Use at your own risk."
            );

            sandboxMultiplier = Config.Bind(
                "Debugging",
                "sandboxMultiplier",
                1f,
                "Sets sandbox metadata multiplier to the entered value. 1 = 100%."
            );

            Harmony harmony = new Harmony(pluginGuid);
            harmony.PatchAll();

            logger.LogInfo(pluginName + " " + pluginVersion + " Patch successful");
        }
    }

    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(PropertySystem), nameof(PropertySystem.GetItemTotalProperty))]
        [HarmonyPrefix]
        public static bool GetItemTotalProperty_Patch(int itemId, PropertySystem __instance, ref long __result)
        {
            // GetItemTotalProperty corresponds to the large "Amount" number at the top of the Metadata panel.

            long currentClusterSeedKey = GameMain.data.GetClusterSeedKey();
            long productionHighScore = 0L;
            long netTotalMetadata = 0L;

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
            {
                ReusableMetadataPlugin.logger.LogInfo("Current Seed " + currentClusterSeedKey);
            }

            for (int i = 0; i < __instance.propertyDatas.Count; i++)
            {
                ClusterPropertyData clusterPropertyData = __instance.propertyDatas[i];

                int production = clusterPropertyData.GetItemProduction(itemId);

                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                {
                    ReusableMetadataPlugin.logger.LogInfo(
                        "GetItemTotalProperty_Patch ID=" + itemId +
                        " production=" + production +
                        " seed=" + clusterPropertyData.seedKey
                    );
                }

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
                else
                {
                    // Otherwise, add up production from all clusters.
                    netTotalMetadata += production;
                }
            }

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
            {
                ReusableMetadataPlugin.logger.LogInfo(
                    "GetItemTotalProperty_Patch ID=" + itemId +
                    " Calculated Total=" + netTotalMetadata
                );
            }

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
)]
        [HarmonyPrefix]
        public static bool GetItemAvaliableProperty_Patch(long seedKey, int itemId, PropertySystem __instance, ref long __result)
        {
            long availableMetadata = __instance.GetItemTotalProperty(itemId);

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
            {
                ReusableMetadataPlugin.logger.LogInfo(
                    $"GetItemAvaliableProperty_Patch_Start={availableMetadata} ID={itemId}"
                );
            }

            if (GameMain.data.GetClusterSeedKey() == seedKey)
            {
                // Only subtract metadata consumed of current seed, not all seeds. This one line is really the main point of this whole mod...
                // Important thing to remember! 
                // GameMain.history.GetPropertyItemComsumption reads the game data itself. It will OVERRULE whatever is in the property file
                // PropertySystem.GetItemConsumption reads from the property file itself! 
                // I use GameMain.history here because it seems to prtevent the vanilla bug where metadata is lost when realizing and exiting without saving.
                availableMetadata -= GameMain.history.GetPropertyItemComsumption(itemId);

                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                {
                    ReusableMetadataPlugin.logger.LogInfo(
                        $"GetItemAvaliableProperty_Patch ID={itemId} PropertySystem.GetItemConsumption={__instance.GetItemConsumption(seedKey, itemId)} seed={seedKey}"
                    );

                    ReusableMetadataPlugin.logger.LogInfo(
                        $"GetItemAvaliableProperty_Patch ID={itemId} GameMain.history.GetPropertyItemComsumption={GameMain.history.GetPropertyItemComsumption(itemId)} seed={seedKey}"
                    );
                }

                // Make sure we can't spend metadata from the current seed.
                if (ReusableMetadataPlugin.useHighestProductionOnly.Value && seedKey != ReusableMetadataPlugin.topSeedForItem[itemId])
                {
                    // This seed did not contribute to the total.
                }
                else
                {
                    availableMetadata -= __instance.GetItemProduction(seedKey, itemId);
                }
            }

            __result = availableMetadata;

            if (ReusableMetadataPlugin.useVerboseLogging.Value)
            {
                ReusableMetadataPlugin.logger.LogInfo(
                    $"GetItemAvaliableProperty_Patch_Result={__result} ID={itemId}"
                );
            }

            return false;
        }

        [HarmonyPatch(typeof(GameScenarioLogic))]
        [HarmonyPatch("GameTick")]
        [HarmonyPostfix]
        public static void GameScenarioLogic_GameTick_Patch(long time, GameScenarioLogic __instance)
        {
            // Make metadata logic tick happen in sandbox mode.
            if (__instance.gameData.gameDesc.isSandboxMode && ReusableMetadataPlugin.useSandboxCheat.Value)
            {
                __instance.propertyLogic.GameTick(time);
            }
        }

        [HarmonyPatch(typeof(GameMain))]
        [HarmonyPatch("Begin")]
        [HarmonyPostfix]
        public static void GameMain_Begin_Patch(GameMain __instance)
        {
            // If in sandbox mode and not using the cheat, reset all metadata production to 0.
            if (DSPGame.GameDesc.isSandboxMode && !ReusableMetadataPlugin.useSandboxCheat.Value)
            {
                for (int itemId = 6001; itemId <= 6006; itemId++)
                {
                    GameMain.history.SetPropertyItemProduction(itemId, 0);
                    DSPGame.propertySystem.SetItemProduction(DSPGame.GameDesc.seedKey64, itemId, 0);
                }
            }
        }

        [HarmonyPatch(typeof(PropertySystem))]
        [HarmonyPatch("GetItemProduction")]
        [HarmonyPostfix]
        public static void PropertySystem_GetItemProduction_Patch(long seedKey, int itemId, PropertySystem __instance, ref int __result)
        {
            if (GameMain.gameScenario != null && seedKey == GameMain.data.GetClusterSeedKey())
            {
                int currentGameContribution = GameMain.history.GetPropertyItemProduction(itemId);
                // Deleting the property file can cause the current game contribution to exceed the cluster contribution
                // Fix this by updating the cluster contribution to equal the game contribution.
                if (__result < currentGameContribution)
                {
                    __instance.SetItemProduction(seedKey, itemId, currentGameContribution);
                    GameMain.history.SetPropertyItemProduction(itemId, currentGameContribution);
                    __result = currentGameContribution;
                }
            }
        }

        [HarmonyPatch(typeof(UIPropertyEntry))]
        [HarmonyPatch("UpdateUIElements")]
        [HarmonyPostfix]
        public static void UIPropertyEntry_UpdateUIElements_Patch(UIPropertyEntry __instance)
        {
            // Update Metadata panel to display custom multiplier and allow button use in sandbox mode.
            if (DSPGame.GameDesc.isSandboxMode && ReusableMetadataPlugin.useSandboxCheat.Value)
            {
                long avaliableProperty = DSPGame.propertySystem.GetItemAvaliableProperty(
                    GameMain.data.GetClusterSeedKey(),
                    __instance.itemId
                );

                __instance.realizeButton.button.interactable = GameMain.mainPlayer.isAlive && avaliableProperty > 0;
                __instance.productionRateText1.text = __instance.productionRateText0.text =
                    string.Format("( x {0:0%} )", ReusableMetadataPlugin.sandboxMultiplier.Value);
            }
        }

        [HarmonyPatch(
            typeof(PropertyLogic),
            nameof(PropertyLogic.UpdateProduction)
        )]
        [HarmonyPrefix]
        public static bool PropertyLogic_UpdateProduction_Patch(PropertyLogic __instance)
        {
            FactoryProductionStat[] factoryStatPool = __instance.gameData.statistics.production.factoryStatPool;
            int factoryCount = __instance.gameData.factoryCount;

            ClusterPropertyData clusterData = __instance.propertySystem.GetClusterData(
                __instance.gameData.GetClusterSeedKey()
            );

            ClusterPropertyData propertyData = __instance.gameData.history.propertyData;
            if (ReusableMetadataPlugin.useVerboseLogging.Value)
                ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch");
            foreach (int productId in PropertySystem.productIds)
            {
                int itemProduction1 = propertyData.GetItemProduction(productId);
                int itemProduction2 = clusterData.GetItemProduction(productId);

                long num = 0L;

                for (int index = 0; index < factoryCount; ++index)
                {
                    int productIndex = factoryStatPool[index].productIndices[productId];
                    if (ReusableMetadataPlugin.useVerboseLogging.Value)
                        ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch productIndex={productIndex}");
                    if (productIndex > 0)
                    {
                        if (ReusableMetadataPlugin.useVerboseLogging.Value)
                            ReusableMetadataPlugin.logger.LogDebug($"PropertyLogic_UpdateProduction_Patch factoryStatPool[index].productPool[productIndex].total[3]={factoryStatPool[index].productPool[productIndex].total[3]}");
                        num += factoryStatPool[index].productPool[productIndex].total[3];
                    }
                }

                /* 
                 * Changing minimalPropertyMultiplier in prefix/postfix would be easier, but this will persist in save files, **even after the mod is removed**.
                 * To avoid this, we reimplement the entirety of the UpdateProduction() function so we may use a multiplier of our choosing.
                 * This is the safest way to pevent unexpected savefile modifications, but could be problematic if the vanilla game changes the production calculation formula in a future update.
                */
                float multiplier;

                if (ReusableMetadataPlugin.useSandboxCheat.Value && DSPGame.GameDesc.isSandboxMode)
                {
                    multiplier = ReusableMetadataPlugin.sandboxMultiplier.Value;
                }
                else
                {
                    multiplier = __instance.gameData.history.minimalPropertyMultiplier;
                }

                long calculatedCount = (long)((double)num * (double)multiplier / 60.0 + 0.001);

                int count = calculatedCount > int.MaxValue
                    ? int.MaxValue
                    : calculatedCount < int.MinValue
                        ? int.MinValue
                        : (int)calculatedCount;

                if (count > itemProduction1)
                {
                    propertyData.SetItemProduction(productId, count);
                }

                if (count > itemProduction2)
                {
                    clusterData.SetItemProduction(productId, count);
                }

                if (ReusableMetadataPlugin.useVerboseLogging.Value)
                {
                    ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch count={count}");
                    ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch num={num}");
                    ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch minimalPropertyMultiplier={multiplier}");
                }
            }

            return false;
        }
    }
}