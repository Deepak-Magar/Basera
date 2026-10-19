// Basera site script: only what HTMX cannot do (camera preview, QR display, service worker).
"use strict";

// PWA: register the service worker (static assets only; see /sw.js).
if ("serviceWorker" in navigator) {
    window.addEventListener("load", () => navigator.serviceWorker.register("/sw.js").catch(() => { }));
}

// A failed HTMX request leaves the page as it was (HX-Reswap: none) and raises "basera:error"
// (BaseraExceptionHandler). Show it as a toast with the reference number the logs are keyed by.
document.body.addEventListener("basera:error", (event) => {
    const detail = event.detail || {};
    const toast = document.createElement("div");
    toast.className = "toast text-bg-danger border-0";
    toast.setAttribute("role", "alert");
    const body = document.createElement("div");
    body.className = "toast-body";
    body.textContent = detail.correlationId ? `${detail.message} (${detail.correlationId})` : (detail.message || "Error");
    toast.appendChild(body);
    document.getElementById("toasts").appendChild(toast);
    bootstrap.Toast.getOrCreateInstance(toast, { delay: 8000 }).show();
    toast.addEventListener("hidden.bs.toast", () => toast.remove());
});

// HTMX does not swap error responses by default; a network failure gets the same toast.
document.body.addEventListener("htmx:sendError", () => {
    document.body.dispatchEvent(new CustomEvent("basera:error", { detail: { message: "No connection. Please try again." } }));
});
