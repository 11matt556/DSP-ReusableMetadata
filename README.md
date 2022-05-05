

# Reusable Metadata Mod
## Recommended: Back up your save data and property folder in Documents\Dyson Sphere Program\ before running this mod. 

The mod changes Metadata to behave more like I envisioned when I first heard about a "NewGame+" mode in DSP. It makes the following changes:

 - Consumption
	- Consumption of other cluster addresses no longer counts against your net total of Metadata or your available Metadata. This allows you to start every new cluster seed with your maximum amount of metadata, no matter how much has been spent in previous saved games.
	- Does not use the property file for the Consumption value of the current cluster. This addresses a vanilla bug that causes Metadata to be lost in some cases, but has some additional effects (See version 1.0.1 change note)

 - Production (Optional)
	- Only the best Metadata values from each of your cluster seeds is counted when `useHighestProductionOnly` is enabled. Metadata can be thought of as a 'high score' when this is enabled. 
	- For example, lets say you have three cluster addresses with the  Metadata production below. The **bolded** numbers are the amount of each metadata type that would available at the start of a new game.
	   - |Meta-EM|Meta-Energy|Meta-Structure|Meta-Information|Meta-Gravity|Metaverse|	
	     |------------|---------------|------------------|---------------------|----------------|------------| 
	     | **100** | 100 | 100 | 15 | 5 | 0 |
	     | 75 | **125** | 50 | 10 | **10** | **5** |
	     | 50 | 25 | **200** | **25** | 0 | 0 |
	 
		

Partially inspired by reddit post https://www.reddit.com/r/Dyson_Sphere_Program/comments/u9l3wv/metadata_usage/

It seems to work fine on my games but there are probably weird things that can happen when this is added or removed from current games with existing metadata consumption.

Let me know if you have suggestions or issues at https://github.com/11matt556/DSP-ReusableMetadata

## Change Log

- Version 1.0.2:
	- Removed error logging for negative amounts of Metadata since logging this is redundant. It is clearly visible in the UI if this occurs.

- Version 1.0.1:
	- Bugfix: Enabling useHighestProductionOnly did not work correctly unless verboseLogging was also enabled
	- Bugfix: Fixed issue in base game that could cause Metadata to be lost when exiting without saving! The fix I used has the following extra side effects/features:
		- Loading an older save of your current seed will also "roll back" any Metadata consumed after that save was created. (Sounds good to me!)
		- Removing the property file will no longer reset "Current Game Realization", but does reset all other stats, including production/contribution (Makes 'cheating' Metadata harder)

- Version 1.0.0:
	- Re-release. The mod is feature complete and should work as expected now. 
	- Bugfix: Bugs that could duplicate metadata should now be fixed 
	- Bugfix: The 'Net Metadata' UI field is now being used correctly.
	- Finished this readme with examples and more info

- Version 0.1.0:
	- Initial accidental release uploaded at https://dsp.thunderstore.io/package/Myself/ReusableMetadata/. Users of this old version should delete it. It was incomplete and did not work as expected.

## Configuration Options


|Key|Type|Default|Description|
|---|---|---|---|
useHighestProductionOnly|bool|false|When True, only metadata contributions from your highest production cluster will be available. Otherwise, Metadata production is unaffected. Metadata can be thought of as a 'high score' with this setting enabled.
verboseLogging|bool|false|For debugging.

## Ideas for additional features/changes
- Update UI tooltip explanations to reflect the changes made by this mod

## Known Issues
- The "Total Realization" stat can be misleading/incorrect. Metadata is still counted in "Total Realization" even if the realization/consumption was not counted by this mod. This bug can happen in Vanilla, but is much easier to trigger when using this mod.