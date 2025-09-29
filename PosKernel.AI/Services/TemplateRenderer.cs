using System;
using System.Linq;
using System.Text;
using PosKernel.AI.Models;
using PosKernel.Configuration.Services;

namespace PosKernel.AI.Services
{
    public class TemplateRenderer
    {
        private readonly ICurrencyFormattingService? _currencyFormatter;
        private readonly StoreConfig _storeConfig;

        public TemplateRenderer(ICurrencyFormattingService? currencyFormatter, StoreConfig storeConfig)
        {
            _currencyFormatter = currencyFormatter;
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
        }

        private string FormatCurrency(decimal amount)
        {
            if (_currencyFormatter != null && _storeConfig != null)
            {
                return _currencyFormatter.FormatCurrency(amount, _storeConfig.Currency, _storeConfig.StoreName);
            }
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Currency formatting service not available. " +
                $"Cannot format {amount} without proper currency service. " +
                $"Register ICurrencyFormattingService in DI container.");
        }

        public string RenderAddItemAck(string itemName, int quantity, decimal currentTotal)
        {
            var qtyText = quantity <= 0 ? 1 : quantity;
            return $"OK, added {qtyText}x {itemName}. Current total: {FormatCurrency(currentTotal)}. Anything else?";
        }

        public string RenderPaymentRequest(Receipt receipt)
        {
            if (_storeConfig?.PaymentMethods?.AcceptedMethods == null)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Payment methods not available to render payment request. Configure StoreConfig.PaymentMethods.");
            }
            var enabled = _storeConfig.PaymentMethods.AcceptedMethods.Where(m => m.IsEnabled).Select(m => m.DisplayName).ToList();
            var methodsText = enabled.Count switch
            {
                0 => string.Empty,
                1 => enabled[0],
                2 => string.Join(" or ", enabled),
                _ => string.Join(", ", enabled.Take(enabled.Count - 1)) + ", or " + enabled.Last()
            };

            var total = FormatCurrency(receipt.Total);
            return $"OK. Your total is {total}. We can take {methodsText}. How you want to pay?";
        }

        public string RenderPaymentReceived()
        {
            return "OK, payment received. Thank you!";
        }
    }
}
