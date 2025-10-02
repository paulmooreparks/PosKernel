#!/bin/bash
cd pos-kernel-rs
cargo run --bin minimal-test &
SERVER_PID=$!
sleep 3
echo "Testing GET:"
curl -s http://localhost:8081/
echo
echo "Testing POST:"
curl -s -X POST http://localhost:8081/test
echo
echo "Testing JSON POST:"
curl -s -X POST http://localhost:8081/json
echo
kill $SERVER_PID
