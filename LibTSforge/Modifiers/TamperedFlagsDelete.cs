namespace LibTSforge.Modifiers
{
    using System.Linq;
    using PhysicalStore;
    using SPP;

    public static class TamperedFlagsDelete
    {
        public static void DeleteTamperFlags(PSVersion version, bool production)
        {
            SPPUtils.KillSPP(version);

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = SPPUtils.GetStore(version, production))
            {
                if (version == PSVersion.Vista)
                {
                    DeleteFlag(store, "6BE8425B-E3CF-4e86-A6AF-5863E3DCB606");
                }
                else if (version == PSVersion.Win7)
                {
                    SetFlag(store, 0xA0001);
                }
                else
                {
                    DeleteFlag(store, "__##USERSEP-RESERVED##__$$RECREATED-FLAG$$");
                    DeleteFlag(store, "__##USERSEP-RESERVED##__$$RECOVERED-FLAG$$");
                }

                Logger.WriteLine("Successfully cleared the tamper state.");
            }

            SPPUtils.RestartSPP(version);
        }

        private static void DeleteFlag(IPhysicalStore store, string flag)
        {
            store.FindBlocks(flag).ToList().ForEach(block => store.DeleteBlock(block.KeyAsStr, block.ValueAsStr));
        }

        private static void SetFlag(IPhysicalStore store, uint flag)
        {
            store.FindBlocks(flag).ToList().ForEach(block => store.SetBlock(block.KeyAsStr, block.ValueAsInt, new byte[8]));
        }
    }
}
