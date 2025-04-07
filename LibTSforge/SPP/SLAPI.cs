namespace LibTSforge.SPP
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    public static class SLApi
    {
        private enum SLIDTYPE
        {
            SL_ID_APPLICATION,
            SL_ID_PRODUCT_SKU,
            SL_ID_LICENSE_FILE,
            SL_ID_LICENSE,
            SL_ID_PKEY,
            SL_ID_ALL_LICENSES,
            SL_ID_ALL_LICENSE_FILES,
            SL_ID_STORE_TOKEN,
            SL_ID_LAST
        }

        private enum SLDATATYPE
        {
            SL_DATA_NONE,
            SL_DATA_SZ,
            SL_DATA_DWORD,
            SL_DATA_BINARY,
            SL_DATA_MULTI_SZ,
            SL_DATA_SUM
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SL_LICENSING_STATUS
        {
            public Guid SkuId;
            public uint eStatus;
            public uint dwGraceTime;
            public uint dwTotalGraceDays;
            public uint hrReason;
            public ulong qwValidityExpiration;
        }

        public static readonly Guid WINDOWS_APP_ID = new Guid("55c92734-d682-4d71-983e-d6ec3f16059f");

        [DllImport("slc.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SLOpen(out IntPtr hSLC);

        [DllImport("slc.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SLClose(IntPtr hSLC);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLGetWindowsInformationDWORD(string ValueName, ref int Value);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLInstallProofOfPurchase(IntPtr hSLC, string pwszPKeyAlgorithm, string pwszPKeyString, uint cbPKeySpecificData, byte[] pbPKeySpecificData, ref Guid PKeyId);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLUninstallProofOfPurchase(IntPtr hSLC, ref Guid PKeyId);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLGetPKeyInformation(IntPtr hSLC, ref Guid pPKeyId, string pwszValueName, out SLDATATYPE peDataType, out uint pcbValue, out IntPtr ppbValue);

        [DllImport("slcext.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLActivateProduct(IntPtr hSLC, ref Guid pProductSkuId, byte[] cbAppSpecificData, byte[] pvAppSpecificData, byte[] pActivationInfo, string pwszProxyServer, ushort wProxyPort);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLGenerateOfflineInstallationId(IntPtr hSLC, ref Guid pProductSkuId, ref string ppwszInstallationId);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLDepositOfflineConfirmationId(IntPtr hSLC, ref Guid pProductSkuId, string pwszInstallationId, string pwszConfirmationId);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLGetSLIDList(IntPtr hSLC, SLIDTYPE eQueryIdType, ref Guid pQueryId, SLIDTYPE eReturnIdType, out uint pnReturnIds, out IntPtr ppReturnIds);

        [DllImport("slc.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SLGetLicensingStatusInformation(IntPtr hSLC, ref Guid pAppID, IntPtr pProductSkuId, string pwszRightName, out uint pnStatusCount, out IntPtr ppLicensingStatus);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLGetInstalledProductKeyIds(IntPtr hSLC, ref Guid pProductSkuId, out uint pnProductKeyIds, out IntPtr ppProductKeyIds);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLConsumeWindowsRight(uint unknown);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLGetProductSkuInformation(IntPtr hSLC, ref Guid pProductSkuId, string pwszValueName, out SLDATATYPE peDataType, out uint pcbValue, out IntPtr ppbValue);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLGetLicense(IntPtr hSLC, ref Guid pLicenseFileId, out uint pcbLicenseFile, out IntPtr ppbLicenseFile);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLSetCurrentProductKey(IntPtr hSLC, ref Guid pProductSkuId, ref Guid pProductKeyId);

        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern uint SLFireEvent(IntPtr hSLC, string pwszEventId, ref Guid pApplicationId);

        private class SLContext : IDisposable
        {
            public readonly IntPtr Handle;

            public SLContext()
            {
                SLOpen(out Handle);
            }

            public void Dispose()
            {
                SLClose(Handle);
                GC.SuppressFinalize(this);
            }

            ~SLContext()
            {
                Dispose();
            }
        }

        public static Guid GetDefaultActivationID(Guid appId, bool includeActivated)
        {
            using (SLContext sl = new SLContext())
            {
                uint count;
                IntPtr pLicStat;

                SLGetLicensingStatusInformation(sl.Handle, ref appId, IntPtr.Zero, null, out count, out pLicStat);

                unsafe
                {
                    SL_LICENSING_STATUS* licensingStatuses = (SL_LICENSING_STATUS*)pLicStat;
                    for (int i = 0; i < count; i++)
                    {
                        SL_LICENSING_STATUS slStatus = licensingStatuses[i];

                        Guid actId = slStatus.SkuId;
                        if (GetInstalledPkeyID(actId) == Guid.Empty) continue;
                        if (IsAddon(actId)) continue;
                        if (!includeActivated && (slStatus.eStatus == 1)) continue;

                        return actId;
                    }
                }

                return Guid.Empty;
            }
        }

        public static string GetInstallationID(Guid actId)
        {
            using (SLContext sl = new SLContext())
            {
                string installationId = null;
                return SLGenerateOfflineInstallationId(sl.Handle, ref actId, ref installationId) == 0 ? installationId : null;
            }
        }

        public static Guid GetInstalledPkeyID(Guid actId)
        {
            using (SLContext sl = new SLContext())
            {
                uint count;
                IntPtr pProductKeyIds;

                uint status = SLGetSLIDList(sl.Handle, SLIDTYPE.SL_ID_PRODUCT_SKU, ref actId, SLIDTYPE.SL_ID_PKEY, out count, out pProductKeyIds);

                if (status != 0 || count == 0)
                {
                    return Guid.Empty;
                }

                unsafe { return *(Guid*)pProductKeyIds; }
            }
        }

        public static uint DepositConfirmationID(Guid actId, string installationId, string confirmationId)
        {
            using (SLContext sl = new SLContext())
            {
                return SLDepositOfflineConfirmationId(sl.Handle, ref actId, installationId, confirmationId);
            }
        }

        public static void RefreshLicenseStatus()
        {
            SLConsumeWindowsRight(0);
        }

        public static void RefreshTrustedTime(Guid actId)
        {
            using (SLContext sl = new SLContext())
            {
                SLDATATYPE type;
                uint count;
                IntPtr ppbValue;

                SLGetProductSkuInformation(sl.Handle, ref actId, "TrustedTime", out type, out count, out ppbValue);
            }
        }

        public static void FireStateChangedEvent(Guid appId)
        {
            using (SLContext sl = new SLContext())
            {
                SLFireEvent(sl.Handle, "msft:rm/event/licensingstatechanged", ref appId);
            }
        }

        public static Guid GetAppId(Guid actId)
        {
            using (SLContext sl = new SLContext())
            {
                uint count;
                IntPtr pAppIds;

                uint status = SLGetSLIDList(sl.Handle, SLIDTYPE.SL_ID_PRODUCT_SKU, ref actId, SLIDTYPE.SL_ID_APPLICATION, out count, out pAppIds);

                if (status != 0 || count == 0)
                {
                    return Guid.Empty;
                }

                unsafe { return *(Guid*)pAppIds; }
            }
        }

        public static bool IsAddon(Guid actId)
        {
            using (SLContext sl = new SLContext())
            {
                uint count;
                SLDATATYPE type;
                IntPtr ppbValue;

                uint status = SLGetProductSkuInformation(sl.Handle, ref actId, "DependsOn", out type, out count, out ppbValue);
                return (int)status >= 0 && status != 0xC004F012;
            }
        }

        public static Guid GetLicenseFileId(Guid licId)
        {
            using (SLContext sl = new SLContext())
            {
                uint count;
                IntPtr ppReturnLics;

                uint status = SLGetSLIDList(sl.Handle, SLIDTYPE.SL_ID_LICENSE, ref licId, SLIDTYPE.SL_ID_LICENSE_FILE, out count, out ppReturnLics);

                if (status != 0 || count == 0)
                {
                    return Guid.Empty;
                }

                unsafe { return *(Guid*)ppReturnLics; }
            }
        }

        public static Guid GetPkeyConfigFileId(Guid actId)
        {
            using (SLContext sl = new SLContext())
            {
                SLDATATYPE type;
                uint len;
                IntPtr ppReturnLics;

                uint status = SLGetProductSkuInformation(sl.Handle, ref actId, "pkeyConfigLicenseId", out type, out len, out ppReturnLics);

                if (status != 0 || len == 0)
                {
                    return Guid.Empty;
                }

                Guid pkcId = new Guid(Marshal.PtrToStringAuto(ppReturnLics));
                return GetLicenseFileId(pkcId);
            }
        }

        public static string GetLicenseContents(Guid fileId)
        {
            if (fileId == Guid.Empty) throw new ArgumentException("License contents could not be retrieved.");

            using (SLContext sl = new SLContext())
            {
                uint dataLen;
                IntPtr dataPtr;

                if (SLGetLicense(sl.Handle, ref fileId, out dataLen, out dataPtr) != 0)
                {
                    return null;
                }

                byte[] data = new byte[dataLen];
                Marshal.Copy(dataPtr, data, 0, (int)dataLen);

                data = data.Skip(Array.IndexOf(data, (byte)'<')).ToArray();
                return Encoding.UTF8.GetString(data);
            }
        }

        public static bool IsPhoneActivatable(Guid actId)
        {
            using (SLContext sl = new SLContext())
            {
                uint count;
                SLDATATYPE type;
                IntPtr ppbValue;

                uint status = SLGetProductSkuInformation(sl.Handle, ref actId, "msft:sl/EUL/PHONE/PUBLIC", out type, out count, out ppbValue);
                return status != 0xC004F012;
            }
        }

        public static string GetPKeyChannel(Guid pkeyId)
        {
            using (SLContext sl = new SLContext())
            {
                SLDATATYPE type;
                uint len;
                IntPtr ppbValue;

                uint status = SLGetPKeyInformation(sl.Handle, ref pkeyId, "Channel", out type, out len, out ppbValue);

                if (status != 0 || len == 0)
                {
                    return null;
                }

                return Marshal.PtrToStringAuto(ppbValue);
            }
        }

        public static string GetMetaStr(Guid actId, string value)
        {
            using (SLContext sl = new SLContext())
            {
                uint len;
                SLDATATYPE type;
                IntPtr ppbValue;

                uint status = SLGetProductSkuInformation(sl.Handle, ref actId, value, out type, out len, out ppbValue);

                if (status != 0 || len == 0 || type != SLDATATYPE.SL_DATA_SZ)
                {
                    return null;
                }

                return Marshal.PtrToStringAuto(ppbValue);
            }
        }

        public static List<Guid> GetActivationIds(Guid appId)
        {
            using (SLContext sl = new SLContext())
            {
                uint count;
                IntPtr pLicStat;

                SLGetLicensingStatusInformation(sl.Handle, ref appId, IntPtr.Zero, null, out count, out pLicStat);

                List<Guid> result = new List<Guid>();

                unsafe
                {
                    SL_LICENSING_STATUS* licensingStatuses = (SL_LICENSING_STATUS*)pLicStat;
                    for (int i = 0; i < count; i++)
                    {
                        result.Add(licensingStatuses[i].SkuId);
                    }
                }

                return result;
            }
        }

        public static uint SetCurrentProductKey(Guid actId, Guid pkeyId)
        {
            using (SLContext sl = new SLContext())
            {
                return SLSetCurrentProductKey(sl.Handle, ref actId, ref pkeyId);
            }
        }

        public static uint InstallProductKey(ProductKey pkey)
        {
            using (SLContext sl = new SLContext())
            {
                Guid pkeyId = Guid.Empty;
                return SLInstallProofOfPurchase(sl.Handle, pkey.GetAlgoUri(), pkey.ToString(), 0, null, ref pkeyId);
            }
        }

        public static void UninstallProductKey(Guid pkeyId)
        {
            using (SLContext sl = new SLContext())
            {
                SLUninstallProofOfPurchase(sl.Handle, ref pkeyId);
            }
        }

        public static void UninstallAllProductKeys(Guid appId)
        {
            foreach (Guid actId in GetActivationIds(appId))
            {
                Guid pkeyId = GetInstalledPkeyID(actId);
                if (pkeyId == Guid.Empty) continue;
                if (IsAddon(actId)) continue;
                UninstallProductKey(pkeyId);
            }
        }
    }
}
