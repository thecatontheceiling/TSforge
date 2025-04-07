namespace LibTSforge.SPP
{
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Runtime.InteropServices;

    public class SPSys
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        private static SafeFileHandle CreateFileSafe(string device)
        {
            return new SafeFileHandle(CreateFile(device, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero), true);
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DeviceIoControl([In] SafeFileHandle hDevice, [In] uint dwIoControlCode, [In] IntPtr lpInBuffer, [In] int nInBufferSize, [Out] IntPtr lpOutBuffer, [In] int nOutBufferSize, out int lpBytesReturned, [In] IntPtr lpOverlapped);

        public static bool IsSpSysRunning()
        {
            SafeFileHandle file = CreateFileSafe(@"\\.\SpDevice");
            IntPtr buffer = Marshal.AllocHGlobal(1);
            int bytesReturned;
            DeviceIoControl(file, 0x80006008, IntPtr.Zero, 0, buffer, 1, out bytesReturned, IntPtr.Zero);
            bool running = Marshal.ReadByte(buffer) != 0;
            Marshal.FreeHGlobal(buffer);
            file.Close();
            return running;
        }

        public static int ControlSpSys(bool start)
        {
            SafeFileHandle file = CreateFileSafe(@"\\.\SpDevice");
            IntPtr buffer = Marshal.AllocHGlobal(4);
            int bytesReturned;
            DeviceIoControl(file, start ? 0x8000a000 : 0x8000a004, IntPtr.Zero, 0, buffer, 4, out bytesReturned, IntPtr.Zero);
            int result = Marshal.ReadInt32(buffer);
            Marshal.FreeHGlobal(buffer);
            file.Close();
            return result;
        }
    }
}
