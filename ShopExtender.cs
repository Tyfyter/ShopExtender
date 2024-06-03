using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using EntryList = System.Collections.Generic.List<Terraria.ModLoader.NPCShop.Entry>;
using EntryEnumerator = System.Collections.Generic.List<Terraria.ModLoader.NPCShop.Entry>.Enumerator;
using System.Reflection;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Terraria.GameContent;
using Terraria.ModLoader.Default;

namespace ShopExtender {
	public class ShopExtender : Mod {
		public static int page = 0;
		public static int pageCount = 0;
		public static bool countingPages = false;
		public static bool scrolling = false;
		public static ShopExtender Instance => ContentInstance<ShopExtender>.Instance;
		public override void Load() {
			MethodInfo method = typeof(NPCShop).GetMethod(
				nameof(NPCShop.FillShop),
				[typeof(Item[]), typeof(NPC), typeof(bool).MakeByRefType()]
			);
			MonoModHooks.Modify(
				method,
				NPCShop_FillShop
			);
			On_Chest.SetupShop_string_NPC += On_Chest_SetupShop_string_NPC;
			method = typeof(PylonShopNPC).GetMethod(
				"AddPylonsToBartenderShop",
				BindingFlags.NonPublic | BindingFlags.Instance,
				[typeof(NPC), typeof(Item[])]
			);
			//_pylonEntries = (EntryList)typeof(PylonShopNPC).GetField(nameof(_pylonEntries), BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
			MonoModHooks.Modify(
				method,
				IL_AddPylonsToBartenderShop
			);
		}
		static void IL_AddPylonsToBartenderShop(ILContext il) {
			ILCursor c = new(il);
			c.GotoNext(MoveType.Before, i => i.MatchLdsfld<PylonShopNPC>("_pylonEntries"));
			ILLabel breakout = c.MarkLabel();
			ILLabel loop = c.DefineLabel();
			ILLabel normal = c.DefineLabel();

			c.Index = 0;
			c.EmitLdsfld(typeof(ShopExtender).GetField(nameof(page)));
			c.EmitLdcI4(0);
			c.EmitBeq(normal);

			c.EmitLdcI4(0);//slot = 0;
			c.EmitStloc0();
			c.MarkLabel(loop);//loop:

			c.EmitLdarg2();//if (items[slot] is null) goto breakout;
			c.EmitLdloc0();
			c.EmitLdelemRef();
			c.EmitBrfalse(breakout);

			c.EmitLdarg2();//if (items[slot].IsAir) goto breakout;
			c.EmitLdloc0();
			c.EmitLdelemRef();
			c.EmitCall(typeof(Item).GetProperty(nameof(Item.IsAir)).GetGetMethod());
			c.EmitBrtrue(breakout);

			c.EmitLdloc0();//slot++;
			c.EmitLdcI4(1);
			c.EmitAdd();
			c.EmitStloc0();

			c.EmitLdloc0();//if (slot <= 30) goto loop;
			c.EmitLdcI4(30);
			c.EmitBle(loop);

			c.EmitRet();
			c.MarkLabel(normal);

			ILLabel breakWhile = c.DefineLabel();
			c.GotoNext(MoveType.After,
				 i => i.MatchLdarg2(),
				 i => i.MatchLdloc0(),
				 i => i.MatchLdelemRef(),
				 i => i.MatchCallOrCallvirt<Item>("get_" + nameof(Item.IsAir)),
				 i => i.MatchBrfalse(out _)
			);
			c.MarkLabel(breakWhile);
			c.Index -= 2;
			c.EmitBrfalse(breakWhile);
			c.EmitLdarg2();
			c.EmitLdloc0();
			c.EmitLdelemRef();
		}
		private static EntryList _pylonEntries;
		private static void AddPylonsToBartenderShop(Action<PylonShopNPC, NPC, Item[]> orig, PylonShopNPC pylonShopNPC, NPC npc, Item[] items) {
			int slot = 0;
			for (; slot <= 30; slot++) {
				if (items[slot]?.IsAir ?? true) break;
			}
			foreach (NPCShop.Entry entry in _pylonEntries) {
				if (entry.Disabled || !entry.ConditionsMet()) {
					continue;
				}
				items[slot] = entry.Item.Clone();
				entry.OnShopOpen(items[slot], npc);
				do {
					if (++slot >= items.Length) {
						return;
					}
				}
				while (!items[slot].IsAir);
			}
		}
		string shopName;
		public void RefreshShop() {
			scrolling = true;
			try {
				Main.instance.shop[Main.npcShop].SetupShop(shopName, Main.LocalPlayer.TalkNPC);
			} finally {
				scrolling = false;
			}
		}
		private void On_Chest_SetupShop_string_NPC(On_Chest.orig_SetupShop_string_NPC orig, Chest self, string shopName, NPC npc) {
			if (!scrolling) {
				page = 0;
				pageCount = 0;
				Instance.shopName = shopName;
			}
			countingPages = false;
			orig(self, shopName, npc);
		}

