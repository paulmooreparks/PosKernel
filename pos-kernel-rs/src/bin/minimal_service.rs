//! Minimal POS Kernel Service - Based on proven Axum patterns
//!
//! This implementation follows the exact patterns from the Axum todos example
//! which is known to work reliably for JSON POST operations.

use axum::{
    error_handling::HandleErrorLayer,
    extract::{Json, State},
    http::{Method, StatusCode},
    response::IntoResponse,
    routing::{get, post},
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

// Session request/response types
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

// Thread-safe session storage
type SessionStore = Arc<RwLock<HashMap<String, SessionResponse>>>;

#[tokio::main]
async fn main() {
    // Initialize tracing with proper formatting - exactly like the todos example
    tracing_subscriber::registry()
        .with(
            tracing_subscriber::EnvFilter::try_from_default_env().unwrap_or_else(|_| {
                format!("{}=debug,tower_http=debug", env!("CARGO_CRATE_NAME")).into()
            }),
        )
        .with(tracing_subscriber::fmt::layer())
        .init();

    // Initialize session store
    let session_store: SessionStore = Arc::new(RwLock::new(HashMap::new()));

    // Build application with comprehensive middleware - following todos example pattern
    let app = Router::new()
        .route("/health", get(health_check))
        .route("/api/sessions", post(create_session))
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
                        .allow_methods([Method::GET, Method::POST])
                        .allow_headers(Any)
                        .allow_origin(Any),
                )
                .into_inner(),
        )
        .with_state(session_store);

    // Bind and serve - exactly like the todos example
    let listener = tokio::net::TcpListener::bind("127.0.0.1:8080")
        .await
        .expect("Failed to bind to address");

    tracing::debug!("listening on {}", listener.local_addr().unwrap());
    axum::serve(listener, app).await.unwrap();
}

// Simple health check - always works
async fn health_check() -> impl IntoResponse {
    "healthy"
}

// Session creation handler - following exact todos pattern for JSON POST
async fn create_session(
    State(store): State<SessionStore>,
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
    store.write().unwrap().insert(session_id, session.clone());

    tracing::info!("Created session: {}", session.session_id);

    // Return 201 Created with JSON response - exactly like todos
    (StatusCode::CREATED, Json(session))
}
