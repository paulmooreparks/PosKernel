// Minimal Axum test to isolate POST issue
use axum::{
    routing::{get, post},
    response::Json,
    Router,
};
use serde_json::json;

#[tokio::main]
async fn main() {
    let app = Router::new()
        .route("/", get(|| async { "Hello GET" }))
        .route("/test", post(|| async { "Hello POST" }))
        .route("/json", post(|| async { Json(json!({"status": "ok"})) }));

    let listener = tokio::net::TcpListener::bind("127.0.0.1:8081").await.unwrap();
    println!("Minimal test server on http://127.0.0.1:8081");
    axum::serve(listener, app).await.unwrap();
}
