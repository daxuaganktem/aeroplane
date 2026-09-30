#!/bin/sh
# Wraps src/game.html (a page fragment) into a standalone index.html.
set -e
{
  printf '<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
  printf '<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover">\n'
  printf '<meta name="apple-mobile-web-app-capable" content="yes">\n<meta name="mobile-web-app-capable" content="yes">\n'
  printf '</head>\n<body>\n'
  cat src/game.html
  printf '</body>\n</html>\n'
} > index.html
