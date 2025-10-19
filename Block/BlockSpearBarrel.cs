using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable

namespace arrowbarrels
{

    public class CollectibleBehaviorSpearBarrel : CollectibleBehaviorHeldBag, IAttachedInteractions
    {
        ICoreAPI Api;
        public CollectibleBehaviorSpearBarrel(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.Api = api;
            base.OnLoaded(api);
        }

        public override bool IsEmpty(ItemStack bagstack)
        {
            bool empty = base.IsEmpty(bagstack);
            return empty;
        }

        public override int GetQuantitySlots(ItemStack bagstack)
        {
            if (collObj is not BlockSpearBarrel barrel) return 0;

            string type = bagstack.Attributes.GetString("type") ?? barrel.Props.DefaultType;
            int quantity = barrel.Props[type].QuantitySlots;
            return quantity;
        }

        public override void OnInteract(ItemSlot bagSlot, int slotIndex, Entity onEntity, EntityAgent byEntity, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled, Action onRequireSave)
        {
            var controls = byEntity.MountedOn?.Controls ?? byEntity.Controls;
            if (controls.Sprint) return;   // This is a weird test, as for players whose Sprint key is the CtrlKey, they then cannot use bulk operations?


            bool put = byEntity.Controls.ShiftKey;
            bool take = !put;
            bool bulk = byEntity.Controls.CtrlKey;

            var byPlayer = (byEntity as EntityPlayer).Player;

            var ws = getOrCreateContainerWorkspace(slotIndex, onEntity, onRequireSave);

            var face = BlockFacing.UP;
            var Pos = byEntity.Pos.XYZ;

            if (!ws.TryLoadInv(bagSlot, slotIndex, onEntity))
            {
                return;
            }

            ItemSlot ownSlot = ws.WrapperInv.FirstNonEmptySlot;
            var hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (take && ownSlot != null)
            {
                ItemStack stack = bulk ? ownSlot.TakeOutWhole() : ownSlot.TakeOut(1);
                var quantity = bulk ? stack.StackSize : 1;
                if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    Api.World.SpawnItemEntity(stack, Pos.Add(0.5f + face.Normalf.X, 0.5f + face.Normalf.Y, 0.5f + face.Normalf.Z));
                }
                else
                {
                    didMoveItems(stack, byPlayer);
                }
                Api.World.Logger.Audit("{0} Took {1}x{2} from Boat barrel at {3}.",
                    byPlayer.PlayerName,
                    quantity,
                    stack.Collectible.Code,
                    Pos
                );
                ws.BagInventory.SaveSlotIntoBag((ItemSlotBagContent)ownSlot);
            }

            if (put && !hotbarslot.Empty)
            {
                var quantity = bulk ? hotbarslot.StackSize : 1;
                if (ownSlot == null)
                {
                    if (hotbarslot.TryPutInto(Api.World, ws.WrapperInv[0], quantity) > 0)
                    {
                        didMoveItems(ws.WrapperInv[0].Itemstack, byPlayer);
                        Api.World.Logger.Audit("{0} Put {1}x{2} into Boat barrel at {3}.",
                            byPlayer.PlayerName,
                            quantity,
                            ws.WrapperInv[0].Itemstack.Collectible.Code,
                            Pos
                        );
                    }

                    ws.BagInventory.SaveSlotIntoBag((ItemSlotBagContent)ws.WrapperInv[0]);
                }
                else
                {
                    List<ItemSlot> skipSlots = new List<ItemSlot>();
                    while (hotbarslot.StackSize > 0 && skipSlots.Count < ws.WrapperInv.Count)
                    {
                        WeightedSlot wslot = ws.WrapperInv.GetBestSuitedSlot(hotbarslot, null, skipSlots);
                        if (wslot.slot == null) break;

                        if (hotbarslot.TryPutInto(Api.World, wslot.slot, quantity) > 0)
                        {
                            didMoveItems(wslot.slot.Itemstack, byPlayer);

                            ws.BagInventory.SaveSlotIntoBag((ItemSlotBagContent)wslot.slot);

                            Api.World.Logger.Audit("{0} Put {1}x{2} into Boat barrel at {3}.",
                                byPlayer.PlayerName,
                                quantity,
                                wslot.slot.Itemstack.Collectible.Code,
                                Pos
                            );
                            if (!bulk) break;
                        }

                        skipSlots.Add(wslot.slot);
                    }
                }

                hotbarslot.MarkDirty();
            }
        }

        protected void didMoveItems(ItemStack stack, IPlayer byPlayer)
        {
            (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            AssetLocation sound = stack?.Block?.Sounds?.Place;
            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
        }
    }


    public class BlockSpearBarrel : BlockContainer, ITexPositionSource, IWearableShapeSupplier
    {

        public Size2i AtlasSize { get { return tmpTextureSource.AtlasSize; } }

        string curType;
        ITexPositionSource tmpTextureSource;

        public BarrelProperties Props;

