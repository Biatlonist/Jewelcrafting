﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ItemManager;
using JetBrains.Annotations;
using Jewelcrafting.GemEffects;
using Jewelcrafting.GemEffects.Groups;
using Jewelcrafting.WorldBosses;
using LocalizationManager;
using ServerSync;
using SkillManager;
using UnityEngine;

namespace Jewelcrafting;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("randyknapp.mods.epicloot")]
[BepInIncompatibility("DasSauerkraut.Terraheim")]
[BepInDependency("org.bepinex.plugins.groups", BepInDependency.DependencyFlags.SoftDependency)]
public partial class Jewelcrafting : BaseUnityPlugin
{
	public const string ModName = "Jewelcrafting";
	private const string ModVersion = "1.3.8";
	private const string ModGUID = "org.bepinex.plugins.jewelcrafting";

	public static readonly ConfigSync configSync = new(ModName) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

	private static ConfigEntry<Toggle> serverConfigLocked = null!;
	public static SyncedConfigEntry<Toggle> useExternalYaml = null!;
	public static ConfigEntry<Toggle> socketSystem = null!;
	public static ConfigEntry<Toggle> inventorySocketing = null!;
	public static ConfigEntry<Toggle> displayGemcursor = null!;
	public static ConfigEntry<Toggle> displaySocketBackground = null!;
	public static ConfigEntry<Toggle> colorItemName = null!;
	public static ConfigEntry<Unsocketing> allowUnsocketing = null!;
	public static ConfigEntry<InteractBehaviour> inventoryInteractBehaviour = null!;
	public static ConfigEntry<int> breakChanceUnsocketSimple = null!;
	public static ConfigEntry<int> breakChanceUnsocketAdvanced = null!;
	public static ConfigEntry<int> breakChanceUnsocketPerfect = null!;
	public static ConfigEntry<int> breakChanceUnsocketMerged = null!;
	public static ConfigEntry<Toggle> socketingItemsExperience = null!;
	public static ConfigEntry<Toggle> visualEffects = null!;
	public static ConfigEntry<UniqueDrop> uniqueGemDropSystem = null!;
	public static ConfigEntry<int> uniqueGemDropChance = null!;
	public static ConfigEntry<Toggle> uniqueGemDropOnePerPlayer = null!;
	public static ConfigEntry<int> resourceReturnRate = null!;
	public static ConfigEntry<int> resourceReturnRateDistance = null!;
	public static ConfigEntry<Toggle> badLuckRecipes = null!;
	public static readonly ConfigEntry<int>[] crystalFusionBoxDropRate = new ConfigEntry<int>[FusionBoxSetup.Boxes.Length];
	public static readonly ConfigEntry<float>[] crystalFusionBoxMergeActivityProgress = new ConfigEntry<float>[FusionBoxSetup.Boxes.Length];
	public static ConfigEntry<int> maximumNumberSockets = null!;
	public static ConfigEntry<int> gemRespawnRate = null!;
	public static ConfigEntry<int> upgradeChanceIncrease = null!;
	public static ConfigEntry<int> awarenessRange = null!;
	private static ConfigEntry<int> rigidDamageReduction = null!;
	private static ConfigEntry<uint> headhunterDuration = null!;
	private static ConfigEntry<int> headhunterDamage = null!;
	private static ConfigEntry<float> experienceGainedFactor = null!;
	public static ConfigEntry<int> magicRepairAmount = null!;
	private static ConfigEntry<int> aquaticDamageIncrease = null!;
	public static ConfigEntry<int> modersBlessingDuration = null!;
	public static ConfigEntry<int> modersBlessingCooldown = null!;
	public static ConfigEntry<int> gemBagSlotsRows = null!;
	public static ConfigEntry<int> gemBagSlotsColumns = null!;
	public static ConfigEntry<Toggle> gemBagAutofill = null!;
	public static ConfigEntry<int> gemBoxSlotsRows = null!;
	public static ConfigEntry<int> gemBoxSlotsColumns = null!;
	public static ConfigEntry<KeyboardShortcut> advancedTooltipKey = null!;
	public static ConfigEntry<AdvancedTooltipMode> advancedTooltipMode = null!;
	public static ConfigEntry<Toggle> advancedTooltipAlwaysOn = null!;
	public static ConfigEntry<Toggle> gachaLocationIcon = null!;
	public static ConfigEntry<int> bossSpawnTimer = null!;
	public static ConfigEntry<int> bossSpawnMinDistance = null!;
	public static ConfigEntry<int> bossSpawnMaxDistance = null!;
	public static ConfigEntry<int> bossSpawnBaseDistance = null!;
	public static ConfigEntry<int> bossTimeLimit = null!;
	public static ConfigEntry<int> bossCoinDrop = null!;
	public static ConfigEntry<GachaSetup.BalanceToggle> worldBossBalance = null!;
	public static ConfigEntry<float> worldBossHealth = null!;
	public static ConfigEntry<float> worldBossPunchDamage = null!;
	public static ConfigEntry<float> worldBossSmashDamage = null!;
	public static ConfigEntry<float> worldBossFireDamage = null!;
	public static ConfigEntry<float> worldBossFrostDamage = null!;
	public static ConfigEntry<float> worldBossPoisonDamage = null!;
	public static ConfigEntry<int> worldBossBonusWeaponDamage = null!;
	public static ConfigEntry<int> worldBossCountdownDisplayOffset = null!;
	public static ConfigEntry<int> frameOfChanceChance = null!;

	public static readonly Dictionary<int, ConfigEntry<int>> socketAddingChances = new();
	public static readonly Dictionary<GameObject, ConfigEntry<float>> gemDropChances = new();
	public static readonly CustomSyncedValue<List<string>> socketEffectDefinitions = new(configSync, "socket effects", new List<string>());

	private readonly Dictionary<string, int[]> defaultBoxMergeChances = new()
	{
		{ "$jc_common_gembox", new[] { 75, 25, 0 } },
		{ "$jc_epic_gembox", new[] { 100, 50, 25 } },
		{ "$jc_legendary_gembox", new[] { 100, 75, 50 } }
	};

