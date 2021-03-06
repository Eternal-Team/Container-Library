﻿using BaseLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader.IO;

namespace ContainerLibrary
{
	public interface IItemHandler
	{
		ItemHandler Handler { get; }
	}

	public interface ICraftingStorage
	{
		ItemHandler CraftingHandler { get; }
	}

	public interface IItemHandlerUI
	{
		ItemHandler Handler { get; }

		string GetTexture(Item item);
	}

	public enum SlotMode
	{
		Both,
		Input,
		Output,
		Locked,
	}

	// todo: fully implement SlotModes
	// todo: prevent user from inserting items into output slots
	
	public class ItemHandler
	{
		public Item[] Items { get; private set; }
		public SlotMode[] Modes { get; private set; }

		public IEnumerable<Item> OutputSlots => Items.Where((item, i) => Modes[i] == SlotMode.Output).ToArray();
		
		public int Slots => Items.Length;

		public Action<int, bool> OnContentsChanged = (slot, user) => { };
		public event Func<int, int> GetSlotLimit = slot => -1;
		public Func<int, Item, bool> IsItemValid = (slot, item) => true;

		public ItemHandler(int size = 1)
		{
			Items = new Item[size];
			for (int i = 0; i < size; i++) Items[i] = new Item();

			Modes = new SlotMode[size];
		}

		public ItemHandler(Item[] items)
		{
			Items = items;
			Modes = new SlotMode[items.Length];
		}

		public ItemHandler Clone() => new ItemHandler(Items.Select(x => x.Clone()).ToArray())
		{
			IsItemValid = (Func<int, Item, bool>)IsItemValid.Clone(),
			GetSlotLimit = (Func<int, int>)GetSlotLimit.Clone(),
			OnContentsChanged = (Action<int, bool>)OnContentsChanged.Clone()
		};

		public void SetSize(int size)
		{
			Items = new Item[size];
			for (int i = 0; i < size; i++) Items[i] = new Item();
		}

		public void SetItemInSlot(int slot, Item stack, bool user = false)
		{
			ValidateSlotIndex(slot);
			Items[slot] = stack;
			OnContentsChanged?.Invoke(slot, user);
		}

		public Item GetItemInSlot(int slot)
		{
			ValidateSlotIndex(slot);
			return Items[slot];
		}

		public ref Item GetItemInSlotByRef(int slot)
		{
			ValidateSlotIndex(slot);
			return ref Items[slot];
		}

		public static bool CanItemsStack(Item a, Item b) => a.type == b.type;

		public static Item CopyItemWithSize(Item itemStack, int size)
		{
			if (size == 0) return new Item();
			Item copy = itemStack.Clone();
			copy.stack = size;
			return copy;
		}

		public Item InsertItem(int slot, Item stack, bool simulate = false, bool user = false)
		{
			if (stack.IsAir) return stack;

			ValidateSlotIndex(slot);

			if (!IsItemValid(slot, stack)) return stack;

			Item existing = Items[slot];

			int limit = GetItemLimit(slot) ?? stack.maxStack;

			if (!existing.IsAir)
			{
				if (!CanItemsStack(stack, existing)) return stack;

				limit -= existing.stack;
			}

			if (limit <= 0) return stack;

			bool reachedLimit = stack.stack > limit;

			if (!simulate)
			{
				if (existing.IsAir) Items[slot] = reachedLimit ? CopyItemWithSize(stack, limit) : stack.Clone();
				else this.Grow(slot, reachedLimit ? limit : stack.stack);

				OnContentsChanged?.Invoke(slot, user);
			}

			return reachedLimit ? CopyItemWithSize(stack, stack.stack - limit) : new Item();
		}

		public Item ExtractItem(int slot, int amount, bool simulate = false, bool user = false)
		{
			if (amount == 0) return new Item();

			ValidateSlotIndex(slot);

			Item existing = Items[slot];

			if (existing.IsAir) return new Item();

			int toExtract = Math.Min(amount, existing.maxStack);

			if (existing.stack <= toExtract)
			{
				if (!simulate)
				{
					Items[slot] = new Item();
					OnContentsChanged?.Invoke(slot, user);
				}

				return existing;
			}

			if (!simulate)
			{
				Items[slot] = CopyItemWithSize(existing, existing.stack - toExtract);
				OnContentsChanged?.Invoke(slot, user);
			}

			return CopyItemWithSize(existing, toExtract);
		}

		public void InsertItem(ref Item stack, int minSlot = -1, int maxSlot = -1)
		{
			if (minSlot < 0) minSlot = 0;
			if (maxSlot < 1 || maxSlot > Slots) maxSlot = Slots;

			for (int i = minSlot; i < maxSlot; i++)
			{
				if (Modes[i] != SlotMode.Both && Modes[i] != SlotMode.Output) continue;
				
				Item other = Items[i];
				if (other.type == stack.type && other.stack < other.maxStack)
				{
					stack = InsertItem(i, stack);
					if (stack.IsAir || !stack.active) return;
				}
			}

			for (int i = minSlot; i < maxSlot; i++)
			{
				if (Modes[i] != SlotMode.Both && Modes[i] != SlotMode.Output) continue;

				stack = InsertItem(i, stack);
				if (stack.IsAir) return;
			}
		}

		public int? GetItemLimit(int slot)
		{
			Item item = Items[slot];
			int limit = GetSlotLimit(slot);

			if (limit >= 0) return limit;

			return !item.IsAir ? item.maxStack : default(int?);
		}

		public TagCompound Save()
		{
			List<TagCompound> items = Items.Select((item, slot) => new TagCompound
			{
				["Slot"] = slot,
				["Item"] = ItemIO.Save(item),
				["Mode"] = (byte)Modes[slot]
			}).ToList();
			return new TagCompound
			{
				["Items"] = items,
				["Count"] = Slots
			};
		}

		public void Load(TagCompound tag)
		{
			SetSize(tag.ContainsKey("Count") ? tag.GetInt("Count") : Slots);
			foreach (TagCompound compound in tag.GetList<TagCompound>("Items"))
			{
				Item item = ItemIO.Load(compound.GetCompound("Item"));
				int slot = compound.GetInt("Slot");
				SlotMode mode = (SlotMode)compound.GetByte("Mode");

				if (slot >= 0 && slot < Slots)
				{
					Items[slot] = item;
					Modes[slot] = mode;
				}
			}
		}

		public void Write(BinaryWriter writer)
		{
			writer.Write(Slots);
			for (int i = 0; i < Slots; i++) writer.Send(Items[i], true, true);
		}

		public void Read(BinaryReader reader)
		{
			int size = reader.ReadInt32();
			SetSize(size);

			for (int i = 0; i < Slots; i++) Items[i] = reader.Receive(true, true);
		}

		protected void ValidateSlotIndex(int slot)
		{
			if (slot < 0 || slot >= Slots) throw new Exception($"Slot {slot} not in valid range - [0, {Slots - 1})");
		}
	}
}