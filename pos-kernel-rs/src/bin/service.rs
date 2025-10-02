// src/bin/service.rs
// Minimal POS Kernel HTTP Service

use pos_kernel::*;
use std::net::SocketAddr;
use std::collections::HashMap;
use serde::{Deserialize, Serialize};
use axum::{
    routing::{get, post, put},
    response::Json,
    extract::Path,
    Router,
};
use std::sync::{Arc, Mutex};

#[derive(Debug, Serialize, Deserialize)]
struct CreateSessionRequest {
    terminal_id: String,
    operator_id: String,
}

#[derive(Debug, Serialize, Deserialize)]
struct CreateSessionResponse {
    success: bool,
    session_id: String,
    error: Option<String>,
}

#[derive(Debug, Serialize, Deserialize)]
struct StartTransactionRequest {
    session_id: String,
    store: String,
    currency: String,
}

#[derive(Debug, Serialize, Deserialize)]
struct AddLineItemRequest {
    session_id: String,
    transaction_id: String,
    product_id: String,
    quantity: i32,
    unit_price: f64,
}

#[derive(Debug, Serialize, Deserialize)]
struct ProcessPaymentRequest {
    session_id: String,
    transaction_id: String,
    amount: f64,
    payment_type: String,
}

// ARCHITECTURAL FIX: Add line item ID modification request
#[derive(Debug, Serialize, Deserialize)]
struct ModifyLineItemByIdRequest {
    session_id: String,
    transaction_id: String,
    line_item_id: String,
    modification_type: String,
    new_value: String,
}

// ARCHITECTURAL FIX: Add NRF-compliant hierarchical line item request
#[derive(Debug, Serialize, Deserialize)]
struct AddChildLineItemRequest {
    session_id: String,
    product_id: String,
    quantity: i32,
    unit_price: f64,
    parent_line_number: u32,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct TransactionLineItem {
    line_item_id: String,    // ARCHITECTURAL FIX: Kernel-assigned stable ID
    line_number: i32,
    product_id: String,
    quantity: i32,
    unit_price: f64,
    extended_price: f64,
    parent_line_number: Option<u32>, // NRF COMPLIANCE: Hierarchical relationships
}

#[derive(Debug, Serialize, Deserialize)]
struct TransactionResponse {
    success: bool,
    session_id: String,
    transaction_id: String,
    total: f64,
    tendered: f64,
    change: f64,
    state: String,
    line_items: Vec<TransactionLineItem>,
    error: Option<String>,
}

// Enhanced session storage with line item tracking
type SessionStore = Arc<Mutex<HashMap<String, Session>>>;
type TransactionStore = Arc<Mutex<HashMap<String, TransactionData>>>;

#[derive(Debug, Clone)]
struct Session {
    #[allow(dead_code)] // Used for session management and audit purposes
    id: String,
    #[allow(dead_code)] // Used for session management and audit purposes
    terminal_id: String,
    #[allow(dead_code)] // Used for session management and audit purposes
    operator_id: String,
    transaction_id: Option<String>,
}

#[derive(Debug, Clone)]
struct TransactionData {
    id: String,
    line_items: Vec<TransactionLineItem>,
    line_counter: i32,
    line_id_counter: u32, // ARCHITECTURAL FIX: Counter for unique line item IDs
}

impl TransactionData {
    fn new(id: String) -> Self {
        Self {
            id,
            line_items: Vec::new(),
            line_counter: 0,
            line_id_counter: 0,
        }
    }

    // ARCHITECTURAL FIX: Use the id field for transaction identification
    #[allow(dead_code)] // Utility method for future use and debugging
    fn get_id(&self) -> &str {
        &self.id
    }

    fn add_line_item(&mut self, product_id: String, quantity: i32, unit_price: f64) -> i32 {
        self.line_counter += 1;
        self.line_id_counter += 1;
        let line_number = self.line_counter;

        // ARCHITECTURAL FIX: Generate kernel-assigned stable line item ID
        let line_item_id = format!("TXN_{}_LN_{:04}", self.id, self.line_id_counter);

        println!("Added line item to transaction {}: {} x {} @ {:.2} (line {}, id: {})",
                 self.id, quantity, product_id, unit_price, line_number, line_item_id);

        self.line_items.push(TransactionLineItem {
            line_item_id,
            line_number,
            product_id,
            quantity,
            unit_price,
            extended_price: unit_price * quantity as f64,
            parent_line_number: None, // Default to None for top-level items
        });

        line_number
    }

