using System.Runtime.InteropServices;
using System.Text;

internal static class RustNative
{
    private const string LIB = "pos_kernel"; // Ensure pos_kernel.dll/.so/.dylib is alongside executable

    // Win32-style handle type
    internal const ulong PK_INVALID_HANDLE = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PkResult { public int code; public int reserved; }

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
