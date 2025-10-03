//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System.Text.Json.Serialization;

namespace PosKernel.HttpService.Models;

// ===== SESSION MODELS =====

public class SessionRequest
{
    [JsonPropertyName("terminal_id")]
    public string TerminalId { get; set; } = "";

    [JsonPropertyName("operator_id")]
    public string OperatorId { get; set; } = "";
}

public class SessionResponse
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("terminal_id")]
    public string TerminalId { get; set; } = "";

    [JsonPropertyName("operator_id")]
    public string OperatorId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

// ===== TRANSACTION MODELS =====

public class TransactionRequest
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("store")]
    public string Store { get; set; } = "";

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";
}

public class TransactionResponse
{
    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = "";

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("store")]
    public string Store { get; set; } = "";

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}

// ===== LINE ITEM MODELS =====

public class LineItemRequest
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("parent_line_item_id")]
    public string? ParentLineItemId { get; set; }
}

public class LineItemResponse
{
    [JsonPropertyName("line_item_id")]
    public string LineItemId { get; set; } = "";

    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("parent_line_item_id")]
    public string? ParentLineItemId { get; set; }
}