    // NRF COMPLIANCE: Add child line item with parent relationship
    fn add_child_line_item(&mut self, product_id: String, quantity: i32, unit_price: f64, parent_line_number: u32) -> Result<i32, String> {
        // Validate parent exists
        if !self.line_items.iter().any(|item| item.line_number as u32 == parent_line_number) {
            return Err(format!("Parent line number {} not found in transaction {}", parent_line_number, self.id));
        }

        self.line_counter += 1;
        self.line_id_counter += 1;
        let line_number = self.line_counter;

        let line_item_id = format!("TXN_{}_LN_{:04}", self.id, self.line_id_counter);

        println!("Added child line item to transaction {}: {} x {} @ {:.2} (line {}, parent {}, id: {})",
                 self.id, quantity, product_id, unit_price, line_number, parent_line_number, line_item_id);

        self.line_items.push(TransactionLineItem {
            line_item_id,
            line_number,
            product_id,
            quantity,
            unit_price,
            extended_price: unit_price * quantity as f64,
            parent_line_number: Some(parent_line_number),
        });

        Ok(line_number)
    }

    // ARCHITECTURAL FIX: Modify line item by stable ID instead of unstable line number
    fn modify_line_item_by_id(&mut self, line_item_id: &str, modification_type: &str, new_value: &str) -> Result<(), String> {
        if let Some(item) = self.line_items.iter_mut().find(|item| item.line_item_id == line_item_id) {
            match modification_type {
                "quantity" => {
                    if let Ok(new_qty) = new_value.parse::<i32>() {
                        if new_qty > 0 {
                            let old_qty = item.quantity;
                            item.quantity = new_qty;
                            item.extended_price = item.unit_price * new_qty as f64;
                            println!("Modified line item {} (line {}): quantity {} -> {}",
                                     line_item_id, item.line_number, old_qty, new_qty);
                            Ok(())
                        } else {
                            Err(format!("Invalid quantity: {} (must be > 0)", new_value))
                        }
                    } else {
                        Err(format!("Invalid quantity format: {}", new_value))
                    }
                },
                _ => Err(format!("Unsupported modification type: {}", modification_type))
            }
        } else {
            Err(format!("Line item ID {} not found in transaction {}", line_item_id, self.id))
        }
    }

