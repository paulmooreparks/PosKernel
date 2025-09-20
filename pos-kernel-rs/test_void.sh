#!/bin/bash
echo "🧪 Testing Rust Kernel Void Functionality"
echo "==========================================="

# Start the service in the background
echo "Starting POS Kernel service..."
cargo run --bin pos-kernel-service &
SERVICE_PID=$!

# Give it a moment to start
sleep 2

echo "Testing void functionality with curl..."

# Test 1: Start a transaction
echo "1. Starting transaction..."
TRANSACTION_RESPONSE=$(curl -s -X POST http://localhost:8080/sessions/test_session/transactions \
  -H "Content-Type: application/json" \
  -d '{"store_name": "Test Store", "currency": "USD"}')

echo "Transaction response: $TRANSACTION_RESPONSE"

# Extract transaction ID (assuming JSON response format)
TRANSACTION_ID=$(echo $TRANSACTION_RESPONSE | grep -o '"transaction_id":"[^"]*"' | cut -d'"' -f4)
echo "Transaction ID: $TRANSACTION_ID"

# Test 2: Add a line item
echo "2. Adding line item..."
curl -s -X POST "http://localhost:8080/sessions/test_session/transactions/$TRANSACTION_ID/lines" \
  -H "Content-Type: application/json" \
  -d '{"sku": "COFFEE", "quantity": 2, "unit_price": 3.50}'

# Test 3: Void the line item
echo "3. Voiding line item 1..."
VOID_RESPONSE=$(curl -s -X DELETE "http://localhost:8080/sessions/test_session/transactions/$TRANSACTION_ID/lines/1" \
  -H "Content-Type: application/json" \
  -d '{"reason": "Customer changed mind"}')

echo "Void response: $VOID_RESPONSE"

echo "✅ Void functionality test completed!"

# Clean up
kill $SERVICE_PID
wait $SERVICE_PID 2>/dev/null

echo "🏁 Test finished!"
