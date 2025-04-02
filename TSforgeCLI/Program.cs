namespace TSforgeCLI
{
    using System;
    using LibTSforge;
    using LibTSforge.Activators;
    using LibTSforge.Modifiers;
    using LibTSforge.SPP;

    public class Program
    {
        private class Options
        {
            public bool Dump = false;
            public string DumpFilePath = "dump.dat";
            public string EncrFilePath = null;
            public bool Load = false;
            public string LoadFilePath = "load.dat";
            public bool KMS4k = false;
            public bool AVMA4k = false;
            public bool ZCID = false;
            public bool TimerReset = false;
            public bool RearmReset = false;
            public bool DeleteUniqueId = false;
            public bool InstallGenPKey = false;
            public bool KMSHostCharge = false;
            public bool TamperedFlagsDelete = false;
            public bool KeyChangeLockDelete = false;
            public bool? Production = null;
            public PSVersion? Version = null;
            public Guid ActivationId = Guid.Empty;
            public bool ShowHelp = false;
        }

        public static void Main(string[] args)
        {
            Logger.WriteLine("TSforge (c) MASSGRAVE 2025");

            try
            {
                if (args.Length == 0)
                {
                    DisplayUsage();
                    return;
                }

                Options options = ParseArguments(args);

                if (options.ShowHelp)
                {
                    DisplayUsage();
                    return;
                }

                PSVersion version = options.Version ?? Utils.DetectVersion();
                bool production = options.Production ?? SPPUtils.DetectCurrentKey();

                if (options.Dump)
                {
                    SPPUtils.DumpStore(version, production, options.DumpFilePath, options.EncrFilePath);
                }
                else if (options.Load)
                {
                    SPPUtils.LoadStore(version, production, options.LoadFilePath);
                }
                else if (options.KMS4k)
                {
                    KMS4k.Activate(version, production, options.ActivationId);
                }
                else if (options.AVMA4k)
                {
                    AVMA4k.Activate(version, production, options.ActivationId);
                }
                else if (options.ZCID)
                {
                    ZeroCID.Activate(version, production, options.ActivationId);
                }
                else if (options.TimerReset)
                {
                    GracePeriodReset.Reset(version, production);
                }
                else if (options.DeleteUniqueId)
                {
                    UniqueIdDelete.DeleteUniqueId(version, production, options.ActivationId);
                }
                else if (options.RearmReset)
                {
                    RearmReset.Reset(version, production);
                }
                else if (options.InstallGenPKey)
                {
                    GenPKeyInstall.InstallGenPKey(version, production, options.ActivationId);
                }
                else if (options.KMSHostCharge)
                {
                    KMSHostCharge.Charge(version, production, options.ActivationId);
                }
                else if (options.TamperedFlagsDelete)
                {
                    TamperedFlagsDelete.DeleteTamperFlags(version, production);
                }
                else if (options.KeyChangeLockDelete)
                {
                    KeyChangeLockDelete.Delete(version, production);
                }
                else
                {
                    DisplayUsage();
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw;
#else
                Logger.WriteLine("Fatal error: " + e.ToString());
                Environment.Exit(1);
#endif
            }
        }

        private static Options ParseArguments(string[] args)
        {
            Options options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim().ToLowerInvariant();
                switch (arg)
                {
                    case "/dump":
                        options.Dump = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("/"))
                        {
                            options.DumpFilePath = args[++i];
                        }
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("/"))
                        {
                            options.EncrFilePath = args[++i];
                        }
                        break;
                    case "/load":
                        options.Load = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("/"))
                        {
                            options.LoadFilePath = args[++i];
                        }
                        break;
                    case "/kms4k":
                        options.KMS4k = true;
                        break;
                    case "/avma4k":
                        options.AVMA4k = true;
                        break;
                    case "/zcid":
                        options.ZCID = true;
                        break;
                    case "/ver":
                        options.Version = i + 1 < args.Length ? ParseVersion(args[++i]) : throw new ArgumentException("/ver requires a version argument.");
                        break;
                    case "/rtmr":
                        options.TimerReset = true;
                        break;
                    case "/?":
                        options.ShowHelp = true;
                        break;
                    case "/duid":
                        options.DeleteUniqueId = true;
                        break;
                    case "/rrmc":
                        options.RearmReset = true;
                        break;
                    case "/igpk":
                        options.InstallGenPKey = true;
                        break;
                    case "/kmsc":
                        options.KMSHostCharge = true;
                        break;
                    case "/test":
                        options.Production = false;
                        break;
                    case "/prod":
                        options.Production = true;
                        break;
                    case "/ctpr":
                        options.TamperedFlagsDelete = true;
                        break;
                    case "/revl":
                        options.KeyChangeLockDelete = true;
                        break;
                    default:
                        try
                        {
                            options.ActivationId = new Guid(arg);
                        }
                        catch (FormatException)
                        {
                            Logger.WriteLine("Argument doesn't exist or the specified activation ID is invalid.");
                            options.ShowHelp = true;
                            break;
                        }
                        break;
                }
            }

            return options;
        }

        private static void DisplayUsage()
        {
            string exeName = typeof(Program).Namespace;
            Logger.WriteLine("Usage: " + exeName + " [/dump <filePath> (<encrFilePath>)] [/load <filePath>] [/kms4k] [/avma4k] [/zcid] [/rtmr] [/duid] [/igpk] [/kmsc] [/ctpr] [/revl] [/prod] [/test] [<activation id>] [/ver <version override>]");
            Logger.WriteLine("Options:");
            Logger.WriteLine("\t/dump <filePath> (<encrFilePath>)       Dump and decrypt the physical store to the specified path.");
            Logger.WriteLine("\t/load <filePath>                        Load and re-encrypt the physical store from the specified path.");
            Logger.WriteLine("\t/kms4k                                  Activate using KMS4k. Only supports KMS-activatable editions.");
            Logger.WriteLine("\t/avma4k                                 Activate using AVMA4k. Only supports Windows Server 2012 R2+.");
            Logger.WriteLine("\t/zcid                                   Activate using ZeroCID. Only supports phone-activatable editions.");
            Logger.WriteLine("\t/rtmr                                   Reset grace/evaluation period timers.");
            Logger.WriteLine("\t/rrmc                                   Reset the rearm count.");
            Logger.WriteLine("\t/duid                                   Delete product key Unique ID used in online key validation.");
            Logger.WriteLine("\t/igpk                                   Install auto-generated/fake product key according to the specified Activation ID");
            Logger.WriteLine("\t/kmsc                                   Reset the charged count on the local KMS server to 25. Requires an activated KMS host.");
            Logger.WriteLine("\t/ctpr                                   Remove the tamper flags that get set in the physical store when sppsvc detects an attempt to tamper with it.");
            Logger.WriteLine("\t/revl                                   Remove the key change lock in evaluation edition store.");
            Logger.WriteLine("\t/prod                                   Use SPP production key.");
            Logger.WriteLine("\t/test                                   Use SPP test key.");
            Logger.WriteLine("\t/ver <version>                          Override the detected version. Available versions: vista, 7, 8early, 8, blue, modern.");
            Logger.WriteLine("\t<activation id>                         A specific activation ID. Useful if you want to activate specific addons like ESU.");
            Logger.WriteLine("\t/?                                      Display this help message.");
        }

        private static PSVersion ParseVersion(string ver)
        {
            switch (ver.Trim().ToLowerInvariant())
            {
                case "vista": return PSVersion.Vista;
                case "7": return PSVersion.Win7;
                case "8early": return PSVersion.Win8Early;
                case "8": return PSVersion.Win8;
                case "blue": return PSVersion.WinBlue;
                case "modern": return PSVersion.WinModern;
                default: throw new ArgumentException("Invalid version specified.");
            }
        }
    }
}
