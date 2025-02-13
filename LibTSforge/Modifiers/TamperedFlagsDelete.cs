namespace LibTSforge.Modifiers
{
    using System;
    using System.Linq;
    using LibTSforge.PhysicalStore;

    public static class TamperedFlagsDelete
    {
        public static void DeleteTamperFlags(PSVersion version, bool production)
        {
            Utils.KillSPP();

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = Utils.GetStore(version, production))
            {
                if (version != PSVersion.Win7)
                {
                    string recreatedFlag = "__##USERSEP-RESERVED##__$$RECREATED-FLAG$$";
                    string recoveredFlag = "__##USERSEP-RESERVED##__$$RECOVERED-FLAG$$";

                    DeleteFlag(store, recreatedFlag);
                    DeleteFlag(store, recoveredFlag);
                }
                else
                {
                    SetFlag(store, 0xA0001);
                }

                Logger.WriteLine("Successfully cleared the tamper state.");
            }
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
