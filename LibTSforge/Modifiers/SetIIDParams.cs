namespace LibTSforge.Modifiers
{
    using PhysicalStore;
    using SPP;
    using System.IO;
    using System;

    public static class SetIIDParams
    {
        public static void SetParams(PSVersion version, bool production, Guid actId, PKeyAlgorithm algorithm, int group, int serial, ulong security)
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
                    throw new InvalidDataException("Failed to get product key data for activation ID " + actId + ".");
                }

                VariableBag pkb = new VariableBag(keyBlock.Data, version);

                ProductKey pkey = new ProductKey
                {
                    Group = group,
                    Serial = serial,
                    Security = security,
                    Algorithm = algorithm,
                    Upgrade = false
                };

                string blockName = version == PSVersion.Win7 ? "SppPkeyShortAuthenticator" : "SppPkeyPhoneActivationData";
                pkb.SetBlock(blockName, pkey.GetPhoneData(version));
                store.SetBlock(key, pkeyId.ToString(), pkb.Serialize());
            }

            Logger.WriteLine("Successfully set IID parameters.");
        }
    }
}
