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

using PosKernel.AI.Models;

namespace PosKernel.AI.Core
{
    /// <summary>
    /// Simple state machine to manage payment flow and prevent infinite loops.
    /// Separates conversation state from receipt status for better reliability.
    /// </summary>
    public class PaymentStateMachine
    {
        public PaymentFlowState CurrentState { get; private set; } = PaymentFlowState.Ordering;
        public string? RequestedPaymentMethod { get; private set; }
        public bool HasAskedForPaymentMethod { get; private set; }

        /// <summary>
        /// Customer indicates they're done ordering
        /// </summary>
        public void TransitionToReadyForPayment()
        {
            CurrentState = PaymentFlowState.ReadyForPayment;
            HasAskedForPaymentMethod = false;
            RequestedPaymentMethod = null;
        }

        /// <summary>
        /// We've asked the customer how they want to pay
        /// </summary>
        public void MarkPaymentMethodRequested()
        {
            HasAskedForPaymentMethod = true;
        }

        /// <summary>
        /// Customer has specified their payment method
        /// </summary>
        public void SetPaymentMethod(string method)
        {
            RequestedPaymentMethod = method.ToLowerInvariant();
            CurrentState = PaymentFlowState.ProcessingPayment;
        }

        /// <summary>
        /// Payment has been processed successfully
        /// </summary>
        public void CompletePayment()
        {
            CurrentState = PaymentFlowState.PaymentComplete;
        }

        /// <summary>
        /// Start a new transaction
        /// </summary>
        public void Reset()
        {
            CurrentState = PaymentFlowState.Ordering;
            RequestedPaymentMethod = null;
            HasAskedForPaymentMethod = false;
        }

        /// <summary>
        /// Check if we should ask for payment method
        /// </summary>
        public bool ShouldAskForPaymentMethod()
        {
            return CurrentState == PaymentFlowState.ReadyForPayment && !HasAskedForPaymentMethod;
        }

        /// <summary>
        /// Check if we should process payment
        /// </summary>
        public bool ShouldProcessPayment()
        {
            return CurrentState == PaymentFlowState.ReadyForPayment && 
                   HasAskedForPaymentMethod && 
                   !string.IsNullOrEmpty(RequestedPaymentMethod);
        }
    }

    public enum PaymentFlowState
    {
        Ordering,
        ReadyForPayment,
        ProcessingPayment,
        PaymentComplete
    }
}
