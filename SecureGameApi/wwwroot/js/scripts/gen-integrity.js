// wwwroot/js/scripts/gen-integrity.js

const path = require('path');
const fs = require('fs');
const crypto = require('crypto');

// __dirname === .../wwwroot/js/scripts
const bundlePath = path.resolve(__dirname, '../dist/game.bundle.js');
const integrityPath = path.resolve(__dirname, '../dist/integrity.json');

const buf = fs.readFileSync(bundlePath);
const hash = crypto.createHash('sha384').update(buf).digest('base64');
const integrity = 'sha384-' + hash;

fs.writeFileSync(integrityPath, JSON.stringify({ bundleHash: integrity }, null, 2));
console.log(`› integrity.json güncellendi: ${integrity}`);
