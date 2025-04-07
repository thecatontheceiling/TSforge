namespace LibTSforge.Modifiers
{
    using System;
    using System.IO;
    using PhysicalStore;
    using SPP;

    public static class KMSHostCharge
    {
        public static void Charge(PSVersion version, bool production, Guid actId)
        {
            if (actId == Guid.Empty)
            {
                actId = SLApi.GetDefaultActivationID(SLApi.WINDOWS_APP_ID, true);

                if (actId == Guid.Empty)
                {
                    throw new NotSupportedException("No applicable activation IDs found.");
                }
            }

            if (SLApi.GetPKeyChannel(SLApi.GetInstalledPkeyID(actId)) != "Volume:CSVLK")
            {
                throw new NotSupportedException("Non-Volume:CSVLK product key installed.");
            }

            Guid appId = SLApi.GetAppId(actId);
            int totalClients = 50;
            int currClients = 25;
            byte[] hwidBlock = Constants.UniversalHWIDBlock;
            string key = string.Format("SPPSVC\\{0}", appId);
            long ldapTimestamp = DateTime.Now.ToFileTime();

            byte[] cmidGuids = { };
            byte[] reqCounts = { };
            byte[] kmsChargeData = { };

            BinaryWriter writer = new BinaryWriter(new MemoryStream());

            if (version == PSVersion.Vista)
            {
                writer.Write(new byte[44]);
                writer.Seek(0, SeekOrigin.Begin);

                writer.Write(totalClients);
                writer.Write(43200);
                writer.Write(32);

                writer.Seek(20, SeekOrigin.Begin);
                writer.Write((byte)currClients);

                writer.Seek(32, SeekOrigin.Begin);
                writer.Write((byte)currClients);

                writer.Seek(0, SeekOrigin.End);

                for (int i = 0; i < currClients; i++)
                {
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write(ldapTimestamp - (10 * (i + 1)));
                }

                kmsChargeData = writer.GetBytes();
            } 
            else
            {
                for (int i = 0; i < currClients; i++)
                {
                    writer.Write(ldapTimestamp - (10 * (i + 1)));
                    writer.Write(Guid.NewGuid().ToByteArray());
                }

                cmidGuids = writer.GetBytes();

                writer = new BinaryWriter(new MemoryStream());

                writer.Write(new byte[40]);

                writer.Seek(4, SeekOrigin.Begin);
                writer.Write((byte)currClients);

                writer.Seek(24, SeekOrigin.Begin);
                writer.Write((byte)currClients);

                reqCounts = writer.GetBytes();
            }

            SPPUtils.KillSPP(version);

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = SPPUtils.GetStore(version, production))
            {
                if (version != PSVersion.Vista)
                {
                    VariableBag kmsCountData = new VariableBag(version);
                    kmsCountData.Blocks.AddRange(new[]
                    {
                        new CRCBlockModern
                        {
                            DataType = CRCBlockType.BINARY,
                            KeyAsStr = "SppBindingLicenseData",
                            Value = hwidBlock
                        },
                        new CRCBlockModern
                        {
                            DataType = CRCBlockType.UINT,
                            Key = new byte[] { },
                            ValueAsInt = (uint)totalClients
                        },
                        new CRCBlockModern
                        {
                            DataType = CRCBlockType.UINT,
                            Key = new byte[] { },
                            ValueAsInt = 1051200000
                        },
                        new CRCBlockModern
                        {
                            DataType = CRCBlockType.UINT,
                            Key = new byte[] { },
                            ValueAsInt = (uint)currClients
                        },
                        new CRCBlockModern
                        {
                            DataType = CRCBlockType.BINARY,
                            Key = new byte[] { },
                            Value = cmidGuids
                        },
                        new CRCBlockModern
                        {
                            DataType = CRCBlockType.BINARY,
                            Key = new byte[] { },
                            Value = reqCounts
                        }
                    });

                    kmsChargeData = kmsCountData.Serialize();
                }

                string countVal = version == PSVersion.Vista ? "C8F6FFF1-79CE-404C-B150-F97991273DF1" : string.Format("msft:spp/kms/host/2.0/store/counters/{0}", appId);

                store.DeleteBlock(key, countVal);
                store.AddBlock(new PSBlock
                {
                    Type = BlockType.NAMED,
                    Flags = (version == PSVersion.WinModern) ? (uint)0x400 : 0,
                    KeyAsStr = key,
                    ValueAsStr = countVal,
                    Data = kmsChargeData
                });

                Logger.WriteLine(string.Format("Set charge count to {0} successfully.", currClients));
            }

            SPPUtils.RestartSPP(version);
        }
    }
}
