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
// TODO: Update UI tooltips to match mod behaviour
// TODO: See if there is a way to fix potentially inaccurate "Instantiation" stats that can occur when instantiating without saving. This does not seem to impact the available metadata calculations.

namespace ReusableMetadata
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ReusableMetadataPlugin : BaseUnityPlugin
    {
        public const string pluginGuid = "11matt556.dysonsphereprogram.ReusableMetadata";
        public const string pluginName = "Reusable Metadata";
        public const string pluginVersion = "1.0.7";
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
            useSandboxCheat = Config.Bind("Debugging", "enableSandboxCheat", false, "Sets sandbox metadata multiplier to sandboxMultiplier. Use at your own risk.");
            sandboxMultiplier = Config.Bind("Debugging", "sandboxMultiplier", 1f, "Sets Sandbox Metadata multiplier to the entered value. 1 = 100%");

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

        [HarmonyPatch(typeof(GameScenarioLogic))]
        [HarmonyPatch("GameTick")]
        [HarmonyPostfix]
        public static void GameScenarioLogic_GameTick_Patch(long time, GameScenarioLogic __instance)
        {
            // Make the metadata logic tick happen in sandbox mode
            if (__instance.gameData.gameDesc.isSandboxMode && ReusableMetadataPlugin.useSandboxCheat.Value == true)
            {
                __instance.propertyLogic.GameTick(time);
            }
        }

        [HarmonyPatch(typeof(GameMain))]
        [HarmonyPatch("Begin")]
        [HarmonyPostfix]
        public static void GameMain_Begin_Patch(GameMain __instance)
        {
            // If we are in sandbox mode and not using the cheat, reset all metadata production to 0
            if (DSPGame.GameDesc.isSandboxMode && ReusableMetadataPlugin.useSandboxCheat.Value == false)
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
        public static void PropertySystem_GetItemProduction_Patch(long seedKey,int itemId, PropertySystem __instance, ref int __result)
        {
            if (GameMain.gameScenario != null && seedKey == GameMain.data.GetClusterSeedKey())
            {
                int currentGameContribution = GameMain.history.GetPropertyItemProduction(itemId);
                // Current game contribution cannot be greater than the current cluster, but this can happen if the property file is deleted.
                // Fix this by setting the cluster contribution to equal the game contribution
                // Note: This is technically a vanilla bug
                if (__result < currentGameContribution )
                {
                    //__instance.SetItemProduction(itemId, currentGameContribution);
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
            // Update Metadata panel to display custom multiplier and allow button to be used in sandbox mode.
            if (DSPGame.GameDesc.isSandboxMode && ReusableMetadataPlugin.useSandboxCheat.Value)
            {
                int avaliableProperty = DSPGame.propertySystem.GetItemAvaliableProperty(GameMain.data.GetClusterSeedKey(), __instance.itemId);
                __instance.realizeButton.button.interactable = GameMain.mainPlayer.isAlive && avaliableProperty > 0;
                __instance.productionRateText1.text = __instance.productionRateText0.text = string.Format("( x {0:0%} )", ReusableMetadataPlugin.sandboxMultiplier.Value);
            }
        }
        
        [HarmonyPatch(
        typeof(PropertyLogic),
        nameof(PropertyLogic.UpdateProduction)
        )
        ]
        [HarmonyPrefix]
        public static bool PropertyLogic_UpdateProduction_Patch(PropertyLogic __instance)
        {

            FactoryProductionStat[] factoryStatPool = __instance.gameData.statistics.production.factoryStatPool;
            int factoryCount = __instance.gameData.factoryCount;
            ClusterPropertyData clusterData = __instance.propertySystem.GetClusterData(__instance.gameData.GetClusterSeedKey());
            ClusterPropertyData propertyData = __instance.gameData.history.propertyData;
            //ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch");
            foreach (int productId in PropertySystem.productIds)
            {
                int itemProduction1 = propertyData.GetItemProduction(productId);
                int itemProduction2 = clusterData.GetItemProduction(productId);

                //ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch itemProduction1={itemProduction1}");
                //ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch itemProduction2={itemProduction2}");

                long num = 0;
                for (int index = 0; index < factoryCount; ++index)
                {
                    int productIndex = factoryStatPool[index].productIndices[productId];
                    //ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch productIndex={productIndex}");
                    if (productIndex > 0)
                    {
                        //ReusableMetadataPlugin.logger.LogInfo($"PropertyLogic_UpdateProduction_Patch factoryStatPool[index].productPool[productIndex].total[3]={factoryStatPool[index].productPool[productIndex].total[3]}");
                        num += factoryStatPool[index].productPool[productIndex].total[3];
                    }
                }

                /* 
                 * Just bodge a multiplier in here since manipulating minimalPropertyMultiplier with prefix and postfix was just asking for trouble.
                 * Turns out changing minimalPropertyMultiplier can permanently change the multiplier value on save files, even after removing the mod.
                */
                float multiplier = 0;
                if (ReusableMetadataPlugin.useSandboxCheat.Value && DSPGame.GameDesc.isSandboxMode) {
                    multiplier = ReusableMetadataPlugin.sandboxMultiplier.Value;
                }
                else {
                    multiplier = __instance.gameData.history.minimalPropertyMultiplier;
                }

                int count = (int)((double)num * (double)multiplier / 60.0 + 0.001);
                if (count > itemProduction1)
                    propertyData.SetItemProduction(productId, count);
                if (count > itemProduction2)
                    clusterData.SetItemProduction(productId, count);

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