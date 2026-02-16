let paypalSdkLoaded = false;

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.async = true;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load PayPal SDK'));
        document.head.appendChild(script);
    });
}

export async function initPayPalButton(dotNetRef) {
    const container = document.getElementById('paypal-button-container');
    if (!container) return;

    container.innerHTML = '';

    if (window.paypal && paypalSdkLoaded) {
        renderButtons(container, dotNetRef);
        return;
    }

    try {
        const res = await fetch('/api/paypal/config', { credentials: 'include' });
        if (!res.ok) throw new Error('Failed to get PayPal config');
        const config = await res.json();
        const env = config.environment === 'live' ? 'www' : 'www.sandbox';
        const sdkUrl = `https://${env}.paypal.com/sdk/js?client-id=${encodeURIComponent(config.clientId)}&components=buttons`;
        await loadScript(sdkUrl);
        paypalSdkLoaded = true;
        renderButtons(container, dotNetRef);
    } catch (err) {
        console.error('PayPal init error:', err);
        container.innerHTML = '<p class="paypal-error">Unable to load PayPal. Please try again or use another payment method.</p>';
    }
}

function renderButtons(container, dotNetRef) {
    container.innerHTML = '';

    window.paypal.Buttons({
        createOrder: async () => {
            const res = await fetch('/api/paypal/order', {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' }
            });
            const data = await res.json();
            if (!res.ok) {
                const detail = data.detail || data.title || 'Failed to create PayPal order';
                throw new Error(detail);
            }
            return data.paypalOrderId;
        },
        onApprove: async (data) => {
            try {
                await dotNetRef.invokeMethodAsync('CompletePayPalCheckoutAsync', data.orderID);
            } catch (err) {
                console.error('PayPal onApprove error:', err);
            }
        },
        onError: (err) => {
            console.error('PayPal button error:', err);
            container.innerHTML = '<p class="paypal-error">PayPal encountered an error. Please try again.</p>';
        }
    }).render('#paypal-button-container');
}

export function destroyPayPalButton() {
    const container = document.getElementById('paypal-button-container');
    if (container) container.innerHTML = '';
}
