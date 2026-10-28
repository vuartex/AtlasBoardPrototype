# Phase 19Y — Emotes & Maps Owner QA

**Candidate only; requires Unity compile, Firebase emulator, live two-client and Android tests.**

1. Install ZIP at project root preserving paths. Do NOT run old Pawn rebuild; do NOT delete existing Kenney sprite files.
2. Unity (Play Mode OFF): `Atlas Board > QA & Diagnostics > Build Emote Runtime Catalog`. Expected `ATB_EMOTES_RUNTIME status=ready sprites=29 missing=0`. If not 29, stop and repair sprites.
3. `Shop > Items > Emotes`: 26 purchasable reactions with preview; free Happy / Sad / Angry are NOT priced; Maps tab is informative only. Germany 30,000 and Mexico 25,000 are not buyable/active yet.
4. Buy one reaction and check ETag wallet Gold exactly once, Owned on refresh, and persistence after restart. Wallet must be verified/eligible.
5. Career > Inventory > Emotes: 3 free + purchased; Add/Remove loadout; maximum 6; require removal before adding 7th; reload and verify persisted six-slot list.
6. Start live online match. Click own Player HUD; select one equipped emote. It should appear above bottom HUDs or below top HUDs for 2.5 sec. Bot/other user HUDs should not allow sending.
7. Wait 3 seconds between messages; 20th send starts 3 minute mute, sending during mute must fail with localized warning. UI-only cooldown is not server proof: verify RTDB emulator denies attempts at bypass.
8. Two authenticated users on separate devices: send/receive/emote rejoin, no duplicate bubble when polling again, no old room replay. Test Remote seat ownership and UID auth, 7 languages and Android.
9. Note prior Pawn new-match stale-selection issue remains OPEN; do not mark as resolved by this emote package.
10. No changes to gameplay RNG / TurnManager / existing Pawn appearances. Firebase rules changed, **deploy needed only after emulator + syntax validation**. No automated GitHub push.
