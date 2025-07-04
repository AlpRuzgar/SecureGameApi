// gen-integrity.js
import { readFileSync, writeFileSync } from 'fs';
import { createHash } from 'crypto';

// 1) Okuyacağımız bundle dosyası
const bundlePath = 'dist/game.bundle.js';
const outputPath = 'dist/integrity.json';

// 2) Dosyayı oku ve SHA-384 hash hesapla
const buf = readFileSync(bundlePath);
const hash = createHash('sha384').update(buf).digest('base64');
const bundleHash = `sha384-${hash}`;

// 3) integrity.json diye yaz
writeFileSync(outputPath,
    JSON.stringify({ bundleHash }),
    'utf8'
);

console.log('integrity.json oluşturuldu:', bundleHash);
