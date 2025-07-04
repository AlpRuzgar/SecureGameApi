#!/usr/bin/env bash
JSFILE="wwwroot/js/dist/game.bundle.js"
HTML="wwwroot/start.html"

HASH=$(openssl dgst -sha384 -binary "$JSFILE" | openssl base64 -A)
SRI="sha384-$HASH"

# HTML i�indeki placeholder�� g�ncelle
sed -i "s|integrity=\"__SRI_HASH__\"|integrity=\"$SRI\"|g" "$HTML"

echo "SRI g�ncellendi: $SRI"
