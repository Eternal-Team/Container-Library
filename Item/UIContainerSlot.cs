﻿using BaseLibrary;
using BaseLibrary.Input;
using BaseLibrary.Input.Mouse;
using BaseLibrary.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ContainerLibrary
{
	public class UIContainerSlot : BaseElement
	{
		public ItemHandler Handler => FuncHandler();

		public Item Item
		{
			get => Handler.GetItemInSlot(slot);
			set => Handler.SetItemInSlot(slot, value);
		}

		private readonly Func<ItemHandler> FuncHandler;
		public Texture2D backgroundTexture = Main.inventoryBackTexture;

		public Item PreviewItem;

		public bool ShortStackSize = false;

		public int slot;

		public UIContainerSlot(Func<ItemHandler> itemHandler, int slot = 0)
		{
			Width.Pixels = SlotSize;
			Height.Pixels = SlotSize;

			this.slot = slot;
			FuncHandler = itemHandler;
		}

		protected override void MouseClick(MouseButtonEventArgs args)
		{
			if (args.Button != MouseButton.Left) return;

			if (Handler.IsItemValid(slot, Main.mouseItem) || Main.mouseItem.IsAir)
			{
				args.Handled = true;

				Item.newAndShiny = false;
				Player player = Main.LocalPlayer;

				if (ItemSlot.ShiftInUse)
				{
					ItemUtility.Loot(Handler, slot, Main.LocalPlayer);
					return;
				}

				if (Main.mouseItem.IsAir) Main.mouseItem = Handler.ExtractItem(slot, Item.maxStack, user: true);
				else
				{
					if (Item.IsTheSameAs(Main.mouseItem)) Main.mouseItem = Handler.InsertItem(slot, Main.mouseItem, user: true);
					else
					{
						if (Item.stack <= Item.maxStack)
						{
							Item temp = Item;
							Utils.Swap(ref temp, ref Main.mouseItem);
							Item = temp;
						}
					}
				}

				if (Item.stack > 0) AchievementsHelper.NotifyItemPickup(player, Item);

				if (Main.mouseItem.type > 0 || Item.type > 0)
				{
					Recipe.FindRecipes();
					Main.PlaySound(SoundID.Grab);
				}
			}
		}

		public override int CompareTo(BaseElement other) => slot.CompareTo(((UIContainerSlot)other).slot);

		private void DrawItem(SpriteBatch spriteBatch, Item item, float scale)
		{
			Texture2D itemTexture = Main.itemTexture[item.type];
			Rectangle rect = Main.itemAnimations[item.type] != null ? Main.itemAnimations[item.type].GetFrame(itemTexture) : itemTexture.Frame();
			Color newColor = Color.White;
			float pulseScale = 1f;
			ItemSlot.GetItemLight(ref newColor, ref pulseScale, item);
			int height = rect.Height;
			int width = rect.Width;
			float drawScale = 1f;

			float availableWidth = InnerDimensions.Width;
			if (width > availableWidth || height > availableWidth)
			{
				if (width > height) drawScale = availableWidth / width;
				else drawScale = availableWidth / height;
			}

			drawScale *= scale;
			Vector2 position = Dimensions.Position() + Dimensions.Size() * 0.5f;
			Vector2 origin = rect.Size() * 0.5f;

			if (ItemLoader.PreDrawInInventory(item, spriteBatch, position - rect.Size() * 0.5f * drawScale, rect, item.GetAlpha(newColor), item.GetColor(Color.White), origin, drawScale * pulseScale))
			{
				spriteBatch.Draw(itemTexture, position, rect, item.GetAlpha(newColor), 0f, origin, drawScale * pulseScale, SpriteEffects.None, 0f);
				if (item.color != Color.Transparent) spriteBatch.Draw(itemTexture, position, rect, item.GetColor(Color.White), 0f, origin, drawScale * pulseScale, SpriteEffects.None, 0f);
			}

			ItemLoader.PostDrawInInventory(item, spriteBatch, position - rect.Size() * 0.5f * drawScale, rect, item.GetAlpha(newColor), item.GetColor(Color.White), origin, drawScale * pulseScale);
			if (ItemID.Sets.TrapSigned[item.type]) spriteBatch.Draw(Main.wireTexture, position + new Vector2(40f, 40f) * scale, new Rectangle(4, 58, 8, 8), Color.White, 0f, new Vector2(4f), 1f, SpriteEffects.None, 0f);
			if (item.stack > 1)
			{
				string text = !ShortStackSize || item.stack < 1000 ? item.stack.ToString() : item.stack.ToSI("N1");
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, Main.fontItemStack, text, InnerDimensions.Position() + new Vector2(8, InnerDimensions.Height - Main.fontMouseText.MeasureString(text).Y * scale), Color.White, 0f, Vector2.Zero, new Vector2(0.85f), -1f, scale);
			}

			if (IsMouseHovering)
			{
				Main.LocalPlayer.showItemIcon = false;
				Main.ItemIconCacheUpdate(0);
				Main.HoverItem = item.Clone();
				Main.hoverItemName = Main.HoverItem.Name;

				if (ItemSlot.ShiftInUse) BaseLibrary.Hooking.SetCursor("Terraria/UI/Cursor_7");
			}
		}

		protected override void Draw(SpriteBatch spriteBatch)
		{
			spriteBatch.DrawSlot(Dimensions, Color.White, !Item.IsAir && Item.favorited ? Main.inventoryBack10Texture : backgroundTexture);

			float scale = Math.Min(InnerDimensions.Width / (float)backgroundTexture.Width, InnerDimensions.Height / (float)backgroundTexture.Height);

			if (!Item.IsAir) DrawItem(spriteBatch, Item, scale);
			else if (PreviewItem != null && !PreviewItem.IsAir) spriteBatch.DrawWithEffect(BaseLibrary.BaseLibrary.DesaturateShader, () => DrawItem(spriteBatch, PreviewItem, scale));
		}

		protected override void MouseHeld(MouseButtonEventArgs args)
		{
			if (args.Button != MouseButton.Right) return;

			if (Handler.IsItemValid(slot, Main.mouseItem) || Main.mouseItem.IsAir)
			{
				args.Handled = true;

				Player player = Main.LocalPlayer;
				Item.newAndShiny = false;

				if (Main.stackSplit <= 1)
				{
					if ((Main.mouseItem.IsTheSameAs(Item) || Main.mouseItem.type == 0) && (Main.mouseItem.stack < Main.mouseItem.maxStack || Main.mouseItem.type == 0))
					{
						if (Main.mouseItem.type == 0)
						{
							Main.mouseItem = Item.Clone();
							Main.mouseItem.stack = 0;
							if (Item.favorited && Item.maxStack == 1) Main.mouseItem.favorited = true;
							Main.mouseItem.favorited = false;
						}

						Main.mouseItem.stack++;
						Handler.Shrink(slot, 1, true);

						Recipe.FindRecipes();

						Main.soundInstanceMenuTick.Stop();
						Main.soundInstanceMenuTick = Main.soundMenuTick.CreateInstance();
						Main.PlaySound(12);

						Main.stackSplit = Main.stackSplit == 0 ? 15 : Main.stackDelay;
					}
				}
			}
		}

		protected override void MouseScroll(MouseScrollEventArgs args)
		{
			if (!Main.keyState.IsKeyDown(Keys.LeftAlt)) return;

			if (args.OffsetY > 0)
			{
				if (Main.mouseItem.type == Item.type && Main.mouseItem.stack < Main.mouseItem.maxStack)
				{
					Main.mouseItem.stack++;
					Handler.Shrink(slot, 1, true);
				}
				else if (Main.mouseItem.IsAir)
				{
					Main.mouseItem = Item.Clone();
					Main.mouseItem.stack = 1;
					Handler.Shrink(slot, 1, true);
				}
			}
			else if (args.OffsetY < 0)
			{
				if (Item.type == Main.mouseItem.type && Handler.Grow(slot, 1))
				{
					if (--Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir();
				}
				else if (Item.IsAir)
				{
					Item = Main.mouseItem.Clone();
					Item.stack = 1;
					if (--Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir();
				}
			}
		}
	}
}