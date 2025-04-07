namespace LibTSforge.Modifiers
{
    using System.Collections.Generic;
    using System.Linq;
    using PhysicalStore;
    using SPP;

    public static class GracePeriodReset
    {
        public static void Reset(PSVersion version, bool production)
        {
            SPPUtils.KillSPP(version);
            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = SPPUtils.GetStore(version, production))
            {
                string value = "msft:sl/timer";
                List<PSBlock> blocks = store.FindBlocks(value).ToList();

                foreach (PSBlock block in blocks)
                {
                    store.DeleteBlock(block.KeyAsStr, block.ValueAsStr);
                }
            }

            SPPUtils.RestartSPP(version);
            Logger.WriteLine("Successfully reset all grace and evaluation period timers.");
        }
    }
}
