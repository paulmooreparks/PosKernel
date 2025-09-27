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
use rust_decimal::Decimal;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct LineItem {
    pub line_number: u32,
    pub product_id: String,
    pub quantity: i32,
    pub unit_price: Decimal,
    pub extended_price: Decimal,
    pub parent_line_item_id: Option<u32>, // NRF linked item support
    // NO preparation_notes field - this is NOT a kernel concept
}

#[derive(Debug, Clone)]
struct Line {
    sku: String,
    qty: i32,
    unit_minor: i64,
    parent_line_item_id: Option<u32>, // ARCHITECTURAL FIX: Add NRF parent support
}

impl Line {
    fn new(sku: String, qty: i32, unit_minor: i64, parent_line_item_id: Option<u32>) -> Self {
        Self { sku, qty, unit_minor, parent_line_item_id }
    }
    
    fn total_minor(&self) -> i64 {
        self.unit_minor * self.qty as i64
    }
    
    // ARCHITECTURAL FIX: Use this method to set parent relationships
    fn set_parent_line_item_id(&mut self, parent_id: Option<u32>) {
        self.parent_line_item_id = parent_id;
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
        self.lines.push(Line::new(sku, qty, unit_minor, None));
    }
    
    fn add_line_with_parent(&mut self, sku: String, qty: i32, unit_minor: i64, parent_line_item_id: Option<u32>) {
        self.lines.push(Line::new(sku, qty, unit_minor, parent_line_item_id));
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

    // ARCHITECTURAL PRINCIPLE: Implement NRF-compliant void cascade
    pub fn void_line_item_with_children(&mut self, line_number: u32, reason: &str) -> Result<(), String> {
        // 1. Find all child items recursively
        let children = self.find_all_children(line_number);
        
        // 2. Void children first (reverse hierarchy order)
        for child_line_number in children.iter().rev() {
            self.void_single_line_item(*child_line_number, &format!("Parent voided: {}", reason))?;
        }
        
        // 3. Void parent item
        self.void_single_line_item(line_number, reason)?;
        
        Ok(())
    }
    
    fn find_all_children(&self, parent_line_number: u32) -> Vec<u32> {
        let mut children = Vec::new();
        
        // Find direct children by checking parent_line_item_id
        for (index, item) in self.lines.iter().enumerate() {
            if item.parent_line_item_id == Some(parent_line_number) {
                let child_line_number = (index + 1) as u32; // Convert to 1-based line number
                children.push(child_line_number);
                // Recursively find grandchildren
                children.extend(self.find_all_children(child_line_number));
            }
        }
        
        children
    }
    
    fn void_single_line_item(&mut self, line_number: u32, reason: &str) -> Result<(), String> {
        // Implementation for voiding a single line item
        // This would mark the item as voided and adjust totals
        // Placeholder for now
        Ok(())
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
    
    fn add_line_with_parent_legal(&mut self, handle: u64, sku: String, qty: i32, unit_minor: i64, parent_line_item_id: Option<u32>) -> Result<(), String> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        if tx.state != TxState::Building {
            return Err("Transaction not in building state".to_string());
        }
        
        tx.add_line_with_parent(sku, qty, unit_minor, parent_line_item_id);
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
    
    fn get_line_item_legal(&self, handle: u64, line_index: u32) -> Result<(String, i32, i64, Option<u32>), String> {
        let tx = self.active_transactions.get(&handle)
            .ok_or("Transaction not found")?;
            
        if line_index >= tx.lines.len() as u32 {
            return Err("Line index out of bounds".to_string());
        }
        
        let line = &tx.lines[line_index as usize];
        Ok((line.sku.clone(), line.qty, line.unit_minor, line.parent_line_item_id))
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
pub extern "C" fn pk_add_line_with_parent(
    handle: PkTransactionHandle,
    sku_ptr: *const u8,
    sku_len: usize,
    qty: i32,
    unit_minor: i64,
    parent_line_item_id: u32  // 0 means no parent
) -> PkResult {
    if handle == PK_INVALID_HANDLE || sku_ptr.is_null() || sku_len == 0 || qty <= 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let sku = unsafe { read_str(sku_ptr, sku_len) };
    let parent_id = if parent_line_item_id == 0 { None } else { Some(parent_line_item_id) };
    
    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.add_line_with_parent_legal(handle, sku, qty, unit_minor, parent_id) {
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

#[no_mangle]
pub extern "C" fn pk_get_line_item(
    handle: PkTransactionHandle,
    line_index: u32,
    out_sku_ptr: *mut u8,
    out_sku_len: *mut usize,
    out_qty: *mut i32,
    out_unit_minor: *mut i64
) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_sku_ptr.is_null() || out_sku_len.is_null() || out_qty.is_null() || out_unit_minor.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let kernel_store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };
    
    match kernel_store.get_line_item_legal(handle, line_index) {
        Ok((sku, qty, unit_minor, _parent_id)) => {
            let sku_bytes = sku.as_bytes();
            let buffer_size = unsafe { *out_sku_len };
            
            if sku_bytes.len() > buffer_size {
                return PkResult::err(ResultCode::InsufficientBuffer);
            }
            
            unsafe {
                std::ptr::copy_nonoverlapping(sku_bytes.as_ptr(), out_sku_ptr, sku_bytes.len());
                *out_sku_len = sku_bytes.len();
                *out_qty = qty;
                *out_unit_minor = unit_minor;
            }
            
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}
