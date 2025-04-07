namespace LibTSforge.Modifiers
{
    using System;
    using System.IO;
    using Microsoft.Win32;
    using PhysicalStore;
    using SPP;
    using TokenStore;

    public static class GenPKeyInstall
    {
        private static void WritePkey2005RegistryValues(PSVersion version, ProductKey pkey)
        {
            Logger.WriteLine("Writing registry data for Windows product key...");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductId", pkey.GetPid2());
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DigitalProductId", pkey.GetPid3());
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DigitalProductId4", pkey.GetPid4());

            if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Internet Explorer\Registration", "ProductId", null) != null)
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Internet Explorer\Registration", "ProductId", pkey.GetPid2());
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Internet Explorer\Registration", "DigitalProductId", pkey.GetPid3());
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Internet Explorer\Registration", "DigitalProductId4", pkey.GetPid4());
            }

            if (pkey.Channel == "Volume:CSVLK" && version != PSVersion.Win7)
            {
                Registry.SetValue(@"HKEY_USERS\S-1-5-20\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform", "KmsHostConfig", 1);
            }
        }

        public static void InstallGenPKey(PSVersion version, bool production, Guid actId)
        {
            if (version == PSVersion.Vista) throw new NotSupportedException("This feature is not supported on Windows Vista/Server 2008.");
            if (actId == Guid.Empty) throw new ArgumentException("Activation ID must be specified for generated product key install.");

            PKeyConfig pkc = new PKeyConfig();
            
            try
            {
                pkc.LoadConfig(actId);
            }
            catch (ArgumentException)
            {
                pkc.LoadAllConfigs(SLApi.GetAppId(actId));
            }

            ProductConfig config;
            pkc.Products.TryGetValue(actId, out config);

            if (config == null) throw new ArgumentException("Activation ID " + actId + " not found in PKeyConfig.");

            ProductKey pkey = config.GetRandomKey();

            Guid instPkeyId = SLApi.GetInstalledPkeyID(actId);
            if (instPkeyId != Guid.Empty) SLApi.UninstallProductKey(instPkeyId);

            if (pkey.Algorithm == PKeyAlgorithm.PKEY2009)
            {
                uint status = SLApi.InstallProductKey(pkey);
                Logger.WriteLine(string.Format("Installing generated product key {0} status {1:X}", pkey, status));

                if ((int)status < 0)
                {
                    throw new ApplicationException("Failed to install generated product key.");
                }

                Logger.WriteLine("Successfully deposited generated product key.");
                return;
            }

            Logger.WriteLine("Key range is PKEY2005, creating fake key data...");

            if (pkey.Channel == "Volume:GVLK" && version == PSVersion.Win7) throw new NotSupportedException("Fake GVLK generation is not supported on Windows 7.");

            VariableBag pkb = new VariableBag(version);
            pkb.Blocks.AddRange(new[]
            {
                new CRCBlockModern
                {
                    DataType = CRCBlockType.STRING,
                    KeyAsStr = "SppPkeyBindingProductKey",
                    ValueAsStr = pkey.ToString()
                },
                new CRCBlockModern
                {
                    DataType = CRCBlockType.STRING,
                    KeyAsStr = "SppPkeyBindingMPC",
                    ValueAsStr = pkey.GetMPC()
                },
                new CRCBlockModern {
                    DataType = CRCBlockType.BINARY,
                    KeyAsStr = "SppPkeyBindingPid2",
                    ValueAsStr = pkey.GetPid2()
                },
                new CRCBlockModern
                {
                    DataType = CRCBlockType.BINARY,
                    KeyAsStr = "SppPkeyBindingPid3",
                    Value = pkey.GetPid3()
                },
                new CRCBlockModern
                {
                    DataType = CRCBlockType.BINARY,
                    KeyAsStr = "SppPkeyBindingPid4",
                    Value = pkey.GetPid4()
                },
                new CRCBlockModern
                {
                    DataType = CRCBlockType.STRING,
                    KeyAsStr = "SppPkeyChannelId",
                    ValueAsStr = pkey.Channel
                },
                new CRCBlockModern
                {
                    DataType = CRCBlockType.STRING,
                    KeyAsStr = "SppPkeyBindingEditionId",
                    ValueAsStr = pkey.Edition
                },
                new CRCBlockModern
                {
                    DataType = CRCBlockType.BINARY,
                    KeyAsStr = (version == PSVersion.Win7) ? "SppPkeyShortAuthenticator" : "SppPkeyPhoneActivationData",
                    Value = pkey.GetPhoneData(version)
                },
                new CRCBlockModern
                {
                    DataType = CRCBlockType.BINARY,
                    KeyAsStr = "SppPkeyBindingMiscData",
                    Value = new byte[] { }
                }
            });

            Guid appId = SLApi.GetAppId(actId);
            string pkeyId = pkey.GetPkeyId().ToString();
            bool isAddon = SLApi.IsAddon(actId);
            string currEdition = SLApi.GetMetaStr(actId, "Family");

            if (appId == SLApi.WINDOWS_APP_ID && !isAddon)
            {
                SLApi.UninstallAllProductKeys(appId);
            }

            SPPUtils.KillSPP(version);

            using (IPhysicalStore ps = SPPUtils.GetStore(version, production))
            {
                using (ITokenStore tks = SPPUtils.GetTokenStore(version))
                {
                    Logger.WriteLine("Writing to physical store and token store...");

                    string suffix = (version == PSVersion.Win8 || version == PSVersion.WinBlue || version == PSVersion.WinModern) ? "_--" : "";
                    string metSuffix = suffix + "_met";

                    if (appId == SLApi.WINDOWS_APP_ID && !isAddon)
                    {
                        string edTokName = "msft:spp/token/windows/productkeyid/" + currEdition;

                        TokenMeta edToken = tks.GetMetaEntry(edTokName);
                        edToken.Data["windowsComponentEditionPkeyId"] = pkeyId;
                        edToken.Data["windowsComponentEditionSkuId"] = actId.ToString();
                        tks.SetEntry(edTokName, "xml", edToken.Serialize());

                        WritePkey2005RegistryValues(version, pkey);
                    }

                    string uriMapName = "msft:spp/token/PKeyIdUriMapper" + metSuffix;
                    TokenMeta uriMap = tks.GetMetaEntry(uriMapName);
                    uriMap.Data[pkeyId] = pkey.GetAlgoUri();
                    tks.SetEntry(uriMapName, "xml", uriMap.Serialize());

                    string skuMetaName = actId + metSuffix;
                    TokenMeta skuMeta = tks.GetMetaEntry(skuMetaName);

                    foreach (string k in skuMeta.Data.Keys)
                    {
                        if (k.StartsWith("pkeyId_"))
                        {
                            skuMeta.Data.Remove(k);
                            break;
                        }
                    }

                    skuMeta.Data["pkeyId"] = pkeyId;
                    skuMeta.Data["pkeyIdList"] = pkeyId;
                    tks.SetEntry(skuMetaName, "xml", skuMeta.Serialize());

                    string psKey = string.Format("SPPSVC\\{0}\\{1}", appId, actId);
                    ps.DeleteBlock(psKey, pkeyId);
                    ps.AddBlock(new PSBlock
                    {
                        Type = BlockType.NAMED,
                        Flags = (version == PSVersion.WinModern) ? (uint)0x402 : 0x2,
                        KeyAsStr = psKey,
                        ValueAsStr = pkeyId,
                        Data = pkb.Serialize()
                    });

                    string cachePath = SPPUtils.GetTokensPath(version).Replace("tokens.dat", @"cache\cache.dat");
                    if (File.Exists(cachePath)) File.Delete(cachePath);
                }
            }

            SLApi.RefreshTrustedTime(actId);
            Logger.WriteLine("Successfully deposited fake product key.");
        }
    }
}
