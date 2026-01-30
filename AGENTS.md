# Product Requirements Doc for Migration

Here are **product-level requirements** for migrating the current PayPal integration to the official PayPal .NET Server SDK (PayPalServerSDK). The base branch for this migration will be [mrafnadeem-apimatic/eShop at MAli/paypal\_new\_agent](https://github.com/mrafnadeem-apimatic/eShop/tree/MAli/paypal_new_agent).

---

## 1\. Functional parity

- **FR-1** The checkout flow must remain: “Pay with PayPal” → redirect to PayPal → user approves → return to checkout with approval → “Place order” → order created with `PayPalOrderId` → PaymentProcessor captures when stock is confirmed. No change to user-visible steps or URLs.  
- **FR-2** WebApp must still create a PayPal order with the basket total (same amount and currency), obtain an approval URL, store the PayPal order ID in session, and redirect the user to that URL. Behavior must match current `/paypal/pay`.  
- **FR-3** WebApp must still handle return and cancel: `/paypal/return` and `/paypal/cancel` behavior (redirects, query params, session validation) must be unchanged.  
- **FR-4** PaymentProcessor must still capture the approved PayPal order by ID after stock confirmation, and publish success/failure integration events based on capture result. Behavior must match current `PayPalPaymentService`.  
- **FR-5(Optional)** When PayPal is disabled or not configured, PaymentProcessor must continue to use the existing “simulated” behavior (`PaymentSucceeded` flag). No new behavior when PayPal is off.

---

## 2\. Configuration and environment

- **FR-6** Support Sandbox and Live via configuration (e.g. `Environment` / `PayPalEnvironment`). Default must remain Sandbox for non-production.  
- **FR-7** Credentials (Client ID and Client Secret) must remain config-driven (e.g. `appsettings` / environment variables). No hardcoded secrets.  
- **FR-8** Existing configuration entry points must keep working: WebApp `PayPal:*` (RedirectUri, CancelUrl, CurrencyCode, etc.) and PaymentProcessor `PaymentOptions` (UsePayPal, PayPalClientId, PayPalClientSecret, PayPalEnvironment). Mapping into SDK client options (e.g. `ClientCredentialsAuth`, `Environment`) is an implementation detail; product behavior must not regress.

---

## 3\. Security

- **FR-9** Session-based validation must stay: the PayPal order ID from the return URL is only accepted when it matches the ID stored in session at order creation. No weakening of this check.  
- **FR-10** Credentials must not be logged or exposed in responses. SDK usage (e.g. built-in auth) must not introduce new credential leakage.  
- **FR-11** Capture must still be performed only by PaymentProcessor for orders that exist in the system and have a valid `PayPalOrderId`. No new paths that capture without an order.

---

## 4\. Error handling and observability

- **FR-12** Failures when creating the PayPal order (e.g. token or create-order API errors) must still result in a clear user-facing outcome (e.g. problem/error page or redirect) and must be logged with enough context to diagnose (e.g. order id, basket, HTTP/API status).  
- **FR-14** Where the SDK supports retries/timeouts, configure them so that transient failures are retried and long-running calls do not hang indefinitely. Product requirement: same or better resilience as current manual HTTP (e.g. no silent infinite waits).

---

## 5\. Compatibility and rollout

- **FR-15** Migration must be possible without changing the public API of the app (same endpoints, query params, and redirect URLs). No breaking change for `/paypal/*` or checkout query params.

---

## 6\. Non-functional(Optional)

- **FR-18(Optional)** The solution must be maintainable: use the official SDK as the single way to talk to PayPal (no duplicate manual OAuth or Orders API calls for the same operations). Document which SDK version and which PayPal APIs are used. We are likely going to reference the SDK NuGet in two separate projects due to existing implementation calling PayPal API directly from two different projects. So this technical debt will exist.

---

## 7\. Documentation and operations(Optional)

- **FR-19(Optional)** Document the chosen SDK package and version (e.g. PayPalServerSDK), how configuration maps to the SDK (credentials, environment, timeouts/retries if configured), and any known SDK limitations that affect this integration. Comments in the SDK integration are enough to satisfy this, or an updated README.

---

## 8\. Testing and validation

- **FR-21** End-to-end tests must cover: create PayPal order from checkout, return with valid session, place order with `PayPalOrderId`, and PaymentProcessor capture success and failure. These scenarios must pass after migration.

---

## Summary

| Area | Focus |
| :---- | :---- |
| **Functional** | Same flows (create → approve → return → place order → capture), same fallback when PayPal is off. |
| **Config** | Same envs (Sandbox/Live) and same config surface; map into SDK without breaking existing config. |
| **Security** | Keep session validation and no credential leakage; capture only for valid orders. |
| **Errors** | Clear outcomes and logging for create/capture failures; bounded retries/timeouts. |
| **Compatibility** | No breaking changes to URLs or behavior; optional toggle for safe rollout. |
| **Non-functional(Optional)** | No UX regression; single, documented SDK usage. |
| **Docs & ops(Optional)** | Document SDK version, config, and failure modes. |
| **Testing** | E2E and Sandbox validation before Live. |

These requirements define *what* the product must do after migrating to the official PayPal .NET Server SDK; the actual code changes (replacing `HttpClient`/manual JSON with SDK client and Orders controller) would be designed to satisfy them.  