        public string Subtype => Props.VariantByGroup == null ? "" : Variant[Props.VariantByGroup];
        public string SubtypeInventory => Props?.VariantByGroupInventory == null ? "" : Variant[Props.VariantByGroupInventory];

        public int RequiresBehindSlots { get; set; } = 0;

        private Vec3f origin = new Vec3f(0.5f, 0.5f, 0.5f);

        #region IAttachableToEntity

        public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            string type = stack.Attributes.GetString("type");
            foreach (var key in shape.Textures.Keys)
            {
                this.Textures.TryGetValue(type + "-" + key, out var ctex);
                if (ctex != null)
                {
                    intoDict[texturePrefixCode + key] = ctex;
                }
                else
                {
                    Textures.TryGetValue(key, out var ctex2);
                    intoDict[texturePrefixCode + key] = ctex2;
                }

            }
        }

        public string GetCategoryCode(ItemStack stack)
        {
            return "barrel";
        }

        public Shape GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            string type = stack.Attributes.GetString("type", Props.DefaultType);
            string isFilled = stack.Attributes.GetString("fillState", "empty");

            CompositeShape cshape = Props[type].Shape;
            var rot = ShapeInventory == null ? null : new Vec3f(ShapeInventory.rotateX, ShapeInventory.rotateY, ShapeInventory.rotateZ);

            var contentStacks = GetNonEmptyContents(api.World, stack);
            var contentStack = contentStacks == null || contentStacks.Length == 0 ? null : contentStacks[0];

            if (isFilled == "stage")
            {
                cshape = cshape.Clone();
                cshape.Base.Path = cshape.Base.Path.Replace("empty", "stage");
            }

            AssetLocation shapeloc = cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape shape = Vintagestory.API.Common.Shape.TryGet(api, shapeloc);

            shape.SubclassForStepParenting(texturePrefixCode, 0);

