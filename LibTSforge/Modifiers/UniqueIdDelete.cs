namespace LibTSforge.Modifiers
{
    using System;
    using PhysicalStore;
    using SPP;

    public static class UniqueIdDelete
    {
        public static void DeleteUniqueId(PSVersion version, bool production, Guid actId)
        {
            if (version == PSVersion.Vista) throw new NotSupportedException("This feature is not supported on Windows Vista/Server 2008.");

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

            Guid pkeyId = SLApi.GetInstalledPkeyID(actId);

            SPPUtils.KillSPP(version);

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = SPPUtils.GetStore(version, production))
            {
                string key = string.Format("SPPSVC\\{0}\\{1}", appId, actId);
                PSBlock keyBlock = store.GetBlock(key, pkeyId.ToString());

                if (keyBlock == null)
                {
                    throw new Exception("No product key found.");
                }

                VariableBag pkb = new VariableBag(keyBlock.Data, version);

                pkb.DeleteBlock("SppPkeyUniqueIdToken");

                store.SetBlock(key, pkeyId.ToString(), pkb.Serialize());
            }

            Logger.WriteLine("Successfully removed Unique ID for product key ID " + pkeyId);
        }
    }
}
