﻿using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using ItemManager;
using UnityEngine;
using UnityEngine.UI;

namespace Jewelcrafting;

public struct GemDefinition
{
	public string Name;
	public GameObject Prefab;
}

public struct GemInfo
{
	public GemType Type;
	public int Tier;
}

public static class GemStoneSetup
{
	public static GameObject SocketTooltip = null!;
	public static readonly Dictionary<GemType, List<GemDefinition>> Gems = new();
	public static readonly Dictionary<GemType, GameObject> shardColors = new();
	public static readonly Dictionary<GemType, GameObject> uncutGems = new();
	public static readonly Dictionary<string, GemInfo> GemInfos = new();

	public static readonly Dictionary<GemType, Color> Colors = new()
	{
		{ GemType.Black, Color.black },
		{ GemType.Blue, Color.blue },
		{ GemType.Red, Color.red },
		{ GemType.Yellow, Color.yellow },
		{ GemType.Green, Color.green },
		{ GemType.Purple, Color.magenta },
		//{ GemType.Orange, new Color(1, 0.6f, 0) }
	};

	public static readonly GameObject[] customGemTierPrefabs = new GameObject[3];
	public static GameObject customUncutGemPrefab = null!;
	public static GameObject customGemShardPrefab = null!;
	private static readonly int ShaderColorKey = Shader.PropertyToID("_Color");
	private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

	private static GameObject CreateItemFromTemplate(GameObject template, string colorName, string localizationName, Color color)
	{
		GameObject prefab = Object.Instantiate(template, MergedGemStoneSetup.gemList.transform);
		prefab.name = template.name.Replace("Custom", colorName);
		ItemDrop.ItemData.SharedData shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
		shared.m_name = $"${localizationName}";
		shared.m_description = $"${localizationName}_description";
		prefab.transform.Find("attach/Custom_Color_Mesh").GetComponent<MeshRenderer>().material.SetColor(ShaderColorKey, color);
		prefab.transform.Find("attach/Custom_Color_Mesh").GetComponent<MeshRenderer>().material.SetColor(EmissionColor, color);
		ItemSnapshots.SnapshotItems(prefab.GetComponent<ItemDrop>());
		return prefab;
	}

	public static GameObject CreateGemFromTemplate(GameObject template, string colorName, Color color, int tier) => CreateItemFromTemplate(template, colorName, $"jc_{tier switch { 1 => "adv_", 2 => "perfect_", _ => "" }}{colorName.Replace(" ", "_").ToLower()}_socket", color);
	public static GameObject CreateShardFromTemplate(GameObject template, string colorName, Color color) => CreateItemFromTemplate(template, colorName, $"jc_shattered_{colorName.Replace(" ", "_").ToLower()}_crystal", color);
	public static GameObject CreateUncutFromTemplate(GameObject template, string colorName, Color color) => CreateItemFromTemplate(template, colorName, $"jc_uncut_{colorName.Replace(" ", "_").ToLower()}_stone", color);
	
	public static void RegisterGem(GameObject prefab, GemType color)
	{
		if (shardColors.TryGetValue(color, out GameObject shard))
		{
			GemStones.gemToShard.Add(prefab.name, shard);
		}

		if (!Gems.TryGetValue(color, out List<GemDefinition> colorGems))
		{
			colorGems = Gems[color] = new List<GemDefinition>();
		}
		string gemName = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
		GemStones.socketableGemStones.Add(gemName);
		colorGems.Add(new GemDefinition
		{
			Prefab = prefab,
			Name = gemName,
		});

		GemInfos[gemName] = new GemInfo
		{
			Type = color,
			Tier = colorGems.Count,
		};
	}

	public static void RegisterShard(GameObject prefab, GemType color)
	{
		shardColors[color] = new Item(prefab).Prefab;
	}

	public static void RegisterUncutGem(GameObject prefab, GemType color, ConfigEntry<int>? dropChance = null)
	{
		Item gemStone = new(prefab);
		uncutGems[color] = prefab;
		if (dropChance is not null)
		{
			Jewelcrafting.gemDropChances.Add(gemStone.Prefab, dropChance);
		}
	}