    // ARCHITECTURAL FIX: Void line item by stable ID
    fn void_line_item_by_id(&mut self, line_item_id: &str) -> Result<(), String> {
        if let Some(index) = self.line_items.iter().position(|item| item.line_item_id == line_item_id) {
            let removed_item = self.line_items.remove(index);
            println!("Voided line item {} from transaction {}: {} x {}",
                     line_item_id, self.id, removed_item.quantity, removed_item.product_id);

            // Renumber remaining line items
            for (new_line_number, item) in self.line_items.iter_mut().enumerate() {
                item.line_number = (new_line_number + 1) as i32;
            }
            self.line_counter = self.line_items.len() as i32;

            Ok(())
        } else {
            Err(format!("Line item ID {} not found in transaction {}", line_item_id, self.id))
        }
    }
}

impl Default for TransactionData {
    fn default() -> Self {
        Self::new("".to_string())
    }
}

// Helper to convert major currency units to minor
fn major_to_minor(amount: f64, _handle: u64) -> i64 {
    (amount * 100.0) as i64
}

// Helper to convert minor currency units to major
fn minor_to_major(amount: i64, _handle: u64) -> f64 {
    amount as f64 / 100.0
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("🦀 POS Kernel Rust Service v0.4.0-minimal");
    println!("🚀 Starting HTTP API on http://127.0.0.1:8080");

    // Initialize terminal
    let terminal_id = "RUST_SERVICE_01";
    let terminal_id_bytes = terminal_id.as_bytes();

    let result = unsafe {
        pk_initialize_terminal(terminal_id_bytes.as_ptr(), terminal_id_bytes.len())
    };

    if !pk_result_is_ok(result) {
        eprintln!("❌ Failed to initialize terminal {}: error code {}",
                 terminal_id, pk_result_get_code(result));
        return Err(format!("Terminal initialization failed: {}", pk_result_get_code(result)).into());
    }

    println!("✅ Terminal {} initialized successfully", terminal_id);

    // Create session store and transaction store
    let sessions: SessionStore = Arc::new(Mutex::new(HashMap::new()));
    let transactions: TransactionStore = Arc::new(Mutex::new(HashMap::new()));

    // HTTP API server
    let addr = SocketAddr::from(([127, 0, 0, 1], 8080));

    let app = Router::new()
        .route("/", get(root_handler))
        .route("/version", get(version_handler))
        .route("/health", get(health_handler))
        // Session management endpoints
        .route("/api/sessions", post({
            let sessions = Arc::clone(&sessions);
            move |body| create_session_handler(body, sessions)
        }))
        .route("/api/sessions/:session_id/transactions", post({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path, body| start_transaction_handler(path, body, sessions, transactions)
        }))
        .route("/api/sessions/:session_id/transactions/:transaction_id/lines", post({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path, body| add_line_handler(path, body, sessions, transactions)
        }))
        // NRF COMPLIANCE: Hierarchical child line item endpoint
        .route("/api/sessions/:session_id/transactions/:transaction_id/child-lines", post({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path, body| add_child_line_handler(path, body, sessions, transactions)
        }))
        // ARCHITECTURAL FIX: Add line item modification by ID endpoint
        .route("/api/sessions/:session_id/transactions/:transaction_id/line-items/:line_item_id/modify", put({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path, body| modify_line_item_by_id_handler(path, body, sessions, transactions)
        }))
        // ARCHITECTURAL FIX: Add line item void by ID endpoint
        .route("/api/sessions/:session_id/transactions/:transaction_id/line-items/:line_item_id/void", post({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path| void_line_item_by_id_handler(path, sessions, transactions)
        }))
        .route("/api/sessions/:session_id/transactions/:transaction_id/payment", post({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path, body| process_payment_handler(path, body, sessions, transactions)
        }))
        .route("/api/sessions/:session_id/transactions/:transaction_id", get({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path| get_transaction_handler(path, sessions, transactions)
        }))
        // ARCHITECTURAL FIX: Add dedicated endpoint for transaction details for AI tools
        .route("/api/transactions/:transaction_id/details", get({
            let transactions = Arc::clone(&transactions);
            move |path| get_transaction_details_handler(path, transactions)
        }))
        // ARCHITECTURAL FIX: Add line item modification endpoint
        .route("/api/sessions/:session_id/transactions/:transaction_id/line-items/:line_item_id/modifications", post({
            let sessions = Arc::clone(&sessions);
            let transactions = Arc::clone(&transactions);
            move |path, body| add_modification_to_line_item_handler(path, body, sessions, transactions)
        }));

    let listener = tokio::net::TcpListener::bind(addr).await?;
    println!("✅ Service ready at http://127.0.0.1:8080");

    axum::serve(listener, app).await?;

    Ok(())
}

async fn root_handler() -> &'static str {
    "🦀 POS Kernel Rust HTTP API - Session-Aware Version with NRF Hierarchical Line Items"
}

async fn version_handler() -> Json<serde_json::Value> {
    let version = unsafe {
        let version_ptr = pk_get_version();
        if version_ptr.is_null() {
            "unknown".to_string()
        } else {
            std::ffi::CStr::from_ptr(version_ptr as *const i8)
                .to_string_lossy()
                .to_string()
        }
    };

    Json(serde_json::json!({
        "version": version,
        "kernel": "rust",
        "service": "pos-kernel-rs-session-aware",
        "api_type": "http",
        "features": ["nrf_hierarchical_line_items", "stable_line_item_ids", "parent_child_relationships"],
        "status": "operational"
    }))
}

async fn health_handler() -> Json<serde_json::Value> {
    Json(serde_json::json!({
        "healthy": true,
        "status": "OK",
        "kernel": "rust-session-aware",
        "features": ["nrf_hierarchical_line_items"]
    }))
}

async fn create_session_handler(
    Json(req): Json<CreateSessionRequest>,
    sessions: SessionStore,
) -> Json<CreateSessionResponse> {
    let session_id = format!("sess_{}", uuid::Uuid::new_v4().to_string().replace('-', "")[..8].to_uppercase());
    
    let session = Session {
        id: session_id.clone(),
        terminal_id: req.terminal_id.clone(),
        operator_id: req.operator_id,
        transaction_id: None,
    };

    let mut store = sessions.lock().unwrap();
    store.insert(session_id.clone(), session);

    println!("Created session {} for terminal {}", session_id, req.terminal_id);

    Json(CreateSessionResponse {
        success: true,
        session_id,
        error: None,
    })
}

