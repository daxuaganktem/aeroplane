#!/bin/sh
# Wraps src/game.html (a page fragment) into a standalone index.html for the web and installable app,
# then copies the files the native apps bundle into www/ (used by Capacitor).
set -e
cd "$(dirname "$0")"
{
  printf '<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
  printf '<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover">\n'
  printf '<meta name="theme-color" content="#a8d6ea">\n'
  printf '<meta name="apple-mobile-web-app-capable" content="yes">\n<meta name="mobile-web-app-capable" content="yes">\n'
  printf '<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">\n'
  printf '<meta name="apple-mobile-web-app-title" content="Skyline Glide">\n'
  printf '<link rel="manifest" href="manifest.webmanifest">\n'
  printf '<link rel="icon" type="image/png" sizes="32x32" href="icons/favicon-32.png">\n'
  printf '<link rel="apple-touch-icon" href="icons/apple-touch-icon.png">\n'
  printf '</head>\n<body>\n'
  cat src/game.html
  # Offline support for the installed web app. Skipped inside the native apps, which bundle their files.
  printf '<script>\n'
  printf "if ('serviceWorker' in navigator && /^https?:\$/.test(location.protocol) && !(window.Capacitor && window.Capacitor.isNativePlatform && window.Capacitor.isNativePlatform())) {\n"
  printf "  window.addEventListener('load', () => navigator.serviceWorker.register('sw.js').catch(() => {}));\n"
  printf '}\n</script>\n'
  printf '</body>\n</html>\n'
} > index.html

rm -rf www
mkdir -p www/icons
cp index.html manifest.webmanifest www/
cp icons/*.png www/icons/
