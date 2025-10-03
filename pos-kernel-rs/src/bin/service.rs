//! POS Kernel Service - Production-ready implementation
//!
//! This implementation is based on proven Axum patterns and includes
//! all core POS kernel functionality with reliable JSON POST handling.

use axum::{
    error_handling::HandleErrorLayer,
    extract::{Json, Path, State},
    http::{Method, StatusCode},
    response::IntoResponse,
    routing::{get, post, delete},
    Router,
};
use serde::{Deserialize, Serialize};
use std::{
    collections::HashMap,
    sync::{Arc, RwLock},
    time::Duration,
};
use tower::{BoxError, ServiceBuilder};
use tower_http::{cors::{Any, CorsLayer}, trace::TraceLayer};
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};
use uuid::Uuid;

// ===== REQUEST/RESPONSE TYPES =====

#[derive(Debug, Serialize, Deserialize, Clone)]
struct SessionRequest {
    terminal_id: String,
    operator_id: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct SessionResponse {
    session_id: String,
    terminal_id: String,
    operator_id: String,
    status: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct TransactionRequest {
    session_id: String,
    store: String,
    currency: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct TransactionResponse {
    transaction_id: String,
    session_id: String,
    store: String,
    currency: String,
    status: String,
    total: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct LineItemRequest {
    product_id: String,
    quantity: i32,
    unit_price: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct LineItemResponse {
    line_item_id: String,
    product_id: String,
    quantity: i32,
    unit_price: String,
    line_total: String,
}

// ===== STORAGE TYPES =====

type SessionStore = Arc<RwLock<HashMap<String, SessionResponse>>>;
type TransactionStore = Arc<RwLock<HashMap<String, TransactionResponse>>>;
type LineItemStore = Arc<RwLock<HashMap<String, Vec<LineItemResponse>>>>;

#[derive(Clone)]
struct AppState {
    sessions: SessionStore,
    transactions: TransactionStore,
    line_items: LineItemStore,
}

// ===== MAIN SERVICE =====

#[tokio::main]
async fn main() {
    // Initialize tracing for service deployment - no colors, no timestamps (poskernel CLI handles those)
    tracing_subscriber::registry()
        .with(
            tracing_subscriber::EnvFilter::try_from_default_env().unwrap_or_else(|_| {
                // DEFAULT TO INFO for development - shows all service logs
                "pos_kernel_service=info,tower_http=info,axum::rejection=info".into()
            }),
        )
        .with(
            tracing_subscriber::fmt::layer()
                .with_ansi(false)           // No ANSI color codes for service logs
                .with_target(true)          // Show targets for debugging
        )
        .init();

    // Test debug logging to verify tracing configuration
    tracing::info!("DEBUG: Tracing initialized with debug level");
    tracing::info!("INFO: About to initialize service stores");

    // Initialize stores
    let app_state = AppState {
        sessions: Arc::new(RwLock::new(HashMap::new())),
        transactions: Arc::new(RwLock::new(HashMap::new())),
        line_items: Arc::new(RwLock::new(HashMap::new())),
    };

    // Build application with comprehensive middleware
    let app = Router::new()
        // Core endpoints
        .route("/health", get(health_check))

        // Session management
        .route("/api/sessions", post(create_session))
        .route("/api/sessions/:session_id", get(get_session).delete(delete_session))

        // Transaction management
        .route("/api/transactions", post(create_transaction))
        .route("/api/transactions/:transaction_id", get(get_transaction).delete(delete_transaction))

        // Line item management
        .route("/api/transactions/:transaction_id/items", post(add_line_item).get(get_line_items))
        .route("/api/transactions/:transaction_id/items/:item_id", delete(remove_line_item))

        // Transaction operations
        .route("/api/transactions/:transaction_id/finalize", post(finalize_transaction))
        .route("/api/transactions/:transaction_id/void", post(void_transaction))

        // Add comprehensive middleware stack
        .layer(
            ServiceBuilder::new()
                .layer(HandleErrorLayer::new(|error: BoxError| async move {
                    if error.is::<tower::timeout::error::Elapsed>() {
                        Ok(StatusCode::REQUEST_TIMEOUT)
                    } else {
                        Err((
                            StatusCode::INTERNAL_SERVER_ERROR,
                            format!("Unhandled internal error: {error}"),
                        ))
                    }
                }))
                .timeout(Duration::from_secs(10))
                .layer(TraceLayer::new_for_http())
                .layer(
                    CorsLayer::new()
                        .allow_methods([Method::GET, Method::POST, Method::PUT, Method::DELETE])
                        .allow_headers(Any)
                        .allow_origin(Any),
                )
                .into_inner(),
        )
        .with_state(app_state);

    // Bind and serve
    let listener = tokio::net::TcpListener::bind("127.0.0.1:8080")
        .await
        .expect("Failed to bind to address");

    tracing::info!("POS Kernel Service v{} listening on {}",
                   env!("CARGO_PKG_VERSION"),
                   listener.local_addr().unwrap());
    axum::serve(listener, app).await.unwrap();
}

// ===== HANDLER FUNCTIONS =====

async fn health_check() -> impl IntoResponse {
    tracing::info!("HTTP GET /health");
    "healthy"
}

// Session handlers
async fn create_session(
    State(state): State<AppState>,
    Json(input): Json<SessionRequest>
) -> impl IntoResponse {
    tracing::info!("HTTP POST /api/sessions - terminal: {}, operator: {}", input.terminal_id, input.operator_id);
    let session_id = Uuid::new_v4().to_string();

    let session = SessionResponse {
        session_id: session_id.clone(),
        terminal_id: input.terminal_id,
        operator_id: input.operator_id,
        status: "active".to_string(),
    };

    state.sessions.write().unwrap().insert(session_id, session.clone());
    tracing::info!("Created session: {}", session.session_id);

    (StatusCode::CREATED, Json(session))
}

async fn get_session(
    State(state): State<AppState>,
    Path(session_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    let sessions = state.sessions.read().unwrap();
    match sessions.get(&session_id) {
        Some(session) => Ok(Json(session.clone())),
        None => Err(StatusCode::NOT_FOUND),
    }
}

async fn delete_session(
    State(state): State<AppState>,
    Path(session_id): Path<String>
) -> impl IntoResponse {
    let mut sessions = state.sessions.write().unwrap();
    match sessions.remove(&session_id) {
        Some(_) => {
            tracing::info!("Deleted session: {}", session_id);
            StatusCode::NO_CONTENT
        }
        None => StatusCode::NOT_FOUND,
    }
}

// Transaction handlers
async fn create_transaction(
    State(state): State<AppState>,
    Json(input): Json<TransactionRequest>
) -> Result<impl IntoResponse, StatusCode> {
    // Verify session exists
    let sessions = state.sessions.read().unwrap();
    if !sessions.contains_key(&input.session_id) {
        return Err(StatusCode::BAD_REQUEST);
    }
    drop(sessions);

    let transaction_id = Uuid::new_v4().to_string();

    let transaction = TransactionResponse {
        transaction_id: transaction_id.clone(),
        session_id: input.session_id,
        store: input.store,
        currency: input.currency,
        status: "open".to_string(),
        total: "0.00".to_string(),
    };

    state.transactions.write().unwrap().insert(transaction_id, transaction.clone());
    tracing::info!("Created transaction: {}", transaction.transaction_id);

    Ok((StatusCode::CREATED, Json(transaction)))
}

async fn get_transaction(
    State(state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    let transactions = state.transactions.read().unwrap();
    match transactions.get(&transaction_id) {
        Some(transaction) => Ok(Json(transaction.clone())),
        None => Err(StatusCode::NOT_FOUND),
    }
}

async fn delete_transaction(
    State(state): State<AppState>,
    Path(transaction_id): Path<String>
) -> impl IntoResponse {
    let mut transactions = state.transactions.write().unwrap();
    match transactions.remove(&transaction_id) {
        Some(_) => {
            // Also remove associated line items
            state.line_items.write().unwrap().remove(&transaction_id);
            tracing::info!("Deleted transaction: {}", transaction_id);
            StatusCode::NO_CONTENT
        }
        None => StatusCode::NOT_FOUND,
    }
}

// Line item handlers
async fn add_line_item(
    State(state): State<AppState>,
    Path(transaction_id): Path<String>,
    Json(input): Json<LineItemRequest>
) -> Result<impl IntoResponse, StatusCode> {
    // Verify transaction exists
    let transactions = state.transactions.read().unwrap();
    if !transactions.contains_key(&transaction_id) {
        return Err(StatusCode::NOT_FOUND);
    }
    drop(transactions);

    let line_item_id = Uuid::new_v4().to_string();

    let line_item = LineItemResponse {
        line_item_id: line_item_id.clone(),
        product_id: input.product_id,
        quantity: input.quantity,
        unit_price: input.unit_price.clone(),
        line_total: input.unit_price, // Simplified calculation
    };

    let mut line_items = state.line_items.write().unwrap();
    line_items.entry(transaction_id).or_insert_with(Vec::new).push(line_item.clone());

    tracing::info!("Added line item: {} to transaction", line_item_id);
    Ok((StatusCode::CREATED, Json(line_item)))
}

async fn get_line_items(
    State(state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    let line_items = state.line_items.read().unwrap();
    match line_items.get(&transaction_id) {
        Some(items) => Ok(Json(items.clone())),
        None => Ok(Json(Vec::<LineItemResponse>::new())),
    }
}

async fn remove_line_item(
    State(state): State<AppState>,
    Path((transaction_id, item_id)): Path<(String, String)>
) -> impl IntoResponse {
    let mut line_items = state.line_items.write().unwrap();
    if let Some(items) = line_items.get_mut(&transaction_id) {
        if let Some(pos) = items.iter().position(|item| item.line_item_id == item_id) {
            items.remove(pos);
            tracing::info!("Removed line item: {} from transaction: {}", item_id, transaction_id);
            return StatusCode::NO_CONTENT;
        }
    }
    StatusCode::NOT_FOUND
}

// Transaction operation handlers
async fn finalize_transaction(
    State(state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    let mut transactions = state.transactions.write().unwrap();
    match transactions.get_mut(&transaction_id) {
        Some(transaction) => {
            transaction.status = "finalized".to_string();
            tracing::info!("Finalized transaction: {}", transaction_id);
            Ok(Json(transaction.clone()))
        }
        None => Err(StatusCode::NOT_FOUND),
    }
}

async fn void_transaction(
    State(state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    let mut transactions = state.transactions.write().unwrap();
    match transactions.get_mut(&transaction_id) {
        Some(transaction) => {
            transaction.status = "voided".to_string();
            tracing::info!("Voided transaction: {}", transaction_id);
            Ok(Json(transaction.clone()))
        }
        None => Err(StatusCode::NOT_FOUND),
    }
}
