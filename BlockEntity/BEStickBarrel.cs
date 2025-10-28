#nullable disable

namespace arrowbarrels
{
    public class BEStickBarrel : BlockEntityContainer, IRotatable
    {
        InventoryGeneric inventory;
        BlockStickBarrel ownBlock;

        public string type = "wood-aged";
        public string preferredFillState = "empty";
        public int quantitySlots = 8;
        public bool retrieveOnly = false;
        float rotAngleY;

        MeshData ownMesh;

        Cuboidf selBoxBarrel;


        public virtual float MeshAngle
        {
            get { return rotAngleY; }
            set
            {
                rotAngleY = value;
            }
        }


        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "barrel";


        public string FillState
        {
            get
            {
                if (inventory.Empty) return "empty";

                foreach (var slot in inventory)
                {
                    if (!slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("stick"))
                    {
                        return "filled";
                    }
                }

                return "empty";
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            ownBlock = (BlockStickBarrel)Block;

            bool isNewlyplaced = inventory == null;
            if (isNewlyplaced)
            {
                InitInventory(Block, api);
            }

            base.Initialize(api);

            if (api.Side == EnumAppSide.Client && !isNewlyplaced)
            {
                loadOrCreateMesh();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack?.Attributes != null)
            {
                string nowType = byItemStack.Attributes.GetString("type", ownBlock.Props.DefaultType);
                string nowFillState = byItemStack.Attributes.GetString("fillState", "empty");

                if (nowType != type || nowFillState != preferredFillState)
                {
                    this.type = nowType;
                    this.preferredFillState = nowFillState;
                    InitInventory(Block, Api);
                    Inventory.LateInitialize(InventoryClassName + "-" + Pos, Api);
                    Inventory.ResolveBlocksOrItems();
                    container.LateInit();
                    MarkDirty();
                }
            }

            base.OnBlockPlaced();
        }

        public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel)
        {
            bool put = byPlayer.Entity.Controls.ShiftKey;
            bool take = !put;
            bool bulk = byPlayer.Entity.Controls.CtrlKey;

            ItemSlot hotbarslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (hotbarslot == null) throw new Exception("Interact called when byPlayer has null ActiveHotbarSlot");


            if (take)
            {
                int i = 0;
                for (; i < inventory.Count; ++i) if (!inventory[i].Empty) break;
                if (i >= inventory.Count) return true; // Can't take. Barrel is empty.

                ItemSlot ownSlot = inventory[i];
                int requestedQuantity = bulk ? ownSlot.Itemstack.Collectible.MaxStackSize : 1;
                for (; i < inventory.Count && ownSlot.StackSize < requestedQuantity; ++i)
                {
                    inventory[i].TryPutInto(Api.World, ownSlot, requestedQuantity - ownSlot.StackSize);
                }
                ItemStack stack = ownSlot.TakeOut(requestedQuantity);

                int originalQuantity = stack.StackSize;
                bool gave = byPlayer.InventoryManager.TryGiveItemstack(stack, true);
                int taken = originalQuantity - stack.StackSize;
                if (gave)
                {
                    if (taken == 0) taken = originalQuantity;
                    if (originalQuantity > taken)
                    {
                        new DummySlot(stack).TryPutInto(Api.World, ownSlot, originalQuantity - taken);
                    }
                    didMoveItems(stack, byPlayer);
                    if (Api.Side == EnumAppSide.Client)
                    {
                        loadOrCreateMesh();
                    }
                    MarkDirty(true);
                }
                else
                {
                    new DummySlot(stack).TryPutInto(Api.World, ownSlot, originalQuantity - taken);
                }

                if (taken == 0)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "invfull", Lang.Get("item-take-error-invfull"));
                }
                else
                {
                    Api.Logger.Audit("{0} Took {1}x{2} from " + Block?.Code + " at {3}.",
                        byPlayer.PlayerName,
                        taken,
                        stack?.Collectible.Code,
                        Pos
                    );

                    ownSlot.MarkDirty();
                    MarkDirty();
                }
                return true;
            }