	private readonly Dictionary<string, float[]> defaultBoxBossProgress = new()
	{
		{ "$enemy_eikthyr", new[] { 3f, 0, 0 } },
		{ "$enemy_gdking", new[] { 4f, 0.5f, 0 } },
		{ "$enemy_bonemass", new[] { 5f, 1f, 0.3f } },
		{ "$enemy_dragon", new[] { 7f, 3f, 0.7f } },
		{ "$enemy_goblinking", new[] { 10f, 5f, 1.5f } }
	};

	public static readonly Dictionary<string, ConfigEntry<float>> gemUpgradeChances = new();
	public static readonly Dictionary<string, ConfigEntry<int>[]> boxMergeChances = new();
	public static readonly Dictionary<string, ConfigEntry<float>[]> boxBossProgress = new();
	public static ConfigEntry<int> boxBossGemMergeChance = null!;

	public static Dictionary<Effect, List<EffectDef>> SocketEffects = new();
	public static readonly Dictionary<int, Dictionary<GemLocation, List<EffectPower>>> EffectPowers = new();
	public static Dictionary<Heightmap.Biome, Dictionary<GemType, float>> GemDistribution = new();
	public static List<string> configFilePaths = null!;
	public static readonly List<SynergyDef> Synergies = new();

	private static Skill jewelcrafting = null!;

	public static GameObject swordFall = null!;
	public static StatusEffect gliding = null!;
	public static StatusEffect glidingDark = null!;
	public static SE_Stats glowingSpirit = null!;
	public static GameObject glowingSpiritPrefab = null!;
	public static SE_Stats lightningSpeed = null!;
	public static SE_Stats rootedRevenge = null!;
	public static SE_Stats poisonousDrain = null!;
	public static GameObject poisonousDrainCloud = null!;
	public static SE_Stats icyProtection = null!;
	public static SE_Stats fieryDoom = null!;
	public static GameObject fieryDoomExplosion = null!;
	public static SE_Stats apotheosis = null!;
	public static SE_Stats awareness = null!;
	public static GameObject heardIcon = null!;
	public static GameObject attackedIcon = null!;
	public static SE_Stats headhunter = null!;
	private static SE_Stats rigidFinger = null!;
	public static GameObject magicRepair = null!;
	public static SE_Stats aquatic = null!;
	public static SE_Stats lightningStart = null!;
	public static SE_Stats rootStart = null!;
	public static SE_Stats poisonStart = null!;
	public static SE_Stats iceStart = null!;
	public static SE_Stats fireStart = null!;
	public static SE_Stats apotheosisStart = null!;
	public static SE_Stats friendshipStart = null!;
	public static SE_Stats friendship = null!;
	public static SE_Stats loneliness = null!;
	public static GameObject friendshipTether = null!;
	public static SE_Stats cowardice = null!;
	public static SE_Stats fireBossDebuff = null!;
	public static SE_Stats frostBossDebuff = null!;
	public static SE_Stats poisonBossDebuff = null!;
	public static SE_Stats thunderclapMark = null!;
	public static GameObject thunderclapExplosion = null!;
	public static SE_Stats fading = null!;
	public static Material fadingMaterial = null!;

	private static Jewelcrafting self = null!;

