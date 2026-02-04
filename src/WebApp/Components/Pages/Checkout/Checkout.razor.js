let paypalScriptLoaded = false;
let paypalScriptLoading = null;

function loadPayPalSdk(clientId, currency) {
    if (paypalScriptLoaded) {
        return Promise.resolve();
    }

    if (paypalScriptLoading) {
        return paypalScriptLoading;
    }

    if (!clientId) {
        return Promise.reject(new Error("PayPal client id is not configured."));
    }

    paypalScriptLoading = new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(clientId)}&currency=${encodeURIComponent(currency)}`;
        script.onload = () => {
            paypalScriptLoaded = true;
            resolve();
        };
        script.onerror = () => reject(new Error("Failed to load PayPal SDK script."));
        document.head.appendChild(script);
    });

    return paypalScriptLoading;
}

export async function initializePayPalButtons(dotNetRef, clientId, currency, container) {
    try {
        await loadPayPalSdk(clientId, currency);

        if (!window.paypal || !window.paypal.Buttons) {
            throw new Error("PayPal SDK is not available.");
        }

        await window.paypal.Buttons({
            style: {
                layout: "vertical",
                color: "gold",
                shape: "rect",
                label: "paypal"
            },
            createOrder: function () {
                return dotNetRef.invokeMethodAsync("CreatePayPalOrder");
            },
            onApprove: function (data) {
                return dotNetRef.invokeMethodAsync("CompletePayPalCheckout", data.orderID);
            },
            onError: function (err) {
                console.error("PayPal Buttons error", err);
            }
        }).render(container);
    } catch (error) {
        console.error("Failed to initialize PayPal Smart Payment Buttons.", error);
    }
}

