/*
 * Copyright 2025 Paul Moore Parks and contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

//! POS Kernel Rust Implementation - Minimal Working Version
//! Focus: Get the Rust service compiling and running with basic functionality

use std::collections::HashMap;
use std::sync::{Arc, Mutex, OnceLock, RwLock};
use std::time::{SystemTime, UNIX_EPOCH};
use std::sync::atomic::{AtomicU64, Ordering};

// === RESULT CODES ===

#[repr(i32)]
pub enum ResultCode {
    Ok = 0,
    NotFound = 1,
    InvalidState = 2,
    ValidationFailed = 3,
    InsufficientBuffer = 4,
    TimedOut = 5,
    InternalError = 255
}

#[repr(C)]
pub struct PkResult {
    pub code: i32,
    pub reserved: i32
}

impl PkResult {
    fn ok() -> Self { Self { code: 0, reserved: 0 } }
    fn err(c: ResultCode) -> Self { Self { code: c as i32, reserved: 0 } }
}

pub type PkTransactionHandle = u64;
pub const PK_INVALID_HANDLE: PkTransactionHandle = 0;

// === BASIC DATA TYPES ===

#[derive(Debug, Clone)]
pub struct Currency {
    code: String,
    decimal_places: u8,
}

impl Currency {
    fn new(code: &str) -> Result<Self, &'static str> {
        let code_upper = code.to_uppercase();
        let decimal_places = match code_upper.as_str() {
            "JPY" => 0,
            "USD" | "EUR" | "GBP" | "CAD" | "AUD" | "SGD" => 2,
            _ => 2, // Default to 2 decimal places
        };
        
        Ok(Currency {
            code: code_upper,
            decimal_places,
        })
    }
    
    fn decimal_places(&self) -> u8 {
        self.decimal_places
    }
}

#[derive(Debug, Clone)]
struct Line {
    sku: String,
    qty: i32,
    unit_minor: i64,
}

impl Line {
    fn new(sku: String, qty: i32, unit_minor: i64) -> Self {
        Self { sku, qty, unit_minor }
    }
    
    fn total_minor(&self) -> i64 {
        self.unit_minor * self.qty as i64
    }
}

#[derive(Debug, Clone, PartialEq)]
pub enum TxState {
    Building,
    Committed,
}

#[derive(Debug)]
struct Transaction {
    id: u64,
    store: String,
    currency: Currency,
    lines: Vec<Line>,
    tendered_minor: i64,
    state: TxState,
}

impl Transaction {
    fn new(id: u64, store: String, currency: Currency) -> Self {
        Self {
            id,
            store,
            currency,
            lines: Vec::new(),
            tendered_minor: 0,
            state: TxState::Building,
        }
    }
    
    fn total_minor(&self) -> i64 {
        self.lines.iter().map(|l| l.total_minor()).sum()
    }
    
    fn change_minor(&self) -> i64 {
        (self.tendered_minor - self.total_minor()).max(0)
    }
    
    fn add_line(&mut self, sku: String, qty: i32, unit_minor: i64) {
        self.lines.push(Line::new(sku, qty, unit_minor));
    }
    
    fn add_tender(&mut self, amount_minor: i64) {
        self.tendered_minor += amount_minor;
        if self.tendered_minor >= self.total_minor() {
            self.state = TxState::Committed;
        }
    }
    
    fn line_count(&self) -> u32 {
        self.lines.len() as u32
    }
}

// === KERNEL STORE ===

pub struct LegalKernelStore {
    next_tx_id: AtomicU64,
    active_transactions: HashMap<u64, Transaction>,
}

impl LegalKernelStore {
    fn new() -> Self {
        Self {
            next_tx_id: AtomicU64::new(1),
            active_transactions: HashMap::new(),
        }
    }
    
    fn begin_transaction_legal(&mut self, store: String, currency: Currency) -> Result<u64, String> {
        let id = self.next_tx_id.fetch_add(1, Ordering::SeqCst);
        let transaction = Transaction::new(id, store, currency);
        self.active_transactions.insert(id, transaction);
        Ok(id)
    }
    
    fn add_line_legal(&mut self, handle: u64, sku: String, qty: i32, unit_minor: i64) -> Result<(), String> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        if tx.state != TxState::Building {
            return Err("Transaction not in building state".to_string());
        }
        
        tx.add_line(sku, qty, unit_minor);
        Ok(())
    }
    
    fn add_cash_tender_legal(&mut self, handle: u64, amount_minor: i64) -> Result<(), String> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        if tx.state != TxState::Building {
            return Err("Transaction not in building state".to_string());
        }
        
        tx.add_tender(amount_minor);
        Ok(())
    }
    
    fn get_transaction_totals(&self, handle: u64) -> Result<(i64, i64, i64, u32), String> {
        let tx = self.active_transactions.get(&handle)
            .ok_or("Transaction not found")?;
        
        let state_code = match tx.state {
            TxState::Building => 0,
            TxState::Committed => 1,
        };
        
        Ok((tx.total_minor(), tx.tendered_minor, tx.change_minor(), state_code))
    }
    
    fn get_line_count_legal(&self, handle: u64) -> Result<u32, String> {
        let tx = self.active_transactions.get(&handle)
            .ok_or("Transaction not found")?;
        Ok(tx.line_count())
    }
    
    fn get_currency_decimal_places(&self, handle: u64) -> Result<u8, String> {
        let tx = self.active_transactions.get(&handle)
            .ok_or("Transaction not found")?;
        Ok(tx.currency.decimal_places())
    }
}

// === GLOBAL STORE ===

static LEGAL_KERNEL_STORE: OnceLock<RwLock<LegalKernelStore>> = OnceLock::new();

fn legal_kernel_store() -> &'static RwLock<LegalKernelStore> {
    LEGAL_KERNEL_STORE.get_or_init(|| {
        let store = LegalKernelStore::new();
        RwLock::new(store)
    })
}

// === UTILITY FUNCTIONS ===

unsafe fn read_str(ptr: *const u8, len: usize) -> String {
    if ptr.is_null() || len == 0 {
        return String::new();
    }
    
    let slice = std::slice::from_raw_parts(ptr, len);
    String::from_utf8_lossy(slice).into_owned()
}

// === FFI FUNCTIONS ===

#[no_mangle]
pub extern "C" fn pk_result_is_ok(result: PkResult) -> bool {
    result.code == 0
}

#[no_mangle]
pub extern "C" fn pk_result_get_code(result: PkResult) -> i32 {
    result.code
}

#[no_mangle]
pub extern "C" fn pk_get_version() -> *const std::os::raw::c_char {
    static VERSION: &[u8] = b"0.4.0-minimal\0";
    VERSION.as_ptr() as *const std::os::raw::c_char
}

#[no_mangle]
pub extern "C" fn pk_initialize_terminal(
    terminal_id_ptr: *const u8,
    terminal_id_len: usize
) -> PkResult {
    if terminal_id_ptr.is_null() || terminal_id_len == 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let _terminal_id = unsafe { read_str(terminal_id_ptr, terminal_id_len) };
    
    // Initialize the kernel store
    let _ = legal_kernel_store();
    
    PkResult::ok()
}

#[no_mangle]
pub extern "C" fn pk_begin_transaction(
    store_ptr: *const u8,
    store_len: usize,
    currency_ptr: *const u8,
    currency_len: usize,
    out_handle: *mut PkTransactionHandle
) -> PkResult {
    if store_ptr.is_null() || store_len == 0 || currency_ptr.is_null() || currency_len == 0 || out_handle.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store = unsafe { read_str(store_ptr, store_len) };
    let currency_code = unsafe { read_str(currency_ptr, currency_len) };
    
    let currency = match Currency::new(&currency_code) {
        Ok(c) => c,
        Err(_) => return PkResult::err(ResultCode::ValidationFailed)
    };
    
    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.begin_transaction_legal(store, currency) {
        Ok(handle) => {
            unsafe { *out_handle = handle; }
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::InternalError)
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
    if handle == PK_INVALID_HANDLE || sku_ptr.is_null() || sku_len == 0 || qty <= 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let sku = unsafe { read_str(sku_ptr, sku_len) };
    
    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.add_line_legal(handle, sku, qty, unit_minor) {
        Ok(_) => PkResult::ok(),
        Err(_) => PkResult::err(ResultCode::InternalError)
    }
}

#[no_mangle]
pub extern "C" fn pk_add_cash_tender(
    handle: PkTransactionHandle,
    amount_minor: i64
) -> PkResult {
    if handle == PK_INVALID_HANDLE || amount_minor <= 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.add_cash_tender_legal(handle, amount_minor) {
        Ok(_) => PkResult::ok(),
        Err(_) => PkResult::err(ResultCode::InternalError)
    }
}

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
    
    let kernel_store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.get_transaction_totals(handle) {
        Ok((total, tendered, change, state)) => {
            unsafe {
                *out_total = total;
                *out_tendered = tendered;
                *out_change = change;
                *out_state = state as i32;
            }
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}

#[no_mangle]
pub extern "C" fn pk_get_line_count(
    handle: PkTransactionHandle,
    out_count: *mut u32
) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_count.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let kernel_store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.get_line_count_legal(handle) {
        Ok(count) => {
            unsafe { *out_count = count; }
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}

#[no_mangle]
pub extern "C" fn pk_get_currency_decimal_places(
    handle: PkTransactionHandle,
    out_decimal_places: *mut u8
) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_decimal_places.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let kernel_store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.get_currency_decimal_places(handle) {
        Ok(decimal_places) => {
            unsafe { *out_decimal_places = decimal_places; }
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}
