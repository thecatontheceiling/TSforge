namespace LibTSforge.Activators
{
    using System;
    using System.IO;
    using LibTSforge.Crypto;
    using LibTSforge.PhysicalStore;
    using LibTSforge.SPP;

    public static class ZeroCID
    {
        public static void Deposit(Guid actId, string instId)
        {
            uint status = SLApi.DepositConfirmationID(actId, instId, Constants.ZeroCID);
            Logger.WriteLine(string.Format("Depositing fake CID status {0:X}", status));

            if (status != 0)
            {
                throw new InvalidOperationException("Failed to deposit fake CID.");
            }
        }

        public static void Activate(PSVersion version, bool production, Guid actId)
        {
            Guid appId;

            if (actId == Guid.Empty)
            {
                appId = SLApi.WINDOWS_APP_ID;
                actId = SLApi.GetDefaultActivationID(appId, false);

                if (actId == Guid.Empty)
                {
                    throw new NotSupportedException("No applicable activation IDs found.");
                }
            }
            else
            {
                appId = SLApi.GetAppId(actId);
            }

            if (!SLApi.IsPhoneActivatable(actId))
            {
                throw new NotSupportedException("Phone license is unavailable for this product.");
            }

            string instId = SLApi.GetInstallationID(actId);
            Guid pkeyId = SLApi.GetInstalledPkeyID(actId);

            if (version == PSVersion.Win7)
            {
                Deposit(actId, instId);
            }

            Utils.KillSPP();

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = Utils.GetStore(version, production))
            {
                byte[] hwidBlock = Constants.UniversalHWIDBlock;

                Logger.WriteLine("Activation ID: " + actId);
                Logger.WriteLine("Installation ID: " + instId);
                Logger.WriteLine("Product Key ID: " + pkeyId);

                byte[] iidHash;

                if (version == PSVersion.Win7)
                {
                    iidHash = CryptoUtils.SHA256Hash(Utils.EncodeString(instId));
                }
                else
                {
                    iidHash = CryptoUtils.SHA256Hash(Utils.EncodeString(instId + '\0' + Constants.ZeroCID));
                }

                string key = string.Format("SPPSVC\\{0}\\{1}", appId, actId);
                PSBlock keyBlock = store.GetBlock(key, pkeyId.ToString());

                if (keyBlock == null)
                {
                    throw new InvalidDataException("Failed to get product key data for activation ID " + actId + ".");
                }

                VariableBag pkb = new VariableBag(keyBlock.Data);

                byte[] pkeyData;

                if (version == PSVersion.Win7)
                {
                    pkeyData = pkb.GetBlock("SppPkeyShortAuthenticator").Value;
                }
                else
                {
                    pkeyData = pkb.GetBlock("SppPkeyPhoneActivationData").Value;
                }

                pkb.DeleteBlock("SppPkeyVirtual");
                store.SetBlock(key, pkeyId.ToString(), pkb.Serialize());

                BinaryWriter writer = new BinaryWriter(new MemoryStream());
                writer.Write(0x20);
                writer.Write(iidHash);
                writer.Write(hwidBlock.Length);
                writer.Write(hwidBlock);
                byte[] tsHwidData = writer.GetBytes();

                writer = new BinaryWriter(new MemoryStream());
                writer.Write(0x20);
                writer.Write(iidHash);
                writer.Write(pkeyData.Length);
                writer.Write(pkeyData);
                byte[] tsPkeyInfoData = writer.GetBytes();

                store.AddBlocks(new PSBlock[] {
                    new PSBlock
                    {
                        Type = BlockType.NAMED,
                        Flags = 0,
                        KeyAsStr = key,
                        ValueAsStr = "msft:Windows/7.0/Phone/Cached/HwidBlock/" + pkeyId,
                        Data = tsHwidData
                    }, 
                    new PSBlock
                    {
                        Type = BlockType.NAMED,
                        Flags = 0,
                        KeyAsStr = key,
                        ValueAsStr = "msft:Windows/7.0/Phone/Cached/PKeyInfo/" + pkeyId,
                        Data = tsPkeyInfoData
                    }
                });
            }

            if (version != PSVersion.Win7)
            {
                Deposit(actId, instId);
            }

            SLApi.RefreshLicenseStatus();
            SLApi.FireStateChangedEvent(appId);
            Logger.WriteLine("Activated using ZeroCID successfully.");
        }
    }
}
