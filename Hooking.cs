﻿using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Recipe = IL.Terraria.Recipe;

namespace ContainerLibrary
{
	public static class Hooking
	{
		public static Func<int> AlchemyConsumeChance = () => 3;
		public static Func<bool> AlchemyApplyChance = () => Main.LocalPlayer.alchemyTable;
		public static Action<Player> ModifyAdjTiles = player => { };

		internal static void Load()
		{
			Recipe.FindRecipes += Recipe_FindRecipes;
			Recipe.Create += Recipe_Create;
			IL.Terraria.Player.AdjTiles += Player_AdjTiles;
		}

		private static void Player_AdjTiles(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);

			if (cursor.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Player).GetField("adjWater"))))
			{
				cursor.Emit(OpCodes.Ldarg, 0);
				cursor.EmitDelegate<Action<Player>>(player => ModifyAdjTiles(player));
			}
		}

		private static void Recipe_Create(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			ILLabel label = cursor.DefineLabel();
			ILLabel labelAlchemy = cursor.DefineLabel();
			ILLabel labelCheckAmount = cursor.DefineLabel();

			if (cursor.TryGotoNext(i => i.MatchLdloc(3), i => i.MatchBrfalse(out _)))
			{
				cursor.Index++;
				cursor.Remove();
				cursor.Emit(OpCodes.Brfalse, labelAlchemy);
			}

			if (cursor.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Terraria.Recipe).GetField("alchemy")), i => i.MatchBrfalse(out _)))
			{
				cursor.MarkLabel(labelAlchemy);

				cursor.Emit(OpCodes.Ldarg, 0);
				cursor.Emit(OpCodes.Ldfld, typeof(Terraria.Recipe).GetField("alchemy"));
				cursor.Emit(OpCodes.Ldloc, 2);

				cursor.EmitDelegate<Func<bool, int, int>>((alchemy, amount) =>
				{
					if (alchemy && AlchemyApplyChance())
					{
						int reduction = 0;
						for (int j = 0; j < amount; j++)
						{
							if (Main.rand.Next(AlchemyConsumeChance()) == 0) reduction++;
						}

						amount -= reduction;
					}

					return amount;
				});

				cursor.Emit(OpCodes.Stloc, 2);
				cursor.Emit(OpCodes.Br, labelCheckAmount);
			}

			if (cursor.TryGotoNext(i => i.MatchLdloc(2), i => i.MatchLdcI4(0), i => i.MatchBle(out _))) cursor.MarkLabel(labelCheckAmount);

			if (cursor.TryGotoNext(i => i.MatchLdfld(typeof(Player).GetField("chest")), i => i.MatchLdcI4(-1), i => i.MatchBeq(out _)))
			{
				cursor.Index += 2;
				cursor.Remove();
				cursor.Emit(OpCodes.Beq, label);
			}

			if (cursor.TryGotoNext(i => i.MatchLdloc(2), i => i.MatchLdcI4(0), i => i.MatchBle(out _)))
			{
				cursor.Index += 2;
				cursor.Remove();
				cursor.Emit(OpCodes.Ble, label);
			}

			if (cursor.TryGotoNext(i => i.MatchLdloc(0), i => i.MatchLdcI4(1), i => i.MatchAdd()))
			{
				cursor.MarkLabel(label);

				cursor.Emit(OpCodes.Ldarg, 0);
				cursor.Emit(OpCodes.Ldloc, 1);
				cursor.Emit(OpCodes.Ldloc, 2);

				cursor.EmitDelegate<Func<Terraria.Recipe, Item, int, int>>((self, ingredient, amount) =>
				{
					foreach (ICraftingStorage storage in Main.LocalPlayer.inventory.Where(item => item.modItem is ICraftingStorage).Select(item => (ICraftingStorage)item.modItem))
					{
						for (int index = 0; index < storage.CraftingHandler.Items.Count; index++)
						{
							if (amount <= 0) return amount;
							Item item = storage.CraftingHandler.Items[index];

							if (item.IsTheSameAs(ingredient) || self.useWood(item.type, ingredient.type) || self.useSand(item.type, ingredient.type) || self.useIronBar(item.type, ingredient.type) || self.usePressurePlate(item.type, ingredient.type) || self.useFragment(item.type, ingredient.type) || self.AcceptedByItemGroups(item.type, ingredient.type))
							{
								int count = Math.Min(amount, item.stack);
								amount -= count;
								storage.CraftingHandler.ExtractItem(index, count);
							}
						}
					}

					return amount;
				});

				cursor.Emit(OpCodes.Stloc, 2);
			}
		}

		private static void Recipe_FindRecipes(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			ILLabel label = cursor.DefineLabel();

			if (cursor.TryGotoNext(i => i.MatchLdfld(typeof(Player).GetField("chest")), i => i.MatchLdcI4(-1), i => i.MatchBeq(out _)))
			{
				cursor.Index += 2;
				cursor.Remove();
				cursor.Emit(OpCodes.Beq, label);
			}

			if (cursor.TryGotoNext(i => i.MatchLdcI4(0), i => i.MatchStloc(9)))
			{
				cursor.MarkLabel(label);

				cursor.Emit(OpCodes.Ldloc, 6);

				cursor.EmitDelegate<Func<Dictionary<int, int>, Dictionary<int, int>>>(availableItems =>
				{
					foreach (ICraftingStorage storage in Main.LocalPlayer.inventory.Where(item => item.modItem is ICraftingStorage).Select(item => (ICraftingStorage)item.modItem))
					{
						foreach (Item item in storage.CraftingHandler.Items)
						{
							if (item.stack > 0)
							{
								if (availableItems.ContainsKey(item.netID)) availableItems[item.netID] += item.stack;
								else availableItems[item.netID] = item.stack;
							}
						}
					}

					return availableItems;
				});

				cursor.Emit(OpCodes.Stloc, 6);
			}
		}
	}
}