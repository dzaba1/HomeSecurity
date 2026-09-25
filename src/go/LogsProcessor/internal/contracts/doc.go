// Package contracts holds the Go bindings for the log-ingestion message-bus
// contract. They are generated from the shared JSON Schema files in
// src/contracts/json/ (the same ones the C# producer generates its own
// classes from) and must never be edited by hand - change the schema and run
// `go generate ./...` instead.
package contracts

//go:generate go tool go-jsonschema -p contracts --tags json --struct-name-from-title -o log_batch_message.gen.go ../../../../contracts/json/log_batch_message.json
