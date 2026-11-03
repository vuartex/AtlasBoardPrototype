# Phase19BV — Owner acceptance (Live Spark Guest QA)

Use the same existing Editor menu, **Build Two-Client Guest Test v1**, then **Launch Live Spark Guest QA (No Steam)**. Do not launch the Steam Editor client for Guest auth testing.

1. **Existing registered Guest:** Open Career → ATLAS HESABI; enter the previously registered email with the correct password. Expect visible **Giriş başarılı** confirmation. Close it and open Career again. Expect Career, not another login prompt. Verify UID, Gold, XP, purchases and history against the same `/users/{uid}`. The registration screen must not create a new UID when the current Guest is already registered.
2. **Blank password:** Valid email + empty password → "Şifre alanı boş bırakılamaz" (`ATB739`). No Auth network request should be sent.
3. **Incorrect password:** Valid email + wrong password → single popup `ATB733` rather than duplicated text or silent dismissal.
4. **Password reset:** In the same login form, choose ŞİFREMİ UNUTTUM. A separate email input opens; enter valid registered email and send. Expect generic confirmation (not an account-existence statement). Check Inbox/Spam and Firebase Auth password-reset email template. Use the link to change the password. Old password should fail; new password should sign in under the **same Firebase UID**.
5. **Restart and cross-account:** Close Guest EXE and reopen. The same registered account and its wallet should persist; sign in with another account only deliberately. Steam account editor identity continuity owner PASS must remain unchanged.
6. **Error codebook:** `ATB739` blank password; `ATB741` invalid reset address; `ATB742` reset request transport/provider failure. `ATB733` remains wrong credentials. No raw Firebase provider/operation text or private credentials in player UI.

**Validation:** source contract PASS is not an Auth/RTDB end-to-end PASS. Firebase production Auth and email delivery were not exercised here. Never delete or merge users to force the test to pass. No Firebase production rules deploy.
