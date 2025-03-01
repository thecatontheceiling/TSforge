namespace LibTSforge.Modifiers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LibTSforge.PhysicalStore;
    using LibTSforge.SPP;

    public static class RearmReset
    {
        public static void Reset(PSVersion version, bool production)
        {
            SPPUtils.KillSPP(version);

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = SPPUtils.GetStore(version, production))
            {
                List<PSBlock> blocks;

                if (version == PSVersion.Win7)
                {
                    blocks = store.FindBlocks(0xA0000).ToList();
                }
                else
                {
                    blocks = store.FindBlocks("__##USERSEP-RESERVED##__$$REARM-COUNT$$").ToList();
                }

                foreach (PSBlock block in blocks)
                {
                    if (version == PSVersion.Win7)
                    {
                        store.SetBlock(block.KeyAsStr, block.ValueAsInt, new byte[8]);
                    }
                    else
                    {
                        store.SetBlock(block.KeyAsStr, block.ValueAsStr, new byte[8]);
                    }
                }

                Logger.WriteLine("Successfully reset all rearm counters.");
            }
        }
    }
}