async fn start_transaction_handler(
    Path(session_id): Path<String>,
    Json(req): Json<StartTransactionRequest>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let mut session_store = sessions.lock().unwrap();
    let session = match session_store.get_mut(&session_id) {
        Some(s) => s,
        None => {
            return Json(TransactionResponse {
                success: false,
                session_id: session_id.clone(),
                transaction_id: "0".to_string(),
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid session ID".to_string()),
            });
        }
    };

    let store_bytes = req.store.as_bytes();
    let currency_bytes = req.currency.as_bytes();
    let mut transaction_handle: u64 = 0;

    let result = unsafe {
        pk_begin_transaction(
            store_bytes.as_ptr(), store_bytes.len(),
            currency_bytes.as_ptr(), currency_bytes.len(),
            2, // ARCHITECTURAL VIOLATION: Decimal places should come from store configuration
            &mut transaction_handle
        )
    };

    if !pk_result_is_ok(result) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: "0".to_string(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some(format!("Failed to start transaction: error code {}", pk_result_get_code(result))),
        });
    }

    let transaction_id = transaction_handle.to_string();
    session.transaction_id = Some(transaction_id.clone());

    // ARCHITECTURAL FIX: Create transaction data for line item tracking
    let mut tx_store = transactions.lock().unwrap();
    tx_store.insert(transaction_id.clone(), TransactionData::new(transaction_id.clone()));

    println!("Started transaction {} for session {} (store: {}, currency: {})",
             transaction_id, session_id, req.store, req.currency);

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: 0.0,
        tendered: 0.0,
        change: 0.0,
        state: "Building".to_string(),
        line_items: Vec::new(),
        error: None,
    })
}

async fn add_line_handler(
    Path((session_id, transaction_id)): Path<(String, String)>,
    Json(req): Json<AddLineItemRequest>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let session_store = sessions.lock().unwrap();
    if !session_store.contains_key(&session_id) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: transaction_id.clone(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Invalid session ID".to_string()),
        });
    }

    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    let product_bytes = req.product_id.as_bytes();
    let unit_minor = major_to_minor(req.unit_price, handle);

    let result = unsafe {
        pk_add_line(
            handle,
            product_bytes.as_ptr(), product_bytes.len(),
            req.quantity,
            unit_minor
        )
    };

    if !pk_result_is_ok(result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some(format!("Failed to add line item: error code {}", pk_result_get_code(result))),
        });
    }

    // ARCHITECTURAL FIX: Track line item in transaction data
    let mut tx_store = transactions.lock().unwrap();
    let _line_number = if let Some(tx_data) = tx_store.get_mut(&transaction_id) {
        tx_data.add_line_item(req.product_id.clone(), req.quantity, req.unit_price)
    } else {
        1 // Fallback if transaction data not found
    };

    // Get updated totals
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let totals_result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(totals_result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Failed to get transaction totals".to_string()),
        });
    }

    // ARCHITECTURAL FIX: Return current line items
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}

// NRF COMPLIANCE: Add child line item with parent relationship
async fn add_child_line_handler(
    Path((session_id, transaction_id)): Path<(String, String)>,
    Json(req): Json<AddChildLineItemRequest>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let session_store = sessions.lock().unwrap();
    if !session_store.contains_key(&session_id) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: transaction_id.clone(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Invalid session ID".to_string()),
        });
    }

    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    let product_bytes = req.product_id.as_bytes();
    let unit_minor = major_to_minor(req.unit_price, handle);

    // Use kernel's hierarchical line item function
    let result = unsafe {
        pk_add_line_with_parent(
            handle,
            product_bytes.as_ptr(), product_bytes.len(),
            req.quantity,
            unit_minor,
            req.parent_line_number
        )
    };

    if !pk_result_is_ok(result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some(format!("Failed to add child line item: error code {}", pk_result_get_code(result))),
        });
    }

    // Track child line item in transaction data
    let mut tx_store = transactions.lock().unwrap();
    let _line_number = if let Some(tx_data) = tx_store.get_mut(&transaction_id) {
        match tx_data.add_child_line_item(req.product_id.clone(), req.quantity, req.unit_price, req.parent_line_number) {
            Ok(line_num) => line_num,
            Err(e) => {
                return Json(TransactionResponse {
                    success: false,
                    session_id,
                    transaction_id,
                    total: 0.0,
                    tendered: 0.0,
                    change: 0.0,
                    state: "Failed".to_string(),
                    line_items: vec![],
                    error: Some(e),
                });
            }
        }
    } else {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Transaction not found".to_string()),
        });
    };

    // ARCHITECTURAL FIX: Keep lock held and get line items from SAME transaction data
    // to ensure consistency within this request
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    // Get updated totals
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let totals_result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(totals_result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Failed to get transaction totals".to_string()),
        });
    }

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}

