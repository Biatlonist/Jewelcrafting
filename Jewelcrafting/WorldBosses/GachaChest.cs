using System;
using System.Collections.Generic;
using System.Linq;
using ExtendedItemDataFramework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Jewelcrafting.WorldBosses;

public class GachaChest : Container, Hoverable
{
	private new void Awake()
	{
		base.Awake();
		m_nview.Unregister("RequestOpen");
		m_nview.Register("RequestOpen", new Action<long, long>(GachaOpen));
		m_nview.Register("Jewelcrafting Gacha Chest No Coins", _ => Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$jc_gacha_chest_locked"));
	}

	public static IEnumerable<GachaChest> NearbyGachaChests(Transform origin) => Resources.FindObjectsOfTypeAll<GachaChest>().Where(g => g.m_nview is not null && Vector3.Distance(g.transform.position, origin.position) < 7);

	public new string GetHoverName() => Localization.instance.Localize("$jc_gacha_chest_name");
	public new string GetHoverText() => GetHoverName() + Localization.instance.Localize("\n[<color=yellow><b>$KEY_Use</b></color>] $piece_container_open");

	private void GachaOpen(long uid, long playerID)
	{
		if (GetInventory().NrOfItems() > 0)
		{
			RPC_RequestOpen(uid, playerID);
			return;
		}
		
		int coins = m_nview.GetZDO().GetInt("Jewelcrafting Gacha Chest");
		if (coins == 0)
		{
			m_nview.InvokeRPC(uid, "Jewelcrafting Gacha Chest No Coins");
			return;
		}

		for (int i = 0; i < coins; ++i)
		{
			if (GachaDef.ActivePrizes() is {} selectedPrizes)
			{
				float random = Random.value;
				Random.State state = SetRandomState(selectedPrizes);
				foreach (Prize prize in selectedPrizes.prizes.OrderBy(p => p.Chance))
				{
					if (random < prize.Chance && GachaDef.getItem(prize.Item) is { } item)
					{
						ItemDrop.ItemData itemData = m_inventory.AddItem(item.name, 1, 1, 0, 0, "");
						if (prize.Sockets.Count > 0)
						{
							itemData.Extended().AddComponent<Sockets>();
							itemData.Extended().GetComponent<Sockets>().socketedGems.Clear();
							foreach (string socket in prize.Sockets)
							{
								itemData.Extended().GetComponent<Sockets>().socketedGems.Add(new SocketItem(socket.ToLower() == "empty" ? "" : GachaDef.getItem(socket)!.name));
							}
							itemData.Extended().Save();
						}
						Save();
						break;
					}
					random -= prize.Chance;
				}

				Random.state = state;
			}
		}

		for (int i = coins - m_inventory.NrOfItems(); i > 0; --i)
		{
			float roll = Random.value;
			if (roll < 0.02)
			{
				m_inventory.AddItem(Utils.getRandomGem(3)!.gameObject, 1);
			}
			else if (roll < 0.05)
			{
				m_inventory.AddItem(Utils.getRandomGem(2)!.gameObject, 1);
			}
			else if (roll < 0.2)
			{
				m_inventory.AddItem(Utils.getRandomGem(1)!.gameObject, 1);
			}
			else
			{
				m_inventory.AddItem(Utils.getRandomGem(-1)!.gameObject, 1);
			}
		}
		
		foreach (GachaChest chest in NearbyGachaChests(transform))
		{
			chest.m_nview.GetZDO().Set("Jewelcrafting Gacha Chest", 0);
		}
		
		RPC_RequestOpen(uid, playerID);
	}

	public static DateTimeOffset Expiration(Prizes prizes)
	{
		if (prizes.RotationDays == 0 && prizes.DurationDays <= 0)
		{
			return DateTimeOffset.MinValue;
		}
		DateTimeOffset next = DateTimeOffset.MaxValue;
		if (prizes.RotationDays != 0)
		{
			long seconds = DateTimeOffset.Now.ToUnixTimeSeconds();
			int interval = Mathf.FloorToInt(prizes.RotationDays * 86400);
			next = DateTimeOffset.FromUnixTimeSeconds(seconds - seconds % interval + interval);
		}
		if (prizes.DurationDays > 0)
		{
			DateTimeOffset durationExpiration = prizes.StartDate.AddDays(prizes.DurationDays);
			if (durationExpiration < next)
			{
				next = durationExpiration;
			}
		}
		return next;
	}

	public static Random.State SetRandomState(Prizes prizes)
	{
		Random.State state = Random.state;
		Random.InitState((int)Expiration(prizes).ToUnixTimeSeconds());
		return state;
	}
}
