# Working AI Integration Setup

## 🚀 **Quick Setup for Real AI**

### **1. OpenAI Setup (Recommended)**

```bash
# Set your OpenAI API key
export OPENAI_API_KEY="sk-your-actual-key-here"

# Run the demo
cd PosKernel.AI
dotnet run
```

### **2. Alternative: Local AI with Ollama**

```bash
# Install Ollama (local AI)
winget install Ollama.Ollama

# Start Ollama service
ollama serve

# Pull a model
ollama pull llama2

# Set local endpoint
export MCP_BASE_URL="http://localhost:11434/v1"
export OPENAI_API_KEY="ollama"  # Placeholder for compatibility

# Run demo
dotnet run
```

### **3. Azure OpenAI**

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_KEY="your-azure-key"
export OPENAI_API_VERSION="2024-02-15-preview"

# Update configuration in code to use Azure endpoint
```

## 🧠 **Live AI Demo Examples**

### **Natural Language Processing**
Input: "I need two coffees and a muffin"
Expected AI Response:
```json
{
  "items": [
    {"sku": "COFFEE", "name": "Coffee", "quantity": 2, "estimated_price": 3.99, "confidence": 0.95},
    {"sku": "MUFFIN", "name": "Muffin", "quantity": 1, "estimated_price": 2.49, "confidence": 0.90}
  ],
  "total_confidence": 0.92,
  "warnings": []
}
```

### **Fraud Detection**
High-value transaction analysis:
```
Transaction: $15,499.85 (5 laptops + 10 phones)
AI Analysis: "High-value electronics purchase detected. Risk factors: unusual quantity, high total value, electronics category. Recommendation: Require manager approval and verify customer identity."
Risk Level: HIGH
```

### **Business Intelligence**
Query: "What were our top sellers yesterday?"
AI Response: "Based on transaction patterns, your top 3 items were: 1) Coffee (45 sales, $178.55 revenue), 2) Sandwiches (23 sales, $149.77), 3) Muffins (19 sales, $47.31). Coffee sales increased 12% vs. same day last week."

## 🔧 **Testing Without Real AI (Mock Mode)**

If you want to see successful responses without API costs:
