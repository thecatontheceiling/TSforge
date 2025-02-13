namespace LibTSforge.Modifiers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LibTSforge.PhysicalStore;

    public static class GracePeriodReset
    {
        public static void Reset(PSVersion version, bool production)
        {
            Utils.KillSPP();
            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = Utils.GetStore(version, production))
            {
                string value = "msft:sl/timer";
                List<PSBlock> blocks = store.FindBlocks(value).ToList();

                foreach (PSBlock block in blocks)
                {
                    store.DeleteBlock(block.KeyAsStr, block.ValueAsStr);
                }
            }

            Logger.WriteLine("Successfully reset all grace and evaluation period timers.");
        }
    }
}
