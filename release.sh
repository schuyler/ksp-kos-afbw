#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

VERSION="${1:?usage: release.sh <version>}"
IFS=. read -r MAJOR MINOR PATCH <<<"$VERSION"
PREV_TAG=$(git describe --tags --abbrev=0 2>/dev/null || true)

sed -i -E "s/^VERSION[[:space:]]*=.*/VERSION = $VERSION/" Makefile
sed -i -E "s/(\"VERSION\": \{ \"MAJOR\": )[0-9]+(, \"MINOR\": )[0-9]+(, \"PATCH\": )[0-9]+(, \"BUILD\": )[0-9]+/\1$MAJOR\2$MINOR\3${PATCH}\40/" kOS-AFBW.version

make package

git add Makefile kOS-AFBW.version
git commit -m "Release v$VERSION"

NOTES_FILE=$(mktemp)
git log "${PREV_TAG:+$PREV_TAG..}HEAD" --pretty=format:'- %s' > "$NOTES_FILE"
${EDITOR:-vi} "$NOTES_FILE"

git tag -a "v$VERSION" -F "$NOTES_FILE"
git push origin main "v$VERSION"
gh release create "v$VERSION" "kOS-AFBW-v$VERSION.zip" --title "v$VERSION" --notes-file "$NOTES_FILE"
