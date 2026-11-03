# Phase19BW owner acceptance (partial REST UI fix)

- Install the ZIP's complete source files over the project root. Do not alter Firebase rules or rebuild any other QA launcher.
- Run Unity compile; no CS errors. The two old RematchFastForwardQA CS0618 warnings should disappear.
- Existing **Build Two-Client Guest Test v1 → Launch Live Spark Guest QA (No Steam)** remains unchanged.
- In an existing registered REST Guest session, open Career and then Settings → Account; the account summary should appear rather than a new Login prompt. Reopen the app and verify `ATB_SESSION_GATE screen=bootstrap state=registered_verified` before checking Account.
- Watch the console logs `ATB_SESSION_GATE`: `screen=global` shows current verified-REST state, `screen=account` shows account entry, and `store_native_registered_gate` explicitly surfaces the remaining Store gate. Logs redact full UIDs; do not send passwords, tokens, or reset links.
- In Forgot Password, title, explanation and email field should not overlap; after submission the message must recommend checking the Spam/Gereksiz folder.
- **Do not mark Store Daily PASS:** the meta-economy native-only registered check and REST anonymous flag are not changed in this package; completing this requires the canonical Meta Economy source update and its emulator validation.
- Do not delete/recreate Firebase users or transfer wallet records for this QA step.
