//! POS Kernel Rust Prototype FFI Implementation (Iteration 2)
//! Win32-style C ABI that's OOP-ready: opaque handles, consistent patterns, resource management.
//!
//! DEVELOPMENT NOTES:
//! - Git repo moved from pos-kernel-rs/ to solution root
//! - Unnecessary .NET assemblies (Services, Runtime, AppHost) removed
//! - Mixed-mode debugging configured for Rust ↔ .NET
//! - Comprehensive documentation structure in docs/
//! - GitHub repo: https://github.com/paulmooreparks/PosKernel

use std::collections::HashMap;
use std::sync::{Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};

#[derive(Debug)]
struct Line { sku: String, qty: i32, unit_minor: i64 }

#[derive(Debug, PartialEq, Eq)]
enum TxState { Building, Completed }

#[derive(Debug)]
struct Transaction {
    id: u64,
    store: String,
    currency: String,
    lines: Vec<Line>,
    tendered_minor: i64,
    state: TxState,
}

impl Transaction {
    fn new(id: u64, store: String, currency: String) -> Self { 
        Self { id, store, currency, lines: Vec::new(), tendered_minor: 0, state: TxState::Building } 
    }

    fn total_minor(&self) -> i64 {
        self.lines.iter().map(|l| l.unit_minor * l.qty as i64).sum()
    }

    fn change_minor(&self) -> i64 {
        (self.tendered_minor - self.total_minor()).max(0)
    }

    fn line_count(&self) -> u32 {
        self.lines.len() as u32
    }
}

struct Store {
    next_id: u64,
    tx: HashMap<u64, Transaction>
}

static STORE: OnceLock<Mutex<Store>> = OnceLock::new();

fn store() -> &'static Mutex<Store> {
    STORE.get_or_init(|| Mutex::new(
        Store {
            next_id: 1,
            tx: HashMap::new()
        }
    ))
}

fn gen_id(st: &mut Store) -> u64 {
    let id = st.next_id; st.next_id += 1; id
}

// Win32-style result codes - consistent across all operations
#[repr(i32)]
pub enum ResultCode { 
    Ok = 0, 
    NotFound = 1, 
    InvalidState = 2, 
    ValidationFailed = 3, 
    InsufficientBuffer = 4,
    InternalError = 255 
}

#[repr(C)]
pub struct PkResult {
    pub code: i32,
    pub reserved: i32
}

impl PkResult { 
    fn ok() -> Self {
        Self {
            code: ResultCode::Ok as i32,
            reserved: 0
        }
    }

    fn err(c: ResultCode) -> Self {
        Self {
            code: c as i32,
            reserved: 0
        }
    } 
}

// Handle types - Win32 style with type safety hints
pub type PkTransactionHandle = u64;
pub const PK_INVALID_HANDLE: PkTransactionHandle = 0;

unsafe fn read_str(ptr: *const u8, len: usize) -> String {
    if ptr.is_null() || len == 0 {
        return String::new();
    }

    let slice = unsafe {
        std::slice::from_raw_parts(ptr, len)
    };

    String::from_utf8_lossy(slice).into_owned()
}

unsafe fn write_str(s: &str, buffer: *mut u8, buffer_size: usize, out_required: *mut usize) -> PkResult {
    if !out_required.is_null() {
        *out_required = s.len();
    }
    
    if buffer.is_null() || buffer_size == 0 {
        return if s.len() == 0 {
            PkResult::ok()
        }
        else {
            PkResult::err(ResultCode::InsufficientBuffer)
        };
    }
    
    if s.len() > buffer_size {
        return PkResult::err(ResultCode::InsufficientBuffer);
    }
    
    let src_bytes = s.as_bytes();
    std::ptr::copy_nonoverlapping(src_bytes.as_ptr(), buffer, src_bytes.len());
    
    PkResult::ok()
}

fn now_nanos() -> u128 {
    SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos()
}

// === CORE TRANSACTION OPERATIONS ===

#[no_mangle]
pub extern "C" fn pk_begin_transaction(
    store_ptr: *const u8, 
    store_len: usize, 
    currency_ptr: *const u8, 
    currency_len: usize, 
    out_handle: *mut PkTransactionHandle
) -> PkResult {
    if out_handle.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store_name = unsafe {
        read_str(store_ptr, store_len)
    };

    let currency = unsafe {
        read_str(currency_ptr, currency_len)
    };

    if currency.is_empty() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    let id = gen_id(&mut *guard);
    guard.tx.insert(id, Transaction::new(id, store_name, currency));
    
    unsafe { *out_handle = id; }
    PkResult::ok()
}

#[no_mangle]
pub extern "C" fn pk_close_transaction(handle: PkTransactionHandle) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    match guard.tx.remove(&handle) {
        Some(_) => PkResult::ok(),
        None => PkResult::err(ResultCode::NotFound)
    }
}

