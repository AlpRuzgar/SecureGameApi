// tamperCheck.js
(async function () {
    try {
        // 1) Beklenen hash'i al
        const res1 = await fetch('js/dist/integrity.json', { cache: 'no-store' });
        const { bundleHash: expected } = await res1.json();

        // 2) Gerçek bundle'ı indir (aynı anda)
        const res2 = await fetch('js/dist/game.bundle.js', { cache: 'no-store' });
        const buf = await res2.arrayBuffer();

        // 3) SHA-384 hesapla
        const digest = await crypto.subtle.digest('SHA-384', buf);
        const arr = new Uint8Array(digest);
        const b64 = btoa(String.fromCharCode(...arr));
        const actual = 'sha384-' + b64;

        // 4) Karşılaştır
        if (actual !== expected) {
            alert('Oyun dosyası değiştirilmiş. Lütfen sayfayı yenileyin.');
            throw new Error(`Integrity hata: ${expected} ≠ ${actual}`);
        }
        // Geçtiyse hiçbir şey yapma, oyun yüklenmeye devam eder
    } catch (e) {
        console.error('Tamper-Detection hatası:', e);
        // Yüklemeyi kes: body’yi boşaltabiliriz
        document.documentElement.innerHTML = '<h1>Oyun yüklenemedi.</h1>';
    }
})();
