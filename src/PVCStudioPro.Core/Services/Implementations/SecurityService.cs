using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PVCStudioPro.Core.Services.Implementations
{
    public class SecurityService : ISecurityService
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        private static extern void SecureZeroMemoryNative(IntPtr dest, IntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);

        public void SecureWipeMemory(IntPtr nativeBuffer, int byteCount)
        {
            if (nativeBuffer == IntPtr.Zero || byteCount <= 0) return;
            try
            {
                SecureZeroMemoryNative(nativeBuffer, (IntPtr)byteCount);
            }
            catch
            {
                // Managed fallback if native kernel32 fails
                unsafe
                {
                    byte* ptr = (byte*)nativeBuffer.ToPointer();
                    for (int i = 0; i < byteCount; i++)
                    {
                        ptr[i] = 0;
                    }
                }
            }
        }

        public void SecureWipeArray(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;
            CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }

        public bool LockMemoryPage(IntPtr nativeBuffer, UIntPtr byteCount)
        {
            if (nativeBuffer == IntPtr.Zero || byteCount == UIntPtr.Zero) return false;
            try
            {
                return VirtualLock(nativeBuffer, byteCount);
            }
            catch
            {
                return false;
            }
        }

        public bool UnlockMemoryPage(IntPtr nativeBuffer, UIntPtr byteCount)
        {
            if (nativeBuffer == IntPtr.Zero || byteCount == UIntPtr.Zero) return false;
            try
            {
                return VirtualUnlock(nativeBuffer, byteCount);
            }
            catch
            {
                return false;
            }
        }
    }
}