#[no_mangle]
pub extern "C" fn pk_add_line(
    handle: PkTransactionHandle, 
    sku_ptr: *const u8, 
    sku_len: usize, 
    qty: i32, 
    unit_minor: i64
) -> PkResult {
    if handle == PK_INVALID_HANDLE || qty <= 0 || unit_minor < 0 { 
        return PkResult::err(ResultCode::ValidationFailed); 
    }
    
    let sku = unsafe {
        read_str(sku_ptr, sku_len)
    };

    if sku.is_empty() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    let tx = match guard.tx.get_mut(&handle) { 
        Some(t) => t, 
        None => return PkResult::err(ResultCode::NotFound) 
    };
    
    if tx.state != TxState::Building {
        return PkResult::err(ResultCode::InvalidState);
    }
    
    tx.lines.push(Line { sku, qty, unit_minor });
    PkResult::ok()
}

#[no_mangle]
pub extern "C" fn pk_add_cash_tender(handle: PkTransactionHandle, amount_minor: i64) -> PkResult {
    if handle == PK_INVALID_HANDLE || amount_minor <= 0 { 
        return PkResult::err(ResultCode::ValidationFailed); 
    }
    
    let mut guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    let tx = match guard.tx.get_mut(&handle) { 
        Some(t) => t, 
        None => return PkResult::err(ResultCode::NotFound) 
    };
    
    if tx.state != TxState::Building {
        return PkResult::err(ResultCode::InvalidState);
    }
    
    tx.tendered_minor += amount_minor;

    if tx.tendered_minor >= tx.total_minor() {
        tx.state = TxState::Completed;
    }

    PkResult::ok()
}

// === QUERY/INSPECTION OPERATIONS (Win32 pattern: separate calls for different data) ===

#[no_mangle]
pub extern "C" fn pk_get_totals(
    handle: PkTransactionHandle, 
    out_total: *mut i64, 
    out_tendered: *mut i64, 
    out_change: *mut i64, 
    out_state: *mut i32
) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_total.is_null() || out_tendered.is_null() || out_change.is_null() || out_state.is_null() { 
        return PkResult::err(ResultCode::ValidationFailed); 
    }
    
    let guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    let tx = match guard.tx.get(&handle) { 
        Some(t) => t, 
        None => return PkResult::err(ResultCode::NotFound) 
    };
    
    unsafe {
        *out_total = tx.total_minor();
        *out_tendered = tx.tendered_minor;
        *out_change = tx.change_minor();
        *out_state = match tx.state {
            TxState::Building => 0, TxState::Completed => 1
        };
    }
    
    PkResult::ok()
}

// Win32-style property getters - allows OOP wrappers to expose as properties
#[no_mangle]
pub extern "C" fn pk_get_line_count(handle: PkTransactionHandle, out_count: *mut u32) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_count.is_null() { 
        return PkResult::err(ResultCode::ValidationFailed); 
    }
    
    let guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    let tx = match guard.tx.get(&handle) { 
        Some(t) => t, 
        None => return PkResult::err(ResultCode::NotFound) 
    };
    
    unsafe {
        *out_count = tx.line_count();
    }

    PkResult::ok()
}

#[no_mangle]
pub extern "C" fn pk_get_store_name(
    handle: PkTransactionHandle,
    buffer: *mut u8,
    buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    let tx = match guard.tx.get(&handle) { 
        Some(t) => t, 
        None => return PkResult::err(ResultCode::NotFound) 
    };
    
    unsafe {
        write_str(&tx.store, buffer, buffer_size, out_required_size)
    }
}

#[no_mangle]
pub extern "C" fn pk_get_currency(
    handle: PkTransactionHandle,
    buffer: *mut u8,
    buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let guard = match store().lock() { 
        Ok(g) => g, 
        Err(_) => return PkResult::err(ResultCode::InternalError) 
    };
    
    let tx = match guard.tx.get(&handle) { 
        Some(t) => t, 
        None => return PkResult::err(ResultCode::NotFound) 
    };
    
    unsafe {
        write_str(&tx.currency, buffer, buffer_size, out_required_size)
    }
}

// === UTILITY FUNCTIONS ===

#[no_mangle]
pub extern "C" fn pk_get_version() -> *const u8 {
    println!("DEBUG: pk_get_version called");
    static VERSION: &str = concat!("0.1.0","\0");
    VERSION.as_ptr()
}

#[no_mangle]
pub extern "C" fn pk_now_nanos() -> u128 {
    now_nanos()
}

// === ERROR HANDLING UTILITIES ===

#[no_mangle]
pub extern "C" fn pk_result_is_ok(result: PkResult) -> bool {
    result.code == ResultCode::Ok as i32
}

#[no_mangle]
pub extern "C" fn pk_result_get_code(result: PkResult) -> i32 {
    result.code
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_transaction_lifecycle() {
        let mut handle = PK_INVALID_HANDLE;
        let store = "TEST-STORE";
        let currency = "USD";
        
        // Begin transaction
        let result = pk_begin_transaction(
            store.as_ptr(), store.len(),
            currency.as_ptr(), currency.len(),
            &mut handle
        );

        assert_eq!(result.code, ResultCode::Ok as i32);
        assert_ne!(handle, PK_INVALID_HANDLE);
        
        // Add line
        let sku = "TEST-SKU";
        let result = pk_add_line(handle, sku.as_ptr(), sku.len(), 1, 100);
        assert_eq!(result.code, ResultCode::Ok as i32);
        
        // Close transaction
        let result = pk_close_transaction(handle);
        assert_eq!(result.code, ResultCode::Ok as i32);
    }
}
