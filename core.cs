using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

[assembly: ModInfo("arrowbarrels",
                    Authors = new string[] { "xXx_Ape_xXx" },
                    Description = "Barrels for storing arrows",
                    Version = "1.3.0")]

namespace arrowbarrels
{
    public class Core : ModSystem
    {
        private ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            this.RegisterBlocks(api);
            this.RegisterEntityclasses(api);
            this.RegisterColBehaviours(api);

            base.Start(api);
            api.World.Logger.Event("started 'Arrow Barrels' mod");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.api = api;
            base.StartClientSide(api);
        }

        private void RegisterBlocks(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockArrowBarrel", typeof(BlockArrowBarrel));
            api.RegisterBlockClass("BlockSpearBarrel", typeof(BlockSpearBarrel));
        }

        private void RegisterEntityclasses(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("BEArrowBarrel", typeof(BEArrowBarrel));
            api.RegisterBlockEntityClass("BESpearBarrel", typeof(BESpearBarrel));
        }
        private void RegisterColBehaviours(ICoreAPI api)
        {
            api.RegisterCollectibleBehaviorClass("CBArrowBarrel", typeof(CollectibleBehaviorArrowBarrel));
            api.RegisterCollectibleBehaviorClass("CBSpearBarrel", typeof(CollectibleBehaviorSpearBarrel));
        }


        public override void Dispose()
        {
            base.Dispose();
        }

    }
}
