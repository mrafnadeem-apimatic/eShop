let paypalScriptPromise;

function loadPayPalScript(clientId) {
    return new Promise((resolve, reject) => {
        if (!clientId) {
            console.warn("PayPal client ID is missing. Skipping PayPal SDK load.");
            resolve();
            return;
        }

        if (document.querySelector('script[data-paypal-sdk=\"true\"]')) {
            resolve();
            return;
        }

        const script = document.createElement('script');
        script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(clientId)}&currency=USD`;
        script.dataset.paypalSdk = 'true';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load PayPal SDK.'));

        document.head.appendChild(script);
    });
}

export async function initializePayPalButton(dotNetRef, createOrderUrl, clientId) {
    if (!paypalScriptPromise) {
        paypalScriptPromise = loadPayPalScript(clientId)
        paypalScriptPromise.catch(error => {
            console.error('Failed to load PayPal SDK.', error);
            paypalScriptPromise = null;
            throw error;
        });
    }

    await paypalScriptPromise;

    if (!window.paypal || !dotNetRef) {
        console.error('PayPal SDK is not available or DotNet reference is missing.');
        return;
    }

    const container = document.getElementById('paypal-button-container');
    if (!container) {
        return;
    }

    // Clear any previously rendered buttons before re-rendering.
    container.innerHTML = '';

    window.paypal.Buttons({
        style: {
            layout: 'vertical',
            color: 'gold',
            shape: 'rect',
            label: 'paypal'
        },
        onClick: function (data, actions) {
            return dotNetRef.invokeMethodAsync('ValidateCheckoutBeforePayPalAsync')
                .then(isValid => {
                    if (isValid) {
                        return actions.resolve();
                    }

                    return actions.reject();
                })
                .catch(err => {
                    console.error('Error validating checkout info before PayPal button click', err);
                    return actions.reject();
                });
        },
        createOrder: function () {
            return fetch(createOrderUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to create PayPal order.');
                    }

                    return response.json();
                })
                .then(data => data.paypalOrderId);
        },
        onApprove: function (data, actions) {
            // data.orderID is the PayPal order identifier that the backend created.
            return dotNetRef.invokeMethodAsync('CompletePayPalCheckoutAsync', data.orderID);
        },
        onError: function (err) {
            console.error('PayPal Checkout error', err);
        }
    }).render('#paypal-button-container');
}

