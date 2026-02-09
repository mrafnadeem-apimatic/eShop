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

        const handleError = (err) => {
            console.error("PayPal Buttons error", err);

            const message =
                "Something went wrong while processing your PayPal payment. Please try again or use the card checkout above.";

            // Surface the error to the user; if there is no dedicated banner,
            // fall back to a simple alert.
            const errorBanner = document.querySelector("[data-paypal-error]");
            if (errorBanner) {
                errorBanner.textContent = message;
                errorBanner.style.display = "block";
            } else {
                alert(message);
            }
        };

        await window.paypal.Buttons({
            style: {
                layout: "vertical",
                color: "gold",
                shape: "rect",
                label: "paypal"
            },
            createOrder: function () {
                return dotNetRef.invokeMethodAsync("CreatePayPalOrder")
                    .catch((err) => {
                        handleError(err);
                        throw err;
                    });
            },
            onApprove: function (data) {
                return dotNetRef.invokeMethodAsync("CompletePayPalCheckout", data.orderID)
                    .catch((err) => {
                        handleError(err);
                        throw err;
                    });
            },
            onError: function (err) {
                handleError(err);
            }
        }).render(container);
    } catch (error) {
        console.error("Failed to initialize PayPal Smart Payment Buttons.", error);
    }
}

