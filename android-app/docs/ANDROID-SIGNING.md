# Android release signing

Android signing is separate from Windows Authenticode signing. A Windows
code-signing certificate cannot sign an APK.

## Release key

1. Create one organization-controlled Android keystore and alias.
2. Store it outside the repository on protected, backed-up media.
3. Keep at least two secured offline backups.
4. Never commit the keystore or its password.
5. Use the same release identity for all updates; losing it can prevent upgrades.

During publishing, pass signing passwords through protected environment variables
or a secured local file. Do not write them into the project file, script, build
log, or project file.

The release build should produce:

- Signed APK for controlled technician sideloading.
- AAB only if Google Play distribution is later approved.
- SHA-256 checksum and version manifest.

Before distribution, verify:

- Package ID is `com.aesprogrammer.troubleshooter`.
- Version number is incremented.
- Debugging is disabled.
- No API key, subscriber cipher, local credential file, JAR staging archive, or
  unnecessary permission is packaged.
- The APK signature verifies.