async fn process_payment_handler(
    Path((session_id, transaction_id)): Path<(String, String)>,
    Json(req): Json<ProcessPaymentRequest>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let session_store = sessions.lock().unwrap();
    if !session_store.contains_key(&session_id) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: transaction_id.clone(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Invalid session ID".to_string()),
        });
    }

    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    let amount_minor = major_to_minor(req.amount, handle);
    let result = pk_add_cash_tender(handle, amount_minor);

    if !pk_result_is_ok(result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some(format!("Failed to process payment: error code {}", pk_result_get_code(result))),
        });
    }

    // Get final totals
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let totals_result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(totals_result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Failed to get final totals".to_string()),
        });
    }

    // ARCHITECTURAL FIX: Return actual line items, not dummy payment line
    let tx_store = transactions.lock().unwrap();
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    println!("Processed payment for transaction {} (session {}): amount {:.2}, change {:.2}",
             transaction_id, session_id, req.amount, minor_to_major(change_minor, handle));

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}

async fn get_transaction_handler(
    Path((session_id, transaction_id)): Path<(String, String)>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let session_store = sessions.lock().unwrap();
    if !session_store.contains_key(&session_id) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: transaction_id.clone(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Invalid session ID".to_string()),
        });
    }

    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    // Get transaction state
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Transaction not found".to_string()),
        });
    }

    // ARCHITECTURAL FIX: Return actual line items
    let tx_store = transactions.lock().unwrap();
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}

// ARCHITECTURAL FIX: Add transaction details handler for AI tools
async fn get_transaction_details_handler(
    Path(transaction_id): Path<String>,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id: "unknown".to_string(),
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    // Get transaction state
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(result) {
        return Json(TransactionResponse {
            success: false,
            session_id: "unknown".to_string(),
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Transaction not found".to_string()),
        });
    }

    // Get line items from transaction store
    let tx_store = transactions.lock().unwrap();
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    Json(TransactionResponse {
        success: true,
        session_id: "unknown".to_string(),
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}

// ARCHITECTURAL FIX: Add line item modification by ID handler
async fn modify_line_item_by_id_handler(
    Path((session_id, transaction_id, line_item_id)): Path<(String, String, String)>,
    Json(req): Json<ModifyLineItemByIdRequest>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let session_store = sessions.lock().unwrap();
    if !session_store.contains_key(&session_id) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: transaction_id.clone(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Invalid session ID".to_string()),
        });
    }

    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    // Modify line item in transaction data using stable ID
    let mut tx_store = transactions.lock().unwrap();
    if let Some(tx_data) = tx_store.get_mut(&transaction_id) {
        if let Err(e) = tx_data.modify_line_item_by_id(&line_item_id, &req.modification_type, &req.new_value) {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some(e),
            });
        }
    } else {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Transaction not found".to_string()),
        });
    }

    // Get current totals from kernel
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let totals_result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(totals_result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Failed to get transaction totals".to_string()),
        });
    }

    // Return updated line items with stable IDs
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}

// ARCHITECTURAL FIX: Add void line item by ID handler
async fn void_line_item_by_id_handler(
    Path((session_id, transaction_id, line_item_id)): Path<(String, String, String)>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let session_store = sessions.lock().unwrap();
    if !session_store.contains_key(&session_id) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: transaction_id.clone(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Invalid session ID".to_string()),
        });
    }

    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    // Void line item using stable ID
    let mut tx_store = transactions.lock().unwrap();
    if let Some(tx_data) = tx_store.get_mut(&transaction_id) {
        if let Err(e) = tx_data.void_line_item_by_id(&line_item_id) {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some(e),
            });
        }
    } else {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Transaction not found".to_string()),
        });
    }

    // Get current totals from kernel
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let totals_result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(totals_result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Failed to get transaction totals".to_string()),
        });
    }

    // Return updated line items
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}