	public static void RegisterTieredGemItem(GameObject prefab, string colorName, int tier)
	{
		string gemName = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
		Jewelcrafting.gemUpgradeChances.Add(gemName, Jewelcrafting.config("Socket Upgrade Chances", Jewelcrafting.english.Localize(gemName), tier switch { 0 => 30f, 1 => 20f, _ => 10f }, new ConfigDescription($"Success chance while trying to create {Localization.instance.Localize(gemName)}.", new AcceptableValueRange<float>(0f, 100f), new Jewelcrafting.ConfigurationManagerAttributes { DispName = Localization.instance.Localize(gemName) })));

		Item gemStone = new(prefab) { Configurable = false };
		switch (tier)
		{
			case 0:
				gemStone["GambleRecipe"].Crafting.Add("op_transmution_table", 1);
				gemStone["GambleRecipe"].RequiredItems.Add($"Uncut_{colorName}_Stone", 1);
				gemStone["GambleRecipe"].RecipeIsActive = Jewelcrafting.socketSystem;
				gemStone["BadLuckRecipe"].Crafting.Add("op_transmution_table", 1);
				gemStone["BadLuckRecipe"].RequiredItems.Add($"Shattered_{colorName}_Crystal", Jewelcrafting.config("Bad Luck Protection", $"Bad Luck Cost Simple {colorName}", 12, new ConfigDescription($"{colorName} shards required to craft a Simple {colorName}.")));
				gemStone["BadLuckRecipe"].RecipeIsActive = Jewelcrafting.badLuckRecipes;
				break;
			case 1:
				gemStone["GambleRecipe"].Crafting.Add("op_transmution_table", 1);
				gemStone["GambleRecipe"].RequiredItems.Add($"Simple_{colorName}_Socket", 1);
				gemStone["GambleRecipe"].RecipeIsActive = Jewelcrafting.socketSystem;
				gemStone["BadLuckRecipe"].Crafting.Add("op_transmution_table", 1);
				gemStone["BadLuckRecipe"].RequiredItems.Add($"Shattered_{colorName}_Crystal", Jewelcrafting.config("Bad Luck Protection", $"Bad Luck Cost Advanced {colorName}", 35, new ConfigDescription($"{colorName} shards required to craft an Advanced {colorName}.")));
				gemStone["BadLuckRecipe"].RecipeIsActive = Jewelcrafting.badLuckRecipes;
				break;
			case 2:
				gemStone.Crafting.Add("op_transmution_table", 2);
				gemStone.RequiredItems.Add($"Advanced_{colorName}_Socket", 1);
				gemStone.RecipeIsActive = Jewelcrafting.socketSystem;
				break;
		}
	}
	
	public static void initializeGemStones(AssetBundle assets)
	{
		SocketTooltip = assets.LoadAsset<GameObject>("CrystalText");
		GemStones.emptySocketSprite = SocketTooltip.transform.Find("Bkg (1)/TrannyHoles/Transmute_Text_1/Border/Transmute_1").GetComponent<Image>().sprite;

		customGemTierPrefabs[0] = assets.LoadAsset<GameObject>("Simple_Custom_Socket");
		customGemTierPrefabs[1] = assets.LoadAsset<GameObject>("Advanced_Custom_Socket");
		customGemTierPrefabs[2] = assets.LoadAsset<GameObject>("Perfect_Custom_Socket");
		customUncutGemPrefab = assets.LoadAsset<GameObject>("Uncut_Custom_Stone");
		customGemShardPrefab = assets.LoadAsset<GameObject>("Shattered_Custom_Crystal");

		foreach (KeyValuePair<GemType, Color> gemType in Colors)
		{
			RegisterShard(assets.LoadAsset<GameObject>(customGemShardPrefab.name.Replace("Custom", gemType.Key.ToString())), gemType.Key);
			string name = Jewelcrafting.english.Localize($"$jc_merged_gemstone_{gemType.Key.ToString().ToLower()}");
			RegisterUncutGem(assets.LoadAsset<GameObject>(customUncutGemPrefab.name.Replace("Custom", gemType.Key.ToString())), gemType.Key, Jewelcrafting.config("Gem Drops", $"Drop chance for {name} Gemstones", 2, new ConfigDescription($"Chance to drop {name} gemstones when killing creatures.", new AcceptableValueRange<int>(0, 100))));

			for (int tier = 0; tier < customGemTierPrefabs.Length; ++tier)
			{
				GameObject prefab = assets.LoadAsset<GameObject>(customGemTierPrefabs[tier].name.Replace("Custom", gemType.Key.ToString()));
				RegisterTieredGemItem(prefab, gemType.Key.ToString(), tier);
				RegisterGem(prefab, gemType.Key);
			}
		}

		Item AddGem(string prefab, GemType color)
		{
			Item gemStone = new(assets, prefab) { Configurable = false };
			RegisterGem(gemStone.Prefab, color);
			return gemStone;
		}

		Item gemStone = AddGem("Boss_Crystal_1", GemType.Elder);
		GemStones.bossToGem.Add("gd_king", gemStone.Prefab);
		gemStone = AddGem("Boss_Crystal_2", GemType.Bonemass);
		GemStones.bossToGem.Add("Bonemass", gemStone.Prefab);
		gemStone = AddGem("Boss_Crystal_4", GemType.Moder);
		GemStones.bossToGem.Add("Dragon", gemStone.Prefab);
		gemStone = AddGem("Boss_Crystal_5", GemType.Yagluth);
		GemStones.bossToGem.Add("GoblinKing", gemStone.Prefab);
		gemStone = AddGem("Boss_Crystal_7", GemType.Eikthyr);
		GemStones.bossToGem.Add("Eikthyr", gemStone.Prefab);
	}
	
	[HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
	private class AddGemStonesToDrops
	{
		private static void Postfix(List<KeyValuePair<GameObject, int>> __result)
		{
			if (Jewelcrafting.socketSystem.Value == Jewelcrafting.Toggle.On)
			{
				__result.AddRange(from gem in Jewelcrafting.gemDropChances.Keys where Random.value < Jewelcrafting.gemDropChances[gem].Value / 100f select new KeyValuePair<GameObject, int>(gem, 1));
			}
		}
	}
}