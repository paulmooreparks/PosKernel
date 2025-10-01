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
use std::sync::{OnceLock, RwLock};
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
#[derive(Copy, Clone)]
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
    #[allow(dead_code)] // Stored for future audit and display purposes
    code: String,
    decimal_places: u8,
}

impl Currency {
    fn new(code: &str, decimal_places: u8) -> Result<Self, &'static str> {
        let code_upper = code.to_uppercase();

        // ARCHITECTURAL PRINCIPLE: Kernel is culture-neutral - client provides decimal places
        // Currency formatting and rules are user-space concerns, not kernel concerns
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
    #[allow(dead_code)] // Stored for audit trail and transaction display
    sku: String,
    qty: i32,
    unit_minor: i64,
    // NRF COMPLIANCE: Support linked items (parent-child relationships) ONLY
    parent_line_item_id: Option<u32>,
    // ARCHITECTURAL ENHANCEMENT: Product metadata for AI display
    product_name: String,
    product_description: String,
}

impl Line {
    fn new(sku: String, qty: i32, unit_minor: i64) -> Self {
        Self {
            sku,
            qty,
            unit_minor,
            parent_line_item_id: None, // Initialize as top-level item
            product_name: String::new(), // Will be populated by extension service
            product_description: String::new(), // Will be populated by extension service
        }
    }

    // Constructor with product metadata for enhanced protocol
    fn new_with_metadata(sku: String, qty: i32, unit_minor: i64, product_name: String, product_description: String) -> Self {
        Self {
            sku,
            qty,
            unit_minor,
            parent_line_item_id: None,
            product_name,
            product_description,
        }
    }

    // Constructor for child items with parent reference
    fn new_with_parent(sku: String, qty: i32, unit_minor: i64, parent_line_id: u32) -> Self {
        Self {
            sku,
            qty,
            unit_minor,
            parent_line_item_id: Some(parent_line_id),
            product_name: String::new(), // Will be populated by extension service
            product_description: String::new(), // Will be populated by extension service
        }
    }

    fn total_minor(&self) -> i64 {
        self.unit_minor * self.qty as i64
    }

    fn get_parent_line_item_id(&self) -> Option<u32> {
        self.parent_line_item_id
    }

    #[allow(dead_code)] // TODO: Will be used when HTTP service supports parent relationship updates
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
    #[allow(dead_code)] // Stored for audit trail and transaction identification
    id: u64,
    #[allow(dead_code)] // Stored for transaction context and audit purposes
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