            return shape;
        }

        public CompositeShape GetAttachedShape(ItemStack stack, string slotCode)
        {
            return null;
        }

        public string[] GetDisableElements(ItemStack stack)
        {
            return null;
        }

        public string[] GetKeepElements(ItemStack stack)
        {
            return null;
        }

        public string GetTexturePrefixCode(ItemStack stack)
        {
            var key = GetKey(stack);
            return key;
        }
        #endregion


        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                TextureAtlasPosition pos = tmpTextureSource[curType + "-" + textureCode];
                if (pos == null) pos = tmpTextureSource[textureCode];
                if (pos == null)
                {
                    pos = (api as ICoreClientAPI).BlockTextureAtlas.UnknownTexturePosition;
                }
                return pos;
            }
        }


        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        /*
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BESpearBarrel be = blockAccessor.GetBlockEntity(pos) as BESpearBarrel;
            if (be != null) return be.GetSelectionBoxes();


            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        Cuboidf[] closedCollBoxes = new Cuboidf[] { new Cuboidf(0.0625f, 0, 0.0625f, 0.9375f, 0.9375f, 0.9375f) };
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BESpearBarrel be = blockAccessor.GetBlockEntity(pos) as BESpearBarrel;
            if (be != null && be.FillState == "empty")
            {
                return closedCollBoxes;
            }

            return base.GetCollisionBoxes(blockAccessor, pos);
        }
        */

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            Props = Attributes.AsObject<BarrelProperties>(null, Code.Domain);

            PlacedPriorityInteract = true;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BESpearBarrel bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BESpearBarrel;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);


                    string type = bect.type;
                    string rotatatableInterval = Props[type].RotatatableInterval;

                    if (rotatatableInterval == "22.5degnot45deg")
                    {
                        float rounded90degRad = ((int)Math.Round(angleHor / GameMath.PIHALF)) * GameMath.PIHALF;
                        float deg45rad = GameMath.PIHALF / 4;


                        if (Math.Abs(angleHor - rounded90degRad) >= deg45rad)
                        {
                            bect.MeshAngle = rounded90degRad + 22.5f * GameMath.DEG2RAD * Math.Sign(angleHor - rounded90degRad);
                        }
                        else
                        {
                            bect.MeshAngle = rounded90degRad;
                        }
                    }
                    if (rotatatableInterval == "22.5deg")
                    {
                        float deg22dot5rad = GameMath.PIHALF / 4;
                        float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                        bect.MeshAngle = roundRad;
                    }
                }
            }

            return val;
        }

        public string GetKey(ItemStack itemstack)
        {
            string type = itemstack.Attributes.GetString("type", Props.DefaultType);
            string isFilled = itemstack.Attributes.GetString("fillState", "empty");
            string key = type + "-" + isFilled;

            return key;
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            string cacheKey = "barrelMeshRefs" + FirstCodePart() + SubtypeInventory;
            var meshrefs = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () => new Dictionary<string, MultiTextureMeshRef>());

            string key = GetKey(itemstack);

            if (!meshrefs.TryGetValue(key, out renderinfo.ModelRef))
            {
                string type = itemstack.Attributes.GetString("type", Props.DefaultType);
                string isFilled = itemstack.Attributes.GetString("fillState", "empty");

                CompositeShape cshape = Props[type].Shape;
                var rot = ShapeInventory == null ? null : new Vec3f(ShapeInventory.rotateX, ShapeInventory.rotateY, ShapeInventory.rotateZ);

                var contentStacks = GetNonEmptyContents(capi.World, itemstack);
                var contentStack = contentStacks == null || contentStacks.Length == 0 ? null : contentStacks[0];

                var mesh = GenMesh(capi, contentStack, type, isFilled, cshape, rot);
                meshrefs[key] = renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(mesh);
            }
        }




        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            string key = "barrelMeshRefs" + FirstCodePart() + SubtypeInventory;
            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, key);

            if (meshrefs != null)
            {
                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(key);
            }
        }



        public Shape GetShape(ICoreClientAPI capi, string type, CompositeShape cshape)
        {
            if (cshape?.Base == null) return null;
            var tesselator = capi.Tesselator;

            tmpTextureSource = tesselator.GetTextureSource(this, 0, true);

            AssetLocation shapeloc = cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape shape = Vintagestory.API.Common.Shape.TryGet(capi, shapeloc);
            curType = type;
            return shape;
        }


        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, string type, string fillStage, CompositeShape cshape, Vec3f rotation = null)
        {
            if (fillStage != "empty")
            {
                cshape = cshape.Clone();

                if (cshape.Base.Path.Contains("empty"))
                {
                    cshape.Base.Path = cshape.Base.Path.Replace("empty", fillStage);
                }
                else
                {
                    cshape.Base.Path = cshape.Base.Path.Replace("stage", fillStage);
                }
            }

            Shape shape = GetShape(capi, type, cshape);
            var tesselator = capi.Tesselator;
            if (shape == null) return new MeshData();

            curType = type;
            tesselator.TesselateShape("spearbarrel", shape, out MeshData mesh, this, rotation == null ? new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ) : rotation);

            if (contentStack != null && fillStage != "empty")
            {
                var contentMesh = genContentMesh(capi, contentStack, rotation);
                if (contentMesh != null) mesh.AddMeshData(contentMesh);
            }

            return mesh;
        }


        protected MeshData genContentMesh(ICoreClientAPI capi, ItemStack contentStack, Vec3f rotation = null)
        {

            var contentSource = BlockBarrel.getContentTexture(capi, contentStack, out float fillHeight);

            if (contentSource != null)
            {
                Shape shape = Vintagestory.API.Common.Shape.TryGet(api, "shapes/block/wood/barrel/spears/contents.json");
                capi.Tesselator.TesselateShape("barrelcontents", shape, out MeshData contentMesh, contentSource, rotation);
                contentMesh.Translate(0, fillHeight * 1.1f, 0);
                return contentMesh;
            }

            return null;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BESpearBarrel be = world.BlockAccessor.GetBlockEntity(pos) as BESpearBarrel;
            if (be != null)
            {

                decalModelData.Rotate(origin, 0, be.MeshAngle, 0);
                decalModelData.Scale(origin, 15f / 16, 1f, 15f / 16);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }




        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = new ItemStack(this);

            BESpearBarrel be = world.BlockAccessor.GetBlockEntity(pos) as BESpearBarrel;
            if (be != null)
            {
                stack.Attributes.SetString("type", be.type);
                stack.Attributes.SetString("fillState", be.preferredFillState);
            }
            else
            {
                stack.Attributes.SetString("type", Props.DefaultType);
            }

            return stack;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BESpearBarrel be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BESpearBarrel;
            if (be != null) return be.OnBlockInteractStart(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type", Props.DefaultType);
            string isFilled = itemStack.Attributes.GetString("fillState", "empty");
            if (isFilled.Length == 0) isFilled = "empty";

            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + "block-" + type + "-" + Code?.Path, Lang.Get("spearbarrelfillstate-" + isFilled, "empty"));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string type = inSlot.Itemstack.Attributes.GetString("type", Props.DefaultType);

            if (type != null)
            {
                int qslots = Props[type].QuantitySlots;
                dsc.AppendLine("\n" + Lang.Get("Storage Slots: {0}", qslots));
            }
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        /*
        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockEntityGenericTypedContainer be = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                if (!Textures.TryGetValue(be.type + "-lid", out CompositeTexture tex))
                {
                    Textures.TryGetValue(be.type + "-top", out tex);
                }
                return capi.BlockTextureAtlas.GetRandomColor(tex?.Baked == null ? 0 : tex.Baked.TextureSubId, rndIndex);
            }

            return base.GetRandomColor(capi, pos, facing, rndIndex);


        }
        */

        public string GetType(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityGenericTypedContainer be = blockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                return be.type;
            }

            return Props.DefaultType;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-spear-add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "shift"
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-spear-remove",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = null
                }
            });
        }

    }

}