// ARCHITECTURAL FIX: Add modification to line item handler
async fn add_modification_to_line_item_handler(
    Path((session_id, transaction_id, line_item_id)): Path<(String, String, String)>,
    Json(req): Json<serde_json::Value>,
    sessions: SessionStore,
    transactions: TransactionStore,
) -> Json<TransactionResponse> {
    // Validate session
    let session_store = sessions.lock().unwrap();
    if !session_store.contains_key(&session_id) {
        return Json(TransactionResponse {
            success: false,
            session_id: session_id.clone(),
            transaction_id: transaction_id.clone(),
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Invalid session ID".to_string()),
        });
    }

    let handle: u64 = match transaction_id.parse() {
        Ok(h) => h,
        Err(_) => {
            return Json(TransactionResponse {
                success: false,
                session_id,
                transaction_id,
                total: 0.0,
                tendered: 0.0,
                change: 0.0,
                state: "Failed".to_string(),
                line_items: vec![],
                error: Some("Invalid transaction ID".to_string()),
            });
        }
    };

    // Extract modification details from request
    let modification_sku = req.get("modification_sku")
        .and_then(|v| v.as_str())
        .unwrap_or("UNKNOWN_MOD");
    let quantity = req.get("quantity")
        .and_then(|v| v.as_i64())
        .unwrap_or(1) as i32;
    let unit_price = req.get("unit_price")
        .and_then(|v| v.as_f64())
        .unwrap_or(0.0);

    // Find the parent line item to get its line number
    let mut tx_store = transactions.lock().unwrap();
    let parent_line_number = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.iter()
            .find(|item| item.line_item_id == line_item_id)
            .map(|item| item.line_number as u32)
    } else {
        None
    };

    if parent_line_number.is_none() {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some(format!("Parent line item {} not found", line_item_id)),
        });
    }

    let parent_line_num = parent_line_number.unwrap();

    // Add modification as child line item using kernel's hierarchical function
    let unit_minor = major_to_minor(unit_price, handle);
    let result = unsafe {
        pk_add_line_with_parent(
            handle,
            modification_sku.as_bytes().as_ptr(), modification_sku.len(),
            quantity,
            unit_minor,
            parent_line_num
        )
    };

    if !pk_result_is_ok(result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some(format!("Failed to add modification: error code {}", pk_result_get_code(result))),
        });
    }

    // Track modification in transaction data
    if let Some(tx_data) = tx_store.get_mut(&transaction_id) {
        match tx_data.add_child_line_item(modification_sku.to_string(), quantity, unit_price, parent_line_num) {
            Ok(_) => {},
            Err(e) => {
                return Json(TransactionResponse {
                    success: false,
                    session_id,
                    transaction_id,
                    total: 0.0,
                    tendered: 0.0,
                    change: 0.0,
                    state: "Failed".to_string(),
                    line_items: vec![],
                    error: Some(e),
                });
            }
        }
    }

    // Get updated totals
    let mut total_minor = 0i64;
    let mut tendered_minor = 0i64;
    let mut change_minor = 0i64;
    let mut state_code = 0i32;

    let totals_result = unsafe {
        pk_get_totals(handle, &mut total_minor, &mut tendered_minor, &mut change_minor, &mut state_code)
    };

    if !pk_result_is_ok(totals_result) {
        return Json(TransactionResponse {
            success: false,
            session_id,
            transaction_id,
            total: 0.0,
            tendered: 0.0,
            change: 0.0,
            state: "Failed".to_string(),
            line_items: vec![],
            error: Some("Failed to get transaction totals".to_string()),
        });
    }

    // Return updated line items with hierarchical relationships
    let line_items = if let Some(tx_data) = tx_store.get(&transaction_id) {
        tx_data.line_items.clone()
    } else {
        vec![]
    };

    Json(TransactionResponse {
        success: true,
        session_id,
        transaction_id,
        total: minor_to_major(total_minor, handle),
        tendered: minor_to_major(tendered_minor, handle),
        change: minor_to_major(change_minor, handle),
        state: if state_code == 0 { "Building" } else { "Completed" }.to_string(),
        line_items,
        error: None,
    })
}
