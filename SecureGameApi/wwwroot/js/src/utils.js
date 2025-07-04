// utils.js
export async function computeHmac(secretBytes, message) {
    const key = await crypto.subtle.importKey(
        "raw",
        new Uint8Array(secretBytes),
        { name: "HMAC", hash: "SHA-256" },
        false,
        ["sign"]
    );
    const msgBuffer = new TextEncoder().encode(message);
    const sigBuffer = await crypto.subtle.sign("HMAC", key, msgBuffer);
    const sigArray = Array.from(new Uint8Array(sigBuffer));
    // Burada spread operatörüyle doğru şekilde dönüştür
    const sigString = btoa(String.fromCharCode(...sigArray));
    return sigString;
}
