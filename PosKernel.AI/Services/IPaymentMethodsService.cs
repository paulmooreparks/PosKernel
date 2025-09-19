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

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Service for managing and validating payment methods based on store configuration.
    /// ARCHITECTURAL PRINCIPLE: Payment method validation is driven by store configuration, not hardcoded lists.
    /// </summary>
    public interface IPaymentMethodsService
    {
        /// <summary>
        /// Gets all accepted payment methods for a store.
        /// </summary>
        /// <param name="storeId">The store identifier.</param>
        /// <returns>List of accepted payment methods.</returns>
        Task<List<PaymentMethodConfig>> GetAcceptedPaymentMethodsAsync(string storeId);

        /// <summary>
        /// Validates if a payment method is accepted by the store.
        /// </summary>
        /// <param name="storeId">The store identifier.</param>
        /// <param name="paymentMethodId">The payment method identifier.</param>
        /// <returns>True if the payment method is accepted, false otherwise.</returns>
        Task<bool> IsPaymentMethodAcceptedAsync(string storeId, string paymentMethodId);

        /// <summary>
        /// Validates if a payment method can process the given amount.
        /// </summary>
        /// <param name="storeId">The store identifier.</param>
        /// <param name="paymentMethodId">The payment method identifier.</param>
        /// <param name="amount">The transaction amount.</param>
        /// <returns>Validation result with details.</returns>
        Task<PaymentMethodValidationResult> ValidatePaymentMethodAsync(string storeId, string paymentMethodId, decimal amount);

        /// <summary>
        /// Gets payment method suggestions based on transaction context.
        /// </summary>
        /// <param name="storeId">The store identifier.</param>
        /// <param name="amount">The transaction amount.</param>
        /// <returns>List of suggested payment methods.</returns>
        Task<List<PaymentMethodConfig>> GetSuggestedPaymentMethodsAsync(string storeId, decimal amount);

        /// <summary>
        /// Gets formatted display information for payment methods available to the AI.
        /// This is what gets loaded at startup like the menu items.
        /// </summary>
        /// <param name="storeId">The store identifier.</param>
        /// <returns>Formatted payment methods context for AI prompts.</returns>
        Task<string> GetPaymentMethodsContextAsync(string storeId);
    }

    /// <summary>
    /// Result of payment method validation.
    /// </summary>
    public class PaymentMethodValidationResult
    {
        /// <summary>
        /// Gets or sets whether the payment method is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the reason if the payment method is not valid.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the payment method configuration if valid.
        /// </summary>
        public PaymentMethodConfig? PaymentMethod { get; set; }

        /// <summary>
        /// Gets or sets any additional validation messages.
        /// </summary>
        public List<string> Messages { get; set; } = new();
    }
}