	public static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
	{
		ConfigEntry<T> configEntry = self.Config.Bind(group, name, value, description);

		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

	public enum Toggle
	{
		On = 1,
		Off = 0
	}

	public enum UniqueDrop
	{
		Disabled = 0,
		TrulyUnique = 1,
		Custom = 2
	}

	public enum Unsocketing
	{
		Disabled = 0,
		UniquesOnly = 1,
		All = 2
	}

	public enum InteractBehaviour
	{
		Disabled = 0,
		Hovering = 1,
		Enabled = 2
	}

	public enum AdvancedTooltipMode
	{
		General = 0,
		Detailed = 1
	}

	[PublicAPI]
	public class ConfigurationManagerAttributes
	{
		public int? Order;
		public bool? Browsable;
		public bool? HideSettingName;
		public bool? HideDefaultButton;
		public string? DispName;
		public Action<ConfigEntryBase>? CustomDrawer;
	}

	private static object? configManager;

	private delegate void setDmgFunc(ref HitData.DamageTypes dmg, float value);

	internal static void reloadConfigDisplay() => configManager?.GetType().GetMethod("BuildSettingList")!.Invoke(configManager, Array.Empty<object>());

	[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
	private static class FetchConfigManager
	{
		private static void Prefix(FejdStartup __instance)
		{
			Assembly? bepinexConfigManager = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ConfigurationManager");

			Type? configManagerType = bepinexConfigManager?.GetType("ConfigurationManager.ConfigurationManager");
			configManager = configManagerType == null ? null : BepInEx.Bootstrap.Chainloader.ManagerObject.GetComponent(configManagerType);
		}
	}

	internal static Localization english = null!;

	public void Awake()
	{
		self = this;

		configFilePaths = new List<string> { Path.GetDirectoryName(Config.ConfigFilePath), Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) };

		Config.SaveOnConfigSet = false;

		Localizer.Load();
		english = new Localization();
		english.SetupLanguage("English");

		AssetBundle assets = PrefabManager.RegisterAssetBundle("jewelcrafting");
		AssetBundle compendiumAssets = PrefabManager.RegisterAssetBundle("jc_ui_additions");
		CompendiumDisplay.initializeCompendiumDisplay(compendiumAssets);

		jewelcrafting = new Skill("Jewelcrafting", assets.LoadAsset<Sprite>("jewelcutting"));
		jewelcrafting.Name.Alias("jc_jewelcrafting_skill_name");
		jewelcrafting.Description.Alias("jc_jewelcrafting_skill_description");
		jewelcrafting.Configurable = false;

		serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
		configSync.AddLockingConfigEntry(serverConfigLocked);
		// Socket System
		socketSystem = config("2 - Socket System", "Socket System", Toggle.On, "Enables or disables the socket system.");
		socketSystem.SettingChanged += (_, _) =>
		{
			if (socketSystem.Value == Toggle.On)
			{
				ConfigLoader.loaders.First(l => l.GetType() == typeof(EffectDef.Loader)).ApplyConfig();
			}
			else
			{
				SocketEffects.Clear();
				EffectPowers.Clear();
			}
			foreach (DestructibleSetup.DestructibleGem destructibleGem in FindObjectsOfType<DestructibleSetup.DestructibleGem>())
			{
				destructibleGem.gameObject.SetActive(socketSystem.Value == Toggle.On);
			}
		};

		int order = 0;

		config("2 - Socket System", "YAML Editor Anchor", 0, new ConfigDescription("Just ignore this.", null, new ConfigurationManagerAttributes { HideSettingName = true, HideDefaultButton = true, CustomDrawer = DrawYamlEditorButton }), false);
		inventorySocketing = config("2 - Socket System", "Inventory Socketing", Toggle.On, "If enabled, you can press the interact key to change gems in your items from your inventory. If disabled, you have to use the Gemcutters Table, to change the gems in your items.");
		inventoryInteractBehaviour = config("2 - Socket System", "Interact Behaviour", InteractBehaviour.Hovering, "Disabled: Interact key is disabled, while the inventory is open.\nHovering: Interact key is disabled, while hovering an item with at least one socket.\nEnabled: Interact key is enabled. You will have to use the Gemcutters Table, to socket your items.", false);
		visualEffects = config("2 - Socket System", "Particle Effects", Toggle.On, "Enables or disables the particle effects for perfect gems.", false);
		visualEffects.SettingChanged += (_, _) =>
		{
			foreach (ItemDrop item in Resources.FindObjectsOfTypeAll<ItemDrop>())
			{
				if (visualEffects.Value == Toggle.On)
				{
					VisualEffects.DisplayEffectOnItemDrop.Postfix(item);
				}
				else
				{
					VisualEffects.DisplayEffectOnItemDrop.RemoveEffects(item);
				}
			}
		};
		displayGemcursor = config("2 - Socket System", "Display Gem Cursor", Toggle.On, "Changes the cursor to a gem, while interacting with the Gemcutters Table or socketing an item.", false);
		displaySocketBackground = config("2 - Socket System", "Display Socket Background", Toggle.On, "Changes the background of items that have sockets.", false);
		displaySocketBackground.SettingChanged += (_, _) => SocketsBackground.UpdateSocketBackground();
		colorItemName = config("2 - Socket System", "Color Item Names", Toggle.On, "Colors the name of items according to their socket levels.", false);
		useExternalYaml = configSync.AddConfigEntry(Config.Bind("2 - Socket System", "Use External YAML", Toggle.Off, new ConfigDescription("If set to on, the YAML file from your config folder will be used, to override gem effects configured inside of that file.", null, new ConfigurationManagerAttributes { Order = --order })));
		useExternalYaml.SourceConfig.SettingChanged += (_, _) => ConfigLoader.reloadConfigFile();
		badLuckRecipes = config("2 - Socket System", "Bad Luck Recipes", Toggle.On, new ConfigDescription("Enables or disables the bad luck recipes of all gems.", null, new ConfigurationManagerAttributes { Order = --order }));
		uniqueGemDropSystem = config("2 - Socket System", "Drop System for Unique Gems", UniqueDrop.TrulyUnique, new ConfigDescription("Disabled: Unique Gems do not drop.\nTruly Unique: The first kill of each boss grants one Unique Gem.\nCustom: Lets you configure a drop chance and rate.", null, new ConfigurationManagerAttributes { Order = --order }));
		uniqueGemDropChance = config("2 - Socket System", "Drop Chance for Unique Gems", 30, new ConfigDescription("Drop chance for Unique Gems. Has no effect, if the drop system is not set to custom.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order }));
		uniqueGemDropOnePerPlayer = config("2 - Socket System", "Drop one Gem per Player", Toggle.On, new ConfigDescription("If bosses should drop one Unique Gem per player. Has no effect, if the drop system is not set to custom.", null, new ConfigurationManagerAttributes { Order = --order }));
		allowUnsocketing = config("2 - Socket System", "Gems can be removed from items", Unsocketing.All, new ConfigDescription("All: All gems can be removed from items.\nUnique Only: Only unique gems can be removed from items.\nDisabled: No gems can be removed from items.\nDoes not affect gems without an effect.", null, new ConfigurationManagerAttributes { Order = --order }));
		breakChanceUnsocketSimple = config("2 - Socket System", "Simple Gem Break Chance", 0, new ConfigDescription("Chance to break a simple gem when trying to remove it from a socket. Does not affect gems without an effect.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order }));
		breakChanceUnsocketAdvanced = config("2 - Socket System", "Advanced Gem Break Chance", 0, new ConfigDescription("Chance to break an advanced gem when trying to remove it from a socket. Does not affect gems without an effect.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order }));
		breakChanceUnsocketPerfect = config("2 - Socket System", "Perfect Gem Break Chance", 0, new ConfigDescription("Chance to break a perfect gem when trying to remove it from a socket. Does not affect gems without an effect.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order }));
		breakChanceUnsocketMerged = config("2 - Socket System", "Merged Gem Break Chance", 0, new ConfigDescription("Chance to break a merged gem when trying to remove it from a socket. Does not affect gems without an effect.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order }));
		resourceReturnRate = config("2 - Socket System", "Percentage Recovered", 0, new ConfigDescription("Percentage of items to be recovered, when an item breaks while trying to add a socket to it.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order }));
		resourceReturnRateDistance = config("2 - Socket System", "Maximum Distance for Item Recovery", 0, new ConfigDescription("Maximum distance between the position where the item has been crafted and the position where the item has been destroyed to recover non-teleportable resources. This can be used, to prevent people from crafting items from metal, taking them through a portal and destroying them on the other side, to teleport the metal. Setting this to 0 disables this.", null, new ConfigurationManagerAttributes { Order = --order }));
		maximumNumberSockets = config("2 - Socket System", "Maximum number of Sockets", 3, new ConfigDescription("Maximum number of sockets on each item.", new AcceptableValueRange<int>(1, 5), new ConfigurationManagerAttributes { Order = --order }));
		maximumNumberSockets.SettingChanged += (_, _) => SocketsBackground.CalculateColors();
		gemRespawnRate = config("2 - Socket System", "Gemstone Respawn Time", 100, new ConfigDescription("Respawn time for raw gemstones in ingame days. Use 0 to disable respawn.", null, new ConfigurationManagerAttributes { Order = --order }));
		socketingItemsExperience = config("2 - Socket System", "Adding Sockets grants Experience", Toggle.On, new ConfigDescription("If off, adding sockets to items does not grant Jewelcrafting experience anymore. This can be used, to prevent people from crafting cheap items and socketing them, to level up the skill.", null, new ConfigurationManagerAttributes { Order = --order }));
		crystalFusionBoxDropRate[0] = config("3 - Fusion Box", "Drop rate for Fusion Box", 200, new ConfigDescription("Drop rate for the Common Crystal Fusion Box. Format is 1:x. The chance is further increased by creature health. Use 0 to disable the drop.", null, new ConfigurationManagerAttributes { Order = --order }));
		crystalFusionBoxDropRate[1] = config("3 - Fusion Box", "Drop rate for Blessed Fusion Box", 500, new ConfigDescription("Drop rate for the Blessed Crystal Fusion Box. Format is 1:x. The chance is further increased by creature health. Use 0 to disable the drop.", null, new ConfigurationManagerAttributes { Order = --order }));
		crystalFusionBoxDropRate[2] = config("3 - Fusion Box", "Drop rate for Celestial Fusion Box", 1000, new ConfigDescription("Drop rate for the Celestial Crystal Fusion Box. Format is 1:x. The chance is further increased by creature health. Use 0 to disable the drop.", null, new ConfigurationManagerAttributes { Order = --order }));
		crystalFusionBoxMergeActivityProgress[0] = config("3 - Fusion Box", "Activity reward for Fusion Box", 1.5f, new ConfigDescription("Progress for the Common Crystal Fusion Box per minute of activity.", null, new ConfigurationManagerAttributes { Order = --order }));
		crystalFusionBoxMergeActivityProgress[1] = config("3 - Fusion Box", "Activity reward for Blessed Fusion Box", 0.7f, new ConfigDescription("Progress for the Blessed Crystal Fusion Box per minute of activity", null, new ConfigurationManagerAttributes { Order = --order }));
		crystalFusionBoxMergeActivityProgress[2] = config("3 - Fusion Box", "Activity reward for Celestial Fusion Box", 0.3f, new ConfigDescription("Progress for the Celestial Crystal Fusion Box per minute of activity.", null, new ConfigurationManagerAttributes { Order = --order }));
		gachaLocationIcon = config("4 - World Boss", "Location Icon", Toggle.On, new ConfigDescription("Display the map icon of the mystical gemstone.", null, new ConfigurationManagerAttributes { Order = --order }));
		gachaLocationIcon.SettingChanged += (_, _) => ZoneSystem.instance.GetLocation("JC_Gacha_Location").m_iconPlaced = gachaLocationIcon.Value == Toggle.On;
		bossSpawnTimer = config("4 - World Boss", "Time between Boss Spawns", 120, new ConfigDescription("Time in minutes between boss spawns. Set this to 0, to disable boss spawns.", null, new ConfigurationManagerAttributes { Order = --order }));
		bossSpawnTimer.SettingChanged += (_, _) => BossSpawn.UpdateBossTimerVisibility();
		bossSpawnMinDistance = config("4 - World Boss", "Minimum Distance Boss Spawns", 1000, new ConfigDescription("Minimum distance from the center of the map for boss spawns.", null, new ConfigurationManagerAttributes { Order = --order }));
		bossSpawnMaxDistance = config("4 - World Boss", "Maximum Distance Boss Spawns", 10000, new ConfigDescription("Maximum distance from the center of the map for boss spawns.", null, new ConfigurationManagerAttributes { Order = --order }));
		bossSpawnBaseDistance = config("4 - World Boss", "Base Distance Boss Spawns", 50, new ConfigDescription("Minimum distance to player build structures for boss spawns.", null, new ConfigurationManagerAttributes { Order = --order }));
		bossTimeLimit = config("4 - World Boss", "Time Limit", 30, new ConfigDescription("Time in minutes before world bosses despawn.", null, new ConfigurationManagerAttributes { Order = --order }));
		bossCoinDrop = config("4 - World Boss", "Coins per Boss Kill", 1, new ConfigDescription("Number of Celestial Coins dropped by bosses per player.", new AcceptableValueRange<int>(0, 20), new ConfigurationManagerAttributes { Order = --order }));
		void WorldBossCustomChanged(object? o, EventArgs? e)
		{
			switch (worldBossBalance.Value)
			{
				case GachaSetup.BalanceToggle.Plains:
					BossSetup.ApplyBalanceConfig(BossSetup.plainsConfigs);
					break;
				case GachaSetup.BalanceToggle.Mistlands:
					BossSetup.ApplyBalanceConfig(BossSetup.mistlandsConfigs);
					break;
				case GachaSetup.BalanceToggle.Custom:
					BossSetup.ApplyBalanceConfig(new BossSetup.BalanceConfig
					{
						health = worldBossHealth.Value,
						punchBlunt = worldBossPunchDamage.Value,
						smashBlunt = worldBossSmashDamage.Value,
						aoeFire = worldBossFireDamage.Value,
						aoeFrost = worldBossFrostDamage.Value,
						aoePoison = worldBossPoisonDamage.Value
					});
					break;
			}
		}
		worldBossBalance = config("4 - World Boss", "Balance", GachaSetup.BalanceToggle.Mistlands, new ConfigDescription("Balancing to use for the world bosses.", null, new ConfigurationManagerAttributes { Order = --order }));
		List<ConfigurationManagerAttributes> worldBossCustomAttributes = new();
		worldBossBalance.SettingChanged += (o, e) =>
		{
			foreach (ConfigurationManagerAttributes attributes in worldBossCustomAttributes)
			{
				attributes.Browsable = worldBossBalance.Value == GachaSetup.BalanceToggle.Custom;
			}
			reloadConfigDisplay();
			WorldBossCustomChanged(o, e);
		};
		ConfigurationManagerAttributes WorldBossAttribute()
		{
			ConfigurationManagerAttributes attributes = new() { Order = --order, Browsable = worldBossBalance.Value == GachaSetup.BalanceToggle.Custom };
			worldBossCustomAttributes.Add(attributes);
			return attributes;
		}
		worldBossHealth = config("4 - World Boss", "Boss Health", BossSetup.mistlandsConfigs.health, new ConfigDescription("Balancing to use for the world bosses.", null, WorldBossAttribute()));
		worldBossHealth.SettingChanged += WorldBossCustomChanged;
		worldBossPunchDamage = config("4 - World Boss", "Punch Damage", BossSetup.mistlandsConfigs.punchBlunt, new ConfigDescription("Basic attack damage dealt by world bosses.", null, WorldBossAttribute()));
		worldBossPunchDamage.SettingChanged += WorldBossCustomChanged;
		worldBossSmashDamage = config("4 - World Boss", "Smash Damage", BossSetup.mistlandsConfigs.smashBlunt, new ConfigDescription("Smash attack damage dealt by world bosses.", null, WorldBossAttribute()));
		worldBossSmashDamage.SettingChanged += WorldBossCustomChanged;
		worldBossFireDamage = config("4 - World Boss", "Fire Damage", BossSetup.mistlandsConfigs.aoeFire, new ConfigDescription("Fire damage dealt by world bosses.", null, WorldBossAttribute()));
		worldBossFireDamage.SettingChanged += WorldBossCustomChanged;
		worldBossFrostDamage = config("4 - World Boss", "Frost Damage", BossSetup.mistlandsConfigs.aoeFrost, new ConfigDescription("Frost damage dealt by world bosses.", null, WorldBossAttribute()));
		worldBossFrostDamage.SettingChanged += WorldBossCustomChanged;
		worldBossPoisonDamage = config("4 - World Boss", "Poison Damage", BossSetup.mistlandsConfigs.aoePoison, new ConfigDescription("Poison damage dealt by world bosses.", null, WorldBossAttribute()));
		worldBossPoisonDamage.SettingChanged += WorldBossCustomChanged;
		worldBossBonusWeaponDamage = config("4 - World Boss", "Celestial Weapon Bonus Damage", 10, new ConfigDescription("Bonus damage taken by world bosses when hit with a celestial weapon.", null, new ConfigurationManagerAttributes { Order = --order }));
		worldBossCountdownDisplayOffset = config("4 - World Boss", "Countdown Display Offset", 0, new ConfigDescription("Offset for the world boss countdown display on the world map. Increase this, to move the display down, to prevent overlapping with other mods.", null, new ConfigurationManagerAttributes { Order = --order }), false);
		worldBossCountdownDisplayOffset.SettingChanged += (_, _) => BossSpawn.UpdateBossTimerPosition();
		upgradeChanceIncrease = config("5 - Other", "Success Chance Increase", 15, new ConfigDescription("Success chance increase at jewelcrafting skill level 100.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --order }));
		experienceGainedFactor = config("5 - Other", "Skill Experience Gain Factor", 1f, new ConfigDescription("Factor for experience gained for the jewelcrafting skill.", new AcceptableValueRange<float>(0.01f, 5f), new ConfigurationManagerAttributes { Order = --order }));
		experienceGainedFactor.SettingChanged += (_, _) => jewelcrafting.SkillGainFactor = experienceGainedFactor.Value;
		jewelcrafting.SkillGainFactor = experienceGainedFactor.Value;
		gemBagSlotsRows = config("5 - Other", "Jewelers Bag Slot Rows", 2, new ConfigDescription("Rows in a Jewelers Bag. Changing this value does not affect existing bags.", new AcceptableValueRange<int>(1, 4), new ConfigurationManagerAttributes { Order = --order }));
		gemBagSlotsColumns = config("5 - Other", "Jewelers Bag Columns", 8, new ConfigDescription("Columns in a Jewelers Bag. Changing this value does not affect existing bags.", new AcceptableValueRange<int>(1, 8), new ConfigurationManagerAttributes { Order = --order }));
		gemBagAutofill = config("5 - Other", "Jewelers Bag Autofill", Toggle.Off, new ConfigDescription("If set to on, gems will be added into a Jewelers Bag automatically on pickup.", null, new ConfigurationManagerAttributes { Order = --order }), false);
		gemBoxSlotsRows = config("5 - Other", "Jewelers Box Slot Rows", 2, new ConfigDescription("Rows in a Jewelers Box. Changing this value does not affect existing boxes.", new AcceptableValueRange<int>(1, 4), new ConfigurationManagerAttributes { Order = --order }));
		gemBoxSlotsColumns = config("5 - Other", "Jewelers Box Columns", 2, new ConfigDescription("Columns in a Jewelers Box. Changing this value does not affect existing boxes.", new AcceptableValueRange<int>(1, 8), new ConfigurationManagerAttributes { Order = --order }));
		frameOfChanceChance = config("5 - Other", "Frame of Chance chance", 50, new ConfigDescription("Chance to add a socket instead of losing one when applying equipment to a frame of chance.", new AcceptableValueRange<int>(1, 100), new ConfigurationManagerAttributes { Order = --order }), false);
		advancedTooltipKey = config("5 - Other", "Advanced Tooltip Key", new KeyboardShortcut(KeyCode.LeftAlt), new ConfigDescription("Key to hold while hovering an item with sockets, to display the advanced tooltip.", null, new ConfigurationManagerAttributes { Order = --order }), false);
		advancedTooltipMode = config("5 - Other", "Advanced Tooltip Details", AdvancedTooltipMode.General, new ConfigDescription("How detailed the advanced tooltip should be.", null, new ConfigurationManagerAttributes { Order = --order }), false);
		advancedTooltipAlwaysOn = config("5 - Other", "Always Display Advanced Tooltip", Toggle.Off, new ConfigDescription("If on, the advanced tooltip is always displayed, instead of the name of the effect.", null, new ConfigurationManagerAttributes { Order = --order }), false);

		awarenessRange = config("Ruby Necklace of Awareness", "Detection Range", 30, new ConfigDescription("Creature detection range for the Ruby Necklace of Awareness.", new AcceptableValueRange<int>(1, 50)));
		rigidDamageReduction = config("Sturdy Spinel Ring", "Damage Reduction", 5, new ConfigDescription("Damage reduction for the Sturdy Spinel Ring.", new AcceptableValueRange<int>(0, 100)));
		headhunterDuration = config("Emerald Headhunter Ring", "Effect Duration", 20U, new ConfigDescription("Effect duration for the Emerald Headhunter Ring."));
		headhunterDamage = config("Emerald Headhunter Ring", "Damage Increase", 30, new ConfigDescription("Damage increase for the Emerald Headhunter Ring effect.", new AcceptableValueRange<int>(0, 100)));
		magicRepairAmount = config("Emerald Necklace of Magic Repair", "Repair Amount", 5, new ConfigDescription("Durability restoration per minute for the Emerald Necklace of Magic Repair effect.", new AcceptableValueRange<int>(0, 100)));
		aquaticDamageIncrease = config("Aquatic Sapphire Necklace", "Damage Increase", 10, new ConfigDescription("Damage increase while wearing the Aquatic Sapphire Necklace and being wet.", new AcceptableValueRange<int>(0, 100)));
		modersBlessingDuration = config("Ring of Moders Sapphire Blessing", "Effect Duration", 15, new ConfigDescription("Effect duration in seconds for the Ring of Moder's Sapphire Blessing."));
		modersBlessingCooldown = config("Ring of Moders Sapphire Blessing", "Effect Cooldown", 60, new ConfigDescription("Effect cooldown in seconds for the Ring of Moder's Sapphire Blessing."));

		void SetCfgValue<T>(Action<T> setter, ConfigEntry<T> config)
		{
			setter(config.Value);
			config.SettingChanged += (_, _) => setter(config.Value);
		}

		MiscSetup.initializeMisc(assets);
		BuildingPiecesSetup.initializeBuildingPieces(assets);
		GemStoneSetup.initializeGemStones(assets);
		DestructibleSetup.initializeDestructibles(assets);
		JewelrySetup.initializeJewelry(assets);
		VisualEffectSetup.initializeVisualEffects(assets);
		MergedGemStoneSetup.initializeMergedGemStones(assets);
		FusionBoxSetup.initializeFusionBoxes(assets);
		Synergy.initializeSynergy(assets);
		ConfigLoader.LoadBuiltinConfig();
		GachaSetup.initializeGacha(assets);
		BossSetup.initializeBosses(assets);
		SocketsBackground.CalculateColors();
		
		WorldBossCustomChanged(null, null);

		foreach (GachaSetup.BalanceConfig balanceConfig in GachaSetup.celestialItemsConfigs.Values)
		{
			ConfigEntry<GachaSetup.BalanceToggle> toggle = config(english.Localize(balanceConfig.item.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name), "Balance", GachaSetup.BalanceToggle.Mistlands, "");
			void Apply()
			{
				if (toggle.Value == GachaSetup.BalanceToggle.Custom)
				{
					balanceConfig.item.ToggleConfigurationVisibility(Configurability.Full);
				}
				else
				{
					balanceConfig.item.ToggleConfigurationVisibility(Configurability.Recipe | Configurability.Drop);
					ItemDrop.ItemData.SharedData sharedData = toggle.Value switch
					{
						GachaSetup.BalanceToggle.Mistlands => balanceConfig.mistlands,
						GachaSetup.BalanceToggle.Plains => balanceConfig.plains,
						_ => throw new ArgumentOutOfRangeException()
					};
					balanceConfig.item.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared = Utils.Clone(sharedData);
					balanceConfig.item.Prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack = Utils.Clone(sharedData.m_attack);

					if (ObjectDB.instance)
					{
						Inventory[] inventories = Player.m_players.Select(p => p.GetInventory()).Concat(FindObjectsOfType<Container>().Select(c => c.GetInventory())).Where(c => c is not null).ToArray();
						foreach (ItemDrop.ItemData itemdata in ObjectDB.instance.m_items.Select(p => p.GetComponent<ItemDrop>()).Where(c => c && c.GetComponent<ZNetView>()).Concat(ItemDrop.m_instances).Select(i => i.m_itemData).Concat(inventories.SelectMany(i => i.GetAllItems())))
						{
							if (itemdata.m_shared.m_name == sharedData.m_name)
							{
								itemdata.m_shared = Utils.Clone(sharedData);
								itemdata.m_shared.m_attack = Utils.Clone(sharedData.m_attack);
							}
						}
					}
				}
			}
			Apply();
			toggle.SettingChanged += (_, _) => Apply();
		}
		
		int socketAddingOrder = 0;
		for (int i = 0; i < 5; ++i)
		{
			socketAddingChances.Add(i, config("Socket Adding Chances", $"{i + 1}. Socket", 50, new ConfigDescription($"Success chance while trying to add the {i + 1}. Socket.", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --socketAddingOrder })));
		}

		string[] boxMergeCategory = { "simple", "advanced", "perfect" };
		int boxMergeOrder = 0;
		foreach (KeyValuePair<string, int[]> kv in defaultBoxMergeChances)
		{
			boxMergeChances.Add(kv.Key, kv.Value.Select((chance, i) => config("3 - Fusion Box", $"Merge Chance {boxMergeCategory[i]} gems in {english.Localize(kv.Key)}", chance, new ConfigDescription($"Success chance while merging two {boxMergeCategory[i]} gems in a {Localization.instance.Localize(kv.Key)}", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --boxMergeOrder }))).ToArray());
		}
		boxBossGemMergeChance = config("3 - Fusion Box", $"Merge Chance boss gems in {english.Localize("$jc_legendary_gembox")}", 100, new ConfigDescription($"Success chance while merging two boss gems in a {Localization.instance.Localize("$jc_legendary_gembox")}", new AcceptableValueRange<int>(0, 100), new ConfigurationManagerAttributes { Order = --boxMergeOrder }));

		foreach (KeyValuePair<string, float[]> kv in defaultBoxBossProgress)
		{
			AddBossBoxProgressConfig(kv.Key, kv.Value);
		}

		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);

		swordFall = PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_9");
		gliding = assets.LoadAsset<SE_Stats>("JCGliding");
		glidingDark = assets.LoadAsset<SE_Stats>("SE_DarkWings");
		glowingSpirit = assets.LoadAsset<SE_Stats>("SE_Crystal_Magelight");
		glowingSpiritPrefab = PrefabManager.RegisterPrefab(assets, "JC_Crystal_Magelight");
		glowingSpiritPrefab.AddComponent<GlowingSpirit.OrbDestroy>();
		lightningSpeed = Utils.ConvertStatusEffect<LightningSpeed.LightningSpeedEffect>(assets.LoadAsset<SE_Stats>("JC_Electric_Wings_SE"));
		apotheosis = Utils.ConvertStatusEffect<Apotheosis.ApotheosisEffect>(assets.LoadAsset<SE_Stats>("SE_Apotheosis"));
		poisonousDrain = assets.LoadAsset<SE_Stats>("SE_Boss_2");
		poisonousDrainCloud = PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_2");
		rootedRevenge = assets.LoadAsset<SE_Stats>("SE_Boss_3");
		icyProtection = assets.LoadAsset<SE_Stats>("SE_Boss_4");
		fieryDoom = assets.LoadAsset<SE_Stats>("SE_Boss_5");
		fieryDoomExplosion = PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_3");
		awareness = assets.LoadAsset<SE_Stats>("JC_SE_Necklace_Red");
		heardIcon = assets.LoadAsset<GameObject>("JC_Eyeball_Obj");
		attackedIcon = assets.LoadAsset<GameObject>("JC_Alert_Obj");
		headhunter = assets.LoadAsset<SE_Stats>("JC_Se_Ring_Green");
		headhunter.m_modifyAttackSkill = Skills.SkillType.All;
		assets.LoadAsset<SE_Stats>("JC_SE_Necklace_Yellow");
		rigidFinger = assets.LoadAsset<SE_Stats>("JC_Se_Ring_Purple");
		rigidFinger.m_modifyAttackSkill = Skills.SkillType.All;
		magicRepair = PrefabManager.RegisterPrefab(assets, "VFX_Buff_Green");
		aquatic = assets.LoadAsset<SE_Stats>("JC_Se_Necklace_Blue");
		aquatic.m_modifyAttackSkill = Skills.SkillType.All;
		lightningStart = assets.LoadAsset<SE_Stats>("SE_VFX_Start_Purple");
		rootStart = assets.LoadAsset<SE_Stats>("SE_VFX_Start_Brown");
		poisonStart = assets.LoadAsset<SE_Stats>("SE_VFX_Start_Green");
		iceStart = assets.LoadAsset<SE_Stats>("SE_VFX_Start_Blue");
		fireStart = assets.LoadAsset<SE_Stats>("SE_VFX_Start_Red");
		apotheosisStart = assets.LoadAsset<SE_Stats>("SE_VFX_Start_Black");
		friendshipStart = assets.LoadAsset<SE_Stats>("SE_VFX_Start_Purple");
		friendship = Utils.ConvertStatusEffect<TogetherForever.TogetherForeverEffect>(assets.LoadAsset<SE_Stats>("SE_Friendship_Group"));
		loneliness = Utils.ConvertStatusEffect<TogetherForever.LonelinessEffect>(assets.LoadAsset<SE_Stats>("SE_Loneliness_Group"));
		friendshipTether = assets.LoadAsset<GameObject>("VFX_FriendLine_Render");
		friendshipTether.AddComponent<FriendshipTether>();
		cowardice = assets.LoadAsset<SE_Stats>("SE_Cowardice");
		fireBossDebuff = assets.LoadAsset<SE_Stats>("SE_Boss_Fire");
		frostBossDebuff = assets.LoadAsset<SE_Stats>("SE_Boss_Frost");
		poisonBossDebuff = assets.LoadAsset<SE_Stats>("SE_Boss_Poison");
		thunderclapMark = assets.LoadAsset<SE_Stats>("SE_ThunderClap_Marked");
		thunderclapExplosion = assets.LoadAsset<GameObject>("JC_Marked_Explode");
		fading = Utils.ConvertStatusEffect<Fade.FadeSE>(assets.LoadAsset<SE_Stats>("SE_VFX_Fade"));
		fadingMaterial = assets.LoadAsset<Material>("JC_Player_Distortion");

		SocketsBackground.background = assets.LoadAsset<GameObject>("JC_ItemBackground");

		SetCfgValue(value => rigidFinger.m_damageModifier = 1 - value / 100f, rigidDamageReduction);
		SetCfgValue(value => headhunter.m_damageModifier = 1 + value / 100f, headhunterDamage);
		SetCfgValue(value => headhunter.m_ttl = value, headhunterDuration);
		SetCfgValue(value => aquatic.m_damageModifier = 1 + value / 100f, aquaticDamageIncrease);

		Necromancer.skeleton = PrefabManager.RegisterPrefab(assets, "JC_Skeleton");

		PrefabManager.RegisterPrefab(assets, "JC_Electric_Wings");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_5");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_6");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_7");
		PrefabManager.RegisterPrefab(assets, "JCGliding");
		PrefabManager.RegisterPrefab(assets, "JC_DarkWings");
		PrefabManager.RegisterPrefab(assets, "VFX_StoneRed");
		PrefabManager.RegisterPrefab(assets, "VFX_StoneBlack");
		PrefabManager.RegisterPrefab(assets, "sfx_build_table");
		PrefabManager.RegisterPrefab(assets, "sfx_crystal_craft");
		PrefabManager.RegisterPrefab(assets, "sfx_crystal_repair");
		PrefabManager.RegisterPrefab(assets, "sfx_destroy_table");
		PrefabManager.RegisterPrefab(assets, "sfx_stoneuse");
		PrefabManager.RegisterPrefab(assets, "sfx_rebirth");
		PrefabManager.RegisterPrefab(assets, "vfx_Destroy_Alchemy");
		PrefabManager.RegisterPrefab(assets, "sfx_buffed");
		PrefabManager.RegisterPrefab(assets, "vfx_crystal_destroyed");
		PrefabManager.RegisterPrefab(assets, "sfx_crystal_destroyed");
		PrefabManager.RegisterPrefab(assets, "vfx_potionhit");
		PrefabManager.RegisterPrefab(assets, "sfx_potion_smash");
		PrefabManager.RegisterPrefab(assets, "vfx_puff_small");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_1");
		PrefabManager.RegisterPrefab(assets, "sfx_start_buff");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Start_Purple");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Start_Brown");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Start_Green");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Start_Blue");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Start_Red");
		PrefabManager.RegisterPrefab(assets, "sfx_start_buff_2");
		PrefabManager.RegisterPrefab(assets, "VFX_Group_Loneliness");
		PrefabManager.RegisterPrefab(assets, "VFX_FriendLine_Render");
		PrefabManager.RegisterPrefab(assets, "VFX_Hearts_Start");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_offering");
		PrefabManager.RegisterPrefab(assets, "VFX_Crystal_Explosion_Red");
		PrefabManager.RegisterPrefab(assets, "VFX_Crystal_Explosion_Blue");
		PrefabManager.RegisterPrefab(assets, "VFX_Crystal_Explosion_Green");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_idle");
		PrefabManager.RegisterPrefab(assets, "vfx_reaper_hit");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_attack");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_death");
		PrefabManager.RegisterPrefab(assets, "VFX_Boss_Death");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_attack_hit");
		PrefabManager.RegisterPrefab(assets, "vfx_reaper_destroyed");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_hurt");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_rock_destroyed");
		PrefabManager.RegisterPrefab(assets, "JC_Boss_Projectile_Fire");
		PrefabManager.RegisterPrefab(assets, "JC_Boss_Projectile_Frost");
		PrefabManager.RegisterPrefab(assets, "JC_Boss_Projectile_Poison");
		PrefabManager.RegisterPrefab(assets, "sfx_reaper_alert");
		PrefabManager.RegisterPrefab(assets, "sfx_open_box");
		PrefabManager.RegisterPrefab(assets, "SFX_Reaper_Bow_Draw");
		PrefabManager.RegisterPrefab(assets, "SFX_Reaper_Bow_Fire");
		PrefabManager.RegisterPrefab(assets, "SFX_Reaper_Weapon_Blocked");
		PrefabManager.RegisterPrefab(assets, "SFX_Reaper_Weapon_Hit");
		PrefabManager.RegisterPrefab(assets, "SFX_Reaper_Weapon_Swing");
		PrefabManager.RegisterPrefab(assets, "VFX_Reaper_Bow_Fire");
		PrefabManager.RegisterPrefab(assets, "VFX_Reaper_Weapon_CamShake");
		PrefabManager.RegisterPrefab(assets, "VFX_Reaper_Weapon_Hit");
		PrefabManager.RegisterPrefab(assets, "SFX_Arrow_Explosion");
		PrefabManager.RegisterPrefab(assets, "vfx_reaper_water_surface");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Apotheosis");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Start_Black");
		PrefabManager.RegisterPrefab(assets, "JC_Marked_Effect");
		PrefabManager.RegisterPrefab(assets, "JC_Marked_Explode");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Fade");
		PrefabManager.RegisterPrefab(assets, "JC_Buff_FX_Fade_End");

		Localizer.AddPlaceholder("jc_ring_purple_description", "power", rigidDamageReduction);
		Localizer.AddPlaceholder("jc_se_ring_purple_description", "power", rigidDamageReduction);
		Localizer.AddPlaceholder("jc_ring_green_description", "power", headhunterDamage);
		Localizer.AddPlaceholder("jc_ring_green_description", "duration", headhunterDuration);
		Localizer.AddPlaceholder("jc_se_ring_green_description", "power", headhunterDamage);
		Localizer.AddPlaceholder("jc_se_necklace_blue_description", "power", aquaticDamageIncrease);
		Localizer.AddPlaceholder("jc_ring_blue_description", "duration", modersBlessingDuration);
		Localizer.AddPlaceholder("jc_ring_blue_description", "cooldown", modersBlessingCooldown);
		Localizer.AddPlaceholder("jc_se_ring_blue_description", "duration", modersBlessingDuration);
		Localizer.AddPlaceholder("jc_se_ring_blue_description", "cooldown", modersBlessingCooldown);
		Localizer.AddPlaceholder("jc_reaper_sword_description", "power", worldBossBonusWeaponDamage);
		Localizer.AddPlaceholder("jc_reaper_axe_description", "power", worldBossBonusWeaponDamage);
		Localizer.AddPlaceholder("jc_reaper_bow_description", "power", worldBossBonusWeaponDamage);
		Localizer.AddPlaceholder("jc_reaper_pickaxe_description", "power", worldBossBonusWeaponDamage);

		Config.SaveOnConfigSet = true;
		Config.Save();

		BossSpawn.SetupBossSpawn();
	}

	private static void AddBossBoxProgressConfig(string name, float[] progress)
	{
		Regex regex = new("['[\"\\]]");

		boxBossProgress.Add(name, progress.Select((chance, i) => config("3 - Fusion Box", $"Boss Progress {regex.Replace(english.Localize(name), "")} {english.Localize(FusionBoxSetup.Boxes[i].GetComponent<ItemDrop>().m_itemData.m_shared.m_name)}", chance, new ConfigDescription($"Progress applied to {english.Localize(FusionBoxSetup.Boxes[i].GetComponent<ItemDrop>().m_itemData.m_shared.m_name)} when killing {regex.Replace(english.Localize(name), "")}", null, new ConfigurationManagerAttributes { Order = -boxBossProgress.Count * 3 - i - 1000 }))).ToArray());
	}

	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	private static class AddBossBoxProgressConfigs
	{
		private static void Postfix(ZNetScene __instance)
		{
			foreach (GameObject prefab in __instance.m_prefabs)
			{
				if (prefab.GetComponent<Character>() is { } character && character.IsBoss() && !boxBossProgress.ContainsKey(character.m_name))
				{
					AddBossBoxProgressConfig(character.m_name, new[] { 0f, 0f, 0f });
				}
			}
		}
	}

	[HarmonyPatch(typeof(ZRoutedRpc), nameof(ZRoutedRpc.AddPeer))]
	private static class AddRPCs
	{
		private static void Postfix(ZNetPeer peer)
		{
			if (ZNet.instance.IsServer())
			{
				peer.m_rpc.Register("Jewelcrafting GenerateVegetation", GenerateVegetationSpawners.RPC_GenerateVegetation);
				peer.m_rpc.Register("Jewelcrafting SpawnBoss", _ => BossSpawn.SpawnBoss());
			}
		}
	}

	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
	private static class AddStatusEffects
	{
		private static void Prefix(ObjectDB __instance)
		{
			__instance.m_StatusEffects.Add(headhunter);
		}
	}
}
