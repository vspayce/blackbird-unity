#!/bin/bash
# Archive the Xcode project Unity made (Black Bird ▸ Apple ▸ Build Xcode Project for Apple TV / iPhone and iPad)
# and upload it to App Store Connect, where it appears in TestFlight once Apple has processed it (~15-30 min).
#
#   Tools/apple/testflight.sh tvOS      (or iOS)
#
# Needs Tools/apple/team.txt (the paid team's ID). With an App Store Connect API key it runs unattended:
# put AuthKey_<KEYID>.p8 in Tools/apple/ (it is git-ignored) and the issuer ID in Tools/apple/issuer.txt.
# Without one, xcodebuild uses the Apple ID signed in to Xcode.
set -euo pipefail
cd "$(dirname "$0")/../.."
PLATFORM="${1:-tvOS}"
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
TEAM="$(cat Tools/apple/team.txt)"
PROJ="Builds/$PLATFORM/Unity-iPhone.xcodeproj"
ARCHIVE="Builds/$PLATFORM.xcarchive"
EXPORT="Builds/$PLATFORM-upload"
DEST="generic/platform=$PLATFORM"; [ "$PLATFORM" = iOS ] && DEST="generic/platform=iOS"

AUTH=()
KEY="$(ls Tools/apple/AuthKey_*.p8 2>/dev/null | head -1 || true)"
if [ -n "$KEY" ] && [ -f Tools/apple/issuer.txt ]; then
  KEYID="$(basename "$KEY" .p8)"; KEYID="${KEYID#AuthKey_}"
  AUTH=(-authenticationKeyPath "$PWD/$KEY" -authenticationKeyID "$KEYID" -authenticationKeyIssuerID "$(cat Tools/apple/issuer.txt)")
fi

rm -rf "$ARCHIVE" "$EXPORT"
xcodebuild -project "$PROJ" -scheme Unity-iPhone -configuration Release -destination "$DEST" \
  -archivePath "$ARCHIVE" -allowProvisioningUpdates ${AUTH[@]+"${AUTH[@]}"} \
  DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic archive | tail -5

cat > Builds/ExportOptions.plist <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>upload</string>
  <key>teamID</key><string>$TEAM</string>
  <key>signingStyle</key><string>automatic</string>
  <key>uploadSymbols</key><true/>
  <key>manageAppVersionAndBuildNumber</key><false/>
</dict></plist>
PLIST

xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist Builds/ExportOptions.plist \
  -exportPath "$EXPORT" -allowProvisioningUpdates ${AUTH[@]+"${AUTH[@]}"} | tail -8
echo "Uploaded $PLATFORM build. It shows in App Store Connect ▸ TestFlight once processed."