            if (put && !hotbarslot.Empty)
            {
                if (!IsStick(hotbarslot.Itemstack))
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "onlysticks", Lang.Get("Only sticks can be stored in this barrel."));
                    return true;
                }

                ItemSlot ownSlot = inventory.FirstNonEmptySlot;
                var quantity = bulk ? hotbarslot.StackSize : 1;
                if (ownSlot == null)
                {
                    if (hotbarslot.TryPutInto(Api.World, inventory[0], quantity) > 0)
                    {
                        didMoveItems(inventory[0].Itemstack, byPlayer);
                        if (Api.Side == EnumAppSide.Client)
                        {
                            loadOrCreateMesh();
                        }
                        MarkDirty(true);
                        Api.World.Logger.Audit("{0} Put {1}x{2} into Barrel at {3}.",
                            byPlayer.PlayerName,
                            quantity,
                            inventory[0].Itemstack?.Collectible.Code,
                            Pos
                        );
                    }
                }
                else
                {
                    if (hotbarslot.Itemstack.Equals(Api.World, ownSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                    {
                        List<ItemSlot> skipSlots = new List<ItemSlot>();
                        while (hotbarslot.StackSize > 0 && skipSlots.Count < inventory.Count)
                        {
                            var wslot = inventory.GetBestSuitedSlot(hotbarslot, null, skipSlots);
                            if (wslot.slot == null) break;

                            if (hotbarslot.TryPutInto(Api.World, wslot.slot, quantity) > 0)
                            {
                                didMoveItems(wslot.slot.Itemstack, byPlayer);
                                Api.World.Logger.Audit("{0} Put {1}x{2} into Barrel at {3}.",
                                    byPlayer.PlayerName,
                                    quantity,
                                    wslot.slot.Itemstack?.Collectible.Code,
                                    Pos
                                );
                                if (!bulk) break;
                            }

                            skipSlots.Add(wslot.slot);
                        }
                    }
                }

                hotbarslot.MarkDirty();
                MarkDirty();
            }


            return true;
        }

        protected void didMoveItems(ItemStack stack, IPlayer byPlayer)
        {
            if (Api.Side == EnumAppSide.Client) loadOrCreateMesh();

            (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            AssetLocation sound = stack?.Block?.Sounds?.Place;
            Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
        }

        protected virtual void InitInventory(Block block, ICoreAPI api)
        {
            if (block?.Attributes != null)
            {
                var props = block.Attributes["properties"][type];
                if (!props.Exists) props = block.Attributes["properties"]["*"];
                quantitySlots = props["quantitySlots"].AsInt(quantitySlots);
                retrieveOnly = props["retrieveOnly"].AsBool(false);
            }

            inventory = new InventoryGeneric(quantitySlots, null, null, null);
            inventory.BaseWeight = 1f;
            inventory.OnGetSuitability = (sourceSlot, targetSlot, isMerge) => (isMerge ? (inventory.BaseWeight + 3) : (inventory.BaseWeight + 1)) + (sourceSlot.Inventory is InventoryBasePlayer ? 1 : 0);
            inventory.OnGetAutoPullFromSlot = GetAutoPullFromSlot;
            inventory.OnGetAutoPushIntoSlot = GetAutoPushIntoSlot;

            inventory.PutLocked = retrieveOnly;
            inventory.OnInventoryClosed += OnInvClosed;
            inventory.OnInventoryOpened += OnInvOpened;

            if (api.Side == EnumAppSide.Server)
            {
                inventory.SlotModified += Inventory_SlotModified;
            }

            inventory.SlotModified += (slotIndex) =>
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    loadOrCreateMesh();
                }
                MarkDirty(true); // Mark block dirty so client updates properly
            };

            container.Reset();
        }


        private void Inventory_SlotModified(int obj)
        {
            MarkDirty(false);
        }

        public Cuboidf[] GetSelectionBoxes()
        {
            if (selBoxBarrel == null)
            {
                selBoxBarrel = Block.SelectionBoxes[0].RotatedCopy(0, ((int)Math.Round(rotAngleY * GameMath.RAD2DEG / 90)) * 90, 0, new Vec3d(0.5, 0, 0.5));
            }

            if (Api.Side == EnumAppSide.Client)
            {
                ItemSlot hotbarslot = ((ICoreClientAPI)Api).World.Player.InventoryManager.ActiveHotbarSlot;
            }

            return new Cuboidf[] { selBoxBarrel };
        }

        #region Load/Store

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            var block = worldForResolving.GetBlock(new AssetLocation(tree.GetString("blockCode"))) as BlockStickBarrel;

            type = tree.GetString("type", block?.Props.DefaultType);
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);

            preferredFillState = tree.GetString("fillState");

            if (inventory == null)
            {
                if (tree.HasAttribute("blockCode"))
                {
                    InitInventory(block, worldForResolving.Api);
                }
                else
                {
                    InitInventory(null, worldForResolving.Api);
                }
            }

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                loadOrCreateMesh();
                MarkDirty(true);
            }

            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (Block != null) tree.SetString("forBlockCode", Block.Code.ToShortString());

            if (type == null) type = ownBlock.Props.DefaultType; // No idea why. Somewhere something has no type. Probably some worldgen ruins

            tree.SetString("type", type);
            tree.SetFloat("meshAngle", MeshAngle);
            tree.SetString("fillState", preferredFillState);

        }


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
        }

        #endregion


        private ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            if (atBlockFace == BlockFacing.DOWN)
            {
                return inventory.FirstNonEmptySlot;
            }

            return null;
        }

        private ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            if (!IsStick(fromSlot?.Itemstack)) return null;

            var slotNonEmpty = inventory.FirstNonEmptySlot;
            if (slotNonEmpty == null) return inventory[0];

            if (slotNonEmpty.Itemstack.Equals(Api.World, fromSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                foreach (var slot in inventory)
                {
                    if (slot.Itemstack == null || slot.StackSize < slot.Itemstack.Collectible.MaxStackSize)
                        return slot;
                }
                return null;
            }

            return null;
        }

        protected virtual void OnInvOpened(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly && player.WorldData.CurrentGameMode != EnumGameMode.Creative;
        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly;
        }

        #region Meshing

        private void loadOrCreateMesh()
        {
            Block ??= Api.World.BlockAccessor.GetBlock(Pos) as BlockStickBarrel;
            BlockStickBarrel block = Block as BlockStickBarrel;
            if (block == null) return;

            string cacheKey = "barrelMeshes" + block.FirstCodePart();
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, cacheKey, () => new Dictionary<string, MeshData>());


            CompositeShape cshape = ownBlock.Props[type].Shape;
            if (cshape?.Base == null)
            {
                return;
            }

            var firstStack = inventory.FirstNonEmptySlot?.Itemstack;

            string stage = ComputeFillStage(); // <-- 5-stage value: empty, stage1..stage5

            string meshKey = type + block.Subtype + "-" + stage;

            if (!meshes.TryGetValue(meshKey, out MeshData mesh))
            {
                mesh = block.GenMesh(Api as ICoreClientAPI, firstStack, type, stage, cshape, new Vec3f(cshape.rotateX, cshape.rotateY, cshape.rotateZ));
                meshes[meshKey] = mesh;
            }

            ownMesh = mesh.Clone().Rotate(origin, 0, MeshAngle, 0).Scale(origin, rndScale, rndScale, rndScale);
        }


        static Vec3f origin = new Vec3f(0.5f, 0f, 0.5f);
        float rndScale => 1 + (GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 100) - 50) / 1000f;

        private string ComputeFillStage()
        {
            int totalCount = 0;
            for (int i = 0; i < inventory.Count; i++)
            {
                totalCount += inventory[i].StackSize;
            }

            if (totalCount <= 0) return "empty";

            int perSlotMax = inventory.FirstNonEmptySlot?.Itemstack?.Collectible?.MaxStackSize ?? 64;

            // Capacity = slots * per-slot max
            int capacity = quantitySlots * perSlotMax;
            if (capacity <= 0) return "stage1";

            float ratio = (float)totalCount / capacity;

            // Divide into 8 slices (each = 12.5% capacity)
            if (ratio <= 0.125f) return "stage1";
            if (ratio <= 0.250f) return "stage2";
            if (ratio <= 0.375f) return "stage3";
            if (ratio <= 0.500f) return "stage4";
            if (ratio <= 0.625f) return "stage5";
            if (ratio <= 0.750f) return "stage6";
            if (ratio <= 0.875f) return "stage7";
            return "stage8";
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            bool skipmesh = base.OnTesselation(mesher, tesselator);
            if (skipmesh) return true;


            if (ownMesh == null)
            {
                return true;
            }


            mesher.AddMeshData(ownMesh);

            return true;
        }

        #endregion


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            int stacksize = 0;
            foreach (var slot in inventory) stacksize += slot.StackSize;

            if (stacksize > 0)
            {
                dsc.AppendLine(Lang.Get("Contents: {0}x{1}", stacksize, inventory.FirstNonEmptySlot.GetStackName()));
            }
            else
            {
                dsc.AppendLine(Lang.Get("Empty"));
            }

            // base.GetBlockInfo(forPlayer, dsc);
        }


        public override void OnBlockUnloaded()
        {
            FreeAtlasSpace();
            base.OnBlockUnloaded();
        }

        public override void OnBlockRemoved()
        {
            FreeAtlasSpace();
            base.OnBlockRemoved();
        }

        private void FreeAtlasSpace()
        {
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation,
            Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            ownMesh = null;
            MeshAngle = tree.GetFloat("meshAngle");
            MeshAngle -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngle", MeshAngle);
        }

        private static bool IsStick(ItemStack stack)
        {
            if (stack == null) return false;

            var path = stack.Collectible?.Code?.Path ?? "";
            if (path.StartsWith("stick")) return true;

            // Optional: allow a JSON flag to mark custom arrows
            if (stack.Collectible?.Attributes?["isStick"].AsBool(false) == true) return true;

            return false;
        }


    }
}