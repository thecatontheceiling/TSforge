namespace LibTSforge.Modifiers
{
    using System.Collections.Generic;
    using System.Linq;
    using PhysicalStore;
    using SPP;
    using System;

    public static class KeyChangeLockDelete
    {
        public static void Delete(PSVersion version, bool production)
        {
            if (version == PSVersion.Vista) throw new NotSupportedException("This feature is not supported on Windows Vista/Server 2008.");

            SPPUtils.KillSPP(version);
            Logger.WriteLine("Writing TrustedStore data...");
            using (IPhysicalStore store = SPPUtils.GetStore(version, production))
            {
                List<string> values = new List<string>
                {
                    "msft:spp/timebased/AB",
                    "msft:spp/timebased/CD"
                };
                List<PSBlock> blocks = new List<PSBlock>();
                foreach (string value in values)
                {
                    blocks.AddRange(store.FindBlocks(value).ToList());
                }
                foreach (PSBlock block in blocks)
                {
                    store.DeleteBlock(block.KeyAsStr, block.ValueAsStr);
                }
            }
            Logger.WriteLine("Successfully removed the key change lock.");
        }
    }
}
