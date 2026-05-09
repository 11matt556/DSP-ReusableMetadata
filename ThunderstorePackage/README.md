

# Reusable Metadata Mod

## Summary

Changes Metadata to behave more like I envisioned when I first heard about a "NewGame+" mode in DSP. The primary change is that using metadata only consumes it for that save, but it also includes an optional change to how Metadata production is calculated.

Partially inspired by this reddit post https://www.reddit.com/r/Dyson_Sphere_Program/comments/u9l3wv/metadata_usage/

## Detailed Explanation
### Consumption
Using metadata will no longer "permanently" consume it. Instead, the metadata will be "consumed" only for that save.

This allows you to start every new game with your maximum amount of metadata, no matter how much you have spent on other seeds or saves.

### Production
There are two ways that Metadata production can be calculated with this mod, depending on the value of the `useHighestProductionOnly` config option.

### useHighestProductionOnly=true (Optional)

Only the highest metadata value from each unique seed is counted towards the availble metadata. Metadata can be thought of as a 'high score' when this is enabled.

This is my preferred way to play because it provides a counterbalance to the additional utility of reusable metadata, but it isn't the default since some may not want metadata to be any harder to obtain.

#### Example

'Raw' Metadata production from 3 seeds:

|Seed|Meta-EM|Meta-Energy|Meta-Structure|Meta-Information|Meta-Gravity|Metaverse|	
|----|-------|-----------|--------------|----------------|------------|---------| 
|1   |**100**| 90        | 60           | 15             | 5          | 0       |
|2   | 75    | **125**   | 40           | 10             | **10**     | **5**   |
|3   | 50    | 25        |**200**       | **25**         | 0          | 0       |
			
The bolded number is the quantity that the mod would select for each metadata type.
<br>So in this example, you would start a new seed with:

|Meta-EM|Meta-Energy|Meta-Structure|Meta-Information|Meta-Gravity|Metaverse|	
|-------|-----------|--------------|----------------|------------|---------|
|100    |125        |200           |25              |10        	 | 5	   |

### useHighestProductionOnly=false (Default)

Use the vanilla formula for calculating metadata prouction (I.E, Every unique seed contributes to the total Metadata production)

#### Example
Using the same production data from the previous example, you would start a new seed with:

|Meta-EM|Meta-Energy|Meta-Structure|Meta-Information|Meta-Gravity|Metaverse|	
|-------|-----------|--------------|----------------|------------|---------|
|225    |240        |300           |50              |15          | 5       |

## Installation

