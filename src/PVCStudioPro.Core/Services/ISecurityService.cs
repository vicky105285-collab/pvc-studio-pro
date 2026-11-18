using System;

namespace PVCStudioPro.Core.Services
{
    public interface ISecurityService
    {
        void SecureWipeMemory(IntPtr nativeBuffer, int byteCount);
        void SecureWipeArray(byte[] buffer);
        bool LockMemoryPage(IntPtr nativeBuffer, UIntPtr byteCount);
        bool UnlockMemoryPage(IntPtr nativeBuffer, UIntPtr byteCount);
    }
}
