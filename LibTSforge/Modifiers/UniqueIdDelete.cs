namespace LibTSforge.Modifiers
{
    using System;
    using LibTSforge.PhysicalStore;
    using LibTSforge.SPP;

    public static class UniqueIdDelete
    {
        public static void DeleteUniqueId(PSVersion version, bool production, Guid actId)
        {
            Guid appId;

            if (actId == Guid.Empty)
            {
                appId = SLApi.WINDOWS_APP_ID;
                actId = SLApi.GetDefaultActivationID(appId, true);

                if (actId == Guid.Empty)
                {
                    throw new Exception("No applicable activation IDs found.");
                }
            }
            else
            {
                appId = SLApi.GetAppId(actId);
            }

            string instId = SLApi.GetInstallationID(actId);
            Guid pkeyId = SLApi.GetInstalledPkeyID(actId);

            Utils.KillSPP();

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = Utils.GetStore(version, production))
            {
                string key = string.Format("SPPSVC\\{0}\\{1}", appId, actId);
                PSBlock keyBlock = store.GetBlock(key, pkeyId.ToString());

                if (keyBlock == null)
                {
                    throw new Exception("No product key found.");
                }

                VariableBag pkb = new VariableBag(keyBlock.Data);

                pkb.DeleteBlock("SppPkeyUniqueIdToken");

                store.SetBlock(key, pkeyId.ToString(), pkb.Serialize());
            }

            Logger.WriteLine("Successfully removed Unique ID for product key ID " + pkeyId);
        }
    }
}