		public static void NPCShop_FillShop(ILContext il) {
			FieldInfo _scrolling = typeof(ShopExtender).GetField(nameof(scrolling));
			FieldInfo _countingPages = typeof(ShopExtender).GetField(nameof(countingPages));
			ILCursor c = new(il);
			c.GotoNext(MoveType.After,
				i => i.MatchLdfld<NPCShop>("_entries"),
				i => i.MatchCallOrCallvirt(out var getenum) && getenum.Name == "GetEnumerator"
			);
			c.Emit(OpCodes.Ldloc_0);
			c.EmitDelegate<Func<EntryEnumerator, int, EntryEnumerator>>((e, limit) => {
				int i;
				for (i = 0; i < limit * page; i++) {
					if (!e.MoveNext()) return e;
					if (e.Current is null || !e.Current.ConditionsMet()) i--;
				}
				return e;
			});
			ILLabel loopLabel = default;
			c.GotoNext(MoveType.After,
				i => i.MatchBr(out loopLabel)
			);
			c.GotoNext(MoveType.Before,
				i => i.MatchLdarg(3),
				i => i.MatchLdcI4(1),
				i => i.MatchStindI1()
			);
			c.RemoveRange(3);

			ILLabel breakLabel = c.DefineLabel();
			c.Emit(OpCodes.Ldsfld, _scrolling);
			c.Emit(OpCodes.Brfalse, breakLabel);
			c.Index++;
			c.MarkLabel(breakLabel);
			c.EmitDelegate(() => {
				countingPages = true;
				pageCount++;
			});
			c.Emit(OpCodes.Ldc_I4_0);
			c.Emit(OpCodes.Stloc_1);
			c.GotoNext(MoveType.AfterLabel);
			c.Emit(OpCodes.Ldsfld, _countingPages);
			c.Emit(OpCodes.Brtrue, loopLabel);
			//MonoModHooks.DumpIL(ContentInstance<ShopExtender>.Instance, il);
		}
	}
	public class ShopScrollInterfaceLayer : GameInterfaceLayer {
		public ShopScrollInterfaceLayer() : base("ShopExtender: Scroll Buttons", InterfaceScaleType.UI) {}
		protected override bool DrawSelf() {
			if (Main.npcShop <= 0) return true;
			bool clicked = false;
			Rectangle scrollUpButton = new Rectangle(496, Main.instance.invBottom + 128, 16, 16);
			if (ShopExtender.page > 0) {
				Color color = Color.White * 0.8f;
				if (scrollUpButton.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					if (Main.mouseLeft && Main.mouseLeftRelease) {
						ShopExtender.page--;
						clicked = true;
					}
					color = Color.White;
				}
				Main.spriteBatch.Draw(TextureAssets.CraftUpButton.Value, scrollUpButton, color);
			}
			Rectangle scrollDownButton = new Rectangle(496, Main.instance.invBottom + 128 + 20, 16, 16);
			if (ShopExtender.page <= ShopExtender.pageCount) {
				Color color = Color.White * 0.8f;
				if (scrollDownButton.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					if (Main.mouseLeft && Main.mouseLeftRelease) {
						ShopExtender.page++;
						clicked = true;
					}
					color = Color.White;
				}
				Main.spriteBatch.Draw(TextureAssets.CraftDownButton.Value, scrollDownButton, color);
			}
			if (clicked) {
				ShopExtender.Instance.RefreshShop();
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
			}
			return true;
		}
	}
	public class ShopExtenderSystem : ModSystem {
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
			if (inventoryIndex != -1) {
				layers.Insert(inventoryIndex + 1, new ShopScrollInterfaceLayer());
			}
		}
	}
	/*public class test : GlobalNPC {
		public override void ModifyShop(NPCShop shop) {
			for (int i = 0; i < 34; i++) {
				shop.Add(i + 1);
			}
			for (int i = 0; i < 39; i++) {
				shop.Add(i + 35, new Condition("", () => Main.LocalPlayer.HeldItem.type != ItemID.AbigailsFlower));
			}
		}
	}*/
}