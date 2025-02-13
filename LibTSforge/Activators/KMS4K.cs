namespace LibTSforge.Activators
{
    using System;
    using LibTSforge.PhysicalStore;
    using LibTSforge.SPP;

    public class KMS4k
    {
        public static void Activate(PSVersion version, bool production, Guid actId)
        {
            Guid appId;
            if (actId == Guid.Empty)
            {
                appId = SLApi.WINDOWS_APP_ID;
                actId = SLApi.GetDefaultActivationID(appId, true);

                if (actId == Guid.Empty)
                {
                    throw new NotSupportedException("No applicable activation IDs found.");
                }
            }
            else
            {
                appId = SLApi.GetAppId(actId);
            }

            if (SLApi.GetPKeyChannel(SLApi.GetInstalledPkeyID(actId)) != "Volume:GVLK")
            {
                throw new NotSupportedException("Non-Volume:GVLK product key installed.");
            }

            Utils.KillSPP();

            Logger.WriteLine("Writing TrustedStore data...");

            using (IPhysicalStore store = Utils.GetStore(version, production))
            {
                string key = string.Format("SPPSVC\\{0}\\{1}", appId, actId);

                ulong unknown = 0;
                ulong time1;
                ulong time2 = (ulong)DateTime.UtcNow.ToFileTime();
                ulong expiry = Constants.TimerMax;

                if (version == PSVersion.Win7)
                {
                    unknown = 0x800000000;
                    time1 = 0;
                }
                else
                {
                    long creationTime = BitConverter.ToInt64(store.GetBlock("__##USERSEP##\\$$_RESERVED_$$\\NAMESPACE__", "__##USERSEP-RESERVED##__$$GLOBAL-CREATION-TIME$$").Data, 0);
                    long tickCount = BitConverter.ToInt64(store.GetBlock("__##USERSEP##\\$$_RESERVED_$$\\NAMESPACE__", "__##USERSEP-RESERVED##__$$GLOBAL-TICKCOUNT-UPTIME$$").Data, 0);
                    long deltaTime = BitConverter.ToInt64(store.GetBlock(key, "__##USERSEP-RESERVED##__$$UP-TIME-DELTA$$").Data, 0);

                    time1 = (ulong)(creationTime + tickCount + deltaTime);
                    time2 /= 10000;
                    expiry /= 10000;
                }

                byte[] hwidBlock = Constants.UniversalHWIDBlock;
                byte[] kmsResp;

                switch (version)
                {
                    case PSVersion.Win7:
                        kmsResp = Constants.KMSv4Response;
                        break;
                    case PSVersion.Win8:
                        kmsResp = Constants.KMSv5Response;
                        break;
                    case PSVersion.WinBlue:
                    case PSVersion.WinModern:
                        kmsResp = Constants.KMSv6Response;
                        break;
                    default:
                        throw new NotSupportedException("Unsupported PSVersion.");
                }

                VariableBag kmsBinding = new VariableBag();

                kmsBinding.Blocks.AddRange(new CRCBlock[]
                {
                    new CRCBlock
                    {
                        DataType = CRCBlockType.BINARY,
                        Key = new byte[] { },
                        Value = kmsResp
                    },
                    new CRCBlock
                    {
                        DataType = CRCBlockType.STRING,
                        Key = new byte[] { },
                        ValueAsStr = "msft:rm/algorithm/hwid/4.0"
                    },
                    new CRCBlock
                    {
                        DataType = CRCBlockType.BINARY,
                        KeyAsStr = "SppBindingLicenseData",
                        Value = hwidBlock
                    }
                });

                if (version == PSVersion.WinModern)
                {
                    kmsBinding.Blocks.AddRange(new CRCBlock[]
                    {
                        new CRCBlock
                        {
                            DataType = CRCBlockType.STRING,
                            Key = new byte[] { },
                            ValueAsStr = "massgrave.dev"
                        },
                        new CRCBlock
                        {
                            DataType = CRCBlockType.STRING,
                            Key = new byte[] { },
                            ValueAsStr = "6969"
                        }
                    });
                }

                byte[] kmsBindingData = kmsBinding.Serialize();

                Timer kmsTimer = new Timer
                {
                    Unknown = unknown,
                    Time1 = time1,
                    Time2 = time2,
                    Expiry = expiry
                };

                string storeVal = string.Format("msft:spp/kms/bind/2.0/store/{0}/{1}", appId, actId);
                string timerVal = string.Format("msft:spp/kms/bind/2.0/timer/{0}/{1}", appId, actId);

                store.DeleteBlock(key, storeVal);
                store.DeleteBlock(key, timerVal);

                store.AddBlocks(new PSBlock[]
                {
                    new PSBlock
                    {
                        Type = BlockType.NAMED,
                        Flags = (version == PSVersion.WinModern) ? (uint)0x400 : 0,
                        KeyAsStr = key,
                        ValueAsStr = storeVal,
                        Data = kmsBindingData
                    },
                    new PSBlock
                    {
                        Type = BlockType.TIMER,
                        Flags = (version == PSVersion.Win7) ? (uint)0 : 0x4,
                        KeyAsStr = key,
                        ValueAsStr = timerVal,
                        Data = kmsTimer.CastToArray()
                    }
                });
            }

            SLApi.RefreshLicenseStatus();
            SLApi.FireStateChangedEvent(appId);
            Logger.WriteLine("Activated using KMS4k successfully.");
        }
    }
}
