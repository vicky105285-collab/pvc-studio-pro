using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using PVCStudioPro.Core.Services.Implementations;
using Xunit;

namespace PVCStudioPro.Tests
{
    public class SecurityServiceTests
    {
        [Fact]
        public void SecureWipeArray_ShouldZeroizeAllBytesInBuffer()
        {
            var security = new SecurityService();
            byte[] buffer = new byte[] { 0x41, 0x42, 0x43, 0xFF, 0xDE, 0xAD, 0xBE, 0xEF };

            security.SecureWipeArray(buffer);

            buffer.Should().OnlyContain(b => b == 0);
        }

        [Fact]
        public void SecureWipeMemory_ShouldZeroizeUnmanagedBuffer()
        {
            var security = new SecurityService();
            int size = 1024;
            IntPtr nativeBuffer = Marshal.AllocHGlobal(size);
            try
            {
                // Fill with non-zero bytes
                byte[] pattern = new byte[size];
                Array.Fill<byte>(pattern, 0x55);
                Marshal.Copy(pattern, 0, nativeBuffer, size);

                // Zeroize
                security.SecureWipeMemory(nativeBuffer, size);

                // Read back
                byte[] verify = new byte[size];
                Marshal.Copy(nativeBuffer, verify, 0, size);
                verify.Should().OnlyContain(b => b == 0);
            }
            finally
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
    }
}
