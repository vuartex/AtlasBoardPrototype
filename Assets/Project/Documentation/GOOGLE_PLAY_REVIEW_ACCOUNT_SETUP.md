# Atlas Board — Google Play Review Account Setup

**Status: PREPARATION ONLY — NO LIVE REVIEW ACCOUNT WAS CREATED IN THIS PACKAGE.**

The real Firebase project is `atlasboard-usa`. The existing client profile writer already stores `general.accountType = "registered"` for a Firebase Auth **non-anonymous** account, and `"guest"` for anonymous accounts. The existing RTDB rules require these exact values. `general.accountType` is a profile classification, **NOT an admin role**, and must not be repurposed for authorization.

## Create a dedicated reviewer account (owner action in Firebase Console)

1. Firebase Console → select **atlasboard-usa** → **Authentication** → **Users** → **Add user**. Choose a dedicated email address **you control** and a strong unique password; keep both OUT of the GitHub repository, Unity project, logs and build artifacts.
2. Ensure **Email/Password** sign-in is enabled under Authentication → Sign-in method. A Firebase Auth account is not necessarily a complete RTDB AtlasBoard profile until it signs into the app.
3. Sign into the CURRENT Android release-candidate using the dedicated reviewer credentials once. Confirm that the profile at `/users/{reviewerUid}` exists and `general.accountType` is exactly `registered`. Also confirm that `userId` matches the authenticated Firebase UID and that Profile, Career, Shop and Private/Public Rooms can be opened.
4. Never set `/users/{uid}/general/accountType` to `admin`. It is forcibly mirrored by the client and the existing rules allow only `registered` or `guest`. Never assume a text field, locally modified APK or an allowlisted developer-wallet tester is an authenticated administrative principal.
5. **Preconfigure access using legitimate app workflows**: if a screen requires owned cosmetics or a premium map, use existing properly authorized payment/ownership mechanisms in a controlled owner QA session and verify server-confirmed ownership, rather than manually inserting wallet or inventory flags. Do NOT give reviewers developer-console access, owner QA unlock codes or the `devWalletTesters` grant. A separate regular test account may be needed for two-human online matches.
6. For public rooms and multiplayer, ensure another clean, compatible test account/build can join or provide a reproducible private test flow. Google review cannot depend on a permanently online personal Host unless you guarantee that experience. Never advertise unavailable game features as accessible.
7. Use a stable account without MFA/OTP, location restrictions, expiry or password rotation during review. Do not post credentials in screenshots, messages, Git or public documentation.

## Submit via Play Console

**Play Console → Policy and programs → App content → Sign-in details → Manage/Start → Add new instructions.** Provide the email/password only in the secured Play Console fields and English instructions. Official requirements: https://support.google.com/googleplay/android-developer/answer/15748846

### English reviewer instruction template (replace placeholders inside Play Console ONLY)

> Atlas Board reviewer account (email/password). Open the game, select Sign In, enter the supplied review email and password, then open the Main Menu. The account can access Profile, Career (Overview/History/Achievements/Inventory), Atlas Store, Settings, Help/FAQ, Credits, Private Table and Public Rooms. To test multiplayer, select Private Table to create a room or Public Rooms to join a compatible active room. No OTP or location-specific verification is required. [If any screen is restricted, explain how to access it using a legitimate reviewer-safe workflow.]

The actual reviewer credentials must be entered separately in Play Console, **not** shipped in this template. Test the account after installation from the exact signed AAB intended for review before submission.

## Owner QA and release security

- In an ordinary **non-Development public build**, `AtlasBoardOwnerConsole` and gameplay fast-forward QA scripts are compiled out. Ordinary registered reviewers cannot open them.
- `devWalletTesters/<uid> = true` is an existing protected wallet-test authorization flag. It is **NOT an admin role** and should not be used as a substitute for review-account authorization. A trusted future admin role would require an additional Firebase-authenticated, read-only-to-client server claim/ACL with rules validation and a full emulator review. Merely changing an RTDB profile string is insufficient.
- The current Career/XP rewards are derived from attested completed matches. There is no safe arbitrary persistent XP mint operation. Do not fabricate Career records for Google reviewers.
- **PRODUCTION APP CHECK and mixed-version server-side admission are still OPEN.** Do not claim the release is ready solely because the account exists.

## Acceptance status

`FIREBASE_REVIEW_USER=NOT_CREATED_BY_ASSISTANT` · `REVIEWER_SIGNIN=NOT_RUN` · `ALL_SCREENS_OWNER_QA=NOT_RUN` · `PLAY_CONSOLE_SUBMISSION=NOT_DONE` · `PRODUCTION=NOT_TOUCHED`.