- Available in the [Thunderstore](https://thunderstore.io/c/dyson-sphere-program/p/11matt556/ReusableMetadata/)
- And on the [DSP-UsableMetadata]( https://github.com/11matt556/DSP-ReusableMetadata/tree/main) github page.

### Remember: Back up your save data and property folder! (%userprofile%\Documents\Dyson Sphere Program)

Let me know if you have suggestions or issues at https://github.com/11matt556/DSP-ReusableMetadata

## Configuration Options


|Key|Type|Default|Description|
|---|---|---|---|
useHighestProductionOnly|bool|false| True: Only metadata from the highest producing seed of each type will be counted towards the available metadata. <br>False: Use the vanilla formula for calculating metadata prouction (I.E, Every unique seed contributes to the total Metadata production)
verboseLogging|bool|false|For debugging.
enableSandboxCheat|bool|false|Intended for debugging and testing. Applies sandboxMultiplier to sandbox games. Use at your own risk.
sandboxMultiplier|float|1.0|Intended for debugging. 1 = 100%

## Change Log
- Version 1.0.10
	- [Dev] Corrected BepInEx.BaseLib version in package dependency file
	- [Dev] Updated unity package versions to match DSP unity version (2022.3.62)
	- [Dev] BepInEx repo should be automatically added (NuGet.Config)

- Version 1.0.9:
	- [Docs] Fixed typo in readme
	- [Dev] Updated DLL assembly version to match the mod version.
- Version 1.0.8:
	- [Compat] Updated to game version 0.10.34.28524 
		- Mod had failed to load due to `GetItemTotalProperty()` return changing from `int` to `long`. (PR #4)
	- [Docs] Added build instructions to readme and other misc readme formatting changes
	- [Docs] Rephrased the description text of the config options. 
		- Note: If you are updating you will need to delete your config file if you want to get the new description, but there is no functional impact either way.

- Version 1.0.7:
	- [Bugfix] Fixed enableSandboxCheat config option
	- [Feature] New config option, sandboxMultiplier. Use this to specify desired metadata multiplier in sandbox mode when enableSandboxCheat=true.
	- [Feature] Loading a sandbox game when enableSandboxCheat=false will zero out the metadata of that game, restoring the 'vanilla' state of the save.

- Version 1.0.6: 
	- [Documentation] Fixed mistake in readme

- Version 1.0.5:
	- [Bugfix] Use GetPropertyItemComsumption from GameMain.history instead of UIPropertyEntry to (hopefully) address Github issues #1 and #2.
	- [Feature] New config option to allow 4x metadata generation in sandbox mode. Intended for debugging and testing. Use at your own risk.

- Version 1.0.4:
	- [Dev] Project Dependencies managed through NuGet now

- Version 1.0.3:
	- [Compat] Updated to game version 0.9.25.12201

- Version 1.0.2:
	- [Dev] Removed error logging for negative amounts of Metadata since logging this is redundant. It is clearly visible in the UI if this occurs.

- Version 1.0.1:
	- [Bugfix] Enabling useHighestProductionOnly did not work correctly unless verboseLogging was also enabled
	- [Bugfix] Fixed issue in base game that could cause Metadata to be lost when exiting without saving! The fix I used has the following extra side effects/features:
		- Loading an older save of your current seed will also "roll back" any Metadata consumed after that save was created. (Thats good!)
		- Removing the property file will no longer reset "Current Game Realization", but does reset all other stats, including production/contribution (That's.... maybe bad? It makes cheating harder...)

- Version 1.0.0:
	- Re-release. The mod is feature complete and should work as expected now. 
	- [Bugfix] Bugs that could duplicate metadata should now be fixed 
	- [Bugfix] The 'Net Metadata' UI field is now being used correctly.
	- [Docs] Finished this readme with examples and more info

- Version 0.1.0:
	- Initial accidental release uploaded at https://dsp.thunderstore.io/package/Myself/ReusableMetadata/. Users of this old version should delete it. It was incomplete and did not work as expected.

## Ideas for additional features/changes
- Update UI tooltip explanations to reflect the changes made by this mod
- Set aside a custom property file to make mod removal and testing easier

## Known Issues
- The "Total Realization" stat can be misleading/incorrect. Metadata is still counted in "Total Realization" even if the realization/consumption was not counted by this mod. This bug can happen in Vanilla, but is much easier to trigger when using this mod.
- When enableSandboxCheat=true the statistics window will show the actual multiplier, but other locations in the game UI still show the vanilla value (0).

## Build Instructions

1. Clone the repository
2. Open the solution in Visual Studio (I used 2022)
3. Navigate to the Dyson Sphere Program installation folder and select "Assembly-CSharp.dll"  
	-     {Steam Library}\steamapps\common\Dyson Sphere Program\DSPGAME_Data\Managed\Assembly-CSharp.dll
4. Run `nuget restore` to install the dependencies. If you encounter errors during the restore:
5. Right click the Project (Not solution) in the solution explorer and select Properties. 
6. Navigate to the Build tab
7. The first line copies the DLL to a dev profile I have configured in my r2modman installation and should look similar to the line below. Remove or adapt this line to your system as needed.
	-      copy /Y "$(TargetPath)" "$(USERPROFILE)\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\dev\BepInEx\plugins\11matt556-ReusableMetadata_DEV
8. Click `Build->Build Solution`
	- If you encounter build errors due to missing unity references, try simply deleting them from the References in the solution explorer. 
	- This can be normal because sometimes newer versions of the game remove redundant/unused unity files, but the visual studio project doesn't know this.
9. Install it like any other mod (manually or or import the package into a ThunderStore-compatible mod manager, such as r2modman). 

#### Tips

The BepInEx should have been automatically added, but if not:

	Tools->NuGet Package Manager->Package Manager Settings->Package Sources
		- Name: BepInEx
		- Source: https://nuget.bepinex.dev/v3/index.json

You may need to update DysonSphereProgram.GameLibs to match your game version. (`Tools->NuGet Package Manager->Manage NuGet Packages for Solution`)

**Remember to back up your save data and property folder (``%userprofile%\Documents\Dyson Sphere Program``)**

