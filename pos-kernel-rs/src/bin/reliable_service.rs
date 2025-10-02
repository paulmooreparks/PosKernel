//! POS Kernel HTTP Service - Based on proven Axum patterns
//!
//! This implementation follows the exact patterns from the working minimal-service
//! to ensure reliable JSON POST operations and proper error handling.

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

// Note: pos_kernel library integration can be added as needed
// use pos_kernel::*;

// ============================================================================
// REQUEST/RESPONSE TYPES
// ============================================================================

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
    total: f64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct LineItemRequest {
    product_id: String,
    quantity: i32,
    unit_price: f64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct LineItemResponse {
    line_item_id: String,
    product_id: String,
    quantity: i32,
    unit_price: f64,
    total: f64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct PaymentRequest {
    payment_method: String,
    amount: f64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct PaymentResponse {
    payment_id: String,
    payment_method: String,
    amount: f64,
    status: String,
}

// ============================================================================
// STATE MANAGEMENT
// ============================================================================

type SessionStore = Arc<RwLock<HashMap<String, SessionResponse>>>;
type TransactionStore = Arc<RwLock<HashMap<String, TransactionResponse>>>;
type LineItemStore = Arc<RwLock<HashMap<String, Vec<LineItemResponse>>>>;

#[derive(Clone)]
struct AppState {
    sessions: SessionStore,
    transactions: TransactionStore,
    line_items: LineItemStore,
}

// ============================================================================
// MAIN SERVICE SETUP
// ============================================================================

#[tokio::main]
async fn main() {
    // Initialize tracing with proper formatting - exactly like minimal-service
    tracing_subscriber::registry()
        .with(
            tracing_subscriber::EnvFilter::try_from_default_env().unwrap_or_else(|_| {
                format!("{}=debug,tower_http=debug", env!("CARGO_CRATE_NAME")).into()
            }),
        )
        .with(tracing_subscriber::fmt::layer())
        .init();

    // Initialize application state
    let app_state = AppState {
        sessions: Arc::new(RwLock::new(HashMap::new())),
        transactions: Arc::new(RwLock::new(HashMap::new())),
        line_items: Arc::new(RwLock::new(HashMap::new())),
    };

    // Build application with comprehensive middleware - following proven pattern
    let app = Router::new()
        .route("/health", get(health_check))
        .route("/api/sessions", post(create_session))
        .route("/api/sessions/:session_id", delete(close_session))
        .route("/api/transactions", post(create_transaction))
        .route("/api/transactions/:transaction_id", get(get_transaction))
        .route("/api/transactions/:transaction_id/line-items", post(add_line_item))
        .route("/api/transactions/:transaction_id/line-items", get(get_line_items))
        .route("/api/transactions/:transaction_id/payments", post(add_payment))
        .route("/api/transactions/:transaction_id/finalize", post(finalize_transaction))
        .route("/api/transactions/:transaction_id/void", post(void_transaction))
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

    // Bind and serve - exactly like minimal-service
    let listener = tokio::net::TcpListener::bind("127.0.0.1:8080")
        .await
        .expect("Failed to bind to address");

    tracing::debug!("listening on {}", listener.local_addr().unwrap());
    axum::serve(listener, app).await.unwrap();
}

// ============================================================================
// HANDLER FUNCTIONS - Following proven JSON POST patterns
// ============================================================================

// Health check - always works
async fn health_check() -> impl IntoResponse {
    "healthy"
}

// Session management - following exact pattern from minimal-service
async fn create_session(
    State(app_state): State<AppState>,
    Json(input): Json<SessionRequest>
) -> impl IntoResponse {
    let session_id = Uuid::new_v4().to_string();

    let session = SessionResponse {
        session_id: session_id.clone(),
        terminal_id: input.terminal_id,
        operator_id: input.operator_id,
        status: "active".to_string(),
    };

    // Store the session
    app_state.sessions.write().unwrap().insert(session_id.clone(), session.clone());

    tracing::info!("Created session: {}", session.session_id);

    // Return 201 Created with JSON response - exactly like proven pattern
    (StatusCode::CREATED, Json(session))
}

async fn close_session(
    State(app_state): State<AppState>,
    Path(session_id): Path<String>
) -> impl IntoResponse {
    match app_state.sessions.write().unwrap().remove(&session_id) {
        Some(_) => {
            tracing::info!("Closed session: {}", session_id);
            StatusCode::NO_CONTENT
        }
        None => StatusCode::NOT_FOUND
    }
}

// Transaction management - following proven JSON POST pattern
async fn create_transaction(
    State(app_state): State<AppState>,
    Json(input): Json<TransactionRequest>
) -> impl IntoResponse {
    // Verify session exists
    if !app_state.sessions.read().unwrap().contains_key(&input.session_id) {
        return Err(StatusCode::BAD_REQUEST);
    }

    let transaction_id = Uuid::new_v4().to_string();

    let transaction = TransactionResponse {
        transaction_id: transaction_id.clone(),
        session_id: input.session_id,
        store: input.store,
        currency: input.currency,
        status: "active".to_string(),
        total: 0.0,
    };

    // Store the transaction
    app_state.transactions.write().unwrap().insert(transaction_id.clone(), transaction.clone());

    // Initialize empty line items for this transaction
    app_state.line_items.write().unwrap().insert(transaction_id.clone(), Vec::new());

    tracing::info!("Created transaction: {}", transaction.transaction_id);

    Ok((StatusCode::CREATED, Json(transaction)))
}

async fn get_transaction(
    State(app_state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    match app_state.transactions.read().unwrap().get(&transaction_id) {
        Some(transaction) => Ok(Json(transaction.clone())),
        None => Err(StatusCode::NOT_FOUND)
    }
}

// Line item management - following proven JSON POST pattern
async fn add_line_item(
    State(app_state): State<AppState>,
    Path(transaction_id): Path<String>,
    Json(input): Json<LineItemRequest>
) -> Result<impl IntoResponse, StatusCode> {
    // Verify transaction exists
    if !app_state.transactions.read().unwrap().contains_key(&transaction_id) {
        return Err(StatusCode::NOT_FOUND);
    }

    let line_item_id = Uuid::new_v4().to_string();
    let total = input.quantity as f64 * input.unit_price;

    let line_item = LineItemResponse {
        line_item_id,
        product_id: input.product_id,
        quantity: input.quantity,
        unit_price: input.unit_price,
        total,
    };

    // Add line item to transaction
    app_state.line_items.write().unwrap()
        .entry(transaction_id.clone())
        .or_insert_with(Vec::new)
        .push(line_item.clone());

    // Update transaction total
    let new_total = app_state.line_items.read().unwrap()
        .get(&transaction_id)
        .map(|items| items.iter().map(|item| item.total).sum())
        .unwrap_or(0.0);

    if let Some(transaction) = app_state.transactions.write().unwrap().get_mut(&transaction_id) {
        transaction.total = new_total;
    }

    tracing::info!("Added line item to transaction {}: {:?}", transaction_id, line_item);

    Ok((StatusCode::CREATED, Json(line_item)))
}

async fn get_line_items(
    State(app_state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    match app_state.line_items.read().unwrap().get(&transaction_id) {
        Some(items) => Ok(Json(items.clone())),
        None => Err(StatusCode::NOT_FOUND)
    }
}

// Payment management - following proven JSON POST pattern
async fn add_payment(
    State(app_state): State<AppState>,
    Path(transaction_id): Path<String>,
    Json(input): Json<PaymentRequest>
) -> Result<impl IntoResponse, StatusCode> {
    // Verify transaction exists
    if !app_state.transactions.read().unwrap().contains_key(&transaction_id) {
        return Err(StatusCode::NOT_FOUND);
    }

    let payment_id = Uuid::new_v4().to_string();

    let payment = PaymentResponse {
        payment_id,
        payment_method: input.payment_method,
        amount: input.amount,
        status: "completed".to_string(),
    };

    tracing::info!("Added payment to transaction {}: {:?}", transaction_id, payment);

    Ok((StatusCode::CREATED, Json(payment)))
}

// Transaction finalization - following proven JSON POST pattern
async fn finalize_transaction(
    State(app_state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    match app_state.transactions.write().unwrap().get_mut(&transaction_id) {
        Some(transaction) => {
            transaction.status = "finalized".to_string();
            tracing::info!("Finalized transaction: {}", transaction_id);
            Ok(Json(transaction.clone()))
        }
        None => Err(StatusCode::NOT_FOUND)
    }
}

async fn void_transaction(
    State(app_state): State<AppState>,
    Path(transaction_id): Path<String>
) -> Result<impl IntoResponse, StatusCode> {
    match app_state.transactions.write().unwrap().get_mut(&transaction_id) {
        Some(transaction) => {
            transaction.status = "voided".to_string();
            tracing::info!("Voided transaction: {}", transaction_id);
            Ok(Json(transaction.clone()))
        }
        None => Err(StatusCode::NOT_FOUND)
    }
}