    // NRF COMPLIANCE: Add child item with parent reference
    fn add_child_line(&mut self, sku: String, qty: i32, unit_minor: i64, parent_line_id: u32) -> Result<(), String> {
        // Validate parent exists
        if parent_line_id == 0 || parent_line_id as usize > self.lines.len() {
            return Err("Invalid parent line item ID".to_string());
        }

        self.lines.push(Line::new_with_parent(sku, qty, unit_minor, parent_line_id));
        Ok(())
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

    // NRF COMPLIANCE: Find all child items recursively for void cascade
    fn find_all_children(&self, parent_line_number: u32) -> Vec<u32> {
        let mut children = Vec::new();

        // Find direct children
        for (index, line) in self.lines.iter().enumerate() {
            if let Some(parent_id) = line.get_parent_line_item_id() {
                if parent_id == parent_line_number {
                    let child_line_number = (index + 1) as u32; // 1-based line numbers
                    children.push(child_line_number);

                    // Recursively find grandchildren
                    let grandchildren = self.find_all_children(child_line_number);
                    children.extend(grandchildren);
                }
            }
        }

        children
    }

    // NRF COMPLIANCE: Get parent line item ID for a given line
    fn get_line_parent_id(&self, line_index: u32) -> Option<u32> {
        if line_index == 0 || line_index as usize > self.lines.len() {
            return None;
        }

        self.lines[(line_index - 1) as usize].get_parent_line_item_id()
    }

    // NRF COMPLIANCE: Void a single line item (used by cascade)
    fn void_single_line_item(&mut self, line_number: u32, _reason: &str) -> Result<(), String> {
        if line_number == 0 || line_number as usize > self.lines.len() {
            return Err("Invalid line number".to_string());
        }

        let line_index = (line_number - 1) as usize;
        // For now, just mark as voided (would need voided flag in real implementation)
        // TODO: Add voided flag to Line struct for proper void tracking
        // TODO: Use _reason parameter for audit logging when void tracking is implemented
        self.lines[line_index].qty = 0; // Simple void implementation

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

    // NRF COMPLIANCE: Add child line item with parent reference
    fn add_child_line_legal(&mut self, handle: u64, sku: String, qty: i32, unit_minor: i64, parent_line_id: u32) -> Result<(), String> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;

        if tx.state != TxState::Building {
            return Err("Transaction not in building state".to_string());
        }

        tx.add_child_line(sku, qty, unit_minor, parent_line_id)
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

    // ARCHITECTURAL FIX: Update get_line_item_details to return parent_line_item_id instead of preparation notes
    fn get_line_item_details(&self, handle: u64, line_index: u32) -> Result<(String, i32, i64, Option<u32>), String> {
        let tx = self.active_transactions.get(&handle)
            .ok_or("Transaction not found")?;

        if line_index as usize >= tx.lines.len() {
            return Err("Line index out of range".to_string());
        }

        let line = &tx.lines[line_index as usize];
        Ok((line.sku.clone(), line.qty, line.unit_minor, line.parent_line_item_id))
    }

    // NRF COMPLIANCE: Get parent line item ID for a given line
    fn get_line_parent_id(&self, handle: u64, line_number: u32) -> Result<Option<u32>, String> {
        let tx = self.active_transactions.get(&handle)
            .ok_or("Transaction not found")?;

        Ok(tx.get_line_parent_id(line_number))
    }

    // NRF COMPLIANCE: Find all children of a line item (for void cascade)
    fn find_line_children(&self, handle: u64, parent_line_number: u32) -> Result<Vec<u32>, String> {
        let tx = self.active_transactions.get(&handle)
            .ok_or("Transaction not found")?;

        Ok(tx.find_all_children(parent_line_number))
    }

    // ARCHITECTURAL COMPONENT: Voids a line item with NRF-compliant cascade to child items.
    // Critical NRF requirement: When parent items are voided, all linked child items must be voided.
    //
    // # Safety
    // The caller must ensure that:
    // - `handle` refers to a valid, active transaction
    // - `line_number` is within the valid range of line items (1-based)
    // - `reason_ptr` points to valid memory containing a UTF-8 encoded reason string
    // - `reason_len` accurately represents the length of the data at `reason_ptr`
    fn void_line_with_cascade(&mut self, handle: u64, line_number: u32, reason: &str) -> Result<(), String> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;

        if tx.state != TxState::Building {
            return Err("Cannot void items in committed transaction".to_string());
        }

        // Find all child items recursively
        let children = tx.find_all_children(line_number);

        // Void children first (reverse hierarchy order)
        for child_line_number in children.iter().rev() {
            tx.void_single_line_item(*child_line_number, &format!("Parent voided: {}", reason))?;
        }

        // Void parent item
        tx.void_single_line_item(line_number, reason)?;

        Ok(())
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

/// ARCHITECTURAL COMPONENT: Gets terminal initialization status and parameters.
///
/// # Safety
/// The caller must ensure that:
/// - `terminal_id_ptr` points to valid memory containing a UTF-8 encoded terminal ID
/// - `terminal_id_len` accurately represents the length of the data at `terminal_id_ptr`
#[no_mangle]
pub unsafe extern "C" fn pk_initialize_terminal(
    terminal_id_ptr: *const u8,
    terminal_id_len: usize
) -> PkResult {
    if terminal_id_ptr.is_null() || terminal_id_len == 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let _terminal_id = read_str(terminal_id_ptr, terminal_id_len);

    // Initialize the kernel store
    let _ = legal_kernel_store();

    PkResult::ok()
}

/// ARCHITECTURAL COMPONENT: Begins a new transaction in the kernel store.
///
/// # Safety
/// The caller must ensure that:
/// - `store_ptr` points to valid memory containing a UTF-8 encoded store name
/// - `store_len` accurately represents the length of the data at `store_ptr`
/// - `currency_ptr` points to valid memory containing a UTF-8 encoded currency code
/// - `currency_len` accurately represents the length of the data at `currency_ptr`
/// - `currency_decimal_places` specifies the decimal places for currency (user-space decision)
/// - `out_handle` points to valid memory where the transaction handle can be written
/// - All pointers remain valid for the duration of this call
#[no_mangle]
pub unsafe extern "C" fn pk_begin_transaction(
    store_ptr: *const u8,
    store_len: usize,
    currency_ptr: *const u8,
    currency_len: usize,
    currency_decimal_places: u8,
    out_handle: *mut PkTransactionHandle
) -> PkResult {
    if store_ptr.is_null() || store_len == 0 || currency_ptr.is_null() || currency_len == 0 || out_handle.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let store = read_str(store_ptr, store_len);
    let currency_code = read_str(currency_ptr, currency_len);

    // ARCHITECTURAL PRINCIPLE: Kernel is culture-neutral - client provides all currency info
    let currency = match Currency::new(&currency_code, currency_decimal_places) {
        Ok(c) => c,
        Err(_) => return PkResult::err(ResultCode::ValidationFailed)
    };

    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };

    match kernel_store.begin_transaction_legal(store, currency) {
        Ok(handle) => {
            *out_handle = handle;
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::InternalError)
    }
}

/// ARCHITECTURAL COMPONENT: Adds a line item to an existing transaction.
///
/// # Safety
/// The caller must ensure that:
/// - `sku_ptr` points to valid memory containing a UTF-8 encoded SKU string
/// - `sku_len` accurately represents the length of the data at `sku_ptr`
/// - The memory pointed to by `sku_ptr` remains valid for the duration of this call
/// - `handle` refers to a valid, active transaction
/// - `qty` is greater than zero
#[no_mangle]
pub unsafe extern "C" fn pk_add_line(
    handle: PkTransactionHandle,
    sku_ptr: *const u8,
    sku_len: usize,
    qty: i32,
    unit_minor: i64
) -> PkResult {
    if handle == PK_INVALID_HANDLE || sku_ptr.is_null() || sku_len == 0 || qty <= 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let sku = read_str(sku_ptr, sku_len);

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

/// ARCHITECTURAL COMPONENT: Retrieves transaction totals and state information.
///
/// # Safety
/// The caller must ensure that:
/// - `handle` refers to a valid, active transaction
/// - `out_total` points to valid memory where the total amount can be written
/// - `out_tendered` points to valid memory where the tendered amount can be written
/// - `out_change` points to valid memory where the change amount can be written
/// - `out_state` points to valid memory where the transaction state can be written
/// - All output pointers remain valid for the duration of this call
#[no_mangle]
pub unsafe extern "C" fn pk_get_totals(
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
            *out_total = total;
            *out_tendered = tendered;
            *out_change = change;
            *out_state = state as i32;
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}

/// ARCHITECTURAL COMPONENT: Retrieves the number of line items in a transaction.
///
/// # Safety
/// The caller must ensure that:
/// - `handle` refers to a valid, active transaction
/// - `out_count` points to valid memory where the line count can be written
/// - The output pointer remains valid for the duration of this call
#[no_mangle]
pub unsafe extern "C" fn pk_get_line_count(
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
            *out_count = count;
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}

/// ARCHITECTURAL COMPONENT: Retrieves the number of decimal places for the transaction's currency.
///
/// # Safety
/// The caller must ensure that:
/// - `handle` refers to a valid, active transaction
/// - `out_decimal_places` points to valid memory where the decimal places value can be written
/// - The output pointer remains valid for the duration of this call
#[no_mangle]
pub unsafe extern "C" fn pk_get_currency_decimal_places(
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
            *out_decimal_places = decimal_places;
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}

/// ARCHITECTURAL COMPONENT: Retrieves details of a specific line item with parent relationship.
/// NRF COMPLIANCE: Returns parent_line_item_id for hierarchical display.
///
/// # Safety
/// The caller must ensure that:
/// - `handle` refers to a valid, active transaction
/// - `line_index` is within the valid range of line items (0 to line_count-1)
/// - `out_sku_ptr` points to valid memory buffer for the SKU string
/// - `out_sku_len` specifies the size of the buffer, receives actual string length
/// - `out_qty`, `out_unit_minor`, and `out_parent_id` point to valid memory for output values
/// - `out_has_parent` points to valid memory for parent existence flag
/// - All output pointers remain valid for the duration of this call
#[no_mangle]
pub unsafe extern "C" fn pk_get_line_item_with_parent(
    handle: PkTransactionHandle,
    line_index: u32,
    out_sku_ptr: *mut u8,
    out_sku_len: *mut usize,
    out_qty: *mut i32,
    out_unit_minor: *mut i64,
    out_parent_id: *mut u32,
    out_has_parent: *mut bool
) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_sku_ptr.is_null() || out_sku_len.is_null() || out_qty.is_null() || out_unit_minor.is_null() || out_parent_id.is_null() || out_has_parent.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let kernel_store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };

    match kernel_store.get_line_item_details(handle, line_index) {
        Ok((sku, qty, unit_minor, parent_id)) => {
            let sku_bytes = sku.as_bytes();
            let buffer_size = *out_sku_len;

            if sku_bytes.len() >= buffer_size {
                // Buffer too small, return required size
                *out_sku_len = sku_bytes.len() + 1; // +1 for null terminator
                return PkResult::err(ResultCode::InsufficientBuffer);
            }

            // Copy SKU to output buffer
            std::ptr::copy_nonoverlapping(sku_bytes.as_ptr(), out_sku_ptr, sku_bytes.len());
            *out_sku_ptr.add(sku_bytes.len()) = 0; // Null terminator

            *out_sku_len = sku_bytes.len();
            *out_qty = qty;
            *out_unit_minor = unit_minor;

            // Set parent information
            if let Some(parent) = parent_id {
                *out_parent_id = parent;
                *out_has_parent = true;
            } else {
                *out_parent_id = 0;
                *out_has_parent = false;
            }

            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}

/// ARCHITECTURAL COMPONENT: Adds a child line item to an existing transaction with parent reference.
/// NRF COMPLIANCE: Supports linked items (parent-child relationships).
///
/// # Safety
/// The caller must ensure that:
/// - `sku_ptr` points to valid memory containing a UTF-8 encoded SKU string
/// - `sku_len` accurately represents the length of the data at `sku_ptr`
/// - The memory pointed to by `sku_ptr` remains valid for the duration of this call
/// - `handle` refers to a valid, active transaction
/// - `qty` is greater than zero
/// - `parent_line_id` is a valid parent line item ID within the transaction
#[no_mangle]
pub unsafe extern "C" fn pk_add_child_line(
    handle: PkTransactionHandle,
    sku_ptr: *const u8,
    sku_len: usize,
    qty: i32,
    unit_minor: i64,
    parent_line_id: u32
) -> PkResult {
    if handle == PK_INVALID_HANDLE || sku_ptr.is_null() || sku_len == 0 || qty <= 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let sku = read_str(sku_ptr, sku_len);

    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };

    match kernel_store.add_child_line_legal(handle, sku, qty, unit_minor, parent_line_id) {
        Ok(_) => PkResult::ok(),
        Err(_) => PkResult::err(ResultCode::ValidationFailed)
    }
}

#[no_mangle]
pub unsafe extern "C" fn pk_add_line_with_parent(
    handle: PkTransactionHandle,
    sku_ptr: *const u8,
    sku_len: usize,
    qty: i32,
    unit_minor: i64,
    parent_line_id: u32  // 0 means no parent
) -> PkResult {
    if handle == PK_INVALID_HANDLE || sku_ptr.is_null() || sku_len == 0 || qty <= 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let sku = read_str(sku_ptr, sku_len);

    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };

    match kernel_store.add_child_line_legal(handle, sku, qty, unit_minor, parent_line_id) {
        Ok(_) => PkResult::ok(),
        Err(_) => PkResult::err(ResultCode::ValidationFailed)
    }
}

/// ARCHITECTURAL COMPONENT: Voids a line item with NRF-compliant cascade to child items.
/// Critical NRF requirement: When parent items are voided, all linked child items must be voided.
///
/// # Safety
/// The caller must ensure that:
/// - `handle` refers to a valid, active transaction
/// - `line_number` is within the valid range of line items (1-based)
/// - `reason_ptr` points to valid memory containing a UTF-8 encoded reason string
/// - `reason_len` accurately represents the length of the data at `reason_ptr`
#[no_mangle]
pub unsafe extern "C" fn pk_void_line_item_with_cascade(
    handle: PkTransactionHandle,
    line_number: u32,
    reason_ptr: *const u8,
    reason_len: usize
) -> PkResult {
    if handle == PK_INVALID_HANDLE || line_number == 0 || reason_ptr.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let reason = read_str(reason_ptr, reason_len);

    let mut kernel_store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };

    // Use the NRF void cascade logic
    match kernel_store.void_line_with_cascade(handle, line_number, &reason) {
        Ok(_) => PkResult::ok(),
        Err(_) => PkResult::err(ResultCode::ValidationFailed)
    }
}

/// ARCHITECTURAL COMPONENT: Gets the parent line item ID for a specific line item.
/// NRF COMPLIANCE: Supports querying linked items hierarchy.
#[no_mangle]
pub unsafe extern "C" fn pk_get_line_parent_id(
    handle: PkTransactionHandle,
    line_number: u32,
    out_parent_id: *mut u32,
    out_has_parent: *mut bool
) -> PkResult {
    if handle == PK_INVALID_HANDLE || line_number == 0 || out_parent_id.is_null() || out_has_parent.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let kernel_store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };

    match kernel_store.get_line_parent_id(handle, line_number) {
        Ok(parent_id_opt) => {
            if let Some(parent_id) = parent_id_opt {
                *out_parent_id = parent_id;
                *out_has_parent = true;
            } else {
                *out_parent_id = 0;
                *out_has_parent = false;
            }
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}

/// ARCHITECTURAL COMPONENT: Finds all child line items of a parent (for void cascade).
/// NRF COMPLIANCE: Supports void cascade for linked items.
#[no_mangle]
pub unsafe extern "C" fn pk_find_line_children(
    handle: PkTransactionHandle,
    parent_line_number: u32,
    out_children_ptr: *mut u32,
    out_children_len: *mut usize
) -> PkResult {
    if handle == PK_INVALID_HANDLE || parent_line_number == 0 || out_children_ptr.is_null() || out_children_len.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }

    let kernel_store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(_) => return PkResult::err(ResultCode::InternalError)
    };

    match kernel_store.find_line_children(handle, parent_line_number) {
        Ok(children) => {
            let buffer_size = *out_children_len;

            if children.len() > buffer_size {
                // Buffer too small, return required size
                *out_children_len = children.len();
                return PkResult::err(ResultCode::InsufficientBuffer);
            }

            // Copy child line numbers to output buffer
            for (i, &child_id) in children.iter().enumerate() {
                *out_children_ptr.add(i) = child_id;
            }

            *out_children_len = children.len();
            PkResult::ok()
        },
        Err(_) => PkResult::err(ResultCode::NotFound)
    }
}
