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

namespace ShopExtender {
	public class ShopExtender : Mod {
		public static int page = 0;
		public static int pageCount = 0;
		public static bool countingPages = false;
		public static bool scrolling = false;
		public static ShopExtender Instance => ContentInstance<ShopExtender>.Instance;
		public override void Load() {
			MethodInfo[] methods = typeof(NPCShop).GetMethods();
			MethodInfo method = typeof(NPCShop).GetMethod(
				nameof(NPCShop.FillShop),
				new Type[] { typeof(Item[]), typeof(NPC), typeof(bool).MakeByRefType() }
			);
			MonoModHooks.Modify(
				method,
				NPCShop_FillShop
			);
			/*MonoModHooks.Add(
				method,
				(hook_Detour_NPCShop_FillShop)Detour_NPCShop_FillShop
			);*/
			On_Chest.SetupShop_string_NPC += On_Chest_SetupShop_string_NPC;
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
					if (!e.Current.ConditionsMet()) i--;
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
			c.EmitDelegate<Action>(() => {
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
			if (ShopExtender.page < ShopExtender.pageCount) {
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
			for (int i = 0; i < 36; i++) {
				shop.Add(i + 1);
			}
			for (int i = 0; i < 39; i++) {
				shop.Add(i + 36, new Condition("", () => Main.LocalPlayer.HeldItem.type != ItemID.AbigailsFlower));
			}
		}
	}*/
}