//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PosKernel.Host
{
    /// <summary>
    /// Direct P/Invoke bindings to the POS Kernel Rust library.
    /// This class provides the low-level FFI interface to the native kernel functions.
    /// </summary>
    internal static class RustNative
    {
        private const string LIB = "pos_kernel"; // Ensure pos_kernel.dll/.so/.dylib is alongside executable

        // Win32-style handle type
        internal const ulong PK_INVALID_HANDLE = 0;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PkResult 
        { 
            public int code; 
            public int reserved; 
        }

        // === TERMINAL MANAGEMENT OPERATIONS ===
        
        [DllImport(LIB, EntryPoint = "pk_initialize_terminal", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_initialize_terminal(
            byte[] terminalId, 
            UIntPtr terminalIdLen);

        [DllImport(LIB, EntryPoint = "pk_get_terminal_info", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_get_terminal_info(
            byte[]? buffer,
            UIntPtr bufferSize,
            out UIntPtr requiredSize);

        [DllImport(LIB, EntryPoint = "pk_shutdown_terminal", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_shutdown_terminal();

        // === CORE TRANSACTION OPERATIONS ===
        
        [DllImport(LIB, EntryPoint = "pk_begin_transaction", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_begin_transaction(
            byte[] store, UIntPtr storeLen,
            byte[] currency, UIntPtr currencyLen,
            out ulong handle);

        [DllImport(LIB, EntryPoint = "pk_close_transaction", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_close_transaction(ulong handle);

        [DllImport(LIB, EntryPoint = "pk_add_line", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_add_line(
            ulong handle,
            byte[] sku, UIntPtr skuLen,
            int qty,
            long unitMinor);

        [DllImport(LIB, EntryPoint = "pk_add_cash_tender", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_add_cash_tender(ulong handle, long amountMinor);

        // === QUERY/INSPECTION OPERATIONS ===

        [DllImport(LIB, EntryPoint = "pk_get_totals", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_get_totals(
            ulong handle,
            out long totalMinor,
            out long tenderedMinor,
            out long changeMinor,
            out int state);

        [DllImport(LIB, EntryPoint = "pk_get_line_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_get_line_count(ulong handle, out uint count);

        [DllImport(LIB, EntryPoint = "pk_get_store_name", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_get_store_name(
            ulong handle,
            byte[]? buffer,
            UIntPtr bufferSize,
            out UIntPtr requiredSize);

        [DllImport(LIB, EntryPoint = "pk_get_currency", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_get_currency(
            ulong handle,
            byte[]? buffer,
            UIntPtr bufferSize,
            out UIntPtr requiredSize);

        // === CURRENCY UTILITY FUNCTIONS ===

        [DllImport(LIB, EntryPoint = "pk_is_standard_currency", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool pk_is_standard_currency(
            byte[] currency, 
            UIntPtr currencyLen);

        [DllImport(LIB, EntryPoint = "pk_get_currency_decimal_places", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_get_currency_decimal_places(
            ulong handle, 
            out byte decimalPlaces);

        [DllImport(LIB, EntryPoint = "pk_validate_currency_code", CallingConvention = CallingConvention.Cdecl)]
        internal static extern PkResult pk_validate_currency_code(
            byte[] currency,
            UIntPtr currencyLen);

        // === UTILITY FUNCTIONS ===

        [DllImport(LIB, EntryPoint = "pk_get_version", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr pk_get_version();

        [DllImport(LIB, EntryPoint = "pk_result_is_ok", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool pk_result_is_ok(PkResult result);

        [DllImport(LIB, EntryPoint = "pk_result_get_code", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int pk_result_get_code(PkResult result);

        // === HELPER METHODS ===

        internal static string PtrToStringUTF8(IntPtr ptr) => Marshal.PtrToStringUTF8(ptr) ?? string.Empty;

        /// <summary>
        /// Initialize terminal with specific ID for multi-process coordination
        /// </summary>
        internal static void InitializeTerminal(string terminalId)
        {
            if (string.IsNullOrEmpty(terminalId))
            {
                throw new ArgumentException("Terminal ID cannot be null or empty", nameof(terminalId));
            }

            var terminalIdBytes = Encoding.UTF8.GetBytes(terminalId);
            var result = pk_initialize_terminal(terminalIdBytes, (UIntPtr)terminalIdBytes.Length);
            
            if (!pk_result_is_ok(result))
            {
                var errorCode = pk_result_get_code(result);
                var errorMessage = errorCode switch
                {
                    2 => $"Terminal '{terminalId}' is already in use by another process",
                    3 => $"Invalid terminal ID format: '{terminalId}'",
                    _ => $"Failed to initialize terminal '{terminalId}': error code {errorCode}"
                };
                
                throw new InvalidOperationException(errorMessage);
            }
        }

        /// <summary>
        /// Get information about the current terminal
        /// </summary>
        internal static string GetTerminalInfo()
        {
            // First call to get required size
            var result = pk_get_terminal_info(null, UIntPtr.Zero, out var requiredSize);
            
            if (result.code != 0 && result.code != 4) // 4 = InsufficientBuffer
            {
                throw new InvalidOperationException($"Failed to get terminal info length: {result.code}");
            }

            if ((int)requiredSize == 0)
            {
                return string.Empty;
            }

            // Second call with proper buffer
            var buffer = new byte[(int)requiredSize];
            result = pk_get_terminal_info(buffer, (UIntPtr)buffer.Length, out _);
            
            if (result.code != 0)
            {
                throw new InvalidOperationException($"Failed to get terminal info: {result.code}");
            }

            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Gracefully shutdown the terminal
        /// </summary>
        internal static void ShutdownTerminal()
        {
            var result = pk_shutdown_terminal();
            
            if (!pk_result_is_ok(result))
            {
                var errorCode = pk_result_get_code(result);
                throw new InvalidOperationException($"Failed to shutdown terminal: error code {errorCode}");
            }
        }

        /// <summary>
        /// Win32-style string retrieval for store name
        /// </summary>
        internal static string GetStoreName(ulong handle)
        {
            // First call to get required size
            var result = pk_get_store_name(handle, null, UIntPtr.Zero, out var requiredSize);
            
            if (result.code != 0 && result.code != 4) // 4 = InsufficientBuffer
            {
                throw new InvalidOperationException($"Failed to get store name length: {result.code}");
            }

            if ((int)requiredSize == 0)
            {
                return string.Empty;
            }

            // Second call with proper buffer
            var buffer = new byte[(int)requiredSize];
            result = pk_get_store_name(handle, buffer, (UIntPtr)buffer.Length, out _);
            
            if (result.code != 0)
            {
                throw new InvalidOperationException($"Failed to get store name: {result.code}");
            }

            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Win32-style string retrieval for currency
        /// </summary>
        internal static string GetCurrency(ulong handle)
        {
            // First call to get required size
            var result = pk_get_currency(handle, null, UIntPtr.Zero, out var requiredSize);
            
            if (result.code != 0 && result.code != 4) // 4 = InsufficientBuffer
            {
                throw new InvalidOperationException($"Failed to get currency length: {result.code}");
            }

            if ((int)requiredSize == 0)
            {
                return string.Empty;
            }

            // Second call with proper buffer
            var buffer = new byte[(int)requiredSize];
            result = pk_get_currency(handle, buffer, (UIntPtr)buffer.Length, out _);
            
            if (result.code != 0)
            {
                throw new InvalidOperationException($"Failed to get currency: {result.code}");
            }

            return Encoding.UTF8.GetString(buffer);
        }
    }
}
