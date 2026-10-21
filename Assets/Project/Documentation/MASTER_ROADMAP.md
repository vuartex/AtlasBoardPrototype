# ATLAS BOARD — MASTER ROADMAP (CANONICAL)

**Güncel konsolidasyon:** 21 Ekim 2026 · **Aktif checkpoint:** local v5.40.11 **OWNER REPORTED FIX — kalıcı Windows Live Spark Guest adı ve Firebase oturumu yeniden başlatmada korunuyor; RTDB `guestNameClaims` rules deploy kullanıcı tarafından başarılı bildirildi (`firebase.cmd`).** v5.40.10 Guest/Host lobi isimleri ve v5.40.8 canlı Editor + Guest Public/Private iki-Human oyun **OWNER MANUAL PASS** (4 maç, aynı Windows makinede iki proses). Üç kontrollü yeniden başlatma/UID eşitliği, farklı UID ile aynı ad claim reddi, Firebase Auth orphan sayımı, Host graceful close/crash, reconnect/Host Migration, tablet/full UI, Android ve production security **AYRI ACCEPTANCE OPEN**. · Unity 6000.5.6f1 · `vuartex/AtlasBoardPrototype` · `atlasboard-usa` · Android önce, Windows/Steam korunur.

> **ÖNCELİK KURALI:** Bu belgenin 1–10 bölümleri geçerli ürün kararı ve test durumudur. Daha alttaki v5.30.0 ve öncesinin tarihsel kaydı geriye dönük denetim içindir. Tarihsel “current/PASS/next” yazıları güncel durumun üzerine çıkamaz. **Kod hazır ≠ Unity compile PASS ≠ gerçek cihaz PASS ≠ iki cihaz online PASS.**
>
> **TEK DOSYA KURALI:** Bundan sonra yalnız `Assets/Project/Documentation/MASTER_ROADMAP.md` kanoniktir. Her yama/sürümde yeni QA/phase/roadmap/next-chat dokümanı açma. Anlamlı mimari değişikliği, kullanıcı test sonucu veya phase milestone olduğunda bu dosyayı güncelle; küçük düzenlemelerde ayrıca doküman yazma. Eski dosyalar şu an otomatik silinmez; bu roadmap kaynaklarını konsolide eder.

## 0M. 21 Ekim 2026 — v5.40.11 PERSISTENT LIVE GUEST + RTDB UID-OWNED NAME CLAIM — OWNER UPDATE

**DURUM: OWNER REPORTED FIX / RTDB RULES DEPLOY REPORTED SUCCESS; KONTROLLÜ REGRESYON TESTLERİ KISMEN OPEN.** Kullanıcı v5.40.11 sonrası sorunun düzeldiğini bildirdi. Üç ayrı restart için aynı UID+adın logla birebir karşılaştırması, eşzamanlı farklı UID isim claim reddi ve Firebase Auth anonim hesap sayısı ayrı test kanıtı olarak henüz sunulmadı; bunlara **PASS** yazma. v5.40.10 lobi isimleri ve v5.40.8 iki istemcili Public/Private maç owner gözlemleri geçerli; Steam/Editor ve Spark/free kontratı korunur.

- **Kök neden:** `AtlasBoardLobbyRuntimeBridge.EnsureLiveGuestQaIdentityAsync` v5.40.10'da her EXE başlangıcında Firebase Anonymous `accounts:signUp` çağırıyordu. `AtlasBoardGuestIdentity` UID hash'inden her seferinde başka isim üretiyordu. Bu Firebase Auth'ta her yeniden açılışta yeni anonim kullanıcı bırakıyordu.
- **v5.40.11 kaynak değişikliği:** Aynı mevcut bridge dosyasında Windows Development Guest için DPAPI CurrentUser şifreli refresh token + UID `Application.persistentDataPath/live_spark_guest_v1.dpapi` dosyasına atomik yazılır. Yeniden başlatınca yeni `signUp` yerine `securetoken` refresh yapılır; UID korunur. Restore/DPAPI/refresh hatasında **yeni UID otomatik oluşturma**, mevcut Guest adını değiştirme ve sessiz orphan yaratma YASAK. Unity PlayerPrefs'e/token log'a credential yazılmaz; Windows kullanıcı profili değişirse credentials otomatik taşınmaz.
- **RTDB global claim:** `Backend/Firebase/database.rules.json` altına `/guestNameClaims/{Guest+16hex}` eklenir. Her isim bir tek `auth.uid` tarafından first-writer-wins `if-match:null_etag` ile claim edilir; başka UID yazamaz, claim silinmez ve UID/name/createdAt değiştirilemez. Bu küçük kalıcı kayıt tek Guest hesabı başına bir defa oluşur. Mevcut `/guestProfiles` kısa ömürlü kayıtları/cleanup sözleşmesi korunur; XP/Gold/wallet güvenilir authority buraya taşınmaz. Spark/free, Cloud Functions/Blaze yok.
- **Güvenlik sınırı:** Guest adı kullanıcı hesabının Firebase UID kimliği değildir; UID/Auth token otoritedir. Aynı UID için hash deterministiktir; oda girişinde Guest adını Host zaten giriş isteğinin UID'sinden hesaplar. Claim ilk yazan sahibi korur, ama dış istemci bu biçimi taklit edebilir; RTDB kuralları yalnız claim üzerinde sahipliği garanti eder. Bunu tüm platformlarda global profil adı rezervasyonu diye genişletme.
- **DEPLOY GATE / OWNER RESULT:** `Backend/Firebase/database.rules.json` içindeki `guestNameClaims` dalını kullanıcı manuel olarak `firebase.cmd deploy --only database --project atlasboard-usa` komutuyla deploy ettiğini ve sorunun düzeldiğini bildirdi. İlk `firebase` komutu Windows PowerShell execution-policy nedeniyle `.ps1` bloklandı; güvenlik ayarı değiştirilmeden `firebase.cmd` kullanıldı. **Deploy owner-reported SUCCESS**; Firebase Console aktif rules hash/diff ayrı bağımsız doğrulama olarak yapılmadı. Firestore, Cloud Functions, Hosting, Blaze ve production ücretli servis deploy edilmedi. Gelecekte her RTDB rule değişikliğinde deploy için PowerShell `firebase.cmd` komutu mutlaka verilmeli.
- **OWNER TEST (KISMİ):** Kullanıcı Guest kimliği sorununun düzeldiğini bildirdi; isim/oturum kalıcılığı için owner-reported fix var. **Ayrı OPEN kanıt:** aynı Windows hesabında EXE 3 kez kapat/aç → `identity=reused`, UID ve isim sabit; yeni anonim Firebase Auth kullanıcı sayısı ve `guestNameClaims` kayıt sayısı artmaz; farklı UID aynı ada claim atamaz (rules/ETag reject). Bu kontrollü testlerin çıktısı alınmadan bütünüyle SERVER ACCEPTANCE PASS yazma. Normal Host Leave ve ani Host Crash/epoch takeover ayrı lifecycle testidir.

---

### 0M.1 GÜNCEL ACCEPTANCE ÖZETİ — 21 Ekim 2026

| Kanıt kapısı | Sonuç |
|---|---|
| İki yerel proses, iki gerçek Firebase UID, dört Public/Private oyun, dice/buy/trade/snapshot | **OWNER MANUAL PASS** (v5.40.8; fiziksel iki cihaz değil) |
| Guest lobi P1=Jr. Vuartex, P2=Guest UID alias; maçta iki kimlik ayrık | **OWNER SCREENSHOT PASS** (v5.40.10) |
| Kalıcı Guest adı / yeniden açılış | **OWNER REPORTED FIXED** (v5.40.11; tekrar sayısı/UID logu belirtilmedi) |
| `guestNameClaims` RTDB Rules deploy | **OWNER REPORTED SUCCESS** (`firebase.cmd`) |
| 3 restart UID equality, orphan Auth/claim growth ve cross-UID claim reject | **OPEN — ayrı kabul testi** |
| Normal Host Leave → Guest menu, host crash migration, 20s/5min reconnect | **OPEN** |
| Android physical FPS/App Check/rewards, Career/Store trust boundaries | **OPEN** |

**Sıradaki roadmap gate:** En düşük riskli veri büyüme testiyle tek Guest UID/claim reuse değerini teyit et; sonra var olan N4 disconnect/reconnect, graceful leave ve gerçek crash takeover testlerine dön. Yeni auth manager veya HUD sahibi ekleme. Public/Private gameplay PASS korunmalı.

---

## 0L. 20 Ekim 2026 — v5.40.10 LIVE SPARK GUEST NAME / LOBBY OWNERSHIP FIX

**STATUS: OWNER VISUAL PASS — TWO DISTINCT LOBBY NAMES (v5.40.10), live Guest UID-bound alias works.** The v5.40.8 four-match owner manual gameplay PASS and the v5.40.9 host-close source candidate remain unchanged. New screenshots on Guest (room 353010, 4 seats) show P1 rapidly alternating between `Guest81842U` and `Jr. Vuartex`, while P2 is `Guest81842U (SEN)`. The user started `Launch_Guest_Live_Spark_QA.bat`, which is the correct real Firebase test path, NOT the cause. The older `Launch_Guest_Local_Emulators.bat` is a different local-emulator topology and must not be used for live RTDB acceptance.

**Verified source defect:** In `AtlasBoardAccountAccessUI.ApplyIdentityPresentation`, the local account presenter unconditionally rewrote `privateSeatNames[0].text = localGuestName` approximately every 1.25 seconds even when the caller was the Guest who owned P2, not the remote P1 Host. The authoritative `AtlasBoardPrivateLobbyUIController` then restored Host's snapshot name. This was a multi-writer presentation race, not evidence of two gameplay principals sharing the same Firebase UID. v5.40.10 removes Account UI ownership of online seat labels. The snapshot renderer owns all four names, and `AtlasBoardUnifiedProfilePresentation` may relabel only a UID-confirmed locally owned seat.

**Guest nickname concern:** The legacy offline `Guest#####X` is `PlayerPrefs`-local; multiple standalone processes on the same Windows profile can read the SAME value. This does NOT mean Firebase anonymous UIDs were duplicated. For **Live Spark Guest QA only**, the presentation alias is now `Guest` + the first 64 bits of SHA-256 of the authenticated anonymous Firebase UID, uppercase hex. The alias is process-scoped (not a credential, not saved to PlayerPrefs) and bound immediately after Anonymous Auth. The Live RTDB transport submits that UID-bound alias rather than a stale cached menu label. Host-authored RTDB admission verifies join UID matches its request key, derives Guest aliases from authenticated join UIDs, and refuses a room-local display-name collision. This is **not server-global nickname registration**; 64-bit alias collisions are theoretically possible and any user-supplied non-Guest alias is not a verified unique account name. Firebase UID is the sole authenticated principal.

**Regressions protected:** v5.40.7 READY/NOT READY transition, v5.40.8 Live Guest Anonymous Auth, v5.40.9 safe host-close, 5-sec Start, online HUD names/turns, gameplay intent/snapshot and Spark/free/no-Functions policies. Historical v5.40.10 behavior: a process restart acquired a new ephemeral Firebase UID; **this specific defect is superseded by v5.40.11 secure session restore** (see 0M). No Android, Steam Release, deployed rules or rewarded economy is modified.

**Required retest:** Apply full-file ZIP, Unity compile zero errors, rebuild Development Guest, run `Launch_Guest_Live_Spark_QA.bat`, join fresh Public + Private rooms with different UIDs. On both clients, monitor P1/P2 identities for 60 seconds with no flicker; READY→NOT READY→READY; then dice/buy/trade on both clients. Return Guest to menu and rejoin; run two separate Guest build processes simultaneously (they must not share a Live UID-bound nickname). Finally test graceful Host Leave closes room and returns Guest to menu. **Do not mark PASS until observed.** The emulator BAT is for local-only tests. The BAT distinction and QA sequence are recorded here in the canonical roadmap.

---

## 0K. 17 Ekim 2026 — v5.40.9 LIVE RTDB TWO-CLIENT OWNER PASS / SAFE HOST CLOSE + HOST LABEL FIX

**OWNER MANUAL PASS (v5.40.8):** Editor gerçek Steam/Firebase Vuartex hesabı ve ayrı Windows Development Guest QA anonim Firebase UID ile `atlasboard-usa` canlı Spark RTDB üzerinde aynı Public/Private odada buluştu. Owner 4 maç başlattı; lobby keşif/join, READY, start, zar/sıra, hareket, satın alma, ticaret ve para/mülk state'i iki istemcide eşleşti. Guest kapanıp/açıp odaya yeniden girme davranışının bazı bölümleri gözlemlendi. Bu **aynı Windows makinesindeki iki bağımsız proses** kanıtıdır; **iki ayrı fiziksel cihaz**, network cut/AFK/reconnect/temporary bot/expiry, kritik auction edge-case, server-authoritative rewarded progression veya Production PASS değildir. `ATB_LIVE_GUEST_QA auth=ready provider=anonymous`, `ATB_RTDB_REMOTE_QA join_confirmed`, `ready_submitted`, Editor `ATB_SPARK_MATCH_START ... remote_humans=1`, Guest takipçi zar/hareket ve BUY logları görüldü. Dört maç owner gözlemi, tüm kombinasyonların otomatik test edildiği anlamına gelmez.

### Açık kalan iki somut UI/lifecycle bug

- **L1 Guest Lobby Host DisplayName (P1):** Editor Host lobby `Vuartex`, Guest lobby P1+P2 her ikisi `Guest81842U` olarak görünüyor; match HUD iki tarafta Vuartex+Guest olarak doğru. Bu v5.40.9 kaynak adayında mevcut `AtlasBoardRtdbCasualLobbyTransport.SnapshotAsync` takipçi yolunda host adı şüpheli/Guest ise **host UID'ye bağlı /roomMembers kaydından yalnız read-only doğrulama** yapar; mevcut `AtlasBoardPrivateLobbyUIController` P1 label'ı authoritative Host UID snapshot isminden gerektiğinde yeniden uzlaştırır. Client guest kimliği host adını değiştiremez. Ek manager/ikinci UI yok, oda adına hardcode yok. **Owner visual retest OPEN**; iki ayrı ad görülmeden CLOSED sayılmaz.
- **L2 Graceful Host Leave (P0):** Host oyundan ana menüye dönerken eski 11G host migration path bir successor devri tamamlayamıyor, Guest tarafında oyun donuk kalıyor ve `RTDB authority match read: http_200` recovery uyarıları tekrarlanıyor. Product owner **oda kapanmasını kabul etti**. v5.40.9 canlı Spark RTDB'de bilinçli Host Leave/Quit için mevcut `CloseLobbyAsync` yolunu seçer (yalnız o hostun odası, join code/match/presence/room cleanup); follower `roomMeta.closed` veya gerçek veri silinmesini **doğruladığında** mevcut `LobbyAccessLost` üzerinden oyun state'ini söküp ana menüye döner. Geçici HTTP 401/403 veya timeout tek başına oda kapandı kabul edilmez. Emulator/Steam Release için yeni migration iddiası yok. **Owner test OPEN**. Crash/force-kill durumunda verified epoch/CAS takeover hâlâ G1-P0-01 OPEN.
- **İkincil warning:** Guest lobby'de non-host `SetPawnCosmeticAsync` denemesi host_only ile reject oluyor. Kozmetik yetki akışı ve trade window/auction UI tam sahiplik ayrı P1 inceleme; bu güvenlik kuralını gevşetmedik. Anonim Guest kalıcı XP/Gold/Wallet ödülü kazanamaz.

### En küçük retest (APK isteme, GitHub push isteme)

1. ZIP içindeki tam dosyaları proje köküne aç. Unity compile `0 errors`; Local Emulator QA, Steam Editor/Vuartex ve mevcut Player HUD/TURN regression korunmalı.
2. **Bir defa** Windows Development Guest'i `Build Two-Client Guest Test v1` ile güncelle, `Launch Live Spark Guest QA (No Steam)` başlat; Editor Live Spark RTDB + Steam/Vuartex. İki hesap ve iki süreç ayrı olmalı.
3. Editor Public iki kişilik lobby oluştur; Guest Public Browser ile katılsın. **Her iki lobby:** P1=Vuartex, P2=Guest#####X. Guest adı host ismine kopyalanırsa `ATB_RTDB_LOBBY_HOST_NAME` ve `ATB_RTDB_LOBBY_HOST_LABEL` loglarını karşılaştır.
4. Guest READY, mümkünse NOT READY→READY; Host 5 saniye Start; her tarafta TURN+isim/para; bir iki zar/BUY kontrolü.
5. **Guest oyundayken Editor host ESC→Leave→Confirm→ana menü.** Guest'in otomatik menüye dönmesi ve odanın Public listeden kaldırılması beklenir. `ATB_RTDB_HOST_GRACEFUL_EXIT mode=close_room`, `ATB_RTDB_ROOM_TERMINATION ... closed`, `ATB_RTDB_FOLLOWER_HOST_LEFT`, Guest menüye dönüş loglarını kontrol et. Host'un görevini Guest'e devretmesi BU testin amacı değildir. Ayrı Host crash/kilitlenme testi çalıştırma.
6. Bu iki UI/lifecycle denetimi geçerse L1/L2 CLOSED; sonra roadmap N4 reconnect/20sn Temporary Bot/5dk expiry/AFK ve gerçek crash-election mimarisine ilerle. **Bu adımda Firebase rules/deploy/Blaze/Cloud Functions/ekstra HUD script değişmedi.**

**TEST STATÜSÜ:** v5.40.8 canlı iki istemci gameplay OWNER MANUAL PASS; v5.40.9 yalnız source/static QA candidate. Unity compile, Windows build ve host-close canlı retest YAPILMADI; başarısızsa PASS yazma. v5.40.6 Player HUD üç maç OWNER PASS hâlâ geçerli.

---

## 0J. 17 Ekim 2026 — v5.40.8 LIVE SPARK GUEST QA / PLAYER-FACING ERROR ISOLATION

**HISTORICAL STATUS (v5.40.8 SOURCE CANDIDATE; OWNER v5.40.8 LIVE GAMEPLAY PASS now recorded in 0K).** v5.40.7 READY/NOT READY contract owner tarafından `ATB_RTDB_REMOTE_READY_QA PASS ... mode=local_no_network remote_join=UNTESTED` olarak doğrulandı; gerçek iki-istemci kabulü YOK. v5.40.6 HUD/TURN 3x OWNER PASS korunuyor.

### Owner test kanıtı ve kök neden

- Editor Live Spark RTDB, Steam AppID 480 + kanonik Firebase hesabı ile Public oda oluşturabildi. Guest `AtlasBoardGuest.exe`'nin `-atlasLocalEmulators` ile başlatılması localhost Auth Emulator'a gidiyor; emülatör çalışmadığı için HTTP=0/ATB110 ve offline fallback. Emülatör bayrağı olmadan çalıştırmak ise mevcut desktop Steam/App Check yolunu seçiyor; SteamAPI.Init başarısız oluyor, Guest build scene'de `AtlasBoardAccountService` kasıtlı stripped olduğu için `AtlasBoardAccountService is not available`, public browser ATB202 ve online join fail.
- Guest adı `Guest#####X` yerel olarak korunuyor; sadece ekran adı Firebase authentication kanıtı değildir. `ATB110`, `ATB202`, `ATB212` gibi mevcut hata kodları player-facing kullanılmalı; `Stable Auth Emulator...`, `AtlasBoardAccountService...`, HTTP response, backend URL veya project id UI'da ASLA görünmez.

### Kaynak değişiklikleri ve tek owner

1. Mevcut `AtlasBoardAppCheckTokenSource`, `AtlasBoardPlatformRuntime` ve `AtlasBoardLobbyRuntimeBridge` içinde **yalnız Windows Development `AtlasBoardGuest.exe` + açık `-atlasLiveGuestQa` bayrağı** için seçilen transport: Steam provider kapalı; native Steam App Check başlamaz; **hiçbir sahte App Check tokenı canlı servise gönderilmez**; HTTPS Firebase Anonymous Auth ile geçici ayrı UID alınır; RTDB kullanıcısı bu UID olur, ekran adı cihazın mevcut Guest ismidir. Token/refresh token yalnız proses belleğinde tutulur, 45 dakika sonra aynı UID ile yenilenir. Bu, Firebase Console'da Anonymous Auth etkin ve canlı RTDB anonymous-member rules izinli ise çalışabilir; gerçek network acceptance OPEN. Anonim UID verified XP/Gold/Wallet/Store ödülleri için asla trusted değildir. Uygulama tam kapanıp yeniden açılırsa aynı anonim UID'ye reconnect henüz garanti edilmez; N4 OPEN.
2. Var olan `AtlasBoardTwoClientLobbyTestV1.cs` builder korunur; ayrıca `Launch Live Spark Guest QA (No Steam)` QA menüsü ve build klasörüne `Launch_Guest_Live_Spark_QA.bat` eklenir. Eski emulator `Launch Two-Client Guest Test v1` ve `Launch_Guest_Local_Emulators.bat` değişmedi. Ayrı oyun modülü/manager yok, Firebase rules/deploy/Blaze değişmedi.
3. `AtlasBoardAccountAccessUI` login panelinde bütün platformlar için tek orijinal-owner `CONTINUE AS GUEST / MİSAFİR OLARAK DEVAM ET` butonu. Career'dan gelinirse local preview (Success=false, Level=1, kalıcı ödül yok) açılır, Settings'den gelinirse modal kapanır. `AtlasBoardMobileAccountUX` içindeki eski login/reset button `Instantiate` guest klonu kaldırıldı; artık iki owner yok.
4. `AtlasBoardPrivateLobbyUIController` Entry/Room/Password/Kick hata yolları mevcut `AtlasBoardFrontendErrorCodes` kayıtlarını (ATB110/212/210/204/206) ve 7 dilde `PublicMessage()` kullanır; sanitize edilmiş teknik detay yalnız iç loga gider. Error codebook'ta zaten tanımlı kodlar yeniden kullanıldı; yeni kod, ayrı doküman ve duplicate error handler yok.

### Şimdi uygulanacak minimum test — APK isteme

1. ZIP'i proje köküne aç; Unity Editor **0 kırmızı compile** olmalı. Editor `Use Live Spark RTDB in Editor` (yalnız Play Mode dışında) ve Steam/Vuartex hesabı aynen çalışmalı; Public/Private Host+Bot smoke regresyon yok.
2. **Bir defa** `Atlas Board/Online & Backend/Online/Build Two-Client Guest Test v1` ile Windows Development client yeniden derle. Testte **`-atlasLocalEmulators` kullanma**. Yeni menü `Launch Live Spark Guest QA (No Steam)` veya build klasöründeki `Launch_Guest_Live_Spark_QA.bat` ile başlat. Log `ATB_LIVE_GUEST_QA auth=ready provider=anonymous` ve `steam=false` olmalı; `ATB110`, Steam API retry veya eski Auth Emulator HTTP=0 olmamalı. Firebase Auth Anonymous kapalıysa sahte PASS değil; log ve gerçek Firebase Console auth-provider durumu üzerinden karar ver, production ayarı/deploy onaysız yapma.
3. Guest Header `Guest#####X` / G kalmalı. Settings Account ve Career login panelinde **Misafir Olarak Devam Et** butonu tek kez görünmeli, Career preview açılmalı, 0 cloud ödül. Yanlış RTDB/Network halinde yalnız `ATBxxx` genel yerelleştirilmiş mesaj görünmeli, teknik metin değil.
4. Editor Public Host oluştur; Guest Public Browser listeleyip Join veya 6 haneli kod ile Private/Public odaya girsin. B READY → NOT READY → READY; Host Start gating; 5 saniye countdown; A/B zar–move–rent–buy–trade–auction–turn snapshots; her iki logdan intent/ACK consistency. Canlı iki-istemci PASS olmadan N1–N3 CLOSED sayma.
5. Play Mode çıkmadan 3 oturum/Leave/re-entry, HDU name/TURN, bot personality/10s auto-roll regressions kontrol et. Network disconnect/20sn Temporary Bot/5dk expiry/Host Migration N4 aşamasında, Career/Store gerçek migration bu online temel testinden sonra.

**Sınırlamalar:** İlk Guest QA build hâlâ gerçek Windows standalone derleme ister; Android APK gerekmez. PowerShell/no-network QA gerçek iki Human online senkronizasyonunun yerine geçmez. Windows/Steam Release ve Android branch'leri bu QA yolu nedeniyle değiştirilmez. Firebase settings/rules/App Check deployment yapılmadı; owner local Unity test kanıtı gelene dek `SOURCE CANDIDATE`.

---

## 0I. 17 Ekim 2026 — v5.40.7 TWO-ACCOUNT LIVE SPARK RTDB READY/ADMISSION SOURCE CANDIDATE

**STATUS: SOURCE CHANGED + STATIC CONTRACT CHECK. TWO-CLIENT LIVE RTDB / UNITY COMPILE / WINDOWS BUILD / ACTUAL GAMEPLAY ALL OPEN.** v5.40.6 3x HUD name + TURN/SIRA OWNER PASS korunuyor. Bu adım yalnız roadmap N1 Ready/admission altyapısının somut kusurlarını kapatır; N1–N3'ü tamamlandı varsayma.

### Kod sahipliği ve düzeltmeler

- Kanonik `AtlasBoardRtdbCasualLobbyTransport.cs`: Host `AcceptReadyUpdates` artık `ready=true` kadar `ready=false` durumunu da kabul eder; aynı settings revision'ında NOT READY tıklaması `ReadyForRevision=0` yapar ve Host Start gate'i false olur. Eski kod yalnız true'yu işleyerek unready sonra bile Start'a izin verebiliyordu.
- Yinelenen join isteğinde zaten aktif bir `AccountId` yeniden `RemoteHuman` olarak atanmaz, ready/Host seat yeniden yazılmaz. Yalnız authoritative lobby snapshot commit'i doğrulanmış join request temizlenir; source fallback ve CAS/ETag guard korunur.
- Aynı Firebase UID ile Host ve Remote Human taklidi reddedilir; iki ayrı Auth account gerekir.
- Privacy-safe `ATB_RTDB_REMOTE_QA` olayları: join_requested, seat_admitted, join_confirmed, join_timeout, ready_submitted, ready_changed. E-posta, Auth token, Firebase ID/UID, 6 haneli kod veya URL loglanmaz.
- Mevcut `AtlasBoardSparkMatchSmokeQA.cs` menüsüne `Verify Remote Ready Toggle (No Network)` eklendi; `AtlasBoardRtdbCasualLobbyTransport.ValidateRemoteReadyTransitionContract()` canlı kodla aynı tek ready transition helper'ını test eder; ikinci Ready algoritması yaratılmadı.
- **Hiçbir HUD, TurnManager, Bot AI, ekonomi, auth, RTDB Rules, App Check enforcement, Cloud Function, Android veya Steam Release dosyası değiştirilmedi.** Spark/free korunur. Kodlar henüz Unity'de compile edilmedi.

### APK olmadan test — kabul seviyeleri

1. **Unity Editor, Live Spark RTDB:** `Atlas Board → QA & Diagnostics → RTDB → Use Live Spark RTDB in Editor`; Unity compile = 0 kırmızı hata. `Verify Remote Ready Toggle (No Network)` -> `ATB_RTDB_REMOTE_READY_QA PASS ... remote_join=UNTESTED` yalnız SOURCE/LOCAL contract kanıtıdır.
2. **İkinci istemci:** mevcut `Atlas Board → Online & Backend → Online → Build Two-Client Guest Test v1` menüsüyle Windows Development `AtlasBoardGuest.exe` **bir defa** yeniden build edilir; online test için `-atlasLocalEmulators` OLMADAN çalıştırılır. Bu durumda eski emulator-only Guest topology değil, normal Firebase Auth/Live Spark yoludur. Steam/native/App Check desktop davranışı ayrıca test edilir; Release veya Android PASS denmez. İkinci hesap başka e-posta/Firebase UID ile giriş yapmalıdır.
3. **N1 live:** Editor Host Account A → Private Table / 2 oyuncu → Show/Copy 6 haneli kod; standalone Account B → Join by Code → Host join kabul eder → Remote seat iki ekranda görünür. B READY → Host READY görünür; B NOT READY → Host Start pasif; B yeniden READY → Host Start aktif. Aynı UID iki oyuncu olamaz.
4. **N2 live:** Host Start → her istemci aynı roomId/matchId ve 5 saniye sunucu zaman damgalı countdown; Human A + Human B (diğer açık slot varsa explicit bot) kalır. BOT 3/4 isimleri ve TURN/SIRA v5.40.6 regresyon yapmaz.
5. **N3 live:** A ve B sırayla dice, pawn movement, buy/rent, trade/auction ve turn flow yapar; iki istemcide snapshot ve state aynı, intent→ACK bir kere, para/ownership aynı. Kayıp veya duplicate command yok. Bu gerçek iki proses/iki hesap live RTDB kanıtı olabilir; **iki ayrı fiziksel cihaz** acceptance ayrıca OPEN.
6. Test sonunda Host Close → joinCode/member/presence/match cleanup başarılı olmalı. Kırmızı hata veya `ATB_RTDB_REMOTE_QA join_timeout`, ready ekranda takılma, stale CAS ya da farklı snapshot varsa iki istemcinin sanitize edilmiş loglarını karşılaştır; N1–N3 PASS yazma.

**Test sınıflandırması:** PowerShell statik sözleşme/smoke veya Editor no-network PASS, gerçek iki-client RTDB gameplay PASS **değildir**. Emulator Guest `-atlasLocalEmulators` eski Functions/Emulator yoludur; Live Spark RTDB kanıtı olarak sayılmaz. Android APK'yi ancak Editor+Windows iki-client kabulünden sonra birleşik performans/UI testinde üret. N4 reconnect/20sn TemporaryBot/5dk expiry/AFK ve Host Migration daha sonra ayrı gate; Career/Shop/Guest migration bunların ardından.

---

## 0H. 17 Ekim 2026 — v5.40.6 PLAYER HUD / TURN-SIRA OWNER ACCEPTANCE

**GÜNCEL DURUM: OWNER MANUAL PASS (Unity Editor, Host + 3 Bot, local v5.40.6).** Önceki v5.40.3–v5.40.5 HUD adı/TURN badge için `SOURCE FIX / RETEST OPEN` statüleri bu dar kapsamda kapanmıştır. Çalışan kaynak sürümü v5.40.6'dır; yalnız roadmap güncellendi diye GitHub main kaynak kodunun v5.40.6'ya geçtiği varsayılmamalı.

### Doğrulanmış

- Aynı Play Mode içinde maç #1, #2 ve #3'te `Vuartex`, `BOT 2`, `BOT 3`, `BOT 4` player HUD isimleri görünür. Önceki BOT 3 isim kaybı giderildi.
- Aktif tur hangi oyuncuya geçtiyse HUD içinde `TURN` / `SIRA` badge yazısı görünür. İlk turdan sonra ve sonraki maçlarda kaybolma sorunu düzeldi.
- Düzeltme mevcut `PlayerHudPanel.cs` ve `AtlasBoardHudTypographyPolish.cs` ile yapıldı; yeni duplicate HUD manager/script/prefab yok. `AtlasBoardHudAlignmentPolish.cs` korunur.
- v5.40.4 ve v5.40.5 ara denemeleri tam başarılı değildi; kabul referansı v5.40.6'dır.

### Hâlâ OPEN — bu sonuca dahil DEĞİL

- Tablet 30 saniye açıkken no-flicker; Travel/Property/Trade/Auction/City Info ve Shop/Career/Settings tam üç-maç roundtrip.
- İki gerçek Human RTDB join/ready/gameplay, 20 saniye Temporary Bot, 5 dakika reconnect/expiry, AFK ve gerçek Host Migration.
- Android S20 FE FPS/performance, production App Check/Play Integrity ve verified XP/Gold/reward authority.
- Normal RTDB account/profile/preferences/Career Editor yolu daha önce CLOSED; legacy Firestore fallback/protected authority ayrı güvenlik konusudur.

### Sıradaki adım

v5.40.6 HUD görüntüsünü koru ve roadmap sırasıyla iki gerçek oyunculu RTDB gameplay → reconnect/expiry/AFK → Host Migration → tablet/full UI roundtrip → Android fiziksel performans testlerine devam et. 10sn Human auto-roll, Bot personality, Spark/free ve tek canonical owner kuralları değişmez. Yeni duplicate HUD kodu yazma. Owner onayı olmadan Blaze, Functions, Firebase rules veya production deploy yapma.

---

## 0G. 17 Ekim 2026 — v5.40.3 RESIDUAL HUD NAME / TURN BADGE LEAF RECOVERY SOURCE FIX

> **KABUL DURUMU:** v5.40.2 owner retest gameplay tarafında olumlu: ana menü/lobby `V` + `Vuartex`, zar, menüler ve Host+3 Bot match akışı çalışıyor; auction ekranında Current Bid / sonraki bid değerleri ve bidder isimleri görünür. Ancak HUD'da iki presentation kusuru kaldı: match #2/#3'te bazı Player/Bot name satırları aralıklı kayboluyor ve aktif oyuncunun renkli TurnBadge kutusu görünmesine rağmen `TURN/SIRA` yazısı ilk/ikinci/üçüncü match fark etmeksizin görünmüyor. Bu bölüm SOURCE FIX'tir; owner retest olmadan CLOSED değildir.

### 0G.1 Kanıt / kapsam ayrımı

- Sağlanan runtime logunda match setup `Vuartex`, `BOT 2`, `BOT 3`, `BOT 4` kimliklerini doğru hazırlıyor; starting order ve sonraki `Turn started:` kayıtları doğru slot+isimlerle ilerliyor. Bu nedenle bu turda RTDB seat identity, TurnManager authority veya economy state yeniden tasarlanmadı.
- HUD screenshot'ında current-turn outline ve TurnBadge renkli kutusu aktif olduğu halde badge text boş. Bu, current-turn seçiminin doğru olup yalnız TMP leaf presentation'ın kayıp/disabled kalabildiğini gösterir.
- v5.40.2 `playerNameText` activeSelf recovery eklemişti; residual bulgu, re-entry sonrasında serialized TMP leaf reference'ın stale/null olabileceği veya TMP component'in disabled kalabileceği edge'i kapatmadığını gösterdi.
- Auction ekranındaki tek anlık kanıt `Current Bid 300`, `Bid 310`, `Bid 350` ve Vuartex/BOT 4 isimlerini birlikte gösteriyor. Bu screenshot current presentation'ın authoritative bid matematiğiyle uyumlu olduğunu gösterir; tüm auction boyunca dinamik update kabulü yine manual retest ile kapanır. Trade visible-value retesti OPEN kalır.

### 0G.2 v5.40.3 kaynak düzeltmesi — tek owner içinde

- Yalnız canonical `PlayerHudPanel` değiştirildi. Yeni HUD manager/prefab/controller/helper owner yaratılmadı; TurnManager, RTDB transport, Bot AI ve economy/trade/auction authority değişmedi.
- `PlayerHudPanel.Refresh()` participating HUD için yalnız kendi panel hiyerarşisindeki iki kritik leaf'i doğrular: direct `PlayerName` ve `TurnBadge/Text`. Serialized reference null/stale veya artık panelin child'ı değilse aynı canonical child'a local olarak rebind eder; **whole-scene search yoktur**.
- `PlayerName` TMP GameObject/component gerekli olduğunda tekrar enable edilir. Stabil durumda `SetActive`/`enabled` yazımı yapılmaz.
- `TurnBadge/Text` TMP GameObject/component de gerekli olduğunda tekrar enable edilir; text önce `hud.turn` ile yazılır, badge root visibility daha sonra current-turn state'e göre uygulanır. Böylece renkli fakat boş badge kutusu kalmamalıdır.
- Rebind yalnız layout cache'ini gerektiğinde invalidate eder; v5.40'ın delta-only HUD refresh/per-frame geometry optimizasyonu korunur.
- 10 saniyelik Human auto-roll fail-safe, Bot personality label, Temporary/Permanent Bot lifecycle, `Vuartex` canonical identity ve tablet single-owner kuralları değiştirilmedi.

### 0G.3 Güncel statü

- **P0-05 reusable session:** manual #1/#2/#3 gameplay start/turn/dice akışı owner tarafından gözlendi; **HUD presentation nedeniyle full PASS değil**. v5.40.3 retest OPEN.
- **P0-01 identity:** authoritative state/log ve main menu/lobby identity doğru; HUD name leaf render residual bug için **SOURCE FIX / RETEST OPEN**.
- **P0-02 HUD:** current-turn outline çalışıyor; `TURN/SIRA` TMP leaf görünürlüğü **SOURCE FIX / RETEST OPEN**. Tablet 30sn no-flicker kapısı da OPEN.
- **P0-03 tablet:** v5.40.2 rearm korunuyor. Auction single screenshot improved; Trade + repeated Auction update retesti OPEN.
- **Two-device Human / reconnect / 20sn Temporary Bot / 5dk expiry / AFK / real Host Migration / Android profiler:** değişmedi, OPEN.

### 0G.4 Owner acceptance — bir sonraki test

1. Unity compile: 0 red error.
2. Aynı Play Mode: Private Host+3 Bot match #1 → leave → #2 → leave → #3. Her matchte dört name lane sürekli görünür: `Vuartex`, `BOT 2`, `BOT 3`, `BOT 4`.
3. Her starting-order roll ve normal turn boyunca yalnız current player HUD'unda badge görünür ve badge içinde aktif dilde `TURN` / `SIRA` (ve diğer lokalizasyon karşılığı) okunur. Renkli boş badge kabul edilmez.
4. Turn değiştikçe outline + badge aynı karta birlikte geçmeli; name/money/control satırlarından hiçbiri kaybolmamalı.
5. Tablet 30sn açık → HUD gizli ve flicker yok; kapat → dört name + current TurnBadge tek repaint ile doğru dönmeli.
6. Auction sırasında en az 3 bid değişimi gözle: Current Bid, next bid buttons, bidder/leader isimleri birlikte ilerlemeli. Trade'de From/To/property/cash presentation authoritative offer ile eşleşmeli.
7. Bu UI retesti geçince roadmap sırasına göre iki gerçek Human join/ready/gameplay → disconnect/reclaim/expiry → Host migration kapılarına devam et.

---

## 0F. 17 Ekim 2026 — v5.40.2 RE-ENTRY HUD NAME + TABLET EDGE REARM SOURCE FIX

> **KABUL DURUMU:** v5.40.1 artık Unity'de compile olup owner tarafından üç manual gameplay session'a kadar çalıştırıldı. Zar, ana gameplay, menüler, lobby `V`/`Vuartex` kimliği ve match start akışı çalışıyor. Yeni bulunan regresyon: match #2 ve #3 HUD kartları para/control bilgisini göstermesine rağmen `Vuartex` ve Bot isim satırlarını göstermedi. Bu bölüm source fix'tir; v5.40.2 owner retest olmadan CLOSED sayılmaz.

### 0F.1 Kanıt ve kök neden

- İkinci ve üçüncü match logları authoritative `PlayerGameState` kimliğinin kaybolmadığını gösteriyor: `Vuartex [Slot 0] prepared...`, ardından `BOT 2/3/4 ... prepared...` ve doğru HUMAN/BOT setup kayıtları geliyor. Ana menü/lobby de `V` + `Vuartex` göstermeye devam ediyor. Bu nedenle RTDB/account identity owner değiştirilmedi.
- Ekran görüntüsünde dört HUD kartı da active; money ve control/personality satırları görünürken yalnız name lane boş. Bu bir HUD presentation/re-entry problemi.
- v5.40 optimizasyonu per-frame unconditional HUD refresh'i kaldırdı; fakat `PlayerHudPanel`/`UXOverlayController` re-enable olduğunda delta/layout cache'lerini yeniden arm etmiyordu. Yeni session aynı slot/highlight değerleriyle başlarsa ilk repaint bastırılabiliyor ve one-shot name geometry tekrar uygulanmıyordu.
- `PlayerHudPanel.OnEnable()` artık yalnız lifecycle boundary'de layout/badge cache'lerini resetler ve bound player'ı bir kez repaint eder. `playerNameText` participating HUD'da açık tutulur. Per-frame refresh geri getirilmedi.
- `UXOverlayController.OnEnable()` delta cache'lerini invalid eder; yeni match'in ilk frame'i force repaint olur. Stabil frame yine delta-only kalır.
- `TabletUIManager.OnEnable()` canonical panel edge tracking'i `captureCurrentState:false` ile yeniden arm eder. Böylece önceki session'dan kalan presentation edge'i yeni Trade/Auction/Travel panelinin rising edge'ini bastıramaz. Yeni tablet manager/panel sistemi yaratılmadı.

### 0F.2 Trade/Auction gözlemi

- Sağlanan Bot trade logunda teklif `offered cash=330`, `requested property=Ordu`, `requested cash=0`; bu teklif tasarım gereği yalnız bir yönde non-zero cash taşır. `TradeManager` iki yönde aynı anda cash'i zaten yasaklar. Bu yüzden sadece bu logdan economy/trade hesaplama hatası çıkarılmadı.
- Ayrı auction logunda authoritative bid zinciri 50→100→150→200→250→300→310→360 olarak ilerliyor ve kazanan state doğru sonuçlanıyor. Eğer ekranda tek sayı sabit kaldıysa sorun presentation/re-entry tarafındadır; v5.40.2 tablet edge rearm sonrası yeniden test edilir.
- Bu turda `TradeManager` veya `AuctionManager` authority/economy kuralları değiştirilmedi; çalışan para/auction mantığını tahmine dayalı bozmak yasaktır.

### 0F.3 Güncel statü

- **v5.40.1 compile blocker:** **OWNER PASS** — oyun çalıştırıldığı için Unity compile kapısı geçildi.
- **P0-05 reusable session:** gameplay #1/#2/#3 açılabiliyor, ancak match #2/#3 HUD names regresyonu nedeniyle full PASS değil; **SOURCE FIX v5.40.2 / RETEST OPEN**.
- **P0-01 identity:** account/lobby canonical `Vuartex` state korunuyor; match HUD name render **RETEST OPEN**. RTDB identity migration tekrar açılmadı.
- **P0-02 HUD flicker / G2-P1-04 redraw:** per-frame refresh optimizasyonu korunur; lifecycle-only repaint eklendi. 30sn tablet testi ve #2/#3 name repaint retesti OPEN.
- **P0-03 tablet stale panel:** v5.40 canonical owner + v5.40.2 re-enable edge rearm source fix. Trade/Auction visible-value retesti OPEN.
- **İki gerçek Human / reconnect / Temporary Bot / 5dk expiry / AFK / gerçek Host Migration:** değişmedi, OPEN.

### 0F.4 Owner acceptance

1. Unity compile: 0 red error.
2. Aynı Play Mode içinde Private Host+3 Bot match #1 → leave → #2 → leave → #3. Her match başlangıcında dört HUD name lane anında görünmeli: `Vuartex`, `BOT 2`, `BOT 3`, `BOT 4`; money/control satırları da kalmalı.
3. Tablet 30sn açık: HUD görünmez/titremez; kapatınca name dahil dört HUD doğru tek seferde dönmeli.
4. Trade: görünen From/To isimleri, property ve iki cash alanı authoritative offer ile eşleşmeli. Tek yön cash olan teklifte diğer tarafın `0 ₵` olması normaldir.
5. Auction: Bot/Human bid geldikçe Current Bid ve bidder/leader presentation ilerlemeli; log 50→100→... ilerlerken UI tek sayıda donmamalı. Trade paneli auction sırasında stale kalmamalı.
6. Live Spark 3x smoke + Bot Wire + Presence QA tekrar PASS kalmalı.

---

## 0E. 17 Ekim 2026 — v5.40.1 PLAYER HUD COMPILE HOTFIX

> **DURUM:** v5.40.0 owner compile denemesinde `PlayerHudPanel.cs(508,9) CS0120` bulundu. `runtimeLayoutApplied = true` yanlışlıkla static `ConfigureTextCommon(...)` içine yerleşmişti. Bu, v5.40 runtime testlerinin başlamasını engelleyen saf compile blocker'dı; gameplay davranışıyla ilgili yeni bir tasarım değişikliği değildir.

- `runtimeLayoutApplied = true` static helper'dan çıkarıldı.
- Flag, sahibi olan instance method `EnsureRuntimeLayout()` sonuna taşındı.
- `ConfigureTextCommon(...)` tekrar yalnız text formatting helper'ı oldu; instance state'e erişmez.
- v5.40 tablet/HUD owner, delta refresh ve polish-churn değişikliklerinin geri kalanı korunur.
- **Kabul:** **OWNER UNITY COMPILE PASS** — v5.40.1 ile oyun üç manual session'a kadar çalıştırıldı. Sonraki presentation regresyonu 0F v5.40.2 altında izlenir.

---

## 0D. 17 Ekim 2026 — v5.40.0 TABLET / HUD OWNERSHIP + DELTA REFRESH SOURCE CANDIDATE

**KABUL DURUMU:** SOURCE CHANGED + STATIC CHECK ONLY. Başlangıç GitHub baseline: `47c47e615c60b157cf7f59deae7d30ec85906977`. Unity compile/runtime ve gerçek cihaz PASS henüz yok.

### 0D.1 Bu turda koddan bulunan gerçek kök nedenler

- `UXOverlayController` match görünürken her frame `RefreshAll` çağırıyor, `RefreshAll` da her `PlayerHudPanel` için `Refresh` çağrısını tekrar ediyordu.
- `PlayerHudPanel.Refresh` her çağrıda yalnız veri değil RectTransform/layout/image ayarlarını da tekrar yazıyordu.
- Tablet açıldığında `UXOverlayController` HUD GameObject durumunu `SetActive` ile değiştirirken `AtlasBoardTabletHudVisibilityGate` aynı görünürlüğü `CanvasGroup` üzerinden ayrıca yönetiyordu. Player state eventleri HUD GameObject durumunu yeniden açabildiği için iki owner birbirine karşı çalışabiliyordu.
- `PlayerHudPanel` control label çözümünde her refresh sırasında network coordinator scene search yapılıyordu.
- `AtlasBoardHudAlignmentPolish` her `LateUpdate`'ta aktif HUD geometrisini yeniden yazıyor ve stabil sahnede bile her 1 saniyede `FindObjectsByType` ile bütün HUD'ları yeniden arıyordu.
- `AtlasBoardHudTypographyPolish` Phase14D'nin per-frame geometry yazımını telafi etmek için `ApplyAll()` çağrısını her `LateUpdate`'ta çalıştırıyor ve stabil sahnede de periyodik target discovery yapıyordu.
- `TabletUIManager` aynı anda aktif kalan eski/yeni gameplay panellerini tek canonical owner altında zorlamıyordu.
- Tablet başlığı stabil durumda bile her frame TMP write alıyordu.

### 0D.2 v5.40 kaynak düzeltmeleri

1. HUD GameObject active-state sahibi `UXOverlayController` içinde match/participation ile sınırlandı; tablet görünürlüğü mevcut `AtlasBoardTabletHudVisibilityGate` cached `CanvasGroup` ownerına bırakıldı.
2. `UXOverlayController` artık `PlayerHudPanel.Refresh` çağrısını initial/match visibility veya highlighted turn gerçekten değiştiğinde yapıyor. Money, identity, control ve participation değişimleri mevcut `PlayerHudPanel` event subscriptionları üzerinden kalıyor.
3. `PlayerHudPanel` runtime layout ve turn-badge geometry bind/config başına bir kez uygulanıyor; her data refreshte tekrar yazılmıyor.
4. `PlayerHudPanel` network coordinator referansını cacheliyor; cache yok/destroyed ise yeniden resolve ediyor.
5. `AtlasBoardHudAlignmentPolish` mevcut owner olarak korunuyor fakat artık geometry'yi Awake/gerçek rediscovery sırasında uygular; dört HUD çözüldükten sonra stabil frame'de scene discovery veya geometry rewrite yapmaz.
6. `AtlasBoardHudTypographyPolish` mevcut owner olarak korunuyor; yalnız ilk bind, gerçek target rediscovery, çözünürlük veya dil değişiminde `ApplyAll` yapar. Phase14D ile eski per-frame karşılıklı rewrite kaldırıldı.
7. Mevcut `TabletUIManager` yeni aktive edilen gameplay panelini canonical panel kabul edip stale diğer gameplay panellerini kapatıyor. Yeni manager/prefab/controller yaratılmadı.
8. Online seat lifecycle notice gameplay karar panelini kapatmayan geçici overlay olarak korunuyor.
9. Tablet title yalnız istenen localized text gerçekten farklıysa yazılıyor.
10. Duplicate gameplay panel gerçekten yakalanırsa `ATB_TABLET_OWNER` diagnostic yalnız o olayda yazılıyor; stabil frame spam beklenmiyor.

### 0D.3 Açık/PASS sınırı

- **P0-02 HUD flicker:** SOURCE CANDIDATE v5.40. Owner 30 saniye tablet testi OPEN.
- **P0-03 Tablet birikmesi:** SOURCE CANDIDATE v5.40. Property → Travel → Trade → Auction → Development/Event/City Info ve manual match #2/#3 acceptance OPEN.
- **G2-P1-03 Bot enable-disable-enable:** v5.39 coordinator kaynakta waiting-lobby Bot toggle kaldırılmış ve match metadata yalnız desired state değişince `BotPlayerController` değiştiriyor. SOURCE IMPLEMENTED; manual #3 ve remote Human runtime gate OPEN.
- **G2-P1-04 unchanged snapshot → HUD redraw/scene search:** v5.39 coordinator delta + v5.40 HUD/UX değişiklikleri SOURCE IMPLEMENTED; owner runtime/profiler kanıtı OPEN.
- **Android FPS:** UI/layout churn kaynaklarından bazıları azaltıldı; fiziksel S20 FE profiler olmadan P0-09 PASS sayılmaz.
- İki gerçek Human, reconnect/Temporary Bot/5dk expiry/AFK ve gerçek Host Migration 0C execution order 1–4 olarak OPEN kalır.

### 0D.4 Owner test kapısı

1. Unity compile: **0 red error**.
2. Private Host+3 Bot maç aç; 30 saniye normal zar/purchase/auction davranışını izle.
3. Property tabletini aç ve **30 saniye açık bırak**. P1–P4 HUD görünmemeli veya titrememeli. Kapatınca doğru HUD tek seferde dönmeli.
4. Property → Travel → Trade → Auction → Development/Event/City Info yollarını dene. Aynı anda iki gameplay panel görünmemeli. `ATB_TABLET_OWNER` görülürse bu gerçek stale duplicate yakalandığını gösterir; her frame gelmemeli.
5. Aynı Play Mode içinde maçtan çık → yeni Private Host+3 Bot match #2 → tablet akışı → çık → **match #3**. Eski Travel/Property/City Info kalmamalı; `Vuartex`/`V` ve Bot label davranışı korunmalı.
6. Live Spark 3x smoke tekrar PASS olmalı.
7. Mümkünse Profiler ile tablet açık/kapalı 30 saniye Canvas/Layout rebuild ve GC patternini karşılaştır.
8. Bu testler iki gerçek Human/Host Migration testinin yerine geçmez.

---

## 0C. 17 Ekim 2026 — v5.39.0 OWNER TEST CHECKPOINT / ROADMAP REBASE (EN GÜNCEL DURUM)

> **GÜNCEL OTORİTE:** Bu bölüm 0B/0A v5.33.0 handoff notlarından daha yenidir. Kullanıcının 17 Ekim Editor/Live Spark logları ve v5.34–v5.39 yerel kaynak zinciri esas alınır. GitHub `main` bu güncelleme hazırlanırken hâlâ `223af7f166337ec48c2d26073e0488883763a7de` v5.33 checkpoint'indeydi; kullanıcı bu roadmap ile yerel tam kaynakları birlikte push ettikten sonra yeni GitHub HEAD yeniden okunacaktır. **Editor PASS ≠ iki gerçek cihaz PASS ≠ Android PASS ≠ production PASS.**

### 0C.1 Kapanan / test seviyesi yükselen maddeler

| Alan | 17 Ekim kanıtı | Güncel statü |
|---|---|---|
| **RTDB lobby CAS / `start_1`** | Smoke sırasında stale `TransportRevision` görüldüğünde `ATB_RTDB_MATCH_REVISION_RESYNC ... retry=once`; ardından Start başarılı. Üç Start→Read→Publish→Complete→Rematch çevrimi sonunda `ATB_SPARK_MATCH_QA PASS stages=create_3x_start_read_publish_rematch_close bots=PASS`. | **CLOSED — Live Spark Editor smoke seviyesi.** CAS kaldırılmadı; `SettingsRevision` / `TransportRevision` ayrımı korunuyor. Remote two-device collision hâlâ OPEN. |
| **Bot wire contract** | `ATB_SPARK_BOT_WIRE_QA PASS mode=local_no_network legacy_permanentbot=normalized bot_mode=permanent_bot host_mode=human`. | **PASS — local/no-network contract.** Normal Bot wire `permanent_bot`; lifecycle history bunun üzerinden çıkarılmaz. |
| **Seat lifecycle state machine** | `ATB_RTDB_PRESENCE_QA PASS stages=human_temp_reconnect_expire_no_reclaim`. | **PASS — local/no-network QA only.** Gerçek cihaz 20sn Temporary Bot, 5dk Human reclaim/expiry ve late reclaim hâlâ OPEN. |
| **Board generated-visual churn** | v5.39 manual start akışlarında aynı Turkey map tekrar uygulanmak istendiğinde `ATB_BOARD_MAP_APPLY skipped=true reason=same_map map=map_turkey`; sağlanan yeni loglarda önceki 64/96 stale visual cleanup storm'u görünmedi. | **OWNER EDITOR PASS — sağlanan iki-session kanıtı.** Manual #3 ve Android profiler altında tekrar kontrol edilir. |
| **Reusable session** | İlk Host+3 Bot maçtan çıkışta `ATB_SHARED_REENTRY presentation_owners_rearmed=true next_match_ready=true`; room cleanup tamamlandı; aynı Play Mode'da yeni Private RTDB room ve ikinci Host+3 Bot match start başarılı. | **KISMİ OWNER PASS: manual #1→#2.** Full kabul için manual #3 + Shop/Career/Settings + tablet/HUD/identity roundtrip hâlâ OPEN. Backend smoke ayrıca 3x rematch PASS. |
| **Normal account/profile/preferences** | Runtime `Firebase Auth + Spark RTDB own account/profile/preferences`; `ATB_RTDB_USER_SYNC success=true ... credentialsStored=false`; `ATB_ACCOUNT_RTDB_LOAD success=true firestore_reads=0`; Career warm read `AtlasBoardRtdbCareerReadModel`. | **CLOSED — current Editor normal-runtime path.** Legacy Firestore belgeleri normal account yüklemesi için zorunlu değil. Protected XP/Gold/wallet/entitlement/reward authority ayrı OPEN güvenlik kapısıdır. |
| **Host+Bot-only room cleanup** | `ATB_RTDB_ROOM_CLOSED data_cleanup_attempted=true complete=True no_other_rooms_touched=true` + `ATB_RTDB_HOST_ONLY_CLOSE cleanup_acknowledged=true context=host-bot-only`; ardından yeni oda kurulabildi. | **OWNER PASS — Host+Bot-only + smoke.** Multi-human leave/reconnect/hard-crash/migration orphan cleanup OPEN. |
| **Canonical identity current session** | `Vuartex` canonical header yüklendi, Steam local display slot 0'a sync oldu, Profile modal `source=canonical_local_identity`. | **CURRENT EDITOR SESSION PASS.** Tüm ekranlar, manual #3 ve two-device Guest→Vuartex flash kabulü OPEN. |
| **Private entry / Cancel clone regression** | `local_button=independent aligned=true cancel_listener_cloned=false duplicates_removed=true`. | **CURRENT EDITOR SESSION PASS.** 10x entry/exit ve Android layout kabulü OPEN. |
| **App Check Editor provider** | Firebase init öncesi Editor debug provider hazırlandı; token değeri loglanmadı. | **EDITOR DEBUG PROVIDER READY.** Android Play Integrity / production enforcement / negative-request acceptance OPEN. |
| **Pawn Animator binding** | Kenney visuals için runtime avatar/Animator binding logları var; bu tur loglarında önceki missing/inactive Animator warning'i görünmedi. | **EVIDENCE IMPROVED; final visual acceptance OPEN.** Walk/idle/result/re-entry manuel görsel kontrolü gerekir. |

### 0C.2 Host Migration statüsü — FOUNDATION VAR, GERÇEK PASS YOK

- Match create sırasında `ATB_RTDB_AUTHORITY_ANCHOR ready=true epoch=...` üretiliyor.
- Host Leave gameplay hareketi sürerken doğrudan state yırtmak yerine safe checkpoint'e queue ediliyor.
- Sağlanan manuel maçlarda kalan P2–P4 koltukları Bot olduğu için doğru sonuç authority'yi Bota devretmek değil, host-only-with-bots room'u güvenle kapatmaktı; cleanup bunu yaptı.
- **OPEN:** iki gerçek Human ile deterministic successor; atomik ve monoton `authorityEpoch`; eski Host publish/intent reddi; original Host follower olarak geri dönebilmesi; crash/heartbeat timeout takeover; trade/auction/decision/movement sırasında split-brain olmaması.

### 0C.3 Şimdi roadmap execution order — buradan sapma

1. **İki gerçek Human RTDB gameplay:** join → ready → 5sn start countdown → dice → move → buy → rent → trade → auction → turn sync. Smoke çıktısı açıkça `remote_join=UNTESTED remote_gameplay=UNTESTED`; bunu PASS varsayma.
2. **Remote Human disconnect:** gerçek ağ kesintisi → yaklaşık 20sn sonra `TEMPORARY BOT`; aynı account 5dk içinde dönerse aynı SeatId/money/properties/development/pawn/turn/decision state ile Human restore.
3. **5dk expiry + AFK removal:** `PERMANENT BOT`; late/fake reclaim ve eski Human intent fail-closed.
4. **Graceful Host Leave + hard Host crash:** gerçek remote Human successor, `authorityEpoch` artışı, stale-host rejection, güvenli checkpoint, split-brain yok.
5. **Manual reusable-session #3:** aynı Editor process; Private match #3 + Shop/Career/Settings + Tablet/HUD/identity dönüşleri; stale UI/listener/old match state yok.
6. **Tablet/HUD:** tablet açık en az 30sn; tek aktif tablet/presentation owner; değişmeyen snapshot'ta gereksiz HUD/scene search/SetActive churn yok.
7. **Android S20 FE profiler:** physical device CPU/Canvas/GC/Update/physics ölçümü; 15–23 FPS P0 kök nedenini Android-only düzelt, Windows/Steam'i bozma.
8. **Production App Check / protected result authority:** Play Integrity/enforcement ve verified reward/result sınırı. Spark/free-first korunur; owner açık izni olmadan Blaze/paid service yok.

### 0C.4 Korunacak kabul edilmiş davranışlar

- **10 saniyelik Human auto-roll fail-safe oyun kuralıdır; kaldırma/kısaltma.** Bot normal karar hızında oynar; 10sn timeout Bot hız mekanizması değildir.
- Normal Bot HUD `BOT • AGGRESSIVE / SAFE / BALANCED / ADAPTIVE` ve lokalize eşleri; disconnected Human `TEMPORARY BOT`; expiry/AFK sonrası `PERMANENT BOT`; zamanında reconnect tekrar Human.
- `permanent_bot` RTDB wire normal Bot için de kullanılabilir; “Human permanent oldu” lifecycle anlamını yalnız wire string'den türetme.
- Canonical local identity `Vuartex` + `V`; transient Guest/G kabul edilmez.
- Tablet/Travel/Property/Trade/Auction/City Info/Career/Shop için tek UI owner; duplicate prefab/controller/callback ile çözüm yasak.
- Firebase Auth kalır. Normal account/profile/preferences/Career RTDB'ye geçmiştir; **real-money/Gold/rewards/verified XP/entitlements client-writable RTDB'ye taşınmaz**.
- Firebase deploy/rules/paid service/production mutation bu checkpoint için gerekmez.

---

## 0B. 16 Ekim 2026 — OWNER SON LOG / İKİ HATA GRUBU / YENİ SOHBET DEVİR SÖZLEŞMESİ (0C TARAFINDAN SUPERSEDE EDİLDİ)

> **TARİHSEL 16 EKİM CHECKPOINT:** Bu bölüm v5.33.0 CAS failure handoff kaydıdır ve 17 Ekim tarihli **0C** tarafından supersede edilmiştir. Buradaki `ATB_SPARK_MATCH_QA FAIL stage=start_1` artık güncel blocker değildir; 0C kanıtında bounded revision resync sonrası 3x Live Spark smoke PASS görülmüştür. Ancak `SOURCE CHANGED`, `STATIC CHECK`, `UNITY EDITOR QA`, `REAL 2-DEVICE`, `ANDROID DEVICE`, `PROD LIVE` durumlarını ayırma kuralı aynen korunur.

### 0B.1 Kanıtlı son test ledger — en güncel owner logları (16 Ekim)

| Test / olay | Sonuç | Doğru kapsam ve yorum |
|---|---|---|
| `ATB_SPARK_BOT_WIRE_QA PASS mode=local_no_network` | **PASS (yerel kod sözleşmesi)** | Legacy `permanentbot` → `permanent_bot`; Human ve Bot wire ayrımı. Gerçek oyuncu reconnect testi değil. |
| `ATB_RTDB_PRESENCE_QA PASS stages=human_temp_reconnect_expire_no_reclaim` | **PASS (ağsız durum makinesi QA)** | Kod seviyesinde Human → Temporary → Reconnected Human / expired Permanent geçişi. Bu, iki cihaz presence/lease/yeniden girişin canlı RTDB'de çalıştığını **kanıtlamaz**. |
| `ATB_RTDB_ROOM_CREATED visibility=private transport=RTDB code_length=6` | **PASS (oda oluşturma)** | Smoke QA test oturumu oluşturuldu; gerçek iki cihazlı maç değil. |
| `ATB_RTDB_LOBBY_STALE_RESPONSE ignored=true incoming=... cached=...` | **BEKLENEN KORUMA OLABİLİR** | Eski yanıt önbelleği ezmedi; olay sıra/host poll yazıcıları incelenmeli. |
| `ATB_RTDB_LOBBY_CAS stale=true expected=... actual=...` | **FAIL — P0 race** | `StoreSnapshot` yeni revizyonu gördü, eski revizyonla yazmayı reddetti. CAS mekanizmasını atlatma; yazma sahiplerini ve concurrency kök nedenini bul. |
| `ATB_SPARK_MATCH_QA FAIL stage=start_1 ... RTDB match start could not commit the lobby event` | **FAIL — P0 release blocker** | Maç state yaratıldıktan sonra roomState commit çakıştı; yalnız yeni match state rollback edildi. `StartCasualMatchAsync` ile `SnapshotAsync/AcceptPendingJoins/AcceptReadyUpdates` ve host UI heartbeat/refresh yarışını incele. |
| `ATB_RTDB_ROOM_CLOSED ... complete=True` | **PASS (bu tek QA oda cleanup'ı)** | Bütün hesap/oda/maçların veya cihazlarda orphan-free cleanupların tamamı için PASS değil. |
| `ATB_SPARK_MATCH_QA FAIL stage=precondition reason=requires_idle_live_spark_lobby_and_match_bridges` | **DOĞRU QA ÖNKOŞULU** | Kullanıcı ayrı bir Private oda açıkken smoke test başlatmış. İlk Start CAS FAIL ile karıştırma; test canlı oda açıkken koşmamalı / zararsız çıkmalı. |
| `ATB_BOOTSTRAP phase=profile ... success=false code=ATB120` | **OPEN** | `AtlasBoardProgressionBridge` hâlâ callable/Firestore profil yolunu kullanıyor; hesap belgeleri de eksik. Spark/RTDB Career/profile göçü gerekiyor. |
| `AtlasBoard account ... required Firestore documents are missing` | **OPEN** | `AtlasBoardAccountService` üç legacy Firestore belgesini (`users`, `public_profiles`, `preferences`) required sanıyor. Yeniden oluşturma hilesi yerine RTDB profil sözleşmesini planla. |
| `PawnCosmetic_* did not contain an Animator` | **OPEN** | `PawnMotionAnimator.BindCosmeticVisual` runtime Animator ekliyor; Kenney rig/avatar ve aktif Animator/sahne lifecycle doğrulanmalı. |
| `ATB_LOADING_DELAY ... elapsedMs=13786 code=ATB101` | **OPEN** | Startup'da 10s uyarısı var; `startup_ready totalMs=815` ile farklı ölçüm kapsamları. FPS/CPU ve startup kritik yolunu ayırıp ölç. |
| `ATB_RTDB_PUBLIC_DIRECTORY success=true rooms=0` | **PASS (istek)** | Boş public liste hata değildir. |

**Log güvenliği:** UID, SteamID, room code, Firebase token/debug credential veya tam kişisel verileri bu roadmap'e, commit açıklamasına, genel QA loglarına kopyalama. ID ve oda kodları kullanıcı oyun UI'sında gerektiğinde gösterilir; kalıcı public troubleshoot metinlerinde maskelenir. App Check debug tokenı hiçbir yerde loglanmaz.

### 0B.2 BİRİNCİ GRUP — 4 kritik RTDB lifecycle kusuru

| ID | Kaynak / risk | v5.33.0 durumu | Doğrulama kapısı |
|---|---|---|---|
| **G1-P0-01 Host migration/crash recovery** | `AtlasBoardMatchRuntimeBridge.TryRecoverCrashedHostAsync()` hâlâ `match.error.service_unavailable` döner; `HostMigrateAtSafeCheckpointAsync` timeout/leave flow kaynakları. Host ayrıldığında maçın authority epoch, hostUid ve roomMeta tek otoriteyle devredilmesi eksik. | **OPEN — v5.33.0 değiştirmedi** | İki gerçek insan, host gracefully leave / crash, stale host publish denial, monotonic epoch, güvenli checkpoint, trade/auction ortasında split brain yok. RTDB rules ile desteklenemeyen yetki devrini sahte PASS ilan etme. |
| **G1-P0-02 Human disconnect → Temporary Bot → reconnect** | `AtlasBoardSparkRtdbMatchTransport.HostMaintenanceAsync`, `TouchAsync`, `LeaveAsync`, `ToSnapshot`, coordinator. v5.33.0 20s stale-presence + 5min TemporaryBot/reclaim kod adayı getirdi. | **YEREL NO-NETWORK QA PASS; REAL RTDB OPEN** | İki hesap, remote heartbeat kes, 20s sonra temp bot, orijinal envanter/kimlik korunur; aynı UID 5 dakika içinde koltuğunu geri alır; Host AI yalnız bot seat'te. Unity restart ve network outage ayrı test. |
| **G1-P0-03 5 dakikalık reconnect expiry / no stale reclaim** | Aynı maintenance CAS + `reconnectExpiresAtEpochMs`; normal Bot kişilikleri ile zaman aşımından sonra `PERMANENT BOT` karıştırılmayacak. | **YEREL NO-NETWORK QA PASS; REAL RTDB OPEN** | 5min bitmeden Human döner; bitince PermanentBot, eski uid rejoin reddedilir; HUD Temporary/Permanent/personality uygun; ACK ve callback race yok. |
| **G1-P0-04 AFK Human → gerçek Bot yetkisi** | `HostRemoveAfkAsync` eski sürümde yalnız `afkLockedOut=true` ve `connection=removed` yazıyordu, controller Human kalıyordu. v5.33.0 kaynaktaki controller=PermanentBot write ve Human intent authorization filtresi kontrol edilmeli. | **SOURCE CANDIDATE; LIVE OWNER TEST OPEN** | Host kaldırma bir kez işlenir, removed user intent yapamaz, Host normal Bot hızında oynar; 10 saniyelik kullanıcı tasarımı zar fail-safe KORUNUR. |

### 0B.3 İKİNCİ GRUP — 6 çapraz sistem regresyonu (eksiksiz)

| ID | Hata / sahip modül | Durum | Bitiş ölçütü |
|---|---|---|---|
| **G2-P0-01 Lobby CAS/start/rematch revision race** | `AtlasBoardRtdbCasualLobbyTransport.StoreSnapshot` ve `SnapshotAsync` asenkron host yazıcıları, `StartCasualMatchAsync`, `AtlasBoardLobbyRuntimeBridge.AcceptSnapshot`. v5.33.0 ETag CAS güvenlik sağlıyor, **ancak `start_1` şimdi gerçek FAIL**. | **BLOCKER** | Tek canonical roomState writer/serialized host room mutation, gerekirse bounded read/rebase/retry; eski response yeniyi ezmez; `Create→Start` 3 kere, Remote Ready ve Rematch 3 kere, `SettingsRevision` ≠ `TransportRevision`, no phantom Start, no orphan match. |
| **G2-P0-02 Room/match cleanup partial failure** | `CloseAsync`, `LeaveAsync`, `HostRematchAsync`; önceden partial delete success dönebiliyordu. v5.33.0 cleanup success koşulları sıkılaştırıldı. | **BU QA ODASI CLEANUP PASS; GENEL OPEN** | Kod, roomMembers, intent/ACK, matchPresence, matchState, directory, join requests ve roomState yalnız doğru odaya ait; işleme retry/idempotency; test eden aktif yabancı room silinmez. |
| **G2-P1-03 Bot AI initialization enable→disable→enable** | `MatchSetupManager.ResetForNewMatchSession`, `AtlasBoardTurnDiceNetworkCoordinator.ApplyLobbyIdentitySnapshot/ApplyOnlineSeatMetadata`, `BotPlayerController.SetBotEnabled`. | **OPEN** | First/second/third maç Host+3 Bot hızlı doğal roll/purchase/auction; gereksiz role toggling yok; oturum exit cleanup yine doğru. `SetBotEnabled(false)` gerçek seat Human geçişi değildir. |
| **G2-P1-04 Unchanged RTDB snapshot → HUD redraw + scene search** | `ApplyOnlineSeatMetadata` koşulsuz `RefreshTurnPresentationForControlChange`; `PlayerHudPanel` koordinatör bulma ve layout tekrarı; tablet HUD flicker. | **OPEN** | Kimlik, seat, bağlantı, turn değişmedikçe redraw/log/SetActive yok; tek UI owner, tablet açık 30s titreme yok, 3 match reentry eski Travel/Property/CityInfo klonlamıyor. |
| **G2-P1-05 Legacy Firestore profile/account dependency** | `AtlasBoardAccountService` `users/public_profiles/preferences`; `AtlasBoardProgressionBridge` callable, `ATB120`; gerçek hesap ve Career RTDB göçü eksik. | **OPEN — LOGDA DOĞRULANDI** | Auth Firebase'de kalır; RTDB canonical profile/settings/career/guest migration, inactive Firestore normal writes, migration fallback only explicit; deneyim/gold entitlements verified backend yokken sahte mint yok. |
| **G2-P2-06 Animator/rig warnings & idle animation** | `PawnMotionAnimator.BindCosmeticVisual`, Kenney FBX imported rigs; disabled/missing Animator üzerinde action; game setup lifecycle. | **OPEN — LOGDA DOĞRULANDI** | Gerçek humanoid/avatar rig çalışır ya da kasıtlı statik mesh fallback; no repeated runtime component add, warnings nedenleri çözülür, gerçek hareket düzgün. |

### 0B.4 Bunlardan ayrı MUST-KEEP ve ilave açık kapılar

1. **v5.32.6 Owner gameplay PASS referansı**: gerçek Private RTDB Host + 3 Bot, doğal hızda zar, satın alma, açık artırma, turn ilerlemesi. v5.33.0 bu oyunun acceptance'ını otomatik devralmaz; regresyon görürsen düzelt ve yeniden PASS al.
2. **Bot HUD sözleşmesi**: normal Bot = `BOT • AGGRESSIVE / SAFE / BALANCED / ADAPTIVE` (yerel dil); reconnect Human geçici kaybı = `TEMPORARY BOT`; sürenin sonunda = `PERMANENT BOT`; reconnect olunca = Human. `permanent_bot` wire her zaman insan-expiry anlamına gelmez. v5.32.8 yanlış kısaltma, v5.32.9 restorasyon; bunu bir daha regresyon yapma.
3. **Rematch guard**: `match_complete` öncesinde yasak; üç maç QA testinde önce negatif active-rematch, ardından gerçekten complete stage ve rematch. Oda kodu/üyeler korunmalı; eski match cleanup'ın hata durumunu raporla. CAS engelini kaldırarak smoke testi yeşile boyama.
4. **Two-real-device ONLINE**: Host/remote dice, movement, tile/rent, trade, auction, decision deadline, purchase, intent submission/ACK/dedupe/retry, UI. 8 snapshot read örneği `median=248ms`, `p95=3060ms` idi; bu **snapshot RTT**, zar/karar süresi değil. `0.75s` REST polling ve seri GET/PUT/ACK maliyeti gerçek iki cihazda ölçülecek; gerçek zamanlı dinleyici refactor risk/bütçe gerekçesiyle incelenir. Firestore'a geçiş çözüm diye varsayma.
5. **Security**: Security Core 01 local emulator PASS Cloud Functions/Firestore testidir; RTDB gerçek casual gameplay proof DEĞİL. RTDB host-authoritative Casual VERIFIED XP/Gold ödülü üretmez; trusted match-result verifier OPEN. Firebase Spark/free; onaysız Blaze, Cloud Functions deploy, RTDB Rules deploy veya canlı silme YOK.
6. **Identity/UI/reentry**: `Vuartex`/`V` (Guest flash/G avatar yok), Public/Private/Shop/Career/Trade aynı canonical profile; tek Cancel ve bağımsız Local Offline; altı haneli kod lobby+ESC; aynı sahnede 3 maç, old UI clone yok; Tablet Property/Travel/Trade/City Info tek sahne owner, account Sign In doğru yerde.
7. **Android & Startup**: S20 FE FPS (~16–23), ATB101 13.786s startup, loaded profile ATB120, Animator warnings; CPU/Canvas/GC ve RTDB request path ayrı profillenecek. Android-önce, Windows/Steam etkilenmemeli. Debug logs APK klasörüne isteniyor; USB şart değil. Unity Editor 0 error olmadan APK build YOK.
8. **RTDB migration gap matrix**: `users/{uid}`, user.general, nested settings.platforms, device data, `guestProfiles`, `careerProfiles`, `casualStats`, achievements, catalogs/store, Shop entitlement, daily/promo, account rename cooldown, Firebase Auth/email identity; her alanın RTDB rules CRUD/owner/old Firestore read-only fallback/parity durumunu tek tek incele. RTDB hedef path roadmap'te yazıyor diye implemented sayma. `AtlasBoardAccountService` ve `AtlasBoardProgressionBridge` eski Firestore/Functions branch'lerini aramayı unutma.
9. **Additional liveness/robustness**: Public join/ready, 5..1 countdown, room close + orphan recovery, real reconnect after app restart, host graceful leave & crash, stale follower/host frame anti-replay, commandId idempotency. Aynı issue iki ayrı controller'da çözülmemeli.

### 0B.5 Yeni sohbet için KESİN çalışma/teslim sırası — onay bekleme

1. **Önce güncel `MASTER_ROADMAP.md`**: bu 0B + 0A ve eski kaynakların tarihsel durumlarını ayır. Sonra GitHub `main` ile **owner'ın son gönderdiği `v5.33.0_RTDB_LIFECYCLE_CAS_REMATCH_FULL_FILES.zip` ve en son local tam dosyaları SHA/diff karşılaştır**. GitHub `main` tarihsel roadmap v5.32.6 iken ZIP içinde daha yeni v5.33.0 roadmap var; yeni kodu eski GitHub kopyasıyla ezme. Sahiplik tablosunu çıkar, mevcut helper/enum/serializer/animator/layout var mı önce ara.
2. **P0 önce**: `ATB_SPARK_MATCH_QA FAIL stage=start_1` CAS race. Host'un `SnapshotAsync` içinde `AcceptPendingJoins/AcceptReadyUpdates` gibi yan etkiler yapan GET'lerini ve aktif lobby poll/heartbeat yazıcılarını araştır; sadece CAS guard eklemek yetmedi. Tek serialized authoritative writer veya güvenli bounded retry yap, hem expected `TransportRevision` hem `SettingsRevision` ve phase/seat invariant'larını koru. Test `start_1 → 3x start/read/publish/complete/rematch/close` PASS değilse sonraki P0'a geçme.
3. **Sonra G1 lifecycle**: 20s disconnect → TemporaryBot, 5dk gerçek reclaim/expiry, AFK Bot, 2nd/3rd maç, host takeover rules+authorityEpoch güvenliği. İki cihaz test edilmeyen security/host migration'a PASS deme. Çalışan Bot label/personality ve 10s auto roll fail-safe bozulmayacak.
4. **Sonra G2**: cleanup failure/retry, bot init duplicate owner, seat metadata delta HUD, animator, Firestore→RTDB profile/account/progression. Diğer açık network metrics/UI/Android gates sırasıyla.
5. **Teslimat**: **yalnız tam değiştirilmiş orijinal dosyalarla proje-relative dizinli ZIP**; `.ps1`, `.py`, diff, patch, snippets file deliver etme. Test/Console/PowerShell komutu gerekirse doğrudan sohbet içine. C# ayrı Editor assembly public/internal erişim kontrolü; 0 compiler errors; actual runnable tests; ZIP integrity + diff against exact source; başarısız veya yapılmamış test OPEN olarak kalır. Tek canonical roadmap güncelle, ayrı phase/QA/md dosyası çıkarma. GitHub push her küçük işlem için beklenmez. Değişen bir fonksiyonun kopyasını üretme; tek owner, kısa açıklayıcı comment, no parallel controllers. Kullanıcıdan ayrıca onay istemeden açık P0'ı çözüp tam dosya ZIP'i ver; ücretli/deploy/irreversible işlemler için önceden onay gerekir.
6. **Durum raporu**: satır satır `SOURCE CHANGED / STATIC CHECK / UNITY COMPILE / EDITOR NO NETWORK / LIVE RTDB / TWO DEVICES` ayır; `ATB_RTDB_PRESENCE_QA PASS` yalnız local state testidir; `start_1` CAS FAIL'den sonra Unity smoke/PASS YOK. GitHub Summary/Description önceki gerçek test ve çözüm sınırlarıyla uyumlu olsun.

---

## 0A. 16 Ekim 2026 — v5.33.0 RTDB lifecycle / CAS / rematch SOURCE CANDIDATE

**KABUL DURUMU:** SOURCE CHANGED + STATIC CHECK hedefi; Unity compile / RTDB Emulator / Owner gameplay / iki gerçek cihaz PASS henüz yok. GitHub blob SHA ile aynı olduğu doğrulanan 5 tam kaynak dosyasından güncellendi. Bot v5.32.7 ve HUD v5.32.9 kaynakları değiştirilmedi.

**Bu paketteki birlikte ele alınan değişiklikler:**
- `AtlasBoardSparkRtdbMatchTransport`: remote Human presence timeout (20sn) -> TemporaryBot (5dk) -> yeni presence ile Human restoration veya `reconnect_expired` PermanentBot; eski `online=true` ile sahte rejoin engeli, ETag CAS, host-only. AFK removal `controller=permanent_bot` ile birlikte yazılır, yalnız `afkLockedOut` atama hatası giderilir. Human intent submit ve host pending kontrolü controller/connection/lockout durumunu doğrular.
- `AtlasBoardRtdbCasualLobbyTransport`: eski `Waiting` state'in yeni `Starting` state'i ezmesini önlemek için `TransportRevision` + ETag CAS; GET ETag header; Unity asenkron request tamamlanma yarışı koruması. Cleanup success yalnız gerçekten tüm kod, public index, roomState, match, members, ephemeral ve roomMeta silinince true. Yabancı room'a dokunulmaz.
- `AtlasBoardSparkRtdbMatchTransport.HostRematchAsync`: oyun `match_complete` değilse reddet; eski maçın presence/intent/ack/state cleanup sırası düzeltildi, başarısız cleanup bildirilir ve completed match ile idempotent retry mümkündür.
- `AtlasBoardMatchRuntimeBridge`: `GetSnapshotAsync` zaten snapshot kabul ettiğinden Update içindeki ikinci AcceptSnapshot kaldırıldı; eski revision yeni match state'i geri alamaz; host bakım sonucu seat snapshot'a uygulanır, kalıcı bot dışındaki remote player için intent polling korunur.
- `Assets/Project/Editor/AtlasBoardSparkMatchSmokeQA`: güncel GitHub testinden türetildi; ağsız Human -> Temp -> Human -> Expire sözleşmesi menüsü ve tamamlanmamış maçtan rematch'in reddedildiği negatif kontrol, sonra completed frame ve 3x rematch.
- **Rules değişmedi**; `database.rules.json` tekrar deploy edilmez. Firebase Spark/free kalır, Cloud Functions/Blaze kullanılmaz. Verified XP/Gold Casual ödül verilmez.

**AÇIK / ASLA PASS DEME:**
1. Gerçek güvenli host migration ve crashed-host takeover: `TryRecoverCrashedHostAsync` hâlâ fail döner; RTDB hostUid, roomMeta owner, authorityEpoch için atomik takeover ve saldırı testleri gerekir.
2. Uygulama tamamen kapanıp yeniden açıldıktan sonra 5dk içinde aynı match/seat'e UI üzerinden attach olmak test edilmedi (oturum içi lease restore mantığı eklendi). İki gerçek cihaz Human karar senkronizasyonu ve bağlantı kaybı doğrulanmadı.
3. `AtlasBoardTurnDiceNetworkCoordinator` her snapshot'ta HUD refresh eder; `ApplyLobbyIdentitySnapshot` önce tüm koltukları Clear/SetBotEnabled(false) yapar. Player HUD instance `FindAnyObjectByType` + tekrar layout, tablet flicker, birden çok match reentry, 3 session OPEN.
4. Bot setup başlangıcı enable -> reset/disable -> enable döngüsü OPEN (doğal hızlı roll owner PASS korunur).
5. `AtlasBoardAccountService` Firestore users/public_profiles/preferences zorunlu bağımlılığı `ATB120`; RTDB account/career migration OPEN.
6. `PawnMotionAnimator` disabled Animator warnings OPEN.
7. RTDB self-authoritative Casual host ekonomik hileyi independent verifier olmadan engelleyemez; rewarded stat/Gold doğrulamasız OFF.
8. Gerçek Android FPS, Steam/Windows regression, 2-device room join/ready + game and match #1/#2/#3 PASS değil.

**KABUL SIRASI:** Unity compile 0 errors -> `Verify RTDB Seat Lifecycle (No Network)` -> boş Live Spark Editor'da `Smoke Test Casual Match Roundtrip` (3x, early rematch reddi) -> owner Host+3 Bot natural dice/buy/auction -> 2 gerçek insan + network disconnect 20sn/within 5min/expiry -> Full app relaunch and host migration ayrı gate. Her değişikliğin testi ayrı ispatlanmalı.

## 0. 16 Ekim 2026 — GERÇEK TEST, KÖK NEDEN VE KOD SAHİPLİĞİ (GÜNCEL OTORİTE)

> **OWNER RESULT / KORUNACAK DAVRANIŞ:** v5.32.5 Bot wire düzeltmesi + v5.32.6 Editor erişim/CS0122 düzeltmesinden sonra, kullanıcı **Private Online RTDB odasında 1 Human host + 3 Bot ile oyuna girip hızlı zar atma / oynanabilir oyun akışını** doğruladı. Loglar `ATB_SPARK_MATCH_START ... bots=3`, botların roll, purchase, auction, turn ilerlemesi ve gerçek RTDB snapshot okuma sonuçlarını içerir. Bu gerçek ve değerli bir geçiştir; **2 gerçek istemci, ödüller, host migration ve tüm re-entry senaryoları henüz PASS değildir.** Bu owner PASS hiçbir sonraki değişiklikle geriye götürülemez.

### 0.1 Kullanıcının açık talimatları — yeniden keşfetme / yeniden bozma YASAK

1. **10 saniyelik zorunlu otomatik zar atma / AFK fail-safe KULLANICININ TASARLADIĞI OYUN KURALIDIR.** Ağın/botun doğal bekleme süresi değildir, 'yeni bulunmuş hata' diye sunulamaz ve hız uğruna kaldırılmaz/kısaltılmaz. Sağlıklı Bot kendi normal karar gecikmesinden sonra oynar; 10 saniyelik fallback yalnız gecikme/aksaklık durumunda çalışır. İnsanın karar hakkı ve AFK streak semantiği korunur.
2. **Bug çözüldüyse regresyona izin yok.** Üç gün önceki davranışları tekrar bozan sonraki sürüm kabul edilemez. Kod değiştirmeden önce en güncel GitHub/local **tam dosyaları** oku, üst üste binen yazıcıları bul, otoriteyi tekilleştir, önce/sonra kabul senaryosunu çalıştır. Kaynağı okumadan tahmini yeni helper, platforma göre kopya logic, başka scripti ezme ve günlükleri susturarak sorunu gizleme YASAK.
3. **Bir görevin tek sahibi, tek state sözleşmesi, açıklayıcı kod yorumu.** Önemli state geçişinin *nedenini*, kimin authoritative olduğunu, 'cleanup' ile 'rol değişimini' ayıran kısa yorumu kanonik ilgili kaynak koduna ekle; aynı davranışın kopyalarını yazma. Yorum davranışı açıklamalı; gereksiz her satıra yorum veya eski gerçeğe aykırı yorum yok. C# `internal` kodu ayrı Unity `Editor` assembly'sinden çağıran QA eklemeden önce erişim/asmdef/compile doğrulanır (`CS0122` tekrarlanmaz).
4. **Önce derleme ve çalışan host+3 Bot, ardından 2 cihaz network.** Kabul: 0 kırmızı derleme, 10s fallback tetiklenmeden Bot roll/purchase/auction/trade/turn, tablet/HUD stabil; Menu→Private→match→Leave→new Private/Public→match, üst üste en az **3 oturum**. Sadece Editor smoke PASS oyunun PASS'i değildir; runtime manual PASS smoke testinin başka dalda FAIL olmasından da otomatik iptal edilmez.
5. **Her kaydın test seviyesi yazılır:** `SOURCE CHANGED`, `STATIC CHECK`, `UNITY COMPILE`, `EMULATOR PASS`, `EDITOR GAMEPLAY OWNER PASS`, `TWO-DEVICE PASS`. Bilinmeyeni PASS diye işaretleme. Tek kanonik `MASTER_ROADMAP.md` güncellenir; ek phase/QA markdown biriktirilmez; kullanıcı istemedikçe her küçük promptta roadmap yazma.

### 0.2 `control mode changed to HUMAN` — logun gerçek anlamı (çözümle / saklama)

**LOG İSPATI (16 Ekim):** Kullanıcının gösterdiği `PlayerPawn_01` ... `PlayerPawn_04 control mode changed to HUMAN` satırları `AtlasBoardLeaveFlowController.ConfirmLeaveMatch → AtlasBoardTurnDiceNetworkCoordinator.TryHandleLeaveMatch → HostMigrateAtSafeCheckpointAsync → DetachLocalClientFromActiveMatch → AtlasBoardMainMenuController.ShowMainMenuAfterActiveMatchExit → ResetReusableGameplaySession → MatchSetupManager.ResetForNewMatchSession → BotPlayerController.SetBotEnabled(false)` zincirinde **maçtan ayrılırken** üretilmiştir. Aynı oyunun geri kalan loglarında P2–P4 `BOT`, hızlı roll ve satın alma/açık artırma çalışmaktadır.

**NET AYRIM:** `SetBotEnabled(false)` yalnız **bu istemcide Bot karar Coroutine'ini/simülasyonunu kapatır**; RTDB'deki `seat.ControllerKind` değerini gerçek anlamda `Human` yapmaz. Buna karşın mevcut `BotPlayerController.SetBotEnabled` hiçbir durum değişimi kontrolü yapmadan her çağrıda `control mode changed to HUMAN/BOT` logladığı için kullanıcıyı yanıltır ve Console'u şişirir. Bu logu gerçek koltuk değişimi gibi yorumlamak **yanlıştır**. Cleanup'ın bu kapatma davranışını silip eski Bot'u bir sonraki maça sızdırma.

**AÇIK TEKNİK BORÇ / DÜZELTME KABULÜ (P0):** `BotPlayerController.SetBotEnabled` **idempotent** olmalı, aynı değer tekrar verilince gereksiz log/AI restart/işlem olmamalı; sadece gerçekten değişen `bot simulation enabled/disabled` olayını açık bir nedenle bildirmeli, gerçek `ControllerKind` ayrı izlenmeli. Kapatırken aktif Coroutine durdurma garantisi korunmalı. Host/follower yetkisi ve Bot/Human kararı RTDB canonical snapshot + `PlayerGameState` üzerinden yürümeli. Değişen seat yoksa `ApplyOnlineSeatMetadata` dört slotta sürekli SetBotEnabled ve `TurnManager.RefreshTurnPresentationForControlChange()` çağırmamalı; HUD yalnız **role/connection/identity/turn gerçekten değiştiğinde** yeniden çizilmeli. Sahne çıkışı reset, ilk başlangıç setup ve ağ snapshot updater farklı 'rol sahibi' olamaz.

### 0.3 Kod sahipliği — hangi script hangi alanı yönetir?

| Kaynak | Tek sorumluluk | Yazmaması / ezmemesi gereken | Kapsam / kabul |
|---|---|---|---|
| `AtlasBoardRtdbCasualLobbyTransport` + `AtlasBoardSparkRtdbMatchTransport` | RTDB oda + authoritative maç snapshot/intent/revision, seat controller wire contract (`human`, `permanent_bot`, `temporary_bot`) | UI `GameObject`ları, sahte Career/Gold, yerel GameObject Bot toggle | `permanentbot` legacy okuma uyumu; wire ile enum dönüşümü bir kanonik contract üzerinden test |
| `AtlasBoardLobbyRuntimeBridge` / `AtlasBoardMatchRuntimeBridge` | Client/host için alınan güncel room/match snapshot'ını taşıma, stale revision reddi | Her eventte sahne bot/GUI reset'i, settings-revision/transport-revision karışıklığı | Yanıt sırası ve rematch üç oturum kabulü |
| `AtlasBoardTurnDiceNetworkCoordinator` | Ağ authority host/follower, lokal sahiplik ve match seat/controller **uygulama geçidi**; `ApplyOnlineSeatMetadata` | Aynı snapshot'ı değişiklik yokken 4 kez render; follower'da Bot AI çalıştırma; Leave sonrası eski callback ile role overwrite | Snapshot delta idempotency, botlar kesintisiz ve HUD titremesiz |
| `PlayerGameState` | Uygulanmış `OnlineControllerKind`, identity, connection bilgisi ve değişim event'i | Bağımsız ek global kimlik/avatar üretme; her kare `IdentityChanged` | Tek kanonik role/identity kaynağı |
| `BotPlayerController` | **Yerel Bot AI yürütme flag'i ve Coroutine yaşam döngüsü** | `HUMAN` olarak authoritative seat sınıflandırma, gereksiz log veya state yazma | `SetBotEnabled(false)` cleanup = simülasyon OFF; gerçek seat rolü UNCHANGED |
| `MatchSetupManager` | İlk start/reuse için başlangıç setup ve gereğinde Bot AI stop | Ağ seat role'ünü kalıcı olarak yeniden belirleme; game start sonrası snapshot'ı ezme | Reset'te eski Bot kararları yok, Start sonrası host botları doğru |
| `AtlasBoardMainMenuController` / `AtlasBoardLeaveFlowController` | Menu/Leave/session sıfırlama ve UI aç-kapa | Maç ortasında scene owner/role rewrite, çift reset zinciri | Menu→Game→Leave→Menu→Game üç kez |
| `TurnManager` / `AtlasBoardHumanRollTimeoutController` | Kurallara uygun tur/roll/AFK; **10s fail-safe korunur** | Bot controller yanlışsa otomatik 10s tetiklemeyi 'normal bot hızı' gibi sunma | Host bot doğal hız, AFK fallback sadece gerçekten gerekli olduğunda |
| `PlayerHudPanel` / tablet manager | State değişince bir kez redraw, tek tablet sahibi | Her RTDB GET'te zorunlu `RefreshTurnPresentationForControlChange`/SetActive, üst üste panel | 30s tablet görünürlüğü stabil, giriş/çıkışta duplicate yok |

### 0.4 Son somut doğrulamalar ve hâlâ açık olanlar

- **OWNER PASS (Editor gameplay):** RTDB Private match create/start, host+3 Bot, hızlı Bot zar atma, satın alma, açık artırma ve turn progression. Bu aşamada eski 10s bottleneck tekrarlanmıyor. Public create/start önceki testte gösterilmişti; Public bütün oyun akışı, reconnect ve iki gerçek insan hâlâ kabul bekliyor.
- **EMULATOR PASS:** `ATB_RULES_QA PASS stages=auth_room_match_atomic_create_cas_publish_heartbeat_outsider_denied`. **RULES DEPLOY OWNER PASS:** `atlasboard-usa-default-rtdb` deploy; aynı rules tekrar tekrar deploy edilmez.
- **EDITOR QA COMPILE FIX:** v5.32.6, `CS0122` class/method visibility kaynak hatasını düzeltmek için tam dosya adayı; kullanıcı Editor gameplay çalıştırdığı için kırmızı compile engelinin aşıldığı anlaşılıyor; ayrıca bağımsız bütün-proje compiler run burada yok.
- **RTDB LATENCY:** Kullanıcı bir gerçek maçta `median_ms=248`, `p95_ms=3060`, daha sonraki logda `median_ms≈160`, `p95_ms≈180` gördü (8 örnek, sadece snapshot roundtrip, karar gecikmesi değil). Firestore'a geçmeyi tek başına haklı çıkarmaz; iki gerçek cihaz intent→ack→turn/render süreleri ayrıca ölçülecek.
- **OPEN/P0:** `ApplyOnlineSeatMetadata` / `BotPlayerController.SetBotEnabled` gereksiz role/log/render tekrarları, 3 maç re-entry regresyonu, tablet-HUD tek sahiplik, `Vuartex`/`V` avatar kararlılığı, real 2-device RTDB sync, reconnect/host migration, missing Firestore profile (`ATB120`), account persistence, Android FPS. `PawnMotionAnimator` inactive Animator uyarıları ayrı gerçek UI/animation bug olarak incelenecek; backend PASS ile kapatılmaz.
- **KORUMA BARİYERİ:** Yeni sürüm host+3 Bot normal hızla gameplay, canonical name/avatar, açılan tek UI, 10 saniyelik fallback kuralı, Steam/Windows/Android ayrımı ve ücretsiz Spark'ı regresyona uğratırsa teslim edilmez. Kod değiştiği her gerçek P0 senaryosu en az 3 ardışık oturumda kontrol edilir; sonuç yoksa `OPEN` kalır.

---

## 1. Değişmez çalışma sözleşmesi

- **Lokal kaynak öncelikli:** GitHub ancak kullanıcının gerçekten push ettiği kod için geçerlidir. GitHub eskiyse hiçbir yeni yerel tam dosyayı ezme. Teslimat sadece değişen **TAM DOSYALAR**, kanonik `Assets/...` yollarında ZIP. Patch/diff/`apply` script, gereksiz Tools klasörü veya tek kullanım belgesi yok. Github commit/push ve Firebase deploy kullanıcının işi; istemedikçe otomatik yapma.
- **Önce Editor compile 0 kırmızı + gerçek source warning temizliği.** APK yaklaşık 30 dakika sürüyor; gerçek Android davranışı gerekmeyen her değişiklik için APK isteme. Bir APK'da çok sayıda Android testi birleşsin. Windows/Steam mouse, input, UI, grafik ayarları Android yüzünden bozulmaz.
- **TEKRAR EDEN HATALARA SIFIR TOLERANS:** Aynı bug tekrar görülürse yeni özellikten önce kök nedeni ve tek sorumluyu düzelt, regression testini kalıcı ekle. İlk maç tek başına geçerliyse PASS değildir; oyun→lobi→maç→menü→ikinci maç→üçüncü maç, Shop/Career/Settings dönüşleri ve scene reload zorunludur.
- **ASLA CANLI UI NESNESİNİ KOPYALAYIP BUTON/PANEL YAPMA:** Özellikle Cancel `GameObject`'ini `Instantiate` ile çoğaltma; böylelikle listener/localization/serialized davranışı taşınıyor. İhtiyaç duyulan yeni öğeyi **ayrı, bağımsız ve tek handler sahibi** olarak oluştur. Tasarım uyumunu merkezi stil/tasarım tokenlarıyla sağla; başka panelin kopyasını üretme. Her öğe aynı header, grid, padding, spacing, color, font, icon, disabled/hover/active state, safe-area sözleşmesinde olsun. Tek bir estetik düzen; eski ve yeni stil iç içe geçmez.
- **TEK KANONİK SAHİPLİK:** Player identity/avatar, HUD visibility, active tablet, match session, seat policy ve Account navigation için her şeyi yöneten bir state/owner. Çoklu `Update/LateUpdate` helper'larının birbirinin `SetActive`, `IdentityChanged` ve text değerini değiştirmesi YASAK. Değişmeyen veriye her kare UI yazma, tekrarlanan `FindObjects`, reflection ve Scene search ekleme. State değişiminde bir kez idempotent render, sahne yeniden girişinde tek güvenli rebind.
- **Dil:** EN/TR/ES/FR/DE/KO/RU. Ekran/hata/toast/Server Status tam **tek seçili dilde**; “SERVER STATUS / SUNUCU DURUMU” gibi çift dil görünmez. Form/klavye/safe-area korunur, metinler hizalı ve taşmasızdır.
- **Güvenlik ve dürüst durum:** Gizli anahtar, Firebase token, şifre, Steam ticket, App Check debug token loglama. Onaysız prod rules/silme/deploy/ücretli servis yok. Derleme veya test yapılmadıysa PASS demek yasak.

## 2. Genel karar / platform özel karar

| Tüm platformlarda aynı ürün kararı | Platforma özel adaptör |
|---|---|
| Career mor, Shop farklı accent; Settings Audio/Gameplay/Graphics/Controls/Account/Server Status aynı tab bar; Career Overview/Match History/Achievements/Account aynı bar | Android/iOS touch/keyboard/pinch/pan/rotate/safe-area, Android-only FPS; Windows/Steam klavye/mouse sabit |
| Tek ad/avatar/wallet görünümü: menü/Shop/Career/Profile/Private/Public Room/Property/Trade/HUD/tur başlığı | Steamworks persona/SteamID sadece Steam; Firebase UID RTDB erişim kimliği; Google Play önce |
| Six-digit Private/Public Room, RTDB session, koltuk kuralları, Bot/Local/Remote, Show/Copy ve aynı ESC kodu | OS-specific graphics/settings RTDB `/platforms/{platform}`; Steam-only provider auth |
| Oyun kararı, Travel, City Info, tüm tabletlerin tekliği, ikinci/üçüncü maç, 7 dil, Shop tasarımı | Android CPU/FPS profiler, safe-area, AAB, cihaz günlükleri; Windows Steam branch ve overlay |

## 3. Doğrulanmış sonuçlar ve açık P0 defteri

**OWNER PASS:** v5.32.6 Private Online RTDB host + 3 Bot hızlı roll/purchase/auction/turn gameplay (gerçek iki insan değil); Firebase Auth ve RTDB kullanıcı erişimi, RTDB Public room list sorgusu (boş olması hata değil), RTDB Private Room Create→Read→Close smoke (`ATB_RTDB_SMOKE PASS stages=create_read_close`), gerçek altı haneli kod/Show/Copy. Ana menüde Vuartex adının V avatara dönüşmesi daha önce onaylandı; Server Status tek dil/ikon hizası onaylandı; Shop ikinci girişte doğru açıldı ve geniş Travel tasarımı beğenildi. Bunlar **iki gerçek cihaz senkronize multiplayer PASS anlamına gelmez**.

| ID / önem | Kullanıcının tekrar gördüğü bug | Kök alan / kalıcı kabul kapısı | Şimdi |
|---|---|---|---|
| **P0-01 Identity/avatar** | Bir ekranda `Vuartex` yanında **G**, başka sahnede V; bir an Guest→Vuartex; kimlik HUD değişimleri | Tek canonical `DisplayName` + `AvatarInitial` aynı snapshot'tan; ilk karede Main Menu / Profile / Private / Public / Career / Shop / tur bar / tablet / HUD; ayrı prefab fallback yok; per-frame `IdentityChanged` yok | **KISMİ OWNER PASS:** v5.39 current Editor session `Vuartex` header + Steam slot0 sync + canonical Profile modal doğru. Cross-screen Guest flash, manual #3 ve two-device OPEN |
| **P0-02 HUD flicker** | Tablet açıkken P1–P4 player HUD görünür/gizli, tekrar tekrar boyanıyor | Tek HUD görünürlük sahibi; tablet açıkken tüm player HUD kapalı 30s hiç titreme yok; kapalıyken doğru geri gelir; değişmeyene SetActive/Canvas yazma yok | **OPEN**, v5.31.9 adayı |
| **P0-03 Tablet birikmesi** | Property üzerinde eski Travel çiziliyor; kararsız karar ekranları birikiyor, City Info ikinci maçta kaybolabiliyor | `TabletUIManager` tek aktif canonical panel; Travel/Property/Trade/Event/Auction/Development/City Info eski ve modernleri aynı anda çizemez; her kapatmada cleanup; 3 maç | **OPEN**, v5.31.9 kısmi adayı |
| **P0-04 Seat auto Bot** | Seçilmemiş P2–P4 yanlışlıkla Local Human | LocalOffline P1 host, explicit P2 Local/P3 Bot korunur; seçili ama boş P4 → Bot. Online explicit Remote Human seçimi asla sessizce bozulmaz. 5→1 countdown ve doğru snapshot | **KISMİ OWNER PASS:** v5.39'da iki ardışık Online Host+3 Bot start doğru; LocalOffline explicit Local/Bot matrix, RemoteHuman ve manual #3 OPEN |
| **P0-05 re-entry** | Maç #1 açılıyor, menü→aynı/başka lobi→maç #2 Start çalışmıyor veya eski UI geliyor | Tek session lifecycle reset, stale callbacks/Unity destroyed refs/listener duplicate yok; maç #1/#2/#3 ve Shop/Career/Settings dönüşü PASS | **KISMİ OWNER PASS:** manual #1→#2 rearm/new-room/new-match geçti; Live Spark backend 3x rematch PASS. Manual #3 + tüm UI roundtrip OPEN |
| **P0-06 online match** | RTDB room kodu var ama ESC `NO NETWORK SESSION`, Public Start pasif, private gameplay local gibi | Match transport Functions→RTDB host snapshot/intent/ack/revision, remote join & ready, reconnect/rematch, gerçek network RTT; LocalOffline N/A | **KISMİ OWNER PASS:** v5.39 Host+3 Bot manual + Live Spark 3x roundtrip PASS; CAS stale revision bounded resync çalışıyor. Smoke hâlâ `remote_join=UNTESTED remote_gameplay=UNTESTED`; 2 gerçek Human/reconnect OPEN |
| **P0-07 Cancel klonu** | İki Cancel, Cancel oda oluşturuyor, Local Offline hizası hatalı | Canlı Cancel klonlamak yasak; tek bağımsız Local Offline ve tek Cancel, tıklama işlevleri ayrık, 10 kez menüye dön/aç, güzel eşit aralık | **CURRENT EDITOR SESSION PASS:** `local_button=independent aligned=true cancel_listener_cloned=false duplicates_removed=true`; 10x repetition/Android acceptance OPEN |
| **P0-08 profil para/gold** | Public Lobby'de `Vuartex` yanında G, `$ 0 / G 0` önceki profille tutarsız | Profil ve wallet tek veri kaynağı, Guest/verified hesap ayrımı, yokken loading/missing doğru; sahte currency yok | **OPEN** |
| **P0-09 Android FPS** | Samsung S20 FE 16–23 FPS, CPU ~40–65ms, GPU birkaç ms | Unity Profiler CPU/Canvas/GC/Update/physics ayrımı, Android-only değişiklik; Steam/Windows değişmez; fiziksel cihaz kabul | **OPEN** |
| **P1-10 account/profile** | `ATB120`, eski Firestore documents missing; Career bazen loading | `AtlasBoardAccountService` eski belge zorunluluğu kaldır, RTDB hesap + Career eşleşmesi; guest preview koru | **NORMAL EDITOR RUNTIME CLOSED:** RTDB user sync + account load `firestore_reads=0` + RTDB Career read proven. Cross-device account migration ve protected progression/reward authority ayrı OPEN gate |
| **P1-11 room cleanup** | RTDB dump'ta closed veya boş inviteCode olan yetim rooms | Host owned atomic close; roomMeta/roomMembers/joinCodes/roomState temizliği; hata/retry idempotent, doğrulanmış live room silinmez | **KISMİ OWNER PASS:** Host+Bot-only close ve smoke close `complete=True no_other_rooms_touched=true`; multi-human leave/hard-crash orphan cleanup OPEN |
| **P0-12 board visual churn** | Aynı map/config akışında generated tile root/label/band üst üste üretimi; consistency guard daha önce 64/96 stale visual temizliyordu | `BoardGenerator` + `BoardTileVisualPresenter` idempotent; same-map reapply no-op; guard yalnız fallback; manual #1/#2/#3 + Android profiler | **OWNER EDITOR PASS (iki-session evidence):** same-map skip logları var ve v5.39 loglarında eski cleanup storm yok. Manual #3 + Android P0-09 OPEN |

**Kesin statü disiplini:** 17 Ekim v5.39 kanıtı CAS 3x Live Spark smoke, normal account/profile/Career RTDB runtime, manual #1→#2 re-entry, Host+Bot-only cleanup, current-session Cancel/Local entry ve board same-map idempotency için statüyü yükseltti. Buna rağmen smoke çıktısı remote join/gameplay'i açıkça UNTESTED bırakıyor; 3. manuel session, iki gerçek Human, reconnect/TemporaryBot/5dk expiry, gerçek Host Migration, Android FPS ve production App Check **OPEN**. *Source prepared ≠ PASS; Editor PASS ≠ real two-device PASS ≠ Android PASS ≠ production PASS.*

## 4. Tek kimlik ve tek session mimarisi

- **Identity State:** `SteamInitialized` ve gerçek Steam persona varsa Steam görsel adı; Firebase hesabı ile kayıtlı kullanıcıda canonical profile displayName; aksi halde cihazda kalıcı `Guest#####X`. Steam persona **Firebase auth izni değildir**. Firebase UID RTDB'de yetki kimliği, Guest ismi offline ve global benzersizlik iddiası yok. Tüm renderers aynı `IdentitySnapshot(DisplayName, AvatarInitial, UID, Provider, IsGuest, Version)` kaynağından bir kez güncellenmeli. Bir isim `Vuartex` ise avatar **V** olmalıdır; hiçbir Public Lobby/Profile gibi ikinci prefab ayrı `G` fallback üretmez. `IdentityChanged` yalnız kimlik gerçekten değiştiğinde tetiklenir, hiçbir `LateUpdate` döngüsünde değil.
- **Session State:** `Menu → Online/Offline mode choice → RTDB Waiting | Local Offline → 5..1 Start countdown → In Match → Leave/Result → Lobby/Menu → next session`. Her session immutable `sessionId`, `roomId`, `matchId`, `stateRevision`; eski callback yeni maç UI'sını güncelleyemez. `DontDestroyOnLoad` bileşenlerinde scene object refs geçersizse bir kez temiz rebind; sıradan Update ile sürekli Scene taraması yok.
- **UI Owner:** Bir tek TabletUIManager aktif paneli yönetir. Modal açıkken P1–P4 PlayerHud alpha/raycast tek owner tarafından kapatılır. Arka planda bekleyen karar nedeniyle birikmiş eski Travel/Property UI kapatılmadan başka panel açılmaz. Kaynakta kopyalanan legacy modal yeniden aktive edilemez.
- **Seats:** Açıkça `Local Player`, `Bot`, `Remote Human` seçimi korunur. Local Offline start'ında seçili/aktif boş seat **Bot** yapılır, asla Local Human yapılmaz. Online waiting seat gerçek remote oyuncu içinse Start ancak doğrulanmış hazır/seat kuralına göre yapılır; bot dönüşümü yalnız önceden belirlenmiş explicit host policy ile olur.
- **Account UX:** Login/Register/Forgot Password/Change Password (old/new/confirm), eye mask, boş input placeholder, mobile keyboard above current input, busy/disable duplicate submit, 24 saatlik display name rename kuralı. Career Guest sign-in/continue-as-guest, guest preview açılabilmeli. Android/Steam giriş davranışını karıştırma.
- **Tablet/General style:** Property, Trade, Travel, Auction, Event, Development, City Info, pause, Settings ve Career aynı responsive grid, aktif/pasif başlık, renk, dokunma alanı, font ölçeğinde. Shop turuncu, Career mor. Scene reload ve 3 maç boyunca modern biçim değişmez.

## 5. Spark RTDB tek veri mimarisi ve göç

Tek Firebase projesi **`atlasboard-usa`**, Belgium `europe-west1`. **Spark'tan çıkılmayacak, Blaze/ücretli Cloud Functions önerilmeyecek ve açılmayacak.** Ücretsiz RTDB kapasite tasarım tavanı yaklaşık **100 eşzamanlı bağlantı, 1 GB saklama, 10 GB/ay indirme**; gerçekleşen kotayı ve güncel Firebase plan sınırlarını yayın öncesi doğrula. Dar sorgu, delta/small snapshot, on-demand listener, bounded 50 room page, deduped writes, idempotency, TTL/close cleanup. Local Offline sıfır gameplay network ve sıfır kalıcı ödül.

```text
RTDB atlasboard-usa
├── users/{uid}/
│   ├── userId, playerId, displayName, createdAt, updatedAt
│   ├── general/{countryCode,language,accountType,membership,accountStatus,...}
│   ├── settings/{schemaVersion,language,audioJson,gameplayJson}
│   │   └── platforms/{android|ios|windows|macos|linux}/{graphicsJson,gameplayJson}
│   └── platforms/{platform}/devices/{deviceId}/...
├── guestProfiles/{guestId}/...                     [MIGRATION OPEN]
├── roomMeta/{roomId}/...                          [ROOM CREATE PASS]
├── roomMembers/{roomId}/{uid}/...                 [HOST PASS]
├── joinCodes/{sixDigitCode}/...                   [CODE PASS]
├── joinRequests/{roomId}/{uid}/...                [REMOTE JOIN OPEN]
├── roomReady/{roomId}/{uid}/...                   [REMOTE READY OPEN]
├── publicRoomDirectory/{roomId}/...              [BOUNDED LIST PASS]
├── presence/{roomId}/{uid}/...                   [NETWORK OPEN]
├── roomState/{roomId}/...                        [LOBBY SNAPSHOT PASS; MATCH OPEN]
├── intents/{matchId}/{intentId}/...              [NETWORK OPEN]
├── intentAcks/{matchId}/{intentId}/...           [NETWORK OPEN]
├── casualStats/{uid}/...                         [CAREER OPEN]
├── careerProfiles/{uid}/...                      [CAREER OPEN]
└── catalogs/{catalogId}/...                      [STORE TARGET OPEN]
```

**Dallar “hedef şema”dır:** `guestProfiles`, `intents`, `casualStats`, `careerProfiles` ve `catalogs` yolu listelendi diye üretimde çalıştığı iddia edilmez. Kullanıcının RTDB export'unda gerçek `roomMeta`, `joinCodes`, `roomMembers`, `roomState`, `users` kayıtları ile bazı başarısız testlerden kalan yetim room metadata görüldü. Bu kayıtları otomatik silme; owner/expiry/closed kontrolüyle temizleme kabulü açık. Kişisel UID, gerçek e-posta, token ve oda kodlarını herkese açık roadmap'ye yazma.

**Migrasyon sırası:**

1. **Preferences:** Mevcut `AtlasBoardCloudPreferencesSync.cs` normal yazımda yalnız `/users/{uid}/settings` RTDB'ye PATCH etsin; 1.2s debounce, sadece değişen platform dalı; local PlayerPrefs çevrimdışı devam. Firestore `preferences/{uid}` ve `platforms/{platform}` eski kayıtları sadece RTDB yoksa salt-okunur geçiş kaynağı, yeni Firestore preference yazısı YOK. Runtime E2E ve deployed rules kanıtı hâlâ açık.
2. **User/Profile:** `AtlasBoardAccountService` içindeki eski Firestore `users`, `public_profiles`, `preferences` required belgeleri ve buna bağlı `ATB120` fallback kaldırılacak; Auth UID ve hesap güvenliği korunacak. Public profile targeted GET mümkün, koleksiyon LIST/enumeration yok. Schema parity/cross-device ve doğru missing profile UX.
3. **Guest lifecycle:** Guest kalıcı yerel kimlik; RTDB guest yazılacaksa owner/auth/rules sınırı. Gerçek hesap login başarılı olduğunda eski guest kaydını üç saniye içinde kaldırma isteği gönder; **RTDB ack alınmadan silindi/PASS deme**. İsim global olarak benzersiz denmez.
4. **Room/Match:** Lobby RTDB Create/Read/Close PASS; remote join/ready ve gerçek oyun host-authoritative Casual intent/ack/snapshot/reconnect/rematch OPEN. Local offline ile online fallback karışmayacak.
5. **Career/Achievements:** signed-in/guest overview, XP/Level/history/wins/roll/map/economy stats ve başarımlar RTDB okuyacak; client-hosted Casual unverified ayrı gösterilecek. Gerçek para/gold, verified XP veya rekabetçi başarım istemcinin kendine sınırsız yazacağı alan olamaz; trusted verifier yokken award OFF. Cloud Functions zorunlu hale getirme.
6. **Shop:** Katalog RTDB read-only; eski Store v2 ve Shop callback korunacak; ödeme/Gold/ownership doğrulanmamışsa sahte satın alma PASS yok. Açık/kapalı shop ekranı ile ödeme hizmeti ayrı status.
7. **Güvenli tamamlanma:** Yeni branch RTDB sahibi, kural validasyonları, real read/write, negatif auth testi, offline, iki cihaz parity, rollback; ardından eski Firestore normal yazıları durdur. Kullanıcı izni olmadan Firestore koleksiyonu silme veya yeni rules deploy etme.

## 6. RTDB network'ü gerçekten bitirme planı — P0

| Gate | Yapılacak tek gerçek iş | Kapanma şartı |
|---|---|---|
| N1 Room join/ready | ikinci gerçek kullanıcı code ile join, host admit, member snapshot, ready, leave/kick, room close orphan-free; 6 haneli kod Lobby + ESC | Aynı `roomId` ve üyeler iki cihazda; 10+ create/join/close; kodu kapatınca geçersiz; `Cancel` hiçbir create tetiklemez |
| N2 Match start + seat | `StartMatchAsync` atomik `MatchId`+seat/config/revision yayınlar, bütün istemciler aynı 5→1 countdown'u görür; boş seat politikası korunur; local fallback yok | 2–4 client Host+Bot+Remote, Start geçer; ilk/ikinci/üçüncü maç |
| N3 Gameplay transport | `AtlasBoardMatchRuntimeBridge` eski callable/Functions yerine RTDB `roomState`, `intents`, `intentAcks` kullanır; monoton revision, commandId dedupe, host validates/moves; stale state red | İki cihazın zar/move/buy/trade/travel/rent/round sync aynı; tekrar intent para/işlem üretmez |
| N4 Recovery | Presence, bounded heartbeat, reconnect, 2nd/3rd match, AFK/kick, rematch, host ayrılırsa açık politika, cleanup | Ağ kes/kapat/aç; exact-once state, old sessions UI döndürmez; testte yetim sınırsız büyümez |
| N5 Real network metrics | RTT/jitter/loss yalnız gerçek active `MatchId` ve karşı taraftan veri varsa; otherwise N/A | Kod + Show/Copy tüm gerçek online maçlarda; offline N/A; hiçbir yerde sahte bağlantı durumu yok |
| N6 Android combined QA | Editor compile ve host+remote network gate önce; tek Android APK ile 2 cihaz test + FPS + UI + Auth | Android 404 yok, network synched, pass/fail log kanıtı, Firebase Spark içinde bütçe |

**Bilinçli olarak eksik:** Son kullanıcı loglarında bazı Private Start işlemleri bir şekilde yerel gameplay açıyor ama ESC “NO NETWORK SESSION” gösteriyor. Bu **online multiplayer** değildir. RTDB Create smoke tamamlandığı için N2/N3'ü PASS göstermeyin.

## 7. Regresyon testi — kısayol yok

**Unity Editor, APK'den önce:** compile 0 kırmızı; API source warnings düzelt; aynı sahne ve modlar 10 kez arka arkaya.

- Main Menu → Profile → Shop → Career → Settings/Server Status → Private Room Cancel/Offline/Online + Public Room → Match #1 → Exit/Return → Lobby #2 → Match #2 → Third match → scene reload.
- Başından itibaren avatar `V` ve `Vuartex`; misafir olunca `Guest#####X` ve `G`; Guest→Steam/Firebase geçişi flash yok. Her modal/Store/Career/tablet/HUD/round aynı canonical snapshot; profil kartı $/Gold tutarlı.
- 4 oyuncu: P1 Local, P2 kullanıcı Local, P3 kullanıcı Bot, P4 boş → **Bot**; başka durumda açık Remote Human korunur; 5→1 countdown.
- Property → Travel → Trade → Property → City Info → Auction → Development/Event; yalnız **tek** aktif tablet; 30 saniye açık, hiçbir Player HUD flicker yok; aç/kapa ve game #2/#3 aynı görünüm.
- UI layout: yeni buton tamamen bağımsız, Create/Local Offline/Cancel aynı sol hiza, aralarında yeterli eşit boşluk; Confirm/Cancel doğru callback; sekmeler anlık kaymaz; Shop old foundation yok. Runtime test menüsü gerçek paneli açsın, sahte mock istemiyoruz.
- Live RTDB Editor: `ATB_RTDB_SMOKE PASS stages=create_read_close`, ardından **ikinci gerçek user** join/ready. Oda kodu gerçek, show/copy/ESC tutarlı. No auth token/password logging. Offline smoke internetsiz ve kalıcı cloud ödül yok.
- Android S20 FE landscape safe-area, roll konumu, Trade HUD overlap yok, touch rotate/pinch/pan, City Info scroll, CPU/GPU FPS/memory log. Android-only kalite profili Steam ve Windows UI/graphics settings değiştiremez.
- Server Status sekmesi 60 saniyede bir dar servis probe; ikon/metin aynı dikey center, check timestamp saniyeye kadar, tek oyun dili, online ancak gerçek match synched iken yeşil. Error codebook ATB100/110/120/communication codes mevcut localization disiplininde.

**Release sınırları:** Eski Security Cores 01–07 yerel emulator testleri (örn. 14/14 economy 19/19 profile 23/23 etc) historical QA olarak korunur; Production App Check, Steam attestation/gerçek Steam AppID, Play Integrity, real result verification, mağaza tahsilatı, RTDB live rules negatives ve Google Play AAB için ayrı kanıt gerekir. Play/Steam production **NO GO** bu kapılar açıkken.

## 8. Çalışma önceliği ve ilerleme

1. Kopyalanmış UI/link/handler çoğalmasını temizle; **güncel yerel Scripts** ile kök sahiplik düzelt; G/V, HUD flicker ve üst üste paneli **3 maç** doğrula.
2. Koltuk finalization ve ikinci/üçüncü Start reset; bot ataması bozulmayacak.
3. RTDB N1→N6: iki cihazlı gerçek start, snapshot/intent/ack, reconnect, metrics; ücretsiz Spark sınırını aşma.
4. Firestore user/Career/Shop göçünü auth/rules ile güvenli sırada tamamla.
5. Android FPS + release security/profiling; kodun çalışmadığını sırf dokümantasyonla gizleme.

**Kullanıcıdan yeni kanıt gelince yalnız değişen PASS/FAIL veya milestone maddelerini bu tek roadmap'te güncelle. `PHASE##_*.md` gibi ayrı dokümanlar üretme.**

---

## 9. Birleştirilen tarihsel kaynakların izi

Bu kaynakların ürün kararları, güvenlik sınırları, test kapıları ve halen açık işleri yukarıdaki 1–8 bölümüne konsolide edildi. Eski dosyalar bu ZIP ile silinmez; bundan sonra yeni `PHASE_*.md`, `*_QA.md` veya handoff belgesi üretilmez. Bu kaynaklardaki eski PASS/OPEN ifadeleri tarihi kayıt olup güncel durum 3 ve 6. bölümlerdedir.

| Önceki belge | Tarihsel kapsam |
|---|---|
| `AtlasBoard_Master_Roadmap_v5_1_2026-09-29.md` | ATLAS BOARD |
| `BUILD_QA_GUIDE.md` | AtlasBoard — Build and QA Guide |
| `FRONTEND_ERROR_CODEBOOK.md` | Atlas Board Frontend Error Codebook |
| `MASTER_ROADMAP.md` | ATLAS BOARD |
| `NEXT_CHAT_HANDOFF_v5.30.1.md` | AtlasBoard next-chat handoff — v5.30.1 |
| `PHASE17A_MATCH_RESULT_CONSISTENCY.md` | AtlasBoard Phase 17A — Server-side result consistency (not a replay verifier) |
| `PHASE17B_ANDROID_DEVICE_QA_v5.25.1.md` | AtlasBoard — Phase 17B.1 / v5.25.1 Android Device QA |
| `PHASE17B_ANDROID_DEVICE_QA_v5.25.2.md` | AtlasBoard Phase 17B.2 — Android physical-device QA v5.25.3 compile hotfix |
| `PHASE17B_ANDROID_SMOKE_ACCEPTANCE.md` | AtlasBoard Phase 17B — Android-first Unity build and device smoke acceptance |
| `PHASE17C_ANDROID_OFFLINE_QA_v5.26.0.md` | AtlasBoard Phase 17C — Android Offline/Cost QA v5.26.0 |
| `PHASE17C_NETWORK_COST_ARCHITECTURE.md` | AtlasBoard — Phase 17C Network Cost Architecture |
| `PHASE17C_RTDB_RULES_v5.26.1.md` | AtlasBoard Phase 17C — RTDB Rules / Free-Tier Transport v5.26.1 |
| `PHASE17D_MOBILE_GAMEPLAY_UX_v5.27.0.md` | AtlasBoard — Phase 17D Mobile Gameplay UX v5.27.0 |
| `PHASE17E_RUNTIME_INTEGRITY_ACCOUNT_PERFORMANCE_v5.28.0.md` | AtlasBoard Phase 17E — Runtime Integrity / Account Access / Android Performance v5.28.0 |
| `PHASE17E_V530_TEST_CHECKLIST.md` | AtlasBoard v5.30.0 — One-build acceptance checklist |
| `PHASE17F_SPARK_MULTIPLATFORM_MIGRATION_ROADMAP.md` | ATLAS BOARD — PHASE 17F: SPARK / SHARED CAREER / RTDB / MOBILE FPS |
| `PHASE17F_v5.30.10_EDITOR_QA.md` | AtlasBoard v5.30.10 — Unity Editor QA (no APK) |
| `PHASE18D_RTDB_ONLINE_ENTRY_TESTS.md` | AtlasBoard Phase 18D — Online Private entry and RTDB room smoke |
| `PHASE18E_SPARK_RTDB_EDITOR_AUTH_QA.md` | AtlasBoard Phase 18E — RTDB Editor auth & Private Room QA |
| `PHASE18G_RTDB_ROOM_CODE_RESERVATION_QA.md` | AtlasBoard Phase 18G / v5.31.7 — Room code reservation and UI |
| `PHASE18H_v5.31.8_PRIVATE_BUTTON_ROOM_CLEANUP_QA.md` | AtlasBoard Phase 18H — v5.31.8 |
| `PHASE18I_v5.31.9_TABLET_HUD_REGRESSION_QA.md` | AtlasBoard Phase 18I — v5.31.9 tablet overlay/HUD repair |
| `PHASE18_SPARK_RTDB_MASTER_MIGRATION.md` | AtlasBoard — Phase 18 Spark / RTDB Migration Master Plan |
| `PHASE_15I_MODERATION_DATA_LIFECYCLE.md` | AtlasBoard — Phase 15I Moderation + Data Lifecycle |
| `PHASE_15J_PRODUCTION_OPERATIONS.md` | AtlasBoard — Phase 15J Production Operations / Environment Hardening |
| `PHASE_15K_STEAM_BACKEND_SECURITY_CLOSE.md` | AtlasBoard — Phase 15K: Steam / Backend Security Close |
| `SEC06_PLATFORM_SETTINGS_ARCHITECTURE.md` | AtlasBoard Security Core 06 — platform-isolated preferences v2 |
| `SEC07_PUBLIC_PROFILE_PRIVACY_POLICY.md` | AtlasBoard Security Core 07 — direct profile access and enumeration privacy |

## 10. Tarihsel ana yol haritası — v5.30.0 ve öncesi (yalnız denetim izi)

Aşağıda ZIP içindeki önceki `MASTER_ROADMAP.md` içeriği eksiksiz korunmuştur. İçindeki "current/next" ifadesi 1–8 bölümleriyle çelişirse **yukarıdaki yeni bölümler geçerlidir**. Eski Firestore/Functions varsayımları otomatik olarak yeniden uygulanmaz.

# ATLAS BOARD
## Master Architecture & Development Roadmap v5.30.0 — PHASE 17E INTEGRATED ANDROID / ACCOUNT / RTDB

**v5.30.0 integrated candidate (2026-10-15):** combines the untested v5.28 account/mobile implementation, the v5.29 compile correction, RTDB private user/device schema and the latest one-build Android acceptance gate. This is a candidate until Unity compile + one physical Android pass succeeds.


**v5.28.0 candidate checkpoint (2026-10-14):** owner device testing of v5.27 additionally confirmed Trade action/touch-camera functionality, but reproduced four release blockers: sustained low Android FPS, Trade HUD overlap/small tablet controls, LocalOffline open-seat/legacy Human leakage with no countdown, and second/third-match presentation drift after `Board_Prototype` reload. v5.28 makes LocalOffline Start canonical/open-to-Bot with a 5-second countdown, self-heals Phase14 scene-local presentation, adds the non-persistent Android render ceiling, separates Career color from Shop, and introduces optional guest/account access. Real Unity compile/APK/device acceptance remains pending; do not mark v5.28 PASS from source inspection alone.


**v5.27.0 candidate checkpoint (2026-10-14):** GitHub `main` is verified at `f170229ca4e20c255d605a35249939f516a863b3` (`Fix offline-mode compile blocker and prepare secure RTDB transport rules`). Physical Samsung SM-G780G / Android 13 evidence confirms the Local Offline fallback can enter PRIVATE TABLE, start a 4-seat Human+Bot match, roll dice, move pawns, buy/rent properties, resolve auctions, open/cancel/complete trade, resolve travel/event/special tiles and return to Main Menu without the Firebase emulator. The device log also proves PRIVATE TABLE currently falls through to the obsolete legacy Human/Bot dropdown setup; v5.27.0 fixes that routing so Local Offline reuses the current private-lobby seat presentation with `+ -> Local Player / Bot`. Mobile gameplay UX adds large Roll Dice and Trade controls beside the lower player HUDs, mobile-only touch camera gestures (one-finger rotate; two-finger pinch zoom + pan), larger tablet/trade presentation, larger touch-scrollable city information, slightly tighter mobile safe-area scaling (`1920x1120` reference target), and 10-second Android FPS/frame-time/memory telemetry. Desktop/Steam input and layout paths are unchanged.

**Performance evidence and policy (updated by v5.28 device evidence):** v5.27 telemetry now proves a real Android performance problem on samsung SM-G780G at 2400x1080: long gameplay windows measured roughly 16–23 FPS while targetFps=60, and dropping MSAA from 4x to 2x did not materially recover frame rate. v5.28 therefore adds a runtime-only mobile render ceiling (VSync off, <=2x AA, low-cost shadows, HDR/post-processing off, URP renderScale 0.82 when available) and a measured emergency profile (renderScale 0.70, AA/shadows off) after sustained <45 FPS. Gameplay timing, dice outcomes and authoritative rules are still not accelerated to fake smoothness. Windows/Steam saved graphics settings are not rewritten.

**Mobile interaction contract:** phones/tablets must expose primary gameplay actions as touch-sized controls without removing the existing desktop controls. `ROLL DICE` is anchored immediately to the right of the lower-left player HUD and `TRADE` immediately to the left of the lower-right player HUD. Tablet/modal content must remain usable at handset size; touch scrolling belongs to the modal and must block board-camera gestures while the modal is open. Camera gestures are mobile-only and must never alter mouse/keyboard/Steam behavior.

**Acceptance still pending:** Unity compile, Android APK build, physical-device verification of the modern offline seat UI, touch button positions, two-finger pinch/pan, one-finger rotate, city scrolling, larger trade controls and measured `ATB_MOBILE_PERF` telemetry. Firebase RTDB live multiplayer transport remains a separate Phase 17C networking task and is not claimed complete by this UX package.


**v5.26.1 checkpoint (2026-10-14):** GitHub `main` is now verified at `5bc6461463862419c50dd985867b2170348d7fd4` (`Introduce offline-first session modes and Android mobile display stabilization`). The owner's first v5.26.0 Android build attempt exposed a compile blocker in `AtlasBoardOfflineModeBootstrap.cs`: the component subscribed/unsubscribed `AtlasBoardFrontendErrorCodes.PublicCodeRaised` using a nonexistent `HandlePublicCode` symbol while the implemented callback was named `HandlePublicCodeRaised`. v5.26.1 fixes those three references without weakening the offline fallback. Firebase Realtime Database has now been created by the owner in Belgium / Europe and MUST start in Locked Mode. This checkpoint adds a deny-by-default `database.rules.json`, wires it into `firebase.json`, defines the planned Private Casual Online RTDB schema, and adds RTDB indexes only where query-based access is planned. RTDB Unity live transport is still PENDING; do not claim remote Private Table is functional until the SDK/transport and two-device test pass.

**Cost contract:** legacy four-human / 20-round / ~30-minute polling is estimated from current code at about 32,280 callable invocations plus roughly 158,400 Firestore document reads per match. The Firestore free quota is therefore the first bottleneck: one such legacy match can exceed the 50,000-read daily no-cost allowance. The Phase 17C target removes gameplay polling. Local Offline uses zero gameplay backend traffic. Private Casual Online targets zero normal gameplay Cloud Functions and zero live Firestore polling, using RTDB event traffic instead. With a design target of ~1–5 MB RTDB download per four-player 20-round match, the Spark 10 GB/month download allowance corresponds to roughly 2,000–10,000 such matches/month, while the hard Spark connection limit caps concurrency at 100 connections (about 25 simultaneous four-player matches or 50 simultaneous two-player matches). These RTDB match counts are DESIGN ESTIMATES and must be replaced with measured Android/desktop telemetry after transport implementation. Online Rewarded targets 200–500 trusted backend calls per match, a 98.5–99.4% reduction from the legacy ~32,280-call path; permanent XP/wallet/purchase authority remains outside untrusted RTDB client writes.

**Storage/security split:** RTDB is for transient presence, room membership, remote-human intents, intent acknowledgements, host state revisions, public room directory metadata and read-only display catalogs. Firestore/trusted backend remains authoritative for account identity, wallet/ledger, owned inventory, purchase/refund records, protected progression/achievements, verification receipts and moderation/audit. Public promo/event display metadata may be mirrored/cached in RTDB, but redeemable secret promo codes and any redemption that can mint XP/currency/items remain trusted operations. Static item/map/store display data should prefer bundled/versioned client catalogs before remote reads.


**v5.26.0 current checkpoint (2026-10-14):** Android physical-device evidence from Samsung SM-G780G / Android 13 confirms LandscapeLeft startup, native 2400x1080 viewport, safe area x=88 width=2312, Android native Back registration, mobile canvas fitting and on-device log sharing. The same log proves startup falls back because online identity is not ready (ATB110) and proves the v5.25.x resolution repair observed transient 1080x2400 pixels twice after Settings interaction. v5.26.0 therefore removes active mobile SetResolution repair, makes mobile Apply use the active landscape viewport, reduces the mobile reference height from 1248 to 1160, and establishes the Local Offline / Private Casual Online / Online Rewarded networking contract. Local/bot-only play is explicitly zero-cloud and non-rewarded. Private Casual Online is targeted to Firebase Realtime Database + host authority with no persistent progression. Online Rewarded is targeted to RTDB live transport plus narrowly bounded trusted Functions/Firestore operations and independent verification. RTDB transport itself is NOT yet implemented in this candidate.

**GitHub source note (superseded):** the earlier API lag is resolved. `main` is now visible at `f170229ca4e20c255d605a35249939f516a863b3`; v5.27.0 is based on the owner's pushed v5.26.1 checkpoint, not an older repository copy.
**Canonical current checkpoint (2026-10-13 UTC):** Security Cores 01–07 plus Phase 17A 19/19 compiled-unit and 12/12 Firebase callable E2E have passed owner Windows emulator tests. **Phase 16I Public Lobby v5.23.2: FULL LOCAL QA PASS** received in owner log `Pasted text(20261013-042741).txt`; 8/8 concurrent requests returned exact stable data, but 4/8 exceeded 5 seconds (`PERFORMANCE OPEN`). This v5.24.0 candidate extends the existing full-file Phase 17A E2E with nine dedicated Phase 17A.3 finalization/proof contention cases; **owner emulator retest is PENDING**. Full independent server replay verifier, live Android/Unity, Play Integrity App Check enforcement and production deployment remain OPEN. Earlier historical checkpoints are retained below.  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Latest verified pushed GitHub HEAD visible through API (2026-10-14):** `f170229ca4e20c255d605a35249939f516a863b3`  
**Last verified pushed HEAD commit visible through API:** `Fix offline-mode compile blocker and prepare secure RTDB transport rules`  
**Previous verified pushed HEAD (v5.17):** `bed153c2071716e1d7216fe0e3e8ecdb36f3f341`  
**Historical accepted checkpoint (superseded by sections 25.7 and 25.9):** `SECURITY CORE 01/02/03 LOCAL QA ACCEPTED by owner on Windows: SEC01 normal progression 14/14 plus guarded host-result negative checks; SEC02 economy 19/19; SEC03 rewards 15/15; restored inventory 11/11 and promo 13/13; terminal AtlasBoard QA FULL LOCAL PASS. SEC11 interim reward gate protects against observed fabricated-match XP (XP=0, wins=0, first_win=false), but an independent server-side full gameplay/result verifier is still NOT implemented and unverified permanent match rewards remain PENDING. All new source/test changes and this roadmap are OWNER LOCAL until pushed; no production deployment. Execution order: remaining Security/Core Audit -> optional pre-release gameplay/UX features -> real Steamworks Phase17C -> final feature-frozen audit -> owner GO/NO-GO.`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Phase 14 close commit:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`

This v5.17 checkpoint is based on already-pushed GitHub main `bed153c2`, while preserving the owner's newer local v5.12 roadmap content. **The owner has explicitly deferred Phase 17C because no official AtlasBoard Steamworks AppID has been obtained.** First complete Security & Core Audit (fix issues independently of the AppID where possible), then select/implement pre-release features, then return to Phase 17C and run actual cross-device/Steam/deployed tests; finally repeat audit on the frozen Release candidate. This roadmap-only handoff does not change running source, deployed services, GitHub commits or production settings; the owner will stage and push the accepted local source separately.

# 1. Source, replacement and Git discipline

Before meaningful source changes:

- Inspect current GitHub `main` before using repository source as a baseline.
- GitHub is authoritative only for work already committed and pushed.
- Newer accepted local work must never be overwritten by an older GitHub copy.
- Deliver only complete replacement source files at their canonical paths when a functional change is necessary; NEVER add new `Tools` folders, one-off patch scripts, installers, or diagnostic helpers to the project by default. A roadmap-only update must contain only the roadmap.
- Do not use textual patches, `git apply` patches or installer scripts unless explicitly requested. Reuse existing QA entrypoints and diagnostics rather than proliferating one-time tools.
- Do not auto-commit, auto-push, deploy or mutate production configuration.
- Do not use `git add .`; stage only intended files.
- Do not stage unrelated historical/font/generated changes.
- Backend QA belongs in `Backend/Firebase/functions/scripts/qa.mjs`.
- Roadmap status changes belong in this canonical `MASTER_ROADMAP.md`.
- Firebase Functions source changes require TypeScript build and focused validation before acceptance.
- Restart emulators after backend source changes when emulator runtime behavior is under test.
- Unity runtime changes require compile + runtime smoke before acceptance.
- Static review is not equivalent to production runtime acceptance.
- Never place Steam publisher secrets, production credentials, App Check debug tokens or raw provider tickets in Unity source/logs.
- Local-emulator QA shortcuts must be narrowly gated and must never silently apply to Release.

Current source authority (checked 2026-10-12 America/Denver):

- Latest verified pushed `main`: `bed153c2071716e1d7216fe0e3e8ecdb36f3f341`, `Accept Phase 16I local QA and Phase 17B Steam invite overlay validation`. Phase 16I and the Phase 17B.2 QA component are in the PUSHED checkpoint.
- The push also included `Tools/Phase17B/SteamGuestPreflight.ps1` and two modified TextMesh Pro/Noto font assets. They are NOT prerequisites for future roadmap-only delivery; review those font changes for unintended diffs, and consider removing the one-off preflight script later through an explicitly approved housekeeping change, NOT an automatic cleanup.
- Keep development-only `steam_appid.txt` at AppID 480 out of the repository and release package; never stage Steam tickets, publisher API keys, private tokens, log dumps, generated builds or emulator database files.
- Only the owner stages/pushes approved project files; inspect local `git status` and any new local work before replacing this roadmap.

# 2. Durable architecture invariants

## 2.1 Canonical identity and provider boundaries

- Canonical Atlas AccountId remains authoritative.
- SeatId remains authoritative inside a match/session.
- SteamID is a verified external provider identity mapped to Atlas AccountId.
- Steam remains an adapter, not the canonical account database.
- Wallet, inventory, progression, lobby, match, moderation and history remain Atlas/Firebase-side systems.
- Development Steam AppID `480` is development-only and must never be treated as the production AtlasBoard AppID.

## 2.2 Server authority

Backend/server authority remains required for:

- account identity and provider mappings;
- wallet/premium economy;
- inventory entitlements;
- promo/event rewards;
- lobby/session membership;
- match authority and authority transfer;
- presence/reconnect state;
- chat/moderation/report state;
- completed-match progression and achievements;
- future competitive/ranked outcomes.

## 2.3 Host/seat invariant

Gameplay state belongs to the Player Seat, not the current controller.

`Human -> TemporaryBot -> Human` must preserve:

- AccountId / SeatId;
- money;
- properties;
- pawn position;
- development;
- turn state;
- bankruptcy state;
- match-result state.

Reconnect and snapshot optimization must preserve this invariant. Do not reduce or discard required authoritative synchronization merely to improve apparent latency.

## 2.4 Hosting strategy

- Managed Firebase remains the account/persistence foundation, but live transport is now mode-specific.
- Local Offline uses no backend.
- Private Casual Online targets Firebase Realtime Database + host-authoritative simulation and intentionally carries no persistent competitive rewards.
- Online Rewarded targets RTDB for transient live transport while trusted Functions/Firestore are reserved for secure economy, moderation, bounded verification and permanent progression.
- Do not add a self-managed always-on VPS/game server by default.
- Steam does not automatically host AtlasBoard gameplay authority.
- Steam Networking/SDR may be evaluated later as an optional transport optimization.
- Local emulator cold-start behavior must not be mistaken for measured deployed production latency.

## 2.5 App Check invariant

- Editor/development security validation uses the Firebase App Check debug-provider path.
- Desktop standalone production architecture uses the Steam custom App Check provider.
- `platformSteamCreateAppCheckToken` remains the bootstrap endpoint and must not require an existing App Check token.
- Production Steam custom App Check runtime acceptance remains deferred until a real AtlasBoard Steam AppID and publisher Web API secret exist.
- Local Functions Emulator acceptance is not cryptographic proof of production App Check validity.
- Local two-client Guest QA may use an explicit emulator-only placeholder App Check header only when:
  - Development Build;
  - Windows standalone Guest;
  - explicit `-atlasLocalEmulators`;
  - loopback Functions endpoint (`127.0.0.1` / `localhost`).
- That placeholder is never a production credential.

## 2.6 Phase 14 UI invariants

- Full gameplay-tablet geometry uses `TabletUIManager.tabletRoot`.
- Existing gameplay managers remain authoritative; presentation wrappers mirror/call them.
- While a gameplay tablet is open, world City Information clicks do not pass through.
- City Information ScrollRect owns wheel/drag while the pointer is interacting with it.
- Generic labels such as `Bölge N` resolve into the active locale.
- Required localization target remains EN/TR/ES/FR/DE/KO/RU.
- Rejected world-space P1/P2/P3/P4 ownership badges stay removed.

## 2.7 Public error-message invariant

Player-facing online/runtime errors must not expose backend implementation details.

Public UI must never expose:

- Firebase / Firestore / Functions provider names;
- hostnames, ports or backend URLs;
- project IDs;
- raw HTTP/service responses;
- auth headers or tokens;
- database/collection names;
- Steam publisher/security internals;
- stack traces.

Player-facing errors use:

- a generic localized message;
- a stable `ATBxxx` support code.

Technical meaning belongs in protected diagnostics and the internal codebook.


# 2A. Offline-first / cost-bounded session architecture (v5.26.0)

This section supersedes any older statement that every AtlasBoard lobby/match must continuously use Firebase Functions/Firestore. The authoritative product contract is now mode-specific.

## 2A.1 Local Offline
- One device with local humans and/or bots requires no internet.
- PRIVATE TABLE without an authenticated online identity routes to the existing local lobby.
- No RTDB, Firestore gameplay traffic or Cloud Functions gameplay traffic.
- Host device resolves all bots and local turns.
- No server anti-cheat requirement because the session cannot mint persistent value.
- No permanent XP, wins, achievements, wallet currency, competitive rating or authenticated cloud match history.

## 2A.2 Private Casual Online
- At least one remote human; local humans/bots remain permitted.
- Target live transport is Firebase Realtime Database persistent/event-driven state, not Cloud Functions polling.
- Host-authoritative simulation remains acceptable because the mode is casual/non-rewarded.
- No permanent XP/wins/achievements/premium rewards/rating.
- RTDB rules still require authenticated membership and per-user intent ownership.
- Presence/reconnect should use RTDB connection semantics rather than 0.25/0.5/1/3/5-second HTTP polling loops.

## 2A.3 Online Rewarded
- Persistent rewards are potentially eligible only with at least two distinct authenticated human accounts.
- RTDB is the target live transport for state revisions, presence and remote-human intents.
- Host computes bots locally and sends only meaningful results/revisions; bot micro-steps are not network traffic.
- Cloud Functions are reserved for trusted boundaries: protected admission where necessary, economy/purchases/promos, moderation, server-owned randomness/proof, bounded checkpoint/final verification and permanent progression finalization.
- Firestore remains the persistent trusted store for valuable/account records.
- A consistent host result is NOT independent proof; the server-authoritative replay/proof gate remains a release blocker for rewarded results.

## 2A.4 Storage placement
- Firestore/trusted Functions: accounts/provider links, wallet/ledger, inventory/entitlements, purchases/refunds, promo redemption, progression/achievements, protected verification receipts, moderation audit.
- RTDB target: transient room state, presence, ready/connectivity, remote-human intents, host live-state revisions, reconnect signaling and other ephemeral match transport.
- Static catalog/display metadata should be bundled/cached whenever possible. Moving purchase authority or promo eligibility to an untrusted RTDB client is forbidden even if display metadata is delivered there.

## 2A.5 Cost rules
- No per-frame or sub-second Cloud Function polling in the target architecture.
- Batch compatible turn changes into one revision/event bundle.
- Publish only on authoritative state change.
- Coalesce rapid settings/lobby edits.
- Do not upload bot think/movement micro-steps.
- Cache immutable/slow-changing documents.
- Measure bandwidth/reads/writes after RTDB migration before making player-capacity claims.

See `Assets/Project/Documentation/PHASE17C_NETWORK_COST_ARCHITECTURE.md` for the detailed implementation/acceptance plan.

# 3. Accepted foundation summary

Accepted foundations include:

- Core board gameplay, dice, movement and economy.
- Auction, Trade, Development, bankruptcy and Event/Special systems.
- Bots and player-seat architecture.
- Camera/audio/settings.
- Seven-language localization.
- Private/public lobby flow.
- Public lobby browser and Join by Code.
- Host-authoritative online gameplay.
- Lobby + Match Chat and moderation/report foundation.
- Store/economy/seasonal-event foundation.
- Career/progression/history/achievement foundation.
- Provider-neutral platform contracts.
- Steamworks.NET integration.
- Secure Steam account linking.
- Returning Steam sign-in and Steam recovery.
- Shared development backend.
- Same-match reconnect preservation.
- Graceful Host Migration and crash failover.
- Phase 12 resilience hardening.
- Phase 14A–14K UI/UX polish.
- Phase 15 security/operations hardening through 15K at the documented local/emulator acceptance level.
- Phase 16 backend QA and Unity build-readiness validation.
- Local two-client Guest startup stabilization.
- Runtime diagnostics.
- Snapshot single-flight/coalescing.
- Host-bootstrap prioritization.
- Localized loading/bootstrap UX.
- ATB frontend error-code framework.

# 4. Phase status overview

## Phase 3B — Seat / Turn / AFK
**Status: DONE.**

## Phase 3C — Account & Cloud Profile
**Status: FOUNDATION DONE; production provider validation remains a release gate.**

## Phase 3D — Lobby / Session UI
**Status: DONE.**

## Phase 4 — Public Lobby Browser
**Status: DONE foundation; Phase 16I local cursor pagination accepted. Production-scale validation remains outstanding.**

## Phase 5 — Online Gameplay Networking
**Status: MAIN PATH DONE / runtime flows proven. Phase 16H latency/concurrency hardening accepted locally.**

## Phase 6 — Chat + Moderation Foundation
**Status: DONE.**

## Phase 8 — Gameplay / Content Checkpoint
**Status: DONE.**

## Phase 9 — Store / Economy / Seasonal Event
**Status: DONE foundation.**

## Phase 10 — Career / Progression / History / Achievements
**Status: DONE foundation.**

## Phase 11 — Platform / Steam / Multiplayer / Host Migration Foundation
**Status: DONE through 11G.**

Key pushed checkpoints:

- `11fdcdaba2f37e506994708674b0ee01e62ab867` — 11A/11B platform foundation + Steam integration.
- `8e91db76914db59cbf17f28f788adf24a193fd17` — 11C secure Steam account linking.
- `a5e4b28f442a7ad22b5789096633c8ef7bd141f1` — 11D returning Steam sign-in.
- `a8d777b8eb9e798fec8b5f750185ae428b34ca18` — 11E Steam recovery / achievement resync.
- `6ddf01ec66f0116396d18b27b171b9c6080ee671` — 11F shared multiplayer lobby/reconnect.
- `068ca6f0d1736516ace63b79681227202a408cf8` — 11G Host Migration parent checkpoint.

## Phase 12 — Resilience Hardening
**Status: DONE / accepted.**

Close commit:

- `0eca81450be91b30a5dc1cd66d901c46e2cab07c`

## Phase 13
**Status: RETIRED AS A SEPARATE IMPLEMENTATION PHASE.**

Host Migration was absorbed into Phase 11G and further validated in Phase 12.

## Phase 14 — Dedicated UI/UX Polish
**Status: DONE / CLOSED / PUSHED.**

Close commit:

- `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157`
- `Close Phase 14 UI/UX polish and finalize localization/input handling`

# 5. Phase 15 — Security and Production Backend Hardening

**Overall engineering status: LOCAL/EMULATOR SECURITY AND OPERATIONS IMPLEMENTATION COMPLETE THROUGH 15K.**  
**Checkpoint through Phase 15 local close is pushed in `74697e472b865f76afe8f23b3ba8036ac5d35032`.**  
**Production Steam cryptographic acceptance: DEFERRED.**

## 15A — Firestore Direct-Client Security Boundary
**Status: DONE / PUSHED.**

## 15B — Unified Emulator Firebase Session
**Status: DONE for Editor/dev architecture; standalone local Guest uses the isolated QA topology documented under Phase 16F.**

## 15C — App Check Transport Plumbing
**Status: DONE / PUSHED.**

## 15D — Firebase App Check SDK + Editor Debug Provider
**Status: DONE / PUSHED.**

## 15E — Windows/Steam Production Custom App Check Provider
**Status: CODE/BUILD ACCEPTED; REAL PRODUCTION RUNTIME PROOF DEFERRED.**

## 15F — Staged App Check Enforcement
**Status: DONE at local/emulator level; deployed cryptographic invalid-token proof deferred.**

## 15G — Callable Authorization + Abuse Hardening
**Status: DONE.**

## 15H — Economy / Commerce Security
**Status: DONE.**

## 15I — Moderation + Data Lifecycle
**Status: DONE / PUSHED CHECKPOINT.**

Accepted direction:

- explicit moderator authority;
- no user self-escalation;
- server-authoritative chat/report/sanction flows;
- deleted/expired chat excluded from normal reads;
- open investigation evidence preserved;
- audit/config evidence protected;
- local moderation security validation passed.

## 15J — Production Operations / Environment Hardening
**Status: DONE / PUSHED CHECKPOINT.**

Accepted direction:

- loopback-only emulator exposure;
- fail-closed environment boundaries;
- publisher secret kept server-side;
- sensitive Steam/App Check logs bounded/redacted;
- credentials excluded from Git;
- production rollout blocked until real production prerequisites exist.

## 15K — Steam / Backend Security Close
**Status: LOCAL CODE/EMULATOR SECURITY CLOSE PASS / PUSHED CHECKPOINT; PRODUCTION CRYPTOGRAPHIC CLOSE DEFERRED.**

Still required before production security close:

- real AtlasBoard Steam AppID;
- real publisher Web API secret;
- real Steam ticket verification;
- real Windows standalone Steam custom App Check runtime acceptance;
- deployed missing-token and cryptographically invalid-token rejection proof;
- bootstrap exemption proof against deployed configuration;
- Release verification that no local emulator shortcut can activate.

# 6. Phase 16 — Build Readiness, Runtime Stability and Performance

**Status: ACTIVE.**  
**Current project workstream: Security & Core Audit active; Phase 17C formally deferred until after pre-release feature work and official Steamworks AppID availability.**

## 16A — Consolidated backend QA
**Status: PASS locally.**

Canonical backend QA:

`Backend/Firebase/functions/scripts/qa.mjs`

## 16B — Unity build readiness
**Status: PASS locally.**

Canonical validator:

`Assets/Project/Editor/AtlasBoardBuildQA.cs`

Canonical menu:

`Atlas Board/QA & Diagnostics/QA/Validate Build Readiness`

## 16C — Canonical two-client local Guest QA build
**Status: IMPLEMENTED / ACCEPTED.**

Canonical builder:

`Assets/Project/Editor/AtlasBoardTwoClientLobbyTestV1.cs`

Canonical menu:

`Atlas Board/Online & Backend/Online/Build Two-Client Guest Test v1`

Output:

`C:\Users\crazy\AtlasBoard_3D3C_GuestBuild\AtlasBoardGuest.exe`

Local emulator testing must use the generated BAT with `-atlasLocalEmulators`.

## 16D — Automatic runtime/crash diagnostics
**Status: IMPLEMENTED / ACCEPTED.**

Canonical Development Guest diagnostics:

`Assets/Project/Scripts/Online/AtlasBoardRuntimeDiagnostics.cs`

Diagnostics include:

- startup/build metadata;
- Unity warnings/errors;
- frame summaries;
- main-thread stalls;
- session clean/unclean markers;
- separate timestamped files per run.

## 16E — October 11 Windows Guest native-crash regression
**Status: MITIGATED / CURRENT LOCAL QA PASS / REGRESSION WATCH.**

Historical signature:

- Windows WER `BEX64`;
- `0xC0000409`;
- subcode `7`;
- `ucrtbase.dll`;
- repeated same fault offset.

Important conclusion:

- exact native root function was not symbolically proven;
- D3D11 did not solve it;
- backend latency was not sufficient to explain the native process death;
- unnecessary overlapping native Firebase/Steam startup paths in the standalone local Guest were strongly correlated with the failure.

Current local Guest mitigation:

1. loopback Development callable requests use an emulator-only App Check placeholder;
2. native App Check provider registration is bypassed;
3. Steam startup/provider path is bypassed;
4. Phase 15B secondary FirebaseAuth/Firestore SDK emulator sign-in is suppressed;
5. Guest build-scene native AccountService/test components are stripped;
6. HTTP Auth Emulator + Functions Emulator multiplayer identity remains authoritative;
7. Editor and Release behavior remain separate.

Repeated later Guest sessions have not reproduced the historical native crash and have reached gameplay/clean exit.

Do not claim the exact historical native root cause is mathematically proven. Reopen immediately if the same signature returns.

## 16F — QA-only isolation and Release retest gate
**Status: ACCEPTED.**

Intentionally bypassed only in the explicit local Windows Development Guest:

- native FirebaseAuth initialization;
- native Firestore initialization;
- Phase 15B secondary SDK emulator sign-in;
- native App Check provider registration;
- SteamAPI/provider startup.

These must not be casually re-enabled in the local Guest.

Before Release:

- verify build-strip logic cannot affect Release;
- verify native Firebase account/profile path;
- verify cloud preferences/profile persistence;
- verify real Steam identity;
- verify returning Steam sign-in;
- verify real Steam custom App Check;
- run repeated Release startup/shutdown tests;
- reopen native crash investigation if the historical signature returns.

There is no forgotten feature to turn back on now. This is an explicit QA topology separation.

## 16G — Lobby/match responsiveness
**Status: ACCEPTED locally.**

Accepted behavior:

- Lobby Back remains usable during pending work.
- Guest match/lobby exit is immediate locally.
- Remote cleanup remains safe/asynchronous.
- Host exit preserves authority handoff semantics.
- stale responses from a departed match cannot reopen old UI/session state.

Invariant:

Do not break reconnect, host migration or authoritative snapshot semantics to improve apparent responsiveness.

## 16H — First-run latency / request concurrency
**Status: PASS locally.**

### 16H.1 — Snapshot single-flight / coalescing
**Status: PASS locally.**

Accepted behavior:

- only one `matchGetSnapshot` network request per client/match may be in flight;
- concurrent callers join the existing task;
- forced refresh during an in-flight call becomes at most one follow-up refresh;
- reconnect/host-migration/explicit refresh semantics remain intact;
- polling cadence was not blindly removed;
- remote pawn-cosmetic intent does not submit before a valid authoritative match snapshot exists.

Observed acceptance evidence:

- runtime diagnostics reported `NetworkRequests=1, CoalescedCallers=1` under overlapping callers;
- previous large snapshot-request bursts were no longer reproduced;
- warm `matchGetSnapshot` execution returned to normal tens-of-milliseconds behavior.

### 16H.2 — Host bootstrap priority
**Status: PASS locally.**

Fresh-match startup now prioritizes:

1. initial authoritative snapshot/readiness;
2. first authoritative host publish;
3. follower/starting-roll visibility;
4. maintenance traffic after the critical frame.

Non-critical startup maintenance such as presence/sweep/reconnect-expiry no longer races ahead of the first authoritative frame.

Observed acceptance evidence:

- first authoritative host frame was published in approximately `221 ms` in the accepted test;
- follower match loading observed authoritative readiness in tens to low hundreds of milliseconds;
- starting-roll flow remained functional.

### 16H.3 — Localized loading/bootstrap UX + frontend diagnostic code system
**Status: PASS locally.**

New runtime files:

- `Assets/Project/Scripts/UI/Loading/AtlasBoardLoadingExperience.cs`
- `Assets/Project/Scripts/UI/Loading/AtlasBoardFrontendErrorCodes.cs`

New documentation:

- `Assets/Project/Documentation/FRONTEND_ERROR_CODEBOOK.md`

Accepted behavior:

- startup loading screen performs real readiness/bootstrap work;
- Development/Editor may prefetch a small public-lobby read to absorb emulator cold start behind loading;
- Release does not add speculative lobby traffic merely for warming;
- match transition shows a short blocking loading state until an authoritative snapshot exists;
- loading screens do not disable reconnect, host migration, snapshots or security;
- localized tips rotate during loading;
- supported loading languages remain EN/TR/ES/FR/DE/KO/RU;
- public error text never exposes infrastructure/provider details;
- player-facing problems use generic localized text plus stable `ATBxxx` codes;
- Development logs include phase name, duration, status and sanitized diagnostic notes.

Delay behavior:

- Startup `10 s` -> `ATB101`
- Startup `20 s` -> `ATB102`
- Startup `30 s` -> `ATB103`
- Match sync `10 s` -> `ATB301`
- Match sync `20 s` -> `ATB302`
- Match sync `30 s` -> `ATB303`

Delay codes are observations, not automatic proof of backend failure.

Accepted startup evidence from the later healthy Guest sample:

- identity readiness approximately `75 ms`;
- lobby prefetch approximately `238 ms`;
- profile bootstrap approximately `994 ms`;
- total startup bootstrap approximately `1203 ms`;
- match authoritative readiness approximately `44–151 ms` depending on transition;
- session completed with clean exit.

Cold emulator runs may still cross a delay threshold such as `ATB101`; if the operation subsequently succeeds, the game continues normally.

### 16H housekeeping — one-shot match-loading completion
**Status: PASS locally.**

The earlier accepted test showed repeated `ATB_MATCH_LOAD event=authoritative_ready` diagnostic lines while the short overlay-close delay was still pending.

Accepted fix:

- `AtlasBoardLoadingExperience.cs` uses a one-shot completion guard per match-loading cycle;
- repeated `SnapshotChanged` notifications no longer duplicate the `authoritative_ready` completion log;
- this does not change polling, networking, reconnect, host migration, backend calls or loading duration.

Acceptance evidence:

- later Guest runtime showed one `ATB_MATCH_LOAD event=authoritative_ready` line for each distinct loading cycle;
- no duplicate completion spam was observed;
- gameplay and networking remained healthy.

### 16H.4 — Critical gameplay-intent transport reliability
**Status: PASS locally.**

A real runtime defect was observed after the initial Phase 16H acceptance:

- a remote purchase decision could surface client-side `HTTP 0: Request timeout`;
- host-crash recovery/maintenance calls could time out in the same window;
- backend execution itself was not consistently slow;
- the missing usable purchase decision could reach the existing 10-second safe-AFK fallback and convert the action to `SKIP`/auction even though the player had acted.

Accepted fix:

- gameplay-critical intents use a short bounded transport attempt;
- one transport retry is allowed only for retryable timeout/connection failure;
- retry preserves the **same `clientCommandId`** so backend idempotency remains authoritative;
- no new gameplay command identity is created for retry;
- while a critical intent is active, non-critical client traffic is deprioritized/suppressed where safe so it does not compete with the decision request;
- snapshot/reconnect/host-migration authority remains intact;
- cosmetic traffic remains non-critical.

Acceptance evidence from the later Editor + Guest test:

- no `HTTP 0: Request timeout`;
- no `Remote purchase decision failed`;
- no host-recovery/sweep timeout regression;
- real Guest and Editor purchase decisions completed successfully;
- backend `matchSubmitIntent` examples completed in tens of milliseconds and were consumed/acknowledged by the host;
- snapshot single-flight remained active;
- host bootstrap remained active;
- session ended with `SESSION_CLEAN_EXIT droppedMessages=0`.

The retry branch itself did not need to trigger in the accepted healthy run; the test proves the normal path did not regress and the previously observed failure did not reproduce under the same local topology.

## 16I — Public lobby scale / discovery / measured capacity
**Status: LOCAL/EMULATOR ACCEPTED — PRODUCTION CAPACITY NOT PROVEN.**

Accepted local Phase 16I work:

- Server-side cursor-based public lobby discovery, callable contract, Unity paging client/browser and focused QA were delivered/tested locally. The implementation replaced the earlier fixed-first-N discovery behavior in this accepted local branch.
- Public-lobby backend E2E, Auth/App Check and host-close test regressions passed in the accepted Phase 16I test sequence; consolidated local QA was reported PASS.
- Initial capacity/read probes: 80 synthetic discovery projections with 2 real two-user test matches, 168/168 workflows; 120 projections with 4 matches, 960/960 workflows. Combined 1,128/1,128 successful measured workflows and 1,410 callable attempts, zero reported failures. These are local emulator observations, **not production user-capacity numbers**.
- Active match transport smoke: 8 independent two-account matches, 56/56 full guest-intent/host-list/ACK/publish/guest-read cycles successful; idempotent replay and stale-revision guards passed.
- Timed active-match soak: 8 concurrent matches 208/208 cycles PASS; 16 concurrent matches 480/480 cycles PASS; total 688/688, 0 errors and no idle matches in that local run.
- Mixed load v1 revealed a **test harness bug**: synthetic discovery heartbeats expired during the run; 4 discovery workflow failures and no 32-match phase. This was not accepted as backend failure or PASS.
- Mixed load v2 refreshed 100 synthetic room heartbeat projections every 8 seconds. With four concurrent browser workers: 16-match stage 184/184 cycles plus 91/91 discovery workflows; 32-match stage 677/677 cycles plus 98/98 discovery workflows. Combined 861/861 match cycles and 189/189 discovery workflows PASS. Zero cleanup warnings.
- A/B latency diagnostic: all 32 match pairs prewarmed. Three 45-second stages: 16 synchronous 288/288 cycles, 32 synchronous 507/507 cycles, 32 staggered by 75 ms 576/576 cycles; 1,371/1,371 cycles and 216/216 discovery workflows successful, with 32 replay plus 64 stale-revision guard probes accepted. Local cleanup: 0 warnings.
- A/B observed complete-cycle P95: 16 synchronous 1,354 ms; 32 synchronous 1,438 ms; 32 staggered 253 ms. Some synchronous-stage tail latency exceeded 19 seconds; staggered-stage P95 was approximately 253 ms. This indicates burst sensitivity in the **local emulator** and may also reflect warmup/order effects. It is not proof of a production pacing fix or production capacity.

Accepted tooling/evidence (local scripts and generated reports are QA artifacts):

- `Backend/Firebase/functions/scripts/qa.mjs` and the focused public-lobby E2E suite.
- `phase16i-active-match-load.mjs`, `phase16i-active-match-soak.mjs`, `phase16i-mixed-stress.mjs`, `phase16i-latency-diagnostic.mjs` local harnesses.
- Representative output JSON: `AtlasBoard_Phase16I_ActiveMatchSoak_mv4i7m57d0bdef27.json`, `AtlasBoard_Phase16I_MixedStress_mv4j5jg3298f3f11.json`, `AtlasBoard_Phase16I_LatencyDiagnostic_mv4jve2k501ed80b.json`.

**Open Phase 16I external gates:** Firebase billing/quota/read/write cost measurements, production-like deployed capacity, physical Windows clients, multi-region latency, release build behavior. No assertion is made that 32 concurrent emulator matches equal a certified player limit. Full production acceptance is deferred to Phase 17.

# 7. Phase 17 — External / Live Testing

**Status: PAUSED BY OWNER AFTER 17A/17B LOCAL STEAM SMOKE PASS. Phase 17C and later external/production tests resume AFTER Security & Core Audit and pre-release features (and after official Steamworks AppID is available).**

## 17A — Steam development client readiness and Unity runtime

**Status: DEVELOPMENT STEAM RUNTIME SMOKE PASS; PUBLISHER VERIFICATION PENDING.**

- Read-only Windows preflight found Steam desktop running, Steamworks.NET present, Unity platform/auth source present, and development AppID 480 configured; standalone build was not yet discovered at the initial preflight.
- In Unity Editor Play Mode: Steam provider active (`steam`), Steam initialized, Steam AppID 480, Steam identity/persona nonempty, and Phase 11B static Steam validation PASS 8/8.
- The Steamworks client/overlay smoke on AppID 480 is explicitly **not** real AtlasBoard Steam publisher authentication and does not prove a deployed App Check token or publisher-ticket verification.

## 17B — Windows Development Guest / Steam invite overlay

**Status: 17B.2 LOCAL OVERLAY OPEN PASS; REAL FRIEND INVITE/JOIN PENDING.**

- Standard local Guest QA is still intentionally isolated from unnecessary native Firebase Auth/Firestore and automatic Steam initialization. In `AtlasBoardPlatformRuntime.ShouldUseSteamProvider()`, explicit loopback Development Guests use the development identity provider; the ordinary Steam-gated invite button therefore cannot be expected to show in this mode.
- Phase 17B added a separate **opt-in Development Windows Guest QA component** `Assets/Project/Scripts/Platform/AtlasBoardSteamGuestOverlayQa.cs`. Only explicit `-atlasLocalEmulators` on loopback **and** `-atlasSteamOverlayQa` activate it. This does not change the production identity or App Check implementation.
- First QA run proved the button was created and clicked, but `SteamAPI.Init()` failed. The later preflight proved `steam_appid.txt` was **missing beside the Guest EXE**. A development-only local file containing `480` was then created; no Steam secret was involved.
- After rebuilding, preflight and manual correction, the accepted 2026-10-11 20:07 America/Denver Guest log showed:
  - `ATB_BOOTSTRAP event=startup_ready` and a real local-emulator private Waiting lobby;
  - `ATB_PHASE17_STEAM_OVERLAY_QA_V2 event=BUTTON_VISIBILITY visible=True`;
  - `CLICK_RECEIVED` -> `STEAM_INIT_BEGIN` -> `STEAM_INIT_OK appid=480 overlay_enabled=True`;
  - `OVERLAY_CALLBACK_REGISTERED` -> `INVITE_API_BEGIN` -> `INVITE_API_RETURNED` -> `OVERLAY_ACTIVATED`;
  - overlay closing callback and `SESSION_CLEAN_EXIT droppedMessages=0`.
- The user screenshot confirms the Steam Overlay opens directly to an **`INVITE ANYONE TO PLAY`** panel with **Spacewar** branding (AppID 480). The normal AtlasBoard production invitation/Steam friends join flow was **not** proved. The absent full direct-invite menu for Spacewar is a development AppID limitation/Steam client context; a real app AppID and publisher setup are necessary but not by themselves sufficient for acceptance.
- V2 QA component diagnostics log button creation/visibility/clicks, environment/AppID paths, Steam process, Steam API init, invite API and Overlay activated/closed callbacks, with the **QA component's** raw room connect payload redacted; repeated failed native init is blocked for that run. **Privacy watch:** a separate, older private-room creation logger still prints the six-digit room code into the overall Guest session log; redact that inherited logger before any public telemetry/log sharing.
- The QA-only Steam native init still carries historical Windows native-crash regression risk. Future production implementation must use the real platform adapter, not promote this QA component or AppID 480 as a release entitlement workaround.
- **Expected Development Guest Steam launch options:**
  `-atlasLocalEmulators -atlasSteamOverlayQa -atlasDevName="Colorado Player" -screen-width 1280 -screen-height 720 -screen-fullscreen 0`
- **Dev-only AppID file:** `steam_appid.txt` alongside the Guest EXE with value `480` (local only, do not commit).
- **Historical one-off preflight:** `Tools/Phase17B/SteamGuestPreflight.ps1` is already committed in bed153c2. Keep only if useful for reproducible developer onboarding; new work must NOT introduce additional `Tools` files, and a future authorized cleanup may remove this helper without touching the accepted Steam QA runtime.

## Remaining Phase 17 acceptance gates

1. Obtain the actual AtlasBoard Steam AppID; configure Steamworks title/app permissions/builds and real publisher credentials. Recheck native Steam initialization and actual friend invitations through Steamworks, including friend acceptance, start/join payload handling and canceled/invalid invites.
2. Validate real Steam ticket server-side with Valve; verified account mapping and returning sign-in must keep canonical Atlas AccountId stable. Never treat a SteamID sent from the client as verified proof.
3. Prove custom Steam App Check minting and deployed enforcement against an explicitly approved staging/production project; valid token positive control plus cryptographically invalid/missing token negative controls. No production deployment or enforcement changes without explicit authorization.
4. Real two-, three- and four-player Unity matches using separate physical PCs and real Internet; examine invite, reconnect, host loss/migration, game-state consistency and match outcome. Local `127.0.0.1` emulators cannot connect separate geographic PCs.
5. Multiple regions/providers where feasible, cold/warm launch, latency and jitter/loss observations, chat UTC timestamps, retry/timeout handling and real deployed quota/cost studies.
6. Windows Release topology gate: prove dev-only flags do not work in Release, no native Firebase/Steam regression, clean build/startup/shutdown, crash diagnostics, signed-off release/security/operations controls.

**Do not mark Phase 17 or production security CLOSED until these gates pass.**

# 8. Optional backlog — not committed scope

Possible later work:

- Friend/party system.
- Quick Play / matchmaking.
- Casual/ranked split.
- Skill rating.
- Spectators.
- Replay/history expansion.
- Quick-chat/emotes/pings.
- Voice only if moderation burden is justified.
- Seasonal profile cosmetics.
- Additional missions.
- Alternate rules / tournament modes.
- Steam Workshop/custom maps.
- Colorblind palettes and additional accessibility.
- Privacy-safe crash telemetry/analytics.
- Feature flags / staged rollout.
- Backend health dashboard.
- Moderation dashboard.

These remain optional. Owner has explicitly put pre-release feature planning/implementation **after the current Security & Core Audit** and **before returning to Phase 17C**. Release-blocking security checks remain mandatory; never treat an optional feature as a security waiver.

# 9. OWNER-APPROVED EXECUTION ORDER — UPDATED v5.17

1. **Phase 14 and prior accepted phases:** preserve as implemented/accepted with documented regressions watched.
2. **Phase 15A–15K:** local/emulator hardening accepted; deployed App Check and real Steam publisher proof still blocked.
3. **Phase 16A–16I:** local/backend/Unity/development test checkpoints accepted; no production capacity claims.
4. **Phase 17A and 17B.2:** Unity Steam AppID 480 and Development Guest invite-overlay smoke accepted; no actual Steam friend invite/remote join proof.
5. **NOW — Security & Core Audit, PASS 1:** Security Core 01/02/03 owner Windows emulator QA accepted; continue untested callable authorization, privacy, rate limits, pagination abuse, Unity gameplay and server-verifiable match event design. The current interim gate must not be mistaken for a permanent trusted result engine. No production or Steamworks changes until explicitly approved.
6. **NEXT — Pre-release Features + UX/Gameplay Polish:** choose scope from Section 16, implement accepted additions with regression tests. Decide Android vs Windows-only and crossplay separately; do not silently add every wishlist item.
7. **THEN — Phase 17C and Phase 17D+:** obtain official Steamworks AppID and approved shared staging, validate real Steam tickets/App Check, 2/3/4 real PCs, invites, reconnect/host loss, cold/warm cross-region, Release topology and real-world quota/cost.
8. **THEN — Security & Core Audit, FINAL PASS:** repeat critical suites against feature-frozen EXACT release candidate and staging; review all P0/P1 findings and dependencies; require human evidence/approval.
9. **LAST — Release candidate, Steam launch preparation and release owner GO/NO-GO.**

Dependencies: Step 5 can start immediately without a real Steamworks AppID. Production cryptographic close in Steps 7/8 **cannot** be labeled PASS until official AppID, publisher verification and approved staging exist. Never weaken production security to bypass this dependency.

# 10. Immediate next action

**ACTIVE: Security & Core Audit — Batch B / Security Core 04 (after accepted Core 01–03 local emulator QA).** The pushed-baseline source inventory covered 21 backend TypeScript files and 55 callable endpoints; newer local guarded exports are now covered by the accepted local suite, but production inventory and Unity proof are still outstanding. Maintain comprehensive security review, not an all-project PASS certificate. Prioritize the remaining tracks:

1. **Authentication / permissions / abuse:** Firestore Rules, callable authorization by account and object, emulator-only helper guards, lobby join throttles, pagination cursor tamper/flood cases, denied access cross-accounts.
2. **Host authority / match results / progression:** trace host `matchHostPublishState` -> completion frame -> `progressionRecordCompletedMatch` -> XP/achievements; design controlled negative cases for forged match completion/outcome/turn sequence; avoid assuming host-hosted casual play is cheat-proof.
3. **Economy:** wallet/ledger and inventory idempotency; duplicate purchase/refund/promo/daily claims; concurrency and overspend; host-progression abuse must not grant tradable or money-backed benefits.
4. **Core Unity gameplay:** deterministic dice/turn order, trade/auction/rent/bankruptcy/development, bot actions, temporary bot and reconnection/host changes, event-sequence handling, snapshot convergence, unclean shutdown recovery. Check complete game scenarios with 2/3/4 human seats later.
5. **Operational code QA:** TypeScript lint/build, `node scripts/qa.mjs --check`, then controlled `node scripts/qa.mjs --full` when **no other emulators are running**; Unity compile, metadata, actual Editor/Guest play tests and crash logs. Capture logs/errors. The owner DID execute fresh local `node scripts\qa.mjs --full` with passing SEC01/02/03 results on Windows, per the 2026-10-13 transcript; Unity Editor/Release and deployed stages remain unverified.

**NEXT AFTER AUDIT:** feature selection and implementation under Section 16. **DEFERRED:** Phase 17C Steamworks publisher/real multi-PC work under Sections 7 and 14, until owner resumes. No need to obtain Steamworks AppID to start current source/core audit.

Do not deploy, update GitHub, force cloud App Check enforcement or provision services without explicit owner permission. Deliver only complete actual replacement source files when a code fix is approved; no new Tools/patch helpers.

# 11. Current checkpoint summary

## Green / accepted locally

- Phase 16A–H.4 local QA and runtime reliability baselines, subject to crash regression watch.
- Phase 16I paginated public discovery and local emulator capacity/read/mixed load, including 32 concurrent match pairs and a 75 ms staggering A/B diagnostic.
- Phase 17A Unity Steam provider initialization and Phase 11B static check 8/8 using AppID 480.
- Phase 17B.2: Windows Development Guest local emulator identity, private lobby, QA-only SteamAPI Init AppID 480 and Steam invite Overlay activation.
- Phase 17B.2: event-level Steam QA component diagnostics with its own invite payload redacted, and clean process exit; inherited lobby creation logger still exposes room code in the session log and must be hardened before public telemetry.

## Yellow / current watch and active work

- **ACTIVE NOW: Security & Core Audit**; Security Core 01/02/03 local emulator fixes and negative tests accepted, but broader auth/abuse, Unity core gameplay and server-verified progression remain OPEN.
- Actual Steam platform join/invite and multi-PC network acceptance are deferred to Phase 17C.
- Historical `BEX64 / 0xC0000409` native Windows Guest crash remains a regression watch; native Steam was re-enabled only on explicit QA user click in opt-in Development Guest.
- Private-lobby room codes appear in local DEVELOPMENT diagnostics. Owner explicitly accepts temporary local logging. Risk: LOW in private short-lived logs, conditional on access, expiry and server-side join controls; reclassify if logs are publicly uploaded or persistent telemetry captures live join codes. No urgent code change requested; verify no production/public log forwarding.
- Emulator burst-tail latency and measured local workload cannot be extrapolated into a production capacity claim.
- Phase 17C real AppID / physical-PC / shared staging requirements are **deferred by owner** until Security & Core Audit and feature work are finished; preserve these release gates. The current owner local Full QA outcome is PASS only for emulators, not production.

## Deferred production gates

- Real AtlasBoard Steam AppID and Steamworks partner setup; published identity/invites/achievements.
- Publisher Web API secret, real Valve Steam-ticket attestation and returning Steam sign-in.
- Correct Firebase App ID and real custom Steam App Check token mint/enforcement/cryptographic rejection.
- Production/staging separation, IAM, logging/alerts, TTL and billing/quota governance.
- Real physical 2/3/4-player gameplay, reconnect, host loss and region/performance measurements.
- Release compilation, startup/shutdown security regressions and confirmation QA-only native bypasses are excluded.
- Release owner approval.

# 12. Next-chat handoff block

Future AtlasBoard chats should start from these facts:

- Canonical roadmap checkpoint: **v5.17, 2026-10-12 America/Denver / 2026-10-13 UTC Windows QA log**, owner-local document to be committed by owner; treat v5.16.1 and earlier status paragraphs as historical when superseded by Section 23.
- Repository: `vuartex/AtlasBoardPrototype`, Unity baseline 6000.5.6f1, scene `Assets/Board_Prototype.unity`.
- Last **already pushed** `main` at latest read: `bed153c2071716e1d7216fe0e3e8ecdb36f3f341`, `Accept Phase 16I local QA and Phase 17B Steam invite overlay validation`. Do not fabricate a future commit SHA.
- Phase 16I and Phase 17B.2 accepted source ARE pushed in bed153c2; all newer Security Core 01/02/03 fixes and tests and this roadmap remain owner-local until the user commits/pushes. Inspect `git status` and exact source before replacing anything; never assume a push already happened.
- Phase 16I cursor pagination and local emulator mixed load PASS; 32 concurrent matches observed with no workflow failures in the accepted v2/A-B tests. Local result is not certified production capacity.
- Phase 17A Editor: Steam initialized with AppID 480 and Phase 11B 8/8. Not real publisher verification.
- Phase 17B.2 Guest on local emulators: opt-in `-atlasSteamOverlayQa` enables a separate QA invite control; `SteamAPI.Init`, `ActivateGameOverlayInviteDialogConnectString` and Overlay activation callback were observed PASS after adding local `steam_appid.txt=480` beside the EXE.
- User-provided 2026-10-11 20:07 America/Denver Guest log demonstrated `STEAM_INIT_OK`, `INVITE_API_RETURNED`, `OVERLAY_ACTIVATED`, `SESSION_CLEAN_EXIT droppedMessages=0` with no credential/ticket exposure.
- Screenshot: Steam opened invite UI with Spacewar branding. The real Steam friend invite/accept/join is NOT tested or accepted. Do not claim the actual real AppID alone automatically resolves all invitation behavior.
- Keep `.local` tokens, app publisher secret and raw tickets out of source/logs; QA component itself redacts invite codes, but an existing lobby creation diagnostic still logs the raw room code. The owner explicitly accepts these temporary codes in local development logs; never publish live-room logs or ship them to remote telemetry without sanitization.
- Production Steam custom App Check and returning sign-in gates remain blocked until real AppID/Valve/deployed proof.
- Historical native Guest crash must remain on regression watch; explicit opt-in Steam Native Init is confined to the development-only test path.
- Owner-approved next work: **Security & Core Audit (Core 01/02/03 locally accepted, Core 04 next) -> pre-release features -> Phase 17C real physical Steam clients / official AppID / approved staging -> final release audit and GO/NO-GO**. Do not connect remote clients to localhost emulator.
- Existing Phase 16I/17B files are accepted and pushed at bed153c2; never overwrite with earlier source or unapproved QA helpers.
- Git discipline: stage only desired paths, no `git add .`, user performs commit/push. Never auto-push/deploy.
- Deliver only complete affected source files when genuinely necessary. For roadmap maintenance deliver only the full `MASTER_ROADMAP.md`; no new `Tools` scripts, patches or installers. Review unrelated font/generated binary changes before future staging.

# 13. Roadmap maintenance rule

At every meaningful checkpoint:

1. Record the latest already-existing pushed SHA/title; never invent a SHA for a commit that does not yet exist.
2. Preserve accepted local work until it is safely pushed.
3. Update the current blocker and acceptance evidence.
4. Separate local/emulator acceptance from production acceptance.
5. Record QA-only bypasses and the release gate proving they cannot leak into Release.
6. Remove obsolete future-scope wording when work is completed.
7. Keep `Assets/Project/Documentation/MASTER_ROADMAP.md` as the canonical roadmap.
8. Keep player-facing ATB error codes stable once published/documented.

# 14. Phase 17C — Real Steam / cross-device test design (OPEN, no deployment)

**Status: FORMALLY DEFERRED BY OWNER until completion of Security & Core Audit, pre-release feature development, and availability of an official Steamworks AppID.** Phase 17B.2 proves that Steamworks API initialized and the Spacewar AppID 480 overlay opened on one Windows Development Guest. It does NOT prove delivery to a friend, approval of an official Steam AppID, cross-PC network reachability or Firebase security in a deployed build.

## 14.1 Prerequisites and architecture decisions

1. Verify whether a real AtlasBoard Steamworks AppID, partner permissions, build branches, Steam publisher Web API secret and Steam-account licensing are available. Keep actual values out of public roadmap and logs. AppID 480 is never production proof.
2. Inventory test devices and accounts: two, then three and four separate authenticated humans using physically distinct clients (and at least two independent networks when feasible). Same-PC Guest Editor/emulator combinations remain regression tests, not external proof.
3. Design dedicated Firebase staging (separate project from `atlasboard-usa`) with explicitly approved owner/IAM/billing quotas/alerts, client config, secrets, App Check registration, deployed Functions/Firestore indexes and rollback procedures. Do not deploy or modify account settings during documentation.
4. Preserve canonical Atlas AccountId and server-side lobby/match persistence. Steam is identity/invite/presence/achievement adapter, not automatically AtlasBoard's dedicated matchmaking/game server. Do not introduce a self-managed VPS/dedicated server merely to finish Phase 17.
5. Confirm the peer/host-authoritative topology and non-loopback Firebase cloud endpoints for every test machine; `127.0.0.1` always resolves to the local machine. Investigate NAT/relay needs separately only if selected transport actually requires them.
6. Decide Steam-only PC launch versus eventual Android/Google Play. Android may use Firebase with a platform-appropriate App Check provider. Cross-platform multiplayer is **OPTIONAL / UNDECIDED**; separate platform queues are acceptable. No premature commitment to cross-play or paid hosting.

## 14.2 Real-user acceptance scenarios

- Steam start / quit / restart, account creation, returning sign-in, identity stability, invite overlay, friend selected, invite accepted/rejected/expired, private/public lobby joining, wrong code/password, revoked access, full/closed/stale lobby.
- Full 2/3/4-human match from lobby ready through authoritative turns, movement, development/trade/auction, settlement, chat, endgame, progression, rematch; verify deterministic seat ownership and same visible state after each move.
- Host closes app, network loss, disconnect/reconnect, temporary-bot handoff, host election/migration, duplicate/stale intent replay, conflicting revisions and idempotent finalization; no duplicate wallet or progression rewards.
- Multiple US/EU networks, cold vs warm runs, last-mile Wi-Fi/mobile hotspot variation, latency and errors, successful/failed requests, bandwidth and costs; report environment and test sample size, not invented global capacity.
- Collect redacted results with software version, test setup, successful/failed assertions and repeatability; define explicit pass/fail criteria before promoting the release target.

**Gate:** any real cloud test requires explicit approved staging target. No production App Check bypass, fake Valve verification, development SteamID trust, or insecure port forwarding to private emulators.

# 15. Mandatory final whole-project security, code and QA audit — PRE-RELEASE GATE

**Status: SECURITY & CORE PASS 1 ACTIVE; SECURITY CORE 01/02/03 LOCAL QA ACCEPTED (owner transcript, 2026-10-13 UTC), FULL PROJECT / RELEASE AUDIT STILL OPEN.** This phase began before feature work and before official Steam AppID issuance. Pushed baseline GitHub `bed153c2` had an inventory of **21 backend TS files, 55 exported callable endpoints** and deny-by-default Firestore rules. The owner subsequently ran the newer local combined suite: 14 progression controls, SEC01 host-result negative test, SEC02 19 economy controls, 11 inventory, 13 promo and SEC03 15 reward controls, with full emulator QA PASS. Selected Unity match/lobby/platform files were inspected, not all Unity assets or gameplay scripts. **A standalone independent gameplay-result verifier, deployed App Check negative proof, full project penetration/dependency audit, Unity Release build and real Steam/physical multiplayer tests have not been performed.** Repeat release-critical checks **after feature freeze** and again on the exact release candidate.

## 15.1 Read-only source-review observations (2026-10-12)

| Ref | Severity / state | Observed evidence | Required proof or disposition |
| --- | --- | --- | --- |
| SEC-01 | **P0 RELEASE BLOCKER** | `Backend/Firebase/functions/src/security/app-check-policy.ts` currently defines `ATLAS_ENFORCE_APP_CHECK` as `isFunctionsEmulator && localEnforcementRequested`. Thus outside emulator protected callables are configured **without enforced App Check**. This is an accepted development staging choice, not production readiness. | Implement explicit owner-approved staging/production policy that fails closed without blocking the legitimate Steam token bootstrap; test valid, missing, forged, revoked and expired tokens against deployed staging. Include emergency rollback. Do not flip an emulator-only flag and assume deploy safety. |
| SEC-02 | **P0 RELEASE BLOCKER** | Steam AppID 480/Spacewar QA does not prove Valve publisher-ticket authenticity, real Steamworks invite delivery, canonical returning sign-in or correct Firebase App Check App ID. Staging / real AppID and publisher-secret setup are pending. | Real-AppID server-side Valve validation, identity mismatch/replay tests, approved secret management, correct App Check app registration and end-to-end Steam account mapping; never trust client-supplied SteamID alone. |
| SEC-03 | **P1 / anti-cheat threat model** | `src/match/network.ts:publishMatchNetworkState` restricts publication to the authenticated current host and uses revision/sequence/seat constraints, but accepts a bounded `snapshotJson` supplied by the host rather than independently simulating all rules server-side. `src/progression/progression.ts` explicitly excludes host telemetry from future ranked authority until backend event-sourcing. | Keep game sessions casual/friend-authoritative at launch unless business/design accepts host-cheat risk. Before ranked, leaderboards with valuable stakes, tradable rewards or competitive payouts, add server-verifiable actions/outcomes and abuse tests. Review normal progression/reward integrity as well. |
| SEC-04 | **P1 / availability & billing review** | `platformSteamCreateAppCheckToken` is intentionally unauthenticated and App Check-exempt to bootstrap Steam proof; current code binds a publisher secret and verifies with Valve, and uses `maxInstances:20`. In this focused read, no dedicated edge pre-auth IP/risk limiter was confirmed. | Threat-model flooding, rate/quota exhaustion and Valve API cost; configure gateway/platform anti-abuse, monitor budgets and enforce safe timeouts without breaking legitimate initial sign-in. Do not call it a proven exploit without testing. |
| SEC-05 | **P1 / rules & privacy review** | `firestore.rules` is deny-by-default for private authority collections; authenticated clients may query `public_profiles`, while `preferences` validates top-level map types but not nested shapes/size limits. | Check profile enumeration policy, data minimization, nested preference schema/limits and security-rules abuse/fuzz tests on staging. Treat this as an audit item, not established privilege escalation. |
| SEC-06 | **P2 / local log privacy accepted** | Local Development Guest lobby creation diagnostic includes a six-digit temporary join code. Host-close/expiry, authenticated joins and invalid-attempt controls limit risk. Owner explicitly permits this in local logs. | No urgent change. Prevent public/player support telemetry from forwarding usable live lobby codes, tokens, private IDs or detailed backend responses; revisit severity if logs become accessible while a room remains open. |
| SEC-07 | **P1 / Release isolation gate** | QA-only native Steam init is guarded by `DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR`, emulator loopback and opt-in `-atlasSteamOverlayQa`. Native Firebase emulator Guest isolation has separate compile/argument gates. Historical `0xC0000409` startup crash remains on regression watch. | Compile a genuine Release; confirm test flags do not activate QA paths, local fake App Check never authenticates remotely, native Firebase/Steam initialization and clean exit work repeatedly. |
| SEC-08 | **P2 / source hygiene** | Commit bed153c2 includes one-off `Tools/Phase17B/SteamGuestPreflight.ps1`, five specialized Phase 16I stress harnesses and modified font assets. This is developer-source footprint, not automatically added to Unity player binary. | Do not add more Tools/patch helpers; reuse existing canonical QA diagnostics. Review font diffs for accidental changes. Archive/consolidate one-off scripts only after preserving accepted QA coverage and only with owner approval. |
| SEC-09 | **P1 / pagination abuse** | `src/lobby/public-lobby-pagination.ts` bounds query chunks and scan budget, validates heartbeat and signs pagination cursors. Runtime public calls are authenticated. | Add invalid/tampered/expired cursor tests, high-churn pagination, abusive authenticated fetch rates, query-index and database-read billing review in staging. |
| SEC-10 | **P1 / Dev-only callable exposure audit** | `index.ts` and Steam platform expose development mutation/identity helpers that use emulator-only guards; Firestore rules deny authority writes from ordinary clients. | Enumerate every deployed callable, verify dev helpers fail closed outside emulator, inspect `onCall` auth/rate limit/roles and object-level authorization, and run negative authorization/abuse suites against safe staging. |
| SEC-11 | **P1 CONFIRMED — interim mitigation LOCAL QA PASS, Release BLOCKED pending real verifier** | Local hosted negative E2E demonstrated that a legitimate host can publish fabricated `match_complete` at revision 0 with zero turns. `progressionRecordCompletedMatch` then accepted the frame and minted 260 XP, a win, `first_win` and a progression marker. The evidence is from isolated Firebase emulators only (2026-10-12). | v5.14 fail-closed gate candidate: zero-turn completion and post-completion host rewrites rejected; `progressionRecordCompletedMatch` writes no permanent XP/wins/achievements without a digest-matched, privileged `match_result_verifications/<matchId>` receipt. All unverified results remain pending; no real independent verifier exists. Integrated 14/14 progression + host negative tests and full local QA PASSED (owner 2026-10-13 receipt). Implement/review a trustworthy server-side event/result verifier before enabling normal permanent rewards. No claim of full anti-cheat closure until replay validation, race/TOCTOU analysis and actual real-world tests. |
| SEC-12 | **P2 / automated quality gates** | Backend `scripts/qa.mjs` provides offline/full suites, and owner newer-local fresh `--full` transcript PASSED on 2026-10-13 UTC; branch CI enforcement and a pushed-checkpoint CI run were NOT verified. | Run and capture current lint/build, `qa.mjs --check/--full`, Unity Editor compilation / build, and verify CI is required for protected branches before release. No claim of CI absence until scope is explicitly verified. |

**Review snapshot:** initial triage plus accepted owner-local Security Core 01/02/03 negative emulator suites is complete enough to prioritize remaining audit work but **not** to close the whole audit. Host-published results affecting progression (SEC-03/SEC-11) are the first code/gameplay integrity investigation. SEC-01 and SEC-02 remain unavoidable production gates requiring future Steam/App Check infrastructure. No completed whole-project audit certificate exists.

## 15.2 Mandatory code and application audit matrix

Every section below needs either an executed automated test, a documented manual device test, or a reviewed written risk acceptance with evidence. **Current Audit Pass 1 is underway BEFORE new features**; after features are complete, repeat the critical security/core suites on the exact build to be published.

- **Identity/session:** Auth account registration and recovery, provider mapping, Steam ticket authenticity/replay, spoofed UID/SteamID, expired credentials, session invalidation, deleted/banned users, account switching, linked-account conflicts.
- **Firebase backend:** enumerate all exported callables and indexes, Firestore Rules least privilege, server checks independent from client-side UI, transaction concurrency, App Check positive/negative, raw HTTP and unauthorized endpoints, rate limits, input validation, pagination cursor integrity and SSRF/secret boundaries.
- **Money/store/entitlements:** duplicate purchases/refunds/claims, concurrency/retries, signed ledger, negative balances, promo redemption abuse, reward replay, season rollover, unfinished match rewards, economic inflation/exploit loops and real-money/IAP compliance if introduced.
- **Online gameplay:** client/host snapshot manipulation, command idempotency, turn order, dice and trade/auction logic, bankruptcy, property prices and payouts, disconnect/reconnect/host loss, state-hash comparison and recovery after app termination, 2/3/4 participants.
- **Chat/moderation/privacy:** flood and malicious payloads, profanity/report/block flow, permissions, input rendering, history retention/TTL, public profiles, data deletion/export, privacy policy, user reporting and escalation.
- **Steam/PC:** official AppID, overlay/invite accept+reject, achievements 14 mappings, Steam restart/offline behavior, startup flags, Windows builds/signing, Steamworks callbacks and SDK versions, no issuer secrets or ticket dumps in logs.
- **Android/Google Play if approved:** Android identity & App Check/Play Integrity provider, SDK/API level, permissions, CPU/GPU/device memory, phone/tablet aspect ratios, touchscreen, store policies, data privacy and purchase receipt validation, platform-isolated/cross-play tests per decision. NOT an automatic Steam release prerequisite if Android scope is deferred.
- **Unity runtime, UI and content:** scene references, null/lifecycle races, asset/meta GUID integrity, memory/GC leaks, performance/frame stalls, device resolutions, input system and controller, pause/back navigation, UI safe areas, camera, city information overlay input, localization for 7 languages, accessibility, audio, tutorial and game rule clarity.
- **Quality/observability:** TypeScript lint/build, current `qa.mjs --check/--full` with test receipts, Unity edit/playmode tests and actual Windows Release build, fuzz/negative tests, crash reproduction, CI workflow and dependency/license/SBOM review; never substitute source regex audits for runtime tests.
- **Operations/release:** separate staging/production projects, least-privilege IAM, managed secrets and rotation, billing alarms, Firestore TTL indexes, retention/backups, alerting, incident and rollback runbook, CDN/assets patching, Steam beta branch, marketing/age/privacy/store legal checks and explicit owner GO/NO-GO decision.

## 15.3 Audit completion evidence template

For each SEC issue: record owner, category, reproduction steps, code path, severity, mitigation, regression test, device/backend configuration, evidence link, remaining risk, and close date. Block production on outstanding P0s, unapproved P1s, an unverified Release build or any failed critical multi-player/identity/economy test. No self-issued "secure" certificate.

# 16. Pre-release features and refinements — OWNER-APPROVED NEXT PHASE AFTER AUDIT

Owner explicitly requested these potential additions be remembered **before** release. Feature planning and development follow the current Security & Core Audit and precede resuming Phase 17C. Presence of a related file does NOT mean UX/reliability acceptance; discuss MVP/optional scope first, and retain the ability to freeze features for final security regression.

## 16.1 Existing features needing final polish / verification

| Work item | Current grounded state | Proposed pre-release acceptance | Priority |
| --- | --- | --- | --- |
| City information on tile/city click | EXISTING `AtlasBoardCityInformationPanel.cs` and tablet/world-input guard; not a missing feature. | Verify city/property ownership, rent/property prices, improvements and rule data match selected map; correct tap/click, scrolling, safe area and seven-language labels without leaking clicks through tablet/modal. | P1 UX |
| Network quality HUD | EXISTING `AtlasBoardNetworkTelemetry.cs` + `AtlasBoardNetworkQualityHud.cs`; current widget is inside PauseWindow and computes smoothed callable RTT, RTT variation and an application-request failure/late proxy. | Consider small nonintrusive corner indicators for ping/jitter/failure quality during matches, localized tooltip and show/hide option. **Do not claim measured UDP packet loss** from HTTPS callable failures; label explicitly as request failure/quality unless real packet transport metrics exist. | P1 UX if owner accepts |
| Startup/error experience | Existing loading tips (7 languages), ATB codes and diagnostics accepted in Phase 16H. | Revalidate cold start, return login, retry/offline, language switching and stable nontechnical player messages on production-like paths. | P0 regression |
| Lobby/public browser & private invitation | Phase 16I local paging and Phase 17B Spacewar UI smoke accepted. | Private/public join/rejoin, invite deep link, expected failures/host-close, pagination under churn, clear player feedback, Steam overlay with official AppID. | P0 regression |
| 2–4-player turn-based gameplay | Core gameplay/host-authoritative transport accepted locally. | Real human sessions and full-game finalization with deterministic state/reward checks; bots/temporary bots, turn timeout, trading/auction/bankruptcy and rematch. | P0 regression |
| Achievements/career/store | Existing 14 Steam achievement mappings and Firebase profile, inventory, seasonal/economy systems. | Real Steam AppID API names, unlock parity, cosmetic entitlement sync, wallet abuse/duplicate award protection, offline/read-only fallback, game completion receipts. | P0/P1 |

## 16.2 Proposed launch-quality features requiring priority decision

- **P1 candidate:** concise in-game new-player tutorial / first-turn hints, controls/settings discoverability, clear error-recovery actions and friend/lobby join guidance.
- **P1 candidate:** game balance and bot difficulty/decision quality; playtest edge cases on available maps, human-vs-bot experience and minimum viable content completeness.
- **P1 candidate:** accessible UI (colorblind patterns beyond color only, keyboard/controller options, text legibility, low-end GPU settings) across supported Windows aspect ratios; verify existing partial accessibility before rebuilding anything.
- **P1 candidate:** clearer network indicators, reconnect countdown/status, host-change notifications and consistent localized fallback when service is unreachable.
- **P1 candidate:** privacy-safe opt-in diagnostic upload / support workflow with removal of credentials, room codes and user identifiers before sharing externally. Local Development logs may retain temporary lobby codes per owner's explicit decision.
- **P2 optional:** friend/party list; Quick Play/automatic matchmaking; party invites; casual/ranked split **only after security design**; skill rating and leaderboards; spectator/replay; quick-chat/emotes; tournament / alternate rule sets; additional content, cosmetics and missions.
- **P2 optional:** Android/Google Play build and device UX, with Android-specific provider and store verification. Cross-platform Windows/Android gameplay is **undecided**, and separate populations are valid. Firebase is preferred managed backend; no self-managed always-on server by default.
- **P3 future:** voice chat only with moderation capacity, Steam Workshop/custom maps only after content moderation/IP review, seasonal live-ops dashboards and richer analytics.

## 16.3 Feature intake and release scope lock

New ideas should enter this Section 16 with user intent, planned UX, dependencies, MVP vs optional label, priority, cost/hosting impact, security/privacy concerns, platform matrix and acceptance criteria. Do not implement without owner approval. Establish a **feature freeze** before the full audit of Section 15; retest modified areas and rerun final release gates afterward.

# 17. Final Release readiness / GO-NO-GO — AFTER AUDIT, FEATURES, PHASE 17C

A Release candidate requires all of the following: confirmed official Steam AppID/publisher ownership; separated approved staging; deployed Steam Auth/App Check proofs; P0 security closure and approved P1 risks; physical 2/3/4-player sessions; complete progression/economy, negative input and reconnect/host-loss testing; production-like latency/billing/alerts; real Windows Release packaging and crash-free repeated launches; seven-language/accessibility and legal/store acceptance; final source+dependency audit receipts; signed-off rollback plan; owner authorization for build and deployment.

**Current state: NO-GO / NOT READY for Steam public launch.** Phase 16I local backend PASS and Phase 17B.2 AppID 480 overlay PASS are retained without exaggeration. Nothing in this document deploys, commits, exposes secrets, runs tests, or authorizes a publishing operation.

# 18. Security & Core Audit — Batch A execution register (2026-10-12)

**Current audit type:** source inspection of pushed GitHub `bed153c2071716e1d7216fe0e3e8ecdb36f3f341` combined with newer owner-local full Firebase Emulator test receipts on Windows through Security Core 03 (2026-10-13 UTC). The earlier host-forged-XP exploit was confirmed locally and the interim deny gate was regression tested. **Not** a full real-game verifier, deployed security proof or independent certification. The owner has authorized audit/planning and local roadmap delivery, NOT automatic push or deploy.

| Batch | Priority | Audit unit / files | Current evidence and action | Status |
| --- | --- | --- | --- | --- |
| A-01 | P0 release | `src/security/app-check-policy.ts`, Firebase entry exports / callable policy | Production-enforcement currently false outside emulator; explicit future staging policy and positive+negative credential tests required; real Steam AppID dependency documented. | **OPEN / blocker before release** |
| A-02 | P1 CONFIRMED | Match publication / progression / Unity | Initial exploit: zero-turn fabricated host match minted 260 XP, win and `first_win`. Updated owner-local test: negative attack rejected, XP/wins/achievement remain zero; ordinary progression 14/14 PASS with local privileged receipt fixture. | **INTERIM MITIGATION LOCAL QA PASS; independent real-match verifier still BLOCKER** |
| A-03 | P1 | `src/economy/wallet.ts`, `inventory.ts`, `commerce.ts`, `promo.ts`, `seasonal.ts` | SEC02 19/19 concurrent purchase/refund tests PASS; inventory 11/11, promo 13/13, SEC03 15/15 rewards abuse tests PASS. Disabled seasonal track tier now rejected through guarded callable. | **LOCAL EMULATOR ACCEPTED; live provider/season rollover not verified** |
| A-04 | P1 NEXT | Firestore Rules, callable entrypoints, Steam platform and `src/security/callable-abuse.ts` | Firestore Rules baseline 17/17 PASS; core auth abuse 6/6 PASS, but exhaustive object-level authorization, rate/DoS and privacy coverage of all callables remains incomplete. | **SECURITY CORE 04 NEXT** |
| A-05 | P1 NEXT | `src/lobby/lobby.ts`, pagination, chat and moderation | Public discovery E2E incl. tampered cursor and concurrent reads PASS; chat/moderation 49 controls PASS. Broader lobby abuse/flood, repeated cursor fetch cost and authenticated enumeration limits remain open. | **PARTIALLY LOCAL ACCEPTED; ABUSE/QUOTA REVIEW OPEN** |
| A-06 | P1 NOW | Unity gameplay/turn, bots, trades, auctions, development, bankruptcy, reconnect | Audit state-machine and Unity lifecycle; validate move/dice/turn/game-end invariants and full-game 2–4-player scenario outcomes (physical part deferred to 17C). | **OPEN / Unity QA PENDING** |
| A-07 | P1 NOW | Windows Development vs Release, Steam QA isolation, logging/crashes | Development Steam QA branch is compile/flag-gated; owner permits local six-digit lobby codes in logs. Genuine Release compile/start-stop/hardening remains open. | **SOURCE CHECKED / RELEASE PENDING** |
| A-08 | P1 NOW | Dependencies/Unity package licenses/assets/GUIDs/configuration | Require dependency advisories, Unity package version compatibility, build outputs, asset GUID validity, crash and memory checks. | **OPEN** |
| A-09 | P1 after features | Final backend+Unity+Steam+economy/security re-audit | Re-run all impacted suites and validate feature-frozen exact release binary and staging. | **DEFERRED UNTIL FEATURE FREEZE** |
| A-10 | P0 after AppID | Real Valve ticket, deployed App Check token and negative enforcement, publisher permissions | No real Steamworks AppID currently; do not fake proof or enable production enforcement prematurely. | **BLOCKED BY OFFICIAL APPID/STAGING** |

**Accepted code-pattern observations, not exploit claims:** 21 TS backend files and 55 `onCall` endpoints enumerated; direct Firestore authority writes are deny-by-default; wallet/inventory use hashed idempotency keys and transactions; join-code lookup uses server-side HMAC; pagination uses signed cursors and bounded scans; match host publication uses monotonic revision/sequence checks; progression reads a host-authored completion frame. These are starting points for negative QA, not full audit results.

**Local QA receipt collection order (manual Windows terminal, not an added project tool):**

```powershell
cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"
node scripts\qa.mjs --check
```

When this completes, ensure no emulator suite is already running, then run `node scripts\qa.mjs --full` from the same directory. Capture the full output; this suite manages its own local emulator configuration/cleanup and must not be run alongside an already-running emulator. If an interrupted QA left `.qa-env-backup.local`, use the existing `node scripts\qa.mjs --restore-env` recovery command **only after inspecting whether the run was interrupted**. No new test scripts or `Tools` directory are needed. Later run controlled source-focused tests for SEC-11 without inventing production credentials.

**Owner decisions captured:**

- No official Steamworks AppID today. Do not ask for it during Batch A or pause unrelated security/core work.
- Do not discard previously accepted local test evidence; distinguish emulator proof from deployed security.
- Local temporary room codes may remain in local development logs; do not publicly upload active-room logs or real credentials.
- No new one-off Tools/patch/helper files. Deliver only the exact complete source files changed by an approved fix, or a single complete roadmap document for roadmap changes.
- Core audit first; features second; Phase 17C third; final audit and publication last. **Preserve this order across future chats.**
- No automatic GitHub commit/push, no production Firebase deployments, and no local files overwritten in the owner's computer by this document.

# 19. SECURITY-CORE-01 — Host result integrity (v5.14 / HISTORICAL DELIVERY; acceptance in Section 23)

**Evidence:** 2026-10-12 local Firebase emulator run `progression-phase10-e2e.mjs --host-integrity-audit` returned `SECURITY_FINDING_CONFIRMED`: authenticated host created a false completion without turns; backend persisted `XP=260`, `wins=1`, `first_win=true`; test-only records cleaned. This was an actual demonstrated local exploit, not a source-only prediction. Prior standard progression `14/14 PASS`, backend `qa.mjs --full PASS`; these tests do not prove resistance to a malicious host.

**Historical package delivery snapshot (this was superseded by the owner LOCAL QA acceptance in Section 23):**

- `Backend/Firebase/functions/src/entry.ts`: exports two explicitly guarded callables **before** the wildcard export; unchanged callable names and Phase 16I pagination remain exported. This is the Firebase deployed entrypoint.
- `Backend/Firebase/functions/src/security/match-integrity-gates.ts`: rejects obvious zero-turn `match_complete`, blocks host publications after complete/proof; crucially gates permanent progression and achievements behind an **independent server-verification receipt** with exact network revision, sequence and SHA-256 digest. Unverified match result returns `ok=true`, `applied=false`, `affectedAccountCount=0`, `verificationStatus=pending_authoritative_verification` so current Unity completion flow can finish without retry storms; zero XP/wins/achievements are persisted. This is a **temporary fail-closed feature limitation**, not complete anti-cheat.
- `Backend/Firebase/functions/scripts/progression-phase10-e2e.mjs`: full replacement with a single `--security-suite` mode combining local self-test, the 14 positive tests (fixture proof created with local Admin SDK **only**) and negative tests for zero-turn completion, no-receipt fail-closed, spoofed receipt digest, direct-client forbidden receipt write and post-completion rewrite. Test-only Admin SDK fixture must never be promoted into normal deployed code.

**Critical limitations and future engineering (Release blocking):**

1. There is no real AtlasBoard match replay verifier or server-trusted gameplay event stream yet. NO legitimate live result can receive the proof automatically; permanent match-based XP/wins/achievements therefore remain pending on this candidate. This is an explicitly disclosed temporary safety lock, not a feature acceptance. Game results are still shown by Unity's local host simulation.
2. The basic minimum revision/sequence check only stops obvious early completions. A modified host can fabricate intermediate stages; do not call this proof of fair gameplay. The mandatory trust boundary is the **privileged receipt required before permanent reward writes**. Game-state validation must be independent from host snapshots.
3. The privileged verifier must generate its receipt only after actual server-side event/rule verification; atomically bind/freeze the final revision to prevent read-to-write races, receipt reuse, replay, collusion or post-proof overwrite. `matchHostPublishState` still delegates an earlier source transaction with host publication; the new wrapper's read-before-publish guard alone is not an atomic finalization lock. Include concurrent finalize/publish adversarial tests before full security sign-off.
4. The Unity UI currently displays an ordinary completion, while pending reward response has zero affected accounts. Before public beta, explicitly display 'Rewards pending verification' and design how a later trusted award is delivered. Never silently show awarded XP if not persisted.
5. Revisit any existing registered Unity progress/career statistics, matchmaking, rematch, leaderboards, economy or Steam achievements that consume these fields. No monetized/ranked progression until the independent verifier, complete negative tests, app-check production gate, real Steam AppID and staging work all pass.
6. Re-run `node functions/scripts/qa.mjs --full` after installing the candidate; the new `--security-suite` is additive and does not replace accepted QA. Static compile, lint, local emulator end-to-end tests and latest Unity Development/Release smoke are mandatory before marking PASS.
7. Observed Firebase CLI message about installed `firebase-functions` version: do a controlled npm dependency/update+lockfile audit separately; **do not auto-run `npm install --save firebase-functions@latest` in this security fix**.

**Acceptance required:** `ATLASBOARD_SECURITY_CORE_01_INTEGRATED_PASS (emulator only)` plus `AtlasBoard QA FULL LOCAL PASS`; real independent verifier remains OPEN, so SEC-11 **cannot be marked fully CLOSED** or approved for Release based on this interim lock.

**Local acceptance update (2026-10-12, v5.15):** Owner-provided full `node scripts/qa.mjs --full` transcript PASSED. It proved 14 positive progression checks, negative forged-zero-turn rejection, no XP/win/first_win, denied direct result-receipt writes, denied mismatched receipt and post-completion rewrite; `ATLASBOARD_SECURITY_CORE_01_INTEGRATED_PASS` and `AtlasBoard QA FULL LOCAL PASS` recorded. The v2 `qa.mjs` compiled gate prevents testing stale Functions `lib` artifacts. **State is temporary mitigation ACCEPTED LOCALLY, not final game-integrity verifier acceptance.** Steamworks Phase17C remains deferred by owner's instruction.

**Git discipline:** GitHub `main` still `bed153c2` at delivery inspection. v5.14 and the guarded source/test are **local only**; no commit, push, production deploy, or existing local user files were changed remotely. Owner must inspect `git status` before replacing a newer local roadmap or staging any files. The diagnostic/tools cleanup remains separate.

# 20. SECURITY-CORE-02 — Concurrent economy and transaction integrity (v5.15; acceptance in Section 23)

**Status (updated 2026-10-13 UTC):** **LOCAL ACCEPTED — 19/19 SEC02 checks plus combined Full QA PASS.** Production attestation still OPEN; Section 23 records exact evidence.

**Baseline and trust boundary:** Existing wallet and entitlement mutations use Firestore transactions with deterministic SHA-256 ledger/event IDs. Commerce reads prices from the server-owned `item_catalog`, validates currency and ownership, charges or refunds in a single transaction, and refuses production execution of the emulator-only mutation callables. The prior 15H test checks canonical catalog price, basic replay, conflict, owner-only refund, inactive item and one refund. These guarantees still require adversarial negative testing, particularly simultaneous requests. This source review alone is NOT proof that the new concurrency tests pass.

**Delivered without introducing new Tools, standalone test entrypoints or game-code changes:** One full replacement file at `Backend/Firebase/functions/scripts/economy-commerce-security-e2e.mjs` reuses the existing `qa.mjs --full` integration registration and runs an expanded sequence that checks: emulator-only preflight, authenticated identities and local Admin SDK fixtures, Firestore direct-client denial for catalog/wallet/inventory/commerce, caller-supplied price and target-UID spoofing, atomic entitlement grant and ledger, purchase idempotency and changed-payload conflict, owner-only refunds, refund ledger and entitlement revocation, distinct-key double refunds, invalid/inactive/zero-price catalog, missing Auth, insufficient funds, parallel same-key purchases, parallel different-key purchases of the same item, parallel same-key refunds and multiple alternate refund keys. Test cleanup deletes only fresh local fixture accounts and collections.

**Execution (from `Backend/Firebase/functions`, after extracting ZIP at the repository root and with emulators closed):**

`node scripts/qa.mjs --full`

The existing QA compiles TypeScript, confirms guarded callable exports, runs all prior backend suites, then invokes `economy-commerce-security-e2e.mjs` in the same fresh emulator session. Do NOT run standalone emulators or disable App Check / security rules to make failures pass. **Owner log now proves `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks` and terminal Full Local QA PASS; SEC02 is locally accepted.** Future concrete failed invariants still require a canonical full-source correction and retest.

**Post-PASS audit continuation:** Verify the public/deployed API surface has no unintended wallet mint/refund operations; review seasonal/promo reward issuance, battle-pass inventory entitlement authority and provider receipts; separately test wallet overflow and seasonal contention. Internal `walletTestMutation`, `inventoryTestMutation`, `commerceTestPurchase`, `commerceTestRefund` and `promoTestRedeem` remain LOCAL-ONLY by design and do not establish live payment validation. Production app-check, real Steam AppID, physical multiplayer and Unity release-build QA remain UNVERIFIED and are release blockers.

**Safety and repository discipline:** Existing guarded Core01 files stay in the owner's local project. This batch changes one existing E2E source file and this full roadmap; no owner filesystem, GitHub commit, deployment, production credentials or emulators have been modified or invoked by the package author. The temporary Core01 reward gate is still not a complete server-authoritative match verifier.


# 21. SECURITY-CORE-03 — Promo, daily reward, seasonal tier and inventory abuse (v5.16; acceptance in Section 23)

**SEC02 closure evidence (2026-10-13):** Owner supplied full `qa.mjs --full` transcript. It contains `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks` and `AtlasBoard QA FULL LOCAL PASS`; single-charge under 3 same-key concurrent purchases, single entitlement under 3 distinct keys, exact-once parallel refund, Firestore deny and atomic ledgers passed. The SEC01 negative fixture also passed. This is an emulator-only acceptance, not deployed provider-payment attestation.

**SEC03 source-review finding (P1, guarded-callable negative test now PASSED locally):** `economy/seasonal.ts` function `claimSeasonalTrackTier` checked existence, XP requirements, reward type/amount and idempotency but did NOT check `tier.active` either preflight or inside the granting Firestore transaction. An administrator-disabled track tier could therefore be invoked directly by a modified authenticated client if its XP threshold was reached. The earlier unguarded legacy branch was a source-reviewed potential exploit, not independently proven as a pre-fix emulator attack. The newly exported guarded callable was directly tested: disabled tier denial **PASS** on the owner local emulator, with the concurrent enabled tier claim also PASS.

**Interim fix delivered:** Add `Backend/Firebase/functions/src/security/seasonal-track-integrity.ts` as a complete new, deployable source file that implements the existing `metaSeasonalClaimTrackTier` callable API with fail-closed `tier.active===true` check both before the mutation and **inside** its atomic Firestore transaction, checking event active window, reward definition, XP threshold, ledger idempotency and wallet overflow. Replace `Backend/Firebase/functions/src/entry.ts` to explicitly export this guarded callable alongside accepted SEC01 host/progression gates, preserving the legacy wildcard exported names and existing Unity API contract. The original economy seasonal claim function remains in `src/economy/seasonal.ts` as a legacy implementation; a follow-up consolidation should remove duplicate logic **after capturing and reconciling the current newer local source**, not by overwriting it from older GitHub main. Do not call the legacy function directly from new exports.

**Added integrated regression:** `Backend/Firebase/functions/scripts/security-core03-rewards-e2e.mjs` exercises 15 local-only checks: emulator preflight, test users, forbidden Firestore direct writes, parallel daily claims, per-account daily isolation, two-account global promo race, exact promo replay, alternative-key promo limits, duplicate currency targets, inactive-tier seed, disabled-tier denial with sufficient XP, locked-tier denial, 3 concurrent active-tier claims with exact-once credit, immutable ledger and cross-account balance isolation. The test cleans its temporary user/doc/ledger fixtures and restores the preexisting local seasonal runtime, even on failure. A failed case stops QA and is not mislabeled as a pass.

**QA orchestration:** `Backend/Firebase/functions/scripts/qa.mjs` full replacement includes the existing `inventory-entitlements-e2e.mjs` and `promo-event-tickets-e2e.mjs` regression scripts plus SEC03 test in the same single compiled-code `--full` run, and asserts the new compiled guarded callable is actually present before starting emulators. `qa.mjs --self-test` must succeed. All scripts run against `atlasboard-usa` LOCAL emulators only. No Firebase deploy/Steamworks/Unity build or actual provider receipt verification.

**Owner verification procedure (now PASSED as recorded in Section 23):** After extraction at repo root, with emulators stopped, run only `cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"` followed by `node scripts\qa.mjs --full`. The expected SEC03 marker is `ATLASBOARD_SECURITY_CORE_03_REWARDS_PASS: 15/15 checks`, then `AtlasBoard QA FULL LOCAL PASS`. Do not mark SEC03 accepted until the full Windows/Emulator log proves the result. If test fails, fix the precise source/test cause and rerun from this one full QA entrypoint, never weaken Firestore Rules or bypass production checks.

**Release blockers unchanged:** Independent server-verified gameplay results and permanent XP grant, production App Check with real Steam AppID, real Steam join/invite staging, Firebase deployment controls, Unity Editor/Release build and physical multiplayer E2E remain incomplete. GitHub commit/push and production deployments require explicit authorization.


# 22. SECURITY-CORE-03 QA Fixture Compatibility — v5.16.1 (historical issue; fixed and accepted in Section 23)

**Owner test evidence:** The `node scripts/qa.mjs --full` log (2026-10-13) passed the offline preflight, Firebase Functions TypeScript compile/ESLint, moderation, Auth/Steam local checks, and SEC02 `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks`. QA then stopped at `scripts/inventory-entitlements-e2e.mjs` with Firestore HTTP 403 while attempting to create `item_catalog/{id}` using unsigned client-side REST. This was a **legacy test fixture error**, not an entitlement or rules vulnerability. Firestore security rules MUST remain deny-by-default. Because the test runner uses `&&`, neither the legacy promo test nor the new SEC03 negative suite executed in that attempt. At that historical failure point SEC03 was still pending; this exact QA fixture issue has now been **resolved and owner-retested successfully** in the later transcript recorded in Section 23.

**One-pass remediation delivered (complete canonical source replacements):**

- `Backend/Firebase/functions/scripts/inventory-entitlements-e2e.mjs`: replace the old REST fixture seeding and cleanup with **Admin SDK strictly bound to verified loopback Auth/Firestore/Functions emulators**, preserving all 11 grant/replay/duplicate/revoke/regrant/unknown-item/audit/final-ownership assertions, adding direct-client catalog-write denial. Fixture identities are temporary and cleanup uses the locally authenticated Admin SDK; rejects unapproved project/environment before importing Admin SDK.
- `Backend/Firebase/functions/scripts/promo-event-tickets-e2e.mjs`: replace the same legacy REST seeding/cleanup pattern for promo and item catalog with the identical emulator-only Admin safety preflight. Preserve 13 stages for multi-currency+ticket+inventory grants, immutable ledgers, per-account/global limits, exact replay, expired/disabled/future rejection, no partial grants, cleanup; add direct-client promo-write denial. Do not weaken production/client Security Rules.
- `Backend/Firebase/functions/scripts/qa.mjs`: keep existing compiled SEC01/SEC03 export gates and **same full regression order**, now invoke both existing fixture offline `--self-test` modes plus SEC03 `--self-test` before launching emulators, so trivial fixture regressions are caught earlier. Full QA continues to start/stops emulators itself and restores private env.

**Offline checks originally performed before owner runtime retest; now superseded by accepted Section 23:** JavaScript `node --check`, the 3 offline `--self-test` checks, QA ordering checks, and refusing execution when no approved emulator is present were validated by the package author. The actual owner Windows Firebase Emulator/Firestore transaction run has NOT yet been rerun. No new server application logic, Firestore Rules, Steamworks AppID, deployment or GitHub state changes are included. On owner machine install extracted full files at repository root, stop standalone emulators, then run exactly `cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"` followed by `node scripts\qa.mjs --full`. Do not run standalone inventory/promo test scripts or parallel emulators. Require inventory 11/11, promo 13/13, SEC03 15/15, SEC01 and SEC02 regressions, then terminal `AtlasBoard QA FULL LOCAL PASS` before accepting Security Core 03. On next success continue the audit without requesting a separate approval.

# 23. SECURITY CORE 01–03 — Owner Windows full emulator acceptance / v5.17 (2026-10-13 UTC)

**Canonical acceptance evidence:** Owner-provided PowerShell transcript `Pasted text(20261013-002449).txt`, executed from `C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase\functions` using **only** `node scripts\qa.mjs --full`. The owner log records a fresh local Functions/Firestore/Auth emulator run with rebuilt TypeScript and a second, independent local App Check emulator run. QA restored `.env.local` from its temporary backup and removed temporary emulator-only secret material. Final marker: **`AtlasBoard QA FULL LOCAL PASS (emulators only). Deployed Steam/App Check, live TTL, Unity build and physical multiplayer remain unverified.`** No production deployment or live-network validation was performed.

## 23.1 Detailed accepted result ledger

| Workstream | Exact owner log evidence | Local result | Not established by this test |
| --- | --- | --- | --- |
| TypeScript + QA build | `> build` / `> tsc`, focused ESLint and full offline preflight | **PASS** | Unity C# compile, Release build, deployed Firebase compile |
| Security Core 01: match results + progression | `Phase 10 Progression + Achievements E2E PASS: 14 checks`; `HOST_INTEGRITY_SECURITY_AUDIT_PASS`; `ATLASBOARD_SECURITY_CORE_01_INTEGRATED_PASS (emulator only)` | **PASS, interim mitigation verified** | Real independently verified full-match completion and permanent legitimate XP awards without test-only trusted receipt fixture |
| Security Core 02: commerce/wallet/refunds | `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks` | **PASS 19/19** | Deployed economy security, payment provider receipts/real-money compliance |
| Restored entitlement tests | `AtlasBoard Inventory + Entitlements Local E2E v2 PASSED: 11/11` | **PASS 11/11** | Live cloud Firestore, large-scale load |
| Restored promo test | `AtlasBoard Promo Codes + Event Tickets Local E2E v2 PASSED: 13/13` | **PASS 13/13** | Production coupon campaign fraud/billing controls |
| Security Core 03: rewards | `ATLASBOARD_SECURITY_CORE_03_REWARDS_PASS: 15/15 checks` | **PASS 15/15** | All conceivable time-of-check/time-of-use and event-rollover races; deployed billing |
| Firestore Rules | `Phase 15A result: 17 passed, 0 failed` | **PASS 17/17** | Exhaustive field/fuzz tests and staging Rules parity |
| Public lobby / pagination | `AtlasBoard Phase 4A + Phase 16I Public Lobby Discovery E2E PASSED` | **PASS local** | Internet-scale pagination costs, sustained remote traffic |
| Chat & moderation | `Phase 15I combined chat/moderation E2E PASS: 49 checks` | **PASS 49 checks** | Live service traffic and end-user operational moderation |
| Steam local account link and return | `Phase 11C ... PASS: 10 checks`, `Phase 11D ... PASS: 10 checks` | **PASS local** | Real Steam publisher authentication / actual friend invite acceptance |
| App Check emulator negative | `Phase 15F local enforcement E2E PASS. StrictChecks=3/3` | **PASS only for emulator-observable boundaries** | Cryptographic invalid-token rejection: Functions Emulator considered a non-empty intentionally invalid header `VALID`. Deployed staging remains mandatory |

**Correctness detail — Core 01:** An earlier emulator attack demonstrated a host could invent a completed match and obtain 260 XP, one win and `first_win`. The newer guarded callable correctly rejected zero-turn completion and retained `XP=0, wins=0, first_win=false` in its negative run. Ordinary 14-case progression passed **only with a synthetic server verification receipt seeded through the local trusted test fixture**. No independent verifier currently generates such receipts from actual turns in a live game. Therefore **do not re-enable permanent XP/wins/achievements for unverified real match results**, and do not mark SEC-03/SEC-11 anti-cheat fully closed.

**Correctness detail — Core 02:** Three simultaneous same-key purchases debit once; three different-key purchases of the same entitlement grant once; three concurrent refunds do not mint duplicate credits; changed item under an existing idempotency key and cross-account refund are rejected. Direct authenticated client writes to server-owned wallet/catalog/commerce/inventory are denied. Owner log contains local cleanup `PASS`.

**Correctness detail — Core 03:** Three concurrent daily claims grant only 250 Gold to the claimant for the same UTC day; another user claims independently. Promo global/account caps and same/alternate-key replay blocks passed. **`active=false` seasonal track tier is now denied even with sufficient XP**; an enabled but locked tier is denied; after enabling the test fixture, three concurrent tier claims award the Gold prize only once, and evidence ledger remains singular. No wallet credit leaked cross-account. Local cleanup passed and the `seasonal_runtime/active` state was restored.

**Fixture compatibility resolution:** The earlier `403 PERMISSION_DENIED` arose when legacy inventory/promo test scripts tried to seed authoritative Firestore records as ordinary clients. Those scripts were fixed to use Admin SDK strictly inside verified local emulators while preserving client-write denial tests; Firestore Rules were not weakened. Their 11/11 and 13/13 owner-local passes verify the earlier test-only blocker is resolved.

**Source package inventory accepted in owner's local working tree (not a claim of push):**

- `Backend/Firebase/functions/src/entry.ts`: accepted SEC01+SEC03 guarded callable exports, preserving existing endpoint names.
- `Backend/Firebase/functions/src/security/match-integrity-gates.ts`: zero-turn result rejection and trusted verification-receipt gate on persistent progression.
- `Backend/Firebase/functions/src/security/seasonal-track-integrity.ts`: disabled/XP-locked track denial, in-transaction checks, idempotency and overflow protection.
- `Backend/Firebase/functions/scripts/progression-phase10-e2e.mjs`: integrated 14 progression cases + host attack negative controls.
- `Backend/Firebase/functions/scripts/economy-commerce-security-e2e.mjs`: SEC02 owner-race and refund tests.
- `Backend/Firebase/functions/scripts/inventory-entitlements-e2e.mjs`: emulator-safe Admin fixture and client denial, 11/11.
- `Backend/Firebase/functions/scripts/promo-event-tickets-e2e.mjs`: emulator-safe Admin fixture and client denial, 13/13.
- `Backend/Firebase/functions/scripts/security-core03-rewards-e2e.mjs`: 15 local reward/concurrency/negative checks.
- `Backend/Firebase/functions/scripts/qa.mjs`: one-pass, compile-before-emulators orchestrator, self-tests and all three security-core suites.
- `Assets/Project/Documentation/MASTER_ROADMAP.md`: this full v5.17 document.

**Important local-versus-pushed split:** Rechecked GitHub `main` during this roadmap preparation: **`bed153c2071716e1d7216fe0e3e8ecdb36f3f341`**. Newer owner-local security changes and this roadmap must be **staged, reviewed and explicitly pushed by the owner**. The assistant has not pushed any code, deployed anything, or modified the user's Windows files. Preserve accepted newer local source over older GitHub main. Do not stage generated `lib`, logs, `.env.local`, credentials, ZIPs, unexpected fonts or unrelated files. Do not use `git add .`.

## 23.2 Remaining severity and release gates after local acceptance

| Gate | Current state | Mandatory action |
| --- | --- | --- |
| **P0: production App Check** | **BLOCKED** | Official Steam AppID, publisher ticket provider, proper Firebase app registration and deployed invalid/missing/replay token proofs; no emulator-only token substitution |
| **P0: real Steam trust / Phase 17C** | **DEFERRED BY OWNER** | Real app ownership, Valve ticket signature+identity, invite/accept/join, returning sign-in, staging and physical PC validation |
| **P1: server-verified progression** | **OPEN — interim denial PASS** | Design server-verifiable turn/event authority, robust game-result proof and end-to-end valid-match reward unlock. Until then unverified permanent XP remains suspended |
| **P1: callable object authorization / abuse costs** | **SECURITY CORE 04 NEXT** | Enumerate current actual exports; test cross-account and anonymous invocation, owner vs guest/host, query/cursor flood, bootstrap abuse, rate limits, privacy, token replay, Firestore Rules |
| **P1: Unity gameplay integrity and native QA** | **OPEN** | Unity Editor C# compile, dice/rules/trade/auction/bankruptcy/bot/full-match outcomes, crash/reconnect/host restore, actual Windows Development and Release binaries |
| **P1: operational/production governance** | **OPEN** | Staging/prod project separation, managed secrets, billing/rate quotas, TTL/retention, IAM, dependency/license checks, rollback and final candidate audit |

# 24. Next-chat handoff — Security Core 04, feature sequencing and owner GitHub push

**First source of truth:** The owner will attach this exact canonical `Assets/Project/Documentation/MASTER_ROADMAP.md` v5.17 in the next chat and perform his own GitHub `main` push. Treat actual local files and newly verified pushed SHA as source authority; the roadmap captures intent/evidence but is not proof that a push happened. The user has specifically instructed: **do not ask for approval between audit phases; when tests PASS proceed to the next audit; when a reproducible security defect is found, prepare the corrected complete canonical source files and integrated regression together in one package.** The owner runs the required Windows emulator tests and sends their logs. No assistant-side auto-push or Firebase deploy.

**What is actually done:** Core 01 host result interim integrity negative PASS + 14/14 standard progression; Core 02 economy race 19/19; Core 03 rewards 15/15; inventory 11/11; promo 13/13; baseline Rules 17/17 and full backend QA local PASS. An offline preflight warning notes EditorBuildSettings starts with another scene: open `Assets/Board_Prototype.unity` in Editor before building the Development Guest; no Build Settings changes were made by backend QA. No Unity Editor compile / physical multiplayer / Release test was established by the backend command. Local Functions emulator also accepts invalid non-empty App Check tokens, so deployed cryptographic proof remains **UNVERIFIED**.

**Exact current source inventory for comparison:** Firebase root `Backend/Firebase`, Functions entrypoint `src/entry.ts`, original `src/index.ts`, guarded sources `src/security/match-integrity-gates.ts` and `src/security/seasonal-track-integrity.ts`, tests under `Backend/Firebase/functions/scripts/`, canonical `qa.mjs` (`--check`, `--full`, `--self-test`). Unity 6000.5.6f1, scene `Assets/Board_Prototype.unity`. Preserve existing city-information UI and network-quality HUD: they already exist; don't duplicate them. Preferred architecture: Firebase managed backend, no user-run always-on server, Steam PC first; Android/Google Play and crossplay optional and not decided.

**Next audit task — SECURITY CORE 04:** Verify source and actual callable exports; review all account/UID/object checks and Firestore rules for lobby/private room, pagination, auth/rate limits, mutable profiles/preferences, chat/moderation claims and Steam bootstrap exception. Write a risk matrix and prioritize a reproducible test case. Reuse/extend existing `scripts/qa.mjs --full` and existing canonical E2E tests; new permanent E2E test is acceptable only when justified as integrated coverage (no `Tools`/patch scripts). Use local emulator-only Auth/Firestore/Functions verification. Examine simultaneous abusive calls, invalid token, non-member, cross-account reads, stale signed pagination cursors, cursor tampering, quota and pre-auth rate-limiter exposure. Distinguish evidence from hypothetical risks. If no defect, record PASS and progress without redundant code.

**Critical independent track — trusted real-game reward verifier:** Do not try to bypass SEC01 by accepting a client-supplied hash, timer, minimum turns or untrusted host signature. Evaluate event log binding, authoritative turn validation, server-generated random dice / deterministic replay, participant identity and state transitions, anti-replay, crash recovery, resource/cost tradeoffs. Without a real trust source, do not award permanent XP to unverified finished games. Design first, implement complete source+tests in logical production-safe units, and never mislabel a mock receipt as a production verifier.

**Owner workflow:** user handles extracting full-file ZIPs into `C:\Users\crazy\AtlasBoardPrototype`, reviews source, stages only intended paths, commits and pushes himself. Assistant must give descriptive English GitHub commit summary and multiline description when requested, then update whole canonical roadmap, no overwriting newer local code, no automatic push, no deployment. Validate backend with `cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"` followed by `node scripts\qa.mjs --full` while standalone emulators are stopped. Only claim a test PASS after owner log confirms the exact marker.

**Next workflow order:** SECURITY CORE 04 and remaining core audit; then mutually prioritized pre-release UI/gameplay features, including city-information and network-quality improvements only where not already implemented; then Phase 17C official Steamworks and approved staging; then final frozen-Release audit and owner GO/NO-GO. Continue the security work immediately after identifying this checkpoint; do not stall for repeated permission/approval questions when work is already within the owner's approved scope.

# 25. SECURITY CORE 04 — Source audit and discovery-abuse candidate / v5.18 (NOT ACCEPTED)

**New verified GitHub main:** `8e1cc9da2187d5af7f5bbbaa9dece68ae7aedb21` is exactly one commit ahead of `bed153c2`. GitHub compare lists ten changed/new paths for Security Core 01–03 and roadmap v5.17. This section supersedes earlier handoff notes that describe those files as unpushed. All historical details and Security Core 01–03 owner-emulator acceptance in Sections 1–24 remain preserved. Any Security Core 04 files below are an assistant-prepared, **uncommitted ZIP candidate**, not a pushed or Windows-validated result. The source snapshot is GitHub main; owner's later independent Windows edits must be reviewed before overwriting them.

## 25.1 Review of actual current source and prior suites

- `src/entry.ts` is the Firebase deployed entrypoint (`package.json` main `lib/entry.js`); guarded `matchHostPublishState`, `progressionRecordCompletedMatch` and seasonal claim exports override unchanged legacy names. No reason to revert these accepted gates.
- `src/index.ts`: lobby create and join invoke `assertAccountRateLimit`, with shared create scope of six/minute and join scope of twenty/minute. Steam link has an account limiter. Two public browser read endpoints, legacy `lobbyListPublicRooms` and Phase 16I `lobbyListPublicRoomsPage`, did not invoke the shared account limiter before discovery queries. This is an observed missing control and a credible cost-amplification threat, **not** a demonstrated cloud bill, availability incident or exploit proof.
- `src/lobby/public-lobby-pagination.ts`: HMAC-signed filter-bound cursor, max 25 result cards, bounded 250 scanned documents/request and bounded stale-record cleanup. It has neither expiry nor caller UID binding. A legitimate signed cursor can be replayed, although this alone does not bypass private-room membership or reveal confidential room codes. Repeated authenticated queries amplify reads; requested scale review remains open. The wrapper additionally coerced a non-string cursor to `undefined`, making malformed input an unvalidated first-page request.
- `src/lobby/lobby.ts`: legacy public discovery still scans discovery documents and may clean stale room state. `getLobbySnapshot` checks authenticated lobby membership after reading state; actual match network `readMemberContext` uses host/seat identity, so stranger match snapshots are rejected by source logic. Broader adversarial match/lifecycle invocation still requires E2E.
- `firestore.rules`: private user docs `get` and prefs `get` require owner UID; direct authority collections fall through to deny-by-default; authenticated `public_profiles` allow `get/list` for all signed-in users. Public profile enumeration is currently **permitted by policy**, and is a privacy product decision, not a proven privilege-escalation exploit. Preference nested maps are type-checked but not deeply schema/size-checked. Before release define permissible fields and bounds while respecting Unity's serialized settings contract.
- `src/platform/app-check-steam.ts`: unauthenticated, App Check-exempt bootstrap correctly requires a Valve-verified Steam ticket before token minting. It has input length bounds, upstream 10-second timeout and maximum instances; no dedicated edge pre-auth rate limiter was observed. `platformSteamReturningSignIn` also verifies a ticket but has no account-derived rate limiter before external verification. Do not fabricate Steam cryptographic tests on AppID 480. Edge threat prevention, Valve-request quotas and platform App Check enforcement are **open release risks**.
- Already integrated local QA: `callable-authorization-abuse-e2e.mjs` covers 6/6 anonymous/nonmember/nonhost and lobby-create/Steam-link cases; `public-lobby-discovery-e2e.mjs` already covers signed cursor tamper, scanned page boundaries, concurrent first pages, privacy of discovery cards and public lifecycle; `security-rules-e2e.mjs` covers 17/17 Rules cases. The new candidate tests only incremental cases and does not reimplement those suites.

## 25.2 Candidate correction — narrowly scoped browser read budget

**Status: SOURCE CANDIDATE / OFFLINE JAVASCRIPT CHECKS PASS / FIREBASE EMULATOR E2E NOT RUN / NOT SECURITY CORE 04 PASS.** Deliver full canonical replacements only:

1. `Backend/Firebase/functions/src/entry.ts`: explicit legacy `lobbyListPublicRooms` guarded override while retaining Security Core 01/03 protected exports and page exports.
2. `Backend/Firebase/functions/src/security/legacy-public-discovery-gate.ts`: new real deployed callable implementation retaining the original `{ok, rooms}` contract, requiring verified `request.auth.uid` and applying the shared `lobby_discovery` 60 calls / 60-second account budget before expensive list reads.
3. `Backend/Firebase/functions/src/lobby/public-lobby-pagination-callable.ts`: same shared per-account discovery limiter for cursor endpoint, retaining existing response shape and App Check policy; reject wrong-type cursor rather than treating it as first-page request.
4. `Backend/Firebase/functions/scripts/security-core04-auth-privacy-abuse-e2e.mjs`: incremental emulator-only test for missing Auth, invalid cursor type, unsigned cursor, normal old/new contract, alternating endpoints/UID spoofing, 60-budget concurrent admission, exhausted account denied on both APIs, and other-account isolation. Uses loopback-only emulator setup and local fixture cleanup. The exact expected marker is `ATLASBOARD_SECURITY_CORE_04_DISCOVERY_PASS: 9/9 checks (emulator only).` **Marker expected, not observed.**
5. `Backend/Firebase/functions/scripts/qa.mjs`: preserve earlier suite ordering and add new Core 04 self-test, compiled export gate, focused ESLint, new E2E at the end of the first fresh emulator pass. The original `--full` command and App Check emulator second stage remain unchanged.
6. This complete updated `Assets/Project/Documentation/MASTER_ROADMAP.md`, preserving all earlier acceptance details.

**Risk/compatibility controls:** a read limiter writes one Firestore transaction per admitted request and one for each rejected request; thus it bounds query scan amplification per UID but does not replace Cloud Armor / account creation abuse prevention / budget alerts. Limit 60/min is a policy candidate; confirm expected client refresh frequency and 16I load scripts using multiple accounts before production. Production App Check is still disabled in code outside emulator and **must not be inferred secure** merely because a new limiter exists. Cursor format/signature and response contracts are deliberately unchanged. No GitHub mutation, deployment, Firebase setting adjustment, Steam credential or emulator session was performed to produce the candidate.

**Before local acceptance:** install this ZIP on top of the latest owner working files only after verifying no newer edits were made, stop all emulator instances and run from `Backend/Firebase/functions`:

`node scripts\qa.mjs --full`

The integrated run must complete TypeScript build, focused lint, all accepted Security Cores 01–03, existing 15A/15G/16I suites, and the new Core 04 test before terminal `AtlasBoard QA FULL LOCAL PASS`. Capture actual output and classify failures precisely. Even after 9/9 and full QA, **the broader Security Core 04 remains OPEN** until Steam bootstrap, account lifecycle, privacy policy and cost/load cases below are assessed. Do not claim deployed App Check proof or live quota protection.

## 25.3 Outstanding Security Core 04 audit and negative test matrix

| Priority | Threat | Existing evidence | Still required |
| --- | --- | --- | --- |
| P1 | Invalid, stolen, switched Firebase token / forged client UID | Existing Auth and owner/nonmember tests cover ordinary cross-user denial; backend uses callable Auth UID | Auth revocation, disabled account, UID data spoof across high-value methods, actual stolen valid token (bearer-token compromise threat) and reauthentication behavior. App Check cannot prevent possession of a stolen valid token on its own. |
| P1 | Lobby/member/object enumeration | Private/member guard and normalized public cards source reviewed; old 15G member denial accepted | Nonmember/removed/kicked lifecycle, host migration, unauthorized match snapshot/intents and high-volume nonexistent lobbyId probes with measured database read/cost under emulator/staging. |
| P1 | Cursor tamper/replay/DoS | Existing signed invalid-cursor 16I PASS, 250 scan bound; new typed-cursor/rate-limiter candidate | Real signed cursor reuse across UID and across time, churn, filter changes, key rotation and larger query-index cost; clarify that replaying public pagination is not equivalent to object authorization bypass. |
| P1 | Steam App Check token mint flood and returning sign-in | Valve verification present; App Check bootstrap exemption necessary | Trusted edge/WAF quota, Valve API call observability, timeouts and 429 response behavior, distributed unauthenticated abuse, staging invalid-token rejection and real publisher identity. No live Steam credentials during offline QA. |
| P1 | Public profile enumeration and user preferences | Rules owner-write and authenticated list, preferences only top-level maps checked | Set explicit public profile discovery policy, minimal fields, nested preference limits and client serialization contract; test Rules with real authenticated queries and oversized/nested values. |
| P1 | Firestore storage/billing rate-limit lifecycle | Rate-limit documents have numeric `expiresAtEpochMs`, not automatically Firestore TTL Timestamp | Define TTL retention/index policy and incident/budget alarms; model extra transaction writes under sustained spam and new-account exhaustion; platform perimeter required. |
| P0 release | Production Steam/App Check cryptography and genuine Release topology | Emulator App Check can treat malformed nonempty header as VALID; current release provider not proven | Deployed staging/real AppID, missing/wrong/revoked tokens, source branch gates, 2–4 device and Release tests after owner approval. |

## 25.4 Mandatory server-verifiable match results — P1 unresolved design

An untrusted host's final snapshot, chosen dice outcome, hash, self-signature or a sequence of invented turns cannot be trusted to grant permanent XP. A low-Firebase-cost serverless design must establish a new authoritative **accepted gameplay event chain** instead of signing host telemetry:

1. At match start commit server-owned canonical board/rules version, seat assignment and initial state, a unique match nonce and server-side random generation state. Do not let the host choose the canonical starting roll or future dice.
2. For each consequential move, accept an authenticated seat-intent with stable idempotency key, expected turn/revision, legal move and bounded payload. A deterministic, versioned backend reducer validates transitions, dice, property ownership, rent, development, auctions, trades, bankruptcy and bot decisions against a previously accepted authoritative state. Server draws and records randomness or supplies commit/reveal where needed, not client RNG.
3. Atomically append a server-validated ordered event and advance an authoritative turn revision (and minimal current-state checkpoint) in a Firestore transaction. Reject out-of-turn actors, duplicate/conflicting IDs, rule-invalid actions, sequence gaps, fake timestamps, reconnect/host migration stale epochs and event writes not authorized through the callable. Use one transaction per consequential turn/action, not per animation frame or heartbeat. Use bounded payload size, expiry/retention and abuse limits to manage costs.
4. Do not trust Host as referee for rewards. At completion, independently verify terminal rules and event-chain continuity and freeze the final authoritative revision; mint a single privileged `match_result_verifications/<matchId>` receipt transactionally bound to final revision/sequence/result digest. Server-owned reducer state is the trust source, not a digest sent by Host. `progressionRecordCompletedMatch` must enforce exact receipt/match binding and idempotent per-seat grants in an atomic transaction. Replayed receipt, forged intermediate turns, incomplete/aborted match, concurrent finalize and post-verification publish must fail.
5. Server verification work can be bounded by periodic validated checkpoints plus compact event journal, rather than full replay of unlimited JSON snapshots on every read. If reducer parity for all game modes is incomplete, leave the interim SEC01 reward gate closed and show truthful pending rewards in the Unity UI. Keep casual host-authoritative rendering but not permanent economic/progression rewards.
6. Integrate positive full 2/3/4-player valid-game, crash/reconnect/temporary-bot migration, conflicting trade/auction, seeded deterministic dice and malicious-host/collusion tests before awarding permanent experience/achievements. `match_result_verifications` Admin-fixture tests from SEC01 are **not** a replacement for this verifier.

**Cost stance:** fully managed Firebase/Cloud Functions/Firestore remains preferred; no continuously running dedicated server by default. Set an explicit per-game write/read budget and profile a real 2–4-player staging run before accepting the proposed reducer architecture. This is a design plan, not implemented production logic.

**Execution order remains immutable:** finish broader Security Core 04 and remaining security/core audits -> pre-release gameplay/UX feature freeze -> official Steamworks Phase 17C / real physical/staging tests -> final audit -> owner GO/NO-GO. No manual confirmation needed between authorized offline audit tasks; no auto-commit, push or deploy.


## 25.5 SEC04 lint correction after first owner Windows execution — v5.18.1, NOT ACCEPTED

**Owner test result:** The first `node scripts\qa.mjs --full` on Windows reached the new Security Core 04 focused ESLint gate after successfully completing the inherited 15K TypeScript build and existing static audits. The run stopped before any new emulator E2E because `src/entry.ts` had eight `import/export` duplicate-name lint errors induced by the new explicit overrides plus wildcard `export * from "./index"`; `src/lobby/public-lobby-pagination-callable.ts` had one missing `require-jsdoc` comment. **This was a candidate packaging/source lint failure; no Core 04 emulator or production PASS has been recorded.**

**v5.18.1 correction:** `src/entry.ts` replaces the wildcard with an explicitly enumerated, collision-free list of the complete 55 Firebase callable names, preserving the earlier Core 01 and Core 03 guarded entrypoints and adding the Core 04 legacy discovery guard. `src/lobby/public-lobby-pagination-callable.ts` adds JSDoc and retains the existing correct ordering in which malformed authenticated cursor requests consume the shared per-UID read budget; this preserves the existing test's four-call accounting before the exhaustion assertion. `scripts/qa.mjs` gains a read-only entrypoint contract gate comparing the 55 exported callable names against `src/index.ts` and the Phase 16I page export, preventing future dropped/duplicate callable exports. Accepted QA suite names, order, expected marker and production limits remain unchanged.

**Validation gate:** Offline source syntax/AST/export checks are necessary but not sufficient. Require actual owner Windows focused ESLint, compiled TypeScript and then integrated Firebase Auth/Firestore/Functions Emulator checks, including `ATLASBOARD_SECURITY_CORE_04_DISCOVERY_PASS: 9/9 checks (emulator only)` and `AtlasBoard QA FULL LOCAL PASS`. Both markers are **PENDING** for v5.18.1. Do not remove inherited Security Core 01–03 gates or bypass lint to get a green run. This correction must not be described as broader Auth/Privacy/Steam/bootstrap or real-game verifier closure.


## 25.6 SEC04 owner Windows full QA finding and deterministic fixture correction — v5.18.2

**Owner Windows evidence:** In `Pasted text(20261013-005450).txt`, the full QA run passed `tsc`, focused ESLint, 55/55 callable exports, legacy authorization 6/6, Security Core 01 host-integrity protections and progression 14/14, Security Core 02 19/19, Inventory 11/11, Promo 13/13, Security Core 03 15/15, Firestore Rules 17/17, Public Discovery/Phase16I pagination, and 49 chat/moderation checks. The new Security Core 04 suite then reported `ATLASBOARD_SECURITY_CORE_04_DISCOVERY_FAIL` because three simultaneous end-of-flood requests returned HTTP 200 instead of rate-limit rejection. The runner shut down emulators, restored `.env.local`, and removed its test-only secret. Neither `ATLASBOARD_SECURITY_CORE_04_DISCOVERY_PASS` nor terminal `AtlasBoard QA FULL LOCAL PASS` occurred on this run.

**Cause and assessment:** The previous Security Core 04 fixture assumed 56 extra requests issued in repeated sequential batches would all occur in one 60-second *fixed window*. Windows emulator responses under the load took ~2–6 seconds per batch, so a window rollover could legitimately reset the account counter; this produces an inaccurate abuse-test failure. The log does not prove a real backend bypass. Do not claim exploit remediation from modifying a fixture. The existing shared backend transactional limiter and quotas remain unchanged.

**v5.18.2 canonical source replacement:** `Backend/Firebase/functions/scripts/security-core04-auth-privacy-abuse-e2e.mjs` replaces the wall-clock-dependent bulk flood with an emulator-only Admin SDK fixture seeded after validating the atlasboard-usa project, localhost Auth/Firestore/Functions and the emulator hub. It seeds only disposable Alice's server-owned SHA256(scope:auth UID) fixed-window counter at 59/60, verifies the next authenticated call is accepted as 60/60, then submits three concurrent legacy/cursor discovery calls with a forged different account ID in request data; all three must be rejected and the blocked cooldown persisted. Bob's own authenticated discovery must remain unaffected. Assertions remain 9/9, all earlier tests and `qa.mjs` orchestration are preserved, and the fixture cleans its emulator-only records.

**Acceptance:** Pending new owner Windows `node scripts\\qa.mjs --full` with both `ATLASBOARD_SECURITY_CORE_04_DISCOVERY_PASS: 9/9 checks (emulator only)` and `AtlasBoard QA FULL LOCAL PASS`. The independent game-result verifier, broader object-level authorization/Firestore privacy tests, pre-auth Steam bootstrap limits, deployed App Check, legitimate Steamworks identity, production Firebase billing/capacity and Unity Release/physical multiplayer remain OPEN. Owner's Security Core 04 local source supersedes unchanged older GitHub main `8e1cc9da2187d5af7f5bbbaa9dece68ae7aedb21`. Do not commit, push, or deploy without explicit authorization.


## 25.7 SEC04 FULL LOCAL ACCEPTANCE — owner Windows 2026-10-13 UTC

**Evidence**: `Pasted text(20261013-010207).txt`, 1,288 lines, is the full owner Windows `node scripts\qa.mjs --full` transcript for Security Core 04 v5.18.2. The prior v5.18.0 lint issue and v5.18.1/5.18.2 flaky time-window test problem are historical and **RESOLVED in local QA**. The owner's newly submitted transcript includes a clean `tsc` build, focused ESLint, callable inventory 55/55, Unity metadata preflight, chat/moderation 49 checks, Phase 15G auth-abuse 6/6, economy 19/19, inventory 11/11, promo 13/13, Firestore Rules 17/17, Phase 4A/16I public lobby pagination, progression 14/14, host-result negative guard, Security Core 03 rewards 15/15, and **`ATLASBOARD_SECURITY_CORE_04_DISCOVERY_PASS: 9/9 checks (emulator only)`**. The separate App Check enforcement emulator suite reported **3/3 strict checks** and explicitly deferred malformed nonempty token cryptographic rejection. The transcript ends with **`AtlasBoard QA FULL LOCAL PASS (emulators only)`** (line 1287). The environment backup and test-only secret were restored/removed; emulators shut down. No GitHub push or Firebase deploy was executed.

**Accepted scope**: Shared authenticated public-lobby discovery limit of 60 requests per account per 60-second fixed window across both legacy and cursor-callable entrypoints, correctly typed cursor rejection, unauthenticated rejection, signed cursor and pagination compatibility, forged `request.data.uid` unable to change the authenticated UID, deterministic 59/60-to-block concurrency fixture, and previous accepted audit suites. This is **SEC04 LOCAL EMULATOR ACCEPTED**. It does not close production App Check, Steam publisher proof, anonymous pre-verification Steam API traffic, broad Firestore profile enumeration, nested preferences bounds, billing/TTL lifecycle, or independent verifiable match progression. Release GO/NO-GO remains NO-GO pending separate gates.

## 25.8 Security Core 05 — profile/Firestore privacy isolation, QA candidate v5.19

**Priority**: continue authorized Security & Core Audit after SEC04 local acceptance; no extra user approval checkpoint required. The base rules file is inspected against pushed GitHub `main` `8e1cc9da2187d5af7f5bbbaa9dece68ae7aedb21` and the Unity account/cloud preferences C# model. Preserve local newer source over this pushed baseline. No production rule change is bundled.

**Proven code-level policy**: `users/{uid}` canonical account get/create/limited locale update is owner-only; `preferences/{uid}` direct reads/writes owner-only; server-owned collections deny client writes/reads. `public_profiles/{uid}` explicitly permits **any authenticated user's GET and LIST** of public projections (display name, avatar, frame, update timestamp), and only owner write; this can enumerate account IDs and requires a product privacy/discovery decision. `validPreferences()` checks that `audio`, `graphics`, `controls`, `gameplay` are *maps* but does not bound nested keys, shapes or payload size. No deployed privacy/perimeter-cost closure may be claimed from the base rules. Firestore Rules 17/17 previous suite is accepted but does not exhaust these cases.

**Deliverable**: new full canonical `Backend/Firebase/functions/scripts/security-core05-profile-privacy-e2e.mjs` with 20 deterministic Auth/Firestore Emulator checks: canonical owner create/read; peer GET denial; signed-in private-users LIST denial; owner tier/UID escalation denial; own public profile create; peer public GET; anonymous GET denial; signed-in public profile LIST allowed (existing contract); peer write denial; extra sensitive projection field denial; own preferences create/read; peer and anonymous GET denial; signed-in preference LIST denial; peer write denial; direct abuse rate-limit state mutation denial; forged server-owned match verification receipt denial. Fixture uses two ephemeral Auth accounts and Admin SDK for **local-only cleanup after loopback Hub preflight**, never for positive client-write authority. It does not assert that public profile LIST is private or that arbitrary nested settings are constrained.

`Backend/Firebase/functions/scripts/qa.mjs` full canonical file appends this suite **after** the accepted Core 04 suite, adds it to offline fixture self-tests, ensures it is present as the final integrated suite, and leaves all previous security and App Check enforcement suites in place. `MASTER_ROADMAP.md` is preserved in full and expanded to v5.19. Prior QA runs remain valid receipts for SEC01–SEC04; this new code is **SEC05 QA CANDIDATE / NOT ACCEPTED** until owner produces a new complete Windows `node scripts\qa.mjs --full` log containing `ATLASBOARD_SECURITY_CORE_05_PROFILE_PRIVACY_PASS: 20/20 checks (emulator only)` and terminal `AtlasBoard QA FULL LOCAL PASS`. No P0 gates are promoted by this package.

**Following audit**: separately decide whether authenticated public profile collection listing is a permitted discovery feature and, if yes, apply explicit query/page limits and monitor enumeration cost; inspect and harden nested preference value sizes/types while preserving current Windows/iOS/Android/macOS/Linux settings sync; audit pre-auth Steam ticket verifier cost/rate limits, Firebase TTL retention/index policy, and cross-account lobby/match callable ownership. Keep independent gameplay match verifier as an unresolved P1. No approval necessary to continue audit, but no deployment/commit/push or real AppID staging without owner authorization.


## 25.9 Security Core 05 — owner Windows FULL LOCAL ACCEPTANCE, 2026-10-13 UTC

Evidence: `Pasted text(20261013-011205).txt` (1,315 lines) from `node scripts\qa.mjs --full`. It records the full 20 individual profile/privacy checks and `ATLASBOARD_SECURITY_CORE_05_PROFILE_PRIVACY_PASS: 20/20 checks (emulator only)` (lines 1177-1200), followed by 3/3 local App Check missing-header/exception checks and terminal `AtlasBoard QA FULL LOCAL PASS (emulators only)` (line 1314). No source/regression failure occurred. `users` and `preferences` owner isolation, public projection content allowlist, prevention of tier promotion, unauthorized user/profile/setting updates, and denial of client writes to rate-limit counters and verifier receipts are **SEC05 LOCALLY ACCEPTED**. `public_profiles` authenticated LIST is intentionally allowed by the current product contract and is an OPEN enumeration policy/cost decision; this acceptance is not an authorization for production deployment. The SDK's cryptographic rejection of malformed nonempty App Check tokens is NOT established in emulators. All prior Sec01–Sec04 gates remained green. Original private `.env.local` restored and test-only secret removed. No production changes or GitHub writes.

## 25.10 Security Core 06 — nested cloud Settings v2 schema, QA candidate v5.20

Priority: remove **unbounded nested client-supplied structures** from the directly writeable `preferences/{uid}` document while preserving current Windows/macOS/Linux/Android/iOS cross-device synchronization. Source inspection of `Assets/Project/Scripts/Online/Firebase/AtlasBoardCloudPreferencesSync.cs`, `AtlasBoardAccountModels.cs`, `AtlasBoardAccountService.cs`, `Assets/Project/Settings/AtlasBoardUserSettings.cs` and `AtlasBoardAudioSettings.cs` shows canonical audio fields `master/mainMusic/theme/dice/effects` (0–1 numeric) and `muted` (bool); 10 per-platform graphics fields (integer enums, resolution/refreshrate, bool flags); gameplay `global` (botTurnSpeed 1–100, UI flags) and four per-platform camera fields. `controls` has no shipping Atlas Board Settings model and is currently an empty map; expanding it requires an explicit migration. Root language supports seven Atlas Board locales; stored crossplay mode currently defaults to `enabled` and supports `disabled`.

**Security finding prior to this candidate:** Existing `validPreferences()` in `Backend/Firebase/firestore.rules` checked `audio`, `graphics`, `controls`, `gameplay` only with `is map`, without validating any nested child key, value type, or quantity. A signed-in owner could store arbitrary nested extra fields/payloads and risk unexpected storage/indexing/cost and future unsafe deserialization. These are user-owned preference fields rather than server economy authority; no privilege escalation is asserted without further proof.

**Candidate v5.20 full-file replacements**:

1. Full `Backend/Firebase/firestore.rules` adds server-evaluated nested whitelists for currently supported audio, 5 platform graphics maps, global+5 platform gameplay sections; optional fields remain compatible with empty legacy maps. It bounds integer enums and valid slider values, rejects nested arrays and unknown keys, requires current unused controls to remain empty, permits only recognized `enabled`/`disabled` crossplay values, and its positive fixture explicitly preserves all five shipping platforms (Windows/macOS/Linux/Android/iOS). All `users`, `public_profiles`, server-owned collection and deny-by-default rules remain structurally unchanged. **Do not deploy Rules until local emulator pass and product review.** Caution: previously created preferences containing unknown platforms, future controls, or out-of-range legacy settings may require an explicit migration; do not silently discard live data.
2. Full `Backend/Firebase/functions/scripts/security-core06-settings-schema-e2e.mjs`: 28 local-only explicit tests covering empty legacy settings, valid multi-platform audio/graphics/gameplay writes, persistence, malformed nested arrays/strings/bools/oversized unknown payloads, out-of-range numeric/enums, unrecognized keys/platforms, forbidden controls, peer isolation, rejected-write immutability, and later valid owner update. Temporary emulator Auth/data cleaned after fixed `atlasboard-usa` project and all loopback emulator endpoints are verified.
3. Full `Backend/Firebase/functions/scripts/qa.mjs` appends Core 06 after accepted Core 05 to the integrated `node scripts\qa.mjs --full` sequence and offline self-tests, retaining all previously accepted Security Cores 01–05, legacy Rules 17/17 and App Check suite.
4. This full `MASTER_ROADMAP.md` preserves all historical content and records Core 05 accepted, Core 06 candidate.

**Acceptance gate:** Candidate is **SEC06 NOT ACCEPTED** until owner Windows `node scripts\qa.mjs --full` produces `ATLASBOARD_SECURITY_CORE_06_SETTINGS_SCHEMA_PASS: 28/28 checks (emulator only)` and `AtlasBoard QA FULL LOCAL PASS`. Do not bypass failing tests. No production Rules publish, GitHub push/commit, real Steam AppID use or server deployment. Continue next with public-profile enumeration product decision, anonymous Steam ticket preverification budget, Game Result independent server verifier, App Check staging proof, Firebase TTL cost and Unity physical multiplayer. No GO/NO-GO release promotion from this package.


### Security Core 06.1 — expression-budget repair after owner Windows regression (2026-10-13)

- **Evidence:** The owner ran `node scripts\qa.mjs --full` on Windows with v5.20. TypeScript, ESLint, offline checks, callables and earlier initial suites passed. The original 17-case Firestore Security Rules E2E failed `owner can write cloud preferences`, expected HTTP 200, got HTTP 403. The returned error explicitly stated `maximum of 1000 expressions to evaluate has been reached` at the `preferences` Rules create/update line. QA stopped cleanly with exit 1; **Security Core 06 28/28 was not reached**. The prior 2026-10-13 owner log proved Core 05 20/20 and FULL LOCAL QA on the old rules; do not describe the v5.20 run as successful.
- **Root cause:** Each optional nested graphics/gameplay field was evaluated for each of five platform records **even when that platform was absent** (`section.get(platform, {})`); repeated optional field helpers spent Firebase's documented **1,000 expressions per request**. This rejected legitimate empty-section cloud preferences. It was an authorization availability regression caused by the new Rules, not a demonstrated cross-account data breach.
- **Repair:** `firestore.rules` v5.20.1 short-circuits validation for missing platforms, uses `section.get(field, typed-default)` to accept absent optional fields without repeated map-presence guards, and retains strict `keys().hasOnly`, numeric bounds, boolean types, `controls` empty until formally migrated, and global crossplay modes. No new Firestore permissions have been granted. Current Windows, macOS, Linux, Android and iOS settings remain within the contract.
- **Regression guard:** `security-core06-settings-schema-e2e.mjs` now fails explicitly on rule expression-budget errors even when the HTTP response is a denial, preventing false-positive negative tests. Added a static offline guard against reintroducing absent-platform full validation. The already-registered `qa.mjs` still runs Core 01–06 and the existing 17/17 Rules suite before new 28/28 suite; no earlier tests removed.
- **Acceptance PENDING** a **fresh** owner Windows log showing original `Phase 15A result: 17 passed, 0 failed`, `ATLASBOARD_SECURITY_CORE_06_SETTINGS_SCHEMA_PASS: 28/28 checks (emulator only).`, terminal `AtlasBoard QA FULL LOCAL PASS (emulators only).` No emulator test was run by the assistant in this environment; offline syntax/self-tests and package integrity are distinct and do not establish emulator PASS. If full multi-platform write still reaches 1,000 expressions, refactor again rather than weakening validation or changing tests to accept the error.
- **Release gates still open:** Authenticated public profile LIST enumeration policy, live App Check cryptographic bad-token rejection, actual Steam verification/AppID and independently verified real gameplay outcomes/rewards, physical multiplayer/Unity build, and live quota/TTL/cost monitoring. No commit/push/deploy without owner's specific instruction.


### Security Core 06.2 — changed-platform-only schema validation (2026-10-13)

- **Observed owner result for v5.20.1:** Previous Firestore Rules 17/17, Core 04 9/9 and Core 05 20/20 PASSED. Security Core 06 passed its first 2/28 checks but rejected the third legitimate rich Windows+Android/all-platform preferences update with HTTP 403: `maximum of 1000 expressions to evaluate has been reached`. The suite failed and full QA was FAILED, not accepted. The second iteration's missing-platform short-circuit was still not sufficient.
- **Root design change:** The existing Unity `AtlasBoardCloudPreferencesSync` already fetches the latest remote preferences, merges all existing platform maps and changes only the current platform's graphics/gameplay map. The server Rules no longer validate every stored platform on each `SetAsync`. After account registration creates *empty* `graphics`/`gameplay` maps, update validation uses `Map.diff().affectedKeys()` to validate at most ONE changed graphics device and ONE changed gameplay device plus optional `global` settings per write. Every changed device map still receives all of its original field whitelist, integer range and boolean type validations. Tampering with multiple platforms in one write is rejected before expensive nested evaluation. Unchanged other platform data is preserved, not silently discarded, including legacy records. This introduces a deliberate incremental-update contract; direct creation of a full rich preferences document without the canonical empty bootstrap is rejected.
- **Positive test contract:** Windows, macOS, Linux, Android, iOS update in a sequence of five actual Unity-style merged full-document writes. After each update, previously saved platform sections remain intact. E2E then confirms all five plus global are still persisted; single-section negative payloads modify ONLY the target field, so their denials exercise their specific schema checks instead of accidentally failing a bulk rewrite. Additional uncounted preflight guard confirms bulk 5-platform injection fails by changed-section limit without expression-budget error. Invalid-field denials cannot be counted as PASS when Firestore reports expression evaluator exhaustion.
- **No Unity service changes** and no migration/deployment have been applied. `users`, `public_profiles`, server-only deny rules, all prior QA registration and Steam release gates remain intact. Historical out-of-contract data previously stored in unchanged platform maps is *not* retrospectively cleansed; a separate audited migration may be necessary for existing live users. A future intentionally multi-platform edit in one write must be divided into sequential single-platform saves.
- **QA candidate / not accepted:** `node scripts\qa.mjs --full` on owner's Windows machine must pass original Rules 17/17, Core 05 20/20, Core 06 28/28, App Check local tests and terminal `AtlasBoard QA FULL LOCAL PASS`. Offline syntax/self-test do not establish an emulator PASS. After acceptance continue Security Core 07 public-profile enumeration/cost/privacy policy audit, not production release. No GitHub push, commit, Steam AppID purchase/use or deploy performed.


### Security Core 06.3 — growth-budget first, then changed-platform schema (2026-10-13)

- **Owner v5.20.2 result (FAILED):** The inherited 17/17 Rules tests and Security Core 04 9/9, Core 05 20/20 still passed. Core 06's original empty-profile create/read both PASSED. The uncounted deliberate attempt to replace the empty document with five graphics/gameplay device maps failed with Firestore's **1,000-expression evaluation error**, not the expected deterministic shape-policy denial. The test correctly refused to count this as security success and terminated before the intended five sequential platform writes. Core 06 28/28 and terminal FULL LOCAL QA were NOT achieved.
- **v5.20.3 Rules correction:** Add `platformGrowthWithinBudget()` at the very beginning of `validUpdatedPreferences()`, **ahead of** the nested schema, diff operations and platform map validation. First verify graphics/gameplay maps exist on old and new documents. Reject requests that increase graphics platform count by more than one or gameplay section count by more than two. The five-at-once injection now has an inexpensive, deterministic false condition and does not depend on expensive `diff()` or nested field validation. The existing separate update gates still enforce at most **one changed graphics platform** and **one changed gameplay platform plus optional `global`**, with full type, nested key and range checks on each affected map. Other devices' unchanged maps remain untouched. No other Firestore collection permission was widened.
- **E2E guard improved:** `security-core06-settings-schema-e2e.mjs` reports the precise original Firebase error if the bulk precondition ever again exhausts the expression budget. Offline self-test requires the new growth precondition to appear before the header and diff calls. Existing 28 counted cases, Auth emulator isolation and cleanup are retained without weakening negative assertions or their 1,000-expression-error rejection.
- **Status: candidate ONLY**, pending a fresh owner local Windows run proving the original Rules 17/17, full SEC06 28/28 and `AtlasBoard QA FULL LOCAL PASS (emulators only).` The owner log may expose other budget problems from later valid updates or the many negative cases; classify and address any such failure rather than accepting HTTP 403 blindly. The container does not include the full Functions dependency tree or Firebase Emulator CLI, so no integrated emulator result is claimed here. No production publish/deploy, git mutation, Steam AppID action or Unity service change.

### Security Core 06.4 — minimum counterexample and efficient Rules evaluation (2026-10-13)

- **Owner v5.20.3 evidence:** Earlier Firestore Rules 17/17, Core 04 9/9, Core 05 20/20 all PASS. Core 06 empty defaults and owner read PASS. The intentionally malformed **five-platforms-at-once** REST PATCH triggered `maximum of 1000 expressions to evaluate has been reached`, including an `update` error, even though the early map-growth guard was present. Core 06 and terminal Full QA are FAILED (not accepted). This is a Firestore evaluator limit under a large negative payload, not proof of any successful bypass.
- **Root cause of repetitive tests:** The E2E incorrectly started with an expensive malformed five-platform document (despite the exact policy being `at most one new platform per update`); no legitimate five-stage write or adverse-schema test was reached. The originally appended Core 06 ran *last*, repeatedly forcing re-execution of all accepted integration suites before encountering its first failure.
- **v5.20.4 Rules optimization:** Bind old/new `preferences` documents once per update, pass them into header and growth gates, and bypass nested graphics/gameplay `diff` when an entire section is unchanged. Validated modified maps still undergo the same strict allowlist/type/range checks; cross-account isolation and all other collection rules remain untouched. This is a candidate; no local emulator execution was available in the packaging environment.
- **v5.20.4 QA correction:** Submit the **minimum two-platform bulk update** as the negative policy test, requiring an ordinary 403 and explicitly rejecting any evaluation-budget errors. Write a one-platform valid baseline, then run unchanged 28 counted schema/privacy/ownership checks against that bounded dataset, ensuring malformed writes leave stored state untouched. Afterward, sequentially write the merged settings for all five actual platforms and verify persistence and an additional valid crossplay update. The 5-platform stress write is removed as an artificial malformed test: one-device-per-write is fully refuted by the cheaper two-device counterexample. No security test was silently treated as passed.
- **Fail-fast QA:** Move Core 06 E2E to the **first** emulator integration test, after offline build/self-tests and before longer accepted regressions; preserve the other accepted tests and App Check enforcement pass. This is a test-order optimization, not a waiver. Full QA remains a blocking gate.
- **Acceptance remains BLOCKED:** Require `ATLASBOARD_SECURITY_CORE_06_SETTINGS_SCHEMA_PASS: 28/28`, original Rules 17/17, all accepted regressions, local App Check E2E, terminal `AtlasBoard QA FULL LOCAL PASS (emulators only)` from the owner before treating v5.20.4 as accepted or starting the next Security Core.


### Security Core 06.5 — lazy structural guard before nested evaluation (2026-10-13)

- **Evidence from v5.20.4 owner Windows run:** Core 06 was first in emulator chain; old static/offline QA passed, defaults create/read passed, the intentionally rejected 2-platform REST PATCH still caused `maximum of 1000 expressions ...` in the **update** rule, despite a growth-count guard. No Core 06 acceptance or FULL LOCAL PASS.
- **Root design issue addressed:** A root Firestore REST PATCH/upsert can produce both create and update authorization diagnostic branches. Previous `validNewPreferences()` performed the heavy `validPreferencesHeader()` (including nested audio validation) **before** checking that the new `graphics` and `gameplay` maps were empty. Also `validUpdatedPreferences()` eagerly bound the old/new nested documents before its fast-rejection gate. With rich maps this can waste evaluator operations before deliberate denial.
- **v5.20.5 correction:** Reject nonempty nested platform sections at the **start** of the create validator; do not call `validPreferencesHeader` for invalid create-shaped content. Split update into `validUpdatedPreferences()` (only early platform-map growth/type guard) and `validUpdatedPreferencesBody()` (deferred nested headers, graph/gameplay changed-map type/range/allowlist checks). This uses Rules' documented `&&` short-circuiting. All strict schema fields, 28 tests, negative budget-error detection, old 17 Rules tests, Core 05 20/20, and QA's Core06-first ordering remain unchanged. Offline self-test asserts the actual guard order and tests bulk-vs-incremental cardinality model, but is NOT an emulator result.
- **Acceptance pending:** First confirm one-platform positive save, negative 2-platform explicit denial without evaluator exhaustion, five sequential platform saves, all 28 checks and terminal FULL LOCAL PASS on owner's local Firebase emulator. If this still reaches 1,000, STOP refining the monolithic document validator and move to a per-platform Firestore document schema plus an explicit Unity read/write migration; do not relax hostile-payload tests or misrepresent 403 evaluator failures as successful denial.
- No deployment, push, commit or live account migration has been performed. Production gates remain blocked.

### Security Core 06.6 — v5.21.0 cross-platform Settings architecture refactor (Android-first, candidate)

**Owner requirement:** Android release precedes Steam, and the same user can later have Windows/macOS/Linux/iOS and new platform-specific Settings. Prefer a durable normalized data model over repeated attempts to shave expressions from Firestore's old, heavily nested preferences validator.

**Owner evidence for v5.20.5 (FAILED):** Owner Windows `node scripts\qa.mjs --full` still passed static suite but `SEC06 [1/28]` and `[2/28]` were the only actual Core06 emulator successes. The third two-platform bulk negative request was denied only after exhausting the Rules **1,000-expression** budget. The test correctly refused to count that as PASS. v5.20.0–v5.20.5 are superseded failed candidates; none were accepted.

**v5.21.0 architectural decision:** Keep the original Firestore `preferences/{uid}` root document so account registration, language/audio/crossplay and other account APIs remain compatible. Shared changes are limited to language/audio/global gameplay/crossplay, and any legacy platform fields already in the root are immutable. Store each supported platform's graphics/camera gameplay independently at `preferences/{uid}/platforms/{platform}`, with `schemaVersion=1`, bounded fields and authenticated owner-only document GET/create/update; deny LIST/delete and unknown platform IDs. This removes the old all-platform `Map.diff()` validator rather than trying another complex short-circuit optimization. Future feature modules must have their own schema/version/allow rule: default deny until reviewed.

**Unity migration:** Complete canonical replacement `Assets/Project/Scripts/Online/Firebase/AtlasBoardCloudPreferencesSync.cs` reads only the active OS's platform doc, falling back to the old root map for that OS or local PlayerPrefs when absent. A Firestore WriteBatch atomically saves root shared fields with `SetOptions.MergeAll` and only the active OS platform doc. The legacy root platform fields are not overwritten or deleted. The existing `AtlasBoardAccountService` remains unchanged, preserving its registration contract; the new sync does not call its legacy full-document `SaveCloudPreferencesAsync`. New C# component still needs live Unity Editor compile + Android runtime smoke acceptance.

**QA:** Replaced the old Core06 E2E with 28 Android-first, per-document positive/negative controls, other-OS isolation, independent persistence, cross-account privacy, type/range/unknown-field denial, and explicit rejection of evaluator-budget errors. Core06 remains first in the emulator chain; the accepted prior Core01–05, 17 Rules, Auth/Steam/commerce/Chat and App Check suites are retained unchanged. `security-core06-settings-schema-e2e.mjs --self-test` and `qa.mjs --self-test` exercise code/fixture contracts offline, but DO NOT establish Firestore emulator acceptance.

**Scope and gates:** See `Assets/Project/Documentation/SEC06_PLATFORM_SETTINGS_ARCHITECTURE.md`. Require actual owner Windows `node scripts\qa.mjs --full` PASS for 28/28 plus terminal `AtlasBoard QA FULL LOCAL PASS (emulators only)`, followed by Unity Editor C# compile and Android persistence check. Security Core 07 public profile enumeration policy audit remains NEXT. Independent real match verifier, deployment App Check, Steam real AppID, staging/load/physical-multiplayer and production GO/NO-GO are not closed. No push/commit/deploy occurred.

### Security Core 06.7 — v5.21.0 owner LOCAL ACCEPTED (2026-10-13)

**Direct Windows owner evidence:** `Pasted text(20261013-032433).txt` from `node scripts\qa.mjs --full` (1,349 lines). The new Android-first separate-platform `preferences/{uid}/platforms/{platform}` schema passed **28/28** actual Auth/Firestore emulator cases; Android and iOS isolation, all five platform writes, bounded schema checks, malformed cross-account/unknown platform denial and cleanup were explicitly reported. The previously accepted Firestore baseline **17/17**, SEC05 **20/20**, SEC04 **9/9**, SEC03 **15/15**, economy **19/19**, inventory **11/11**, promo **13/13**, moderation **49 checks**, Steam development emulators and App Check local smoke tests still passed. Terminal log: **`AtlasBoard QA FULL LOCAL PASS (emulators only).`** Earlier monolithic SEC06 v5.20.0–v5.20.5 failures are superseded; the Firestore 1,000-expression problem was eliminated in this executed scenario by document isolation rather than loosening security tests. **Core06 LOCAL ACCEPTED**, but Unity Editor compile and Android live device preference migration/sync remain UNVERIFIED. No production deployment.

### Security Core 07.0 — v5.22.0 public profile targeted GET / enumeration prevention (2026-10-13, candidate)

**Policy:** Public profile documents are UID-targeted projections for authenticated participants, not an unrestricted authenticated directory. In `firestore.rules`, split `public_profiles/{uid}` `allow get: if signedIn()` from `allow list: if false`; owner-only writes, exact 4-field projection and deny delete remain unchanged. This blocks both collection listing and Firestore `runQuery` collection scans, regardless of pagination, without breaking Unity's known-profile `Document(uid).GetSnapshotAsync()` access. Firebase Admin SDK server lookups remain unchanged. Android-first cross-platform cloud Settings schema from Core06 is unchanged.

**SEC05 explicit policy migration:** Update only SEC05 E2E assertion `[10/20]` from authenticated profile LIST **allowed** to **denied**, preserve the 20/20 regression count and every other existing profile/privacy test. SEC07 is not a silent tightening of a previously approved product contract: the deliberate rule change and compatibility rationale are recorded here and in `SEC07_PUBLIC_PROFILE_PRIVACY_POLICY.md`.

**SEC07 E2E:** New `security-core07-public-profile-enumeration-e2e.mjs`: 23 isolated emulator checks for sign-in, owner/peer/anonymous known-UID GET, collection LIST with sizes 1/50/500, LIST as owner and anonymous, Firestore REST `runQuery` limit and equality-filter bypass attempts, owner updates, outsider overwrite/delete denial, public field allowlist/length bounds and immutable outcome after failed writes. Local-only preflight, no Admin SDK until strict localhost checks, fixture cleanup on all paths, HTTP denied and Firestore Rules expression-exhaustion diagnosis. `qa.mjs` now runs Core07 **first**, Core06 second, then all prior accepted E2E plus App Check exception suite. Offline fixture self-test and syntax checks PASS; Firebase Emulator and Unity Editor NOT executed in packaging environment.

**Required acceptance:** Fresh Windows `node scripts\qa.mjs --full` must yield `ATLASBOARD_SECURITY_CORE_07_PUBLIC_PROFILE_PASS: 23/23 checks (emulator only)`, retain SEC06 28/28, SEC05 20/20 and all prior suites, and finish with `AtlasBoard QA FULL LOCAL PASS (emulators only)`. No claim of CORE07 acceptance from offline tests alone. No deploy, repository commit/push, live app credentials or production App Check changes. Remaining independent security and production readiness work is separate from the 01–07 local series.

### Security Core 07.1 — v5.22.0 owner LOCAL ACCEPTED (2026-10-13)

**Owner Windows test evidence:** `Pasted text(20261013-033448).txt` and its identical copy `Pasted text (2)(20261013-033452).txt` report the same 1,378-line `node scripts\qa.mjs --full` run. Core07 `ATLASBOARD_SECURITY_CORE_07_PUBLIC_PROFILE_PASS: 23/23`, Core06 `28/28`, Core05 `20/20`, Core04 `9/9`, original Firestore `17/17` and all preserved security/functional E2E passed. Terminal `AtlasBoard QA FULL LOCAL PASS (emulators only).` **Core01–07 local series now ACCEPTED.** Not an endorsement of production App Check, Unity Editor, actual Android build, physical multiplayer or independent server-authoritative gameplay verification. No production deploy, push or Steam AppID action.

### Phase 17A — v5.23.0 match-result self-consistency security hardening (2026-10-13, emulator candidate)

**Android-first continuation:** User priority order is existing security and play QA, optional gameplay/UI additions, **Google Play first**, Steam optionally later while preserving its prepared integration. Independent server verification of actual game history is still the largest security gap; this phase provides a bounded first layer without mislabeling snapshot arithmetic as independent game verification.

**Concrete source finding:** `MatchResultManager.BuildOnlineResultSnapshot` determines net worth from money + property investment + development investment and selects all highest non-bankrupt participating slots; the legacy `matchHostPublishState` completion guard checked `frame.matchResult.valid` but did not validate those invariants. A modified host could submit internally contradictory scoreboard arrays after the low revision/sequence threshold. `match_result_verifications` remains protected and no production writer exists.

**v5.23.0 change:** Add a deterministic TypeScript pure-result invariant checker to the callable before Firestore publish, fail closed for forged winners, contradictory net worth, malformed slot arrays, negative assets, non-participant payload injections and zero-turn/zero-dice final frames. Preserve original host seat/revision and App Check checks and keep the verified-receipt requirement for permanent XP. The checker cannot grant a receipt and must not be represented as a real rules replay. New 19-case compiled-module unit suite, 12-case isolated *actual callable* local emulator probe (negative state must remain unchanged; valid-looking result still earns XP=0 absent proof), and fail-fast orchestration in `qa.mjs` before all previously accepted suites. See `PHASE17A_MATCH_RESULT_CONSISTENCY.md`.

**Local packaging checks:** isolated validator TypeScript strict compile and compiled 19/19 unit checks PASSED; JS `node --check`, `qa.mjs --self-test` passed. Firebase emulator, whole Functions TypeScript build/ESLint, Unity Editor and physical Android execution were NOT performed here. **Acceptance pending:** owner `node scripts\qa.mjs --full` must show `ATLASBOARD_PHASE_17A_CONSISTENCY_PASS: 12/12`, existing Core07 23/23, Core06 28/28, every prior full suite and terminal FULL LOCAL PASS. No remote mutation.

**Unclosed security work:** A self-consistent fake game is still fake. Before awarding permanent live XP, implement server-authoritative rules replay, server-owned dice/economy evidence, protected verified-output receipt and reward telemetry authority; do not weaken the pending gate. Google Play/Android runtime, real App Check enforcement, deployed staging, TTL/limits/load, and store release GO/NO-GO remain separate.


## ATLASBOARD PACKAGE DELIVERY CONTRACT — FULL FILES ONLY (MANDATORY, 2026-10-13)

**Owner's explicit requirement, highest priority for all future AtlasBoard deliveries:**

1. Deliver a **ZIP containing the COMPLETE replacement source/documentation files**, under their exact paths relative to `C:\Users\crazy\AtlasBoardPrototype`.
2. **NEVER deliver a patch, diff, incremental code fragment, installer/patch application script, text-based search/replace program, or a command that edits source files** as a substitute for those full files. The owner extracts ZIP over project root; no install command is required.
3. Include all files changed by the iteration as whole files. Do not silently omit new/modified files. No `APPLY_*.mjs`, `.patch`, `.diff`, or instructions to manually splice snippets into existing files.
4. **Do not overwrite unrelated project files**: deliver only explicitly changed full files, preserving previous accepted work and full roadmap history. Keep project-relative paths and existing names. A README with extraction path and QA command is permitted; it must not be a file installer.
5. Verify ZIP member paths, syntax/tests available offline, and archive integrity before delivery. State untested Emulator/Unity/Android gates candidly; never label incomplete testing as PASS.
6. With every change update and **include complete** `Assets/Project/Documentation/MASTER_ROADMAP.md` in the ZIP. Keep Android-first ordering: finish tests -> extra features -> Google Play -> optional Steam, while retaining Steam-ready architecture.
7. On QA failure, diagnose and supply the next corrected **full-file ZIP** and direct QA command; do not ask the owner for prompt confirmation. No GitHub push, Firebase production deploy, or production data mutation without owner authorization.

**Incident / corrective action:** The Phase 17A v5.23.1 delivery incorrectly included `APPLY_PHASE17A_v5.23.1.mjs` instead of full replacement project files. That delivery does **not** comply with this mandatory contract and must not be repeated.

**Phase 17A status from owner's latest report:** Match-result consistency 19/19 offline + 12/12 Emulator PASS, Core07 23/23, Core06 28/28 and preceding suites PASS. Full QA remains **FAILED** at Phase16I public-lobby 8-concurrent-request timeout. A larger diagnostic timeout alone does NOT certify production latency; client correctness and staging/physical-device p95 still OPEN. Server-side independent replay verifier and permanent rewards gate still BLOCKED until independently verified.

### Delivery-standard-only archive (2026-10-13)

- Owner expressly rejected the v5.23.1 installer delivery and requires complete replacement files in ZIP. This roadmap-only corrective archive contains this entire canonical roadmap file; it **does not claim** to include the missing full `public-lobby-discovery-e2e.mjs` and therefore does not resolve the current 16I-5 timeout. The old v5.23.1 installer archive is superseded for delivery-format purposes and must not be applied. Next test revision must include the entire current `Backend/Firebase/functions/scripts/public-lobby-discovery-e2e.mjs` plus this entire roadmap (and any other changed files) as ZIP members under their actual project-relative paths.

### Phase 17A.2 — v5.23.2 complete-file Public Lobby Discovery concurrency QA correction (2026-10-13)

**Owner-observed blocker:** The owner's `Pasted text(20261013-035103)(1).txt` `node scripts\qa.mjs --full` transcript passed the Phase17A independent checksum/consistency *limits* (19/19 offline + 12/12 emulator), Core07 23/23, Core06 28/28 and earlier suites; FULL QA STOPPED at Phase16I `public-lobby-discovery-e2e.mjs` [16I-5/6] 8 simultaneous calls, with local `TimeoutError`. Backend Functions invocations that did finish returned normal successful responses, although some workers queued for many seconds. No evidence establishes this as a corrupted Firestore query, deterministic pagination failure or a production user-device latency metric.

**Correction delivered as complete files:** Existing `Backend/Firebase/functions/scripts/public-lobby-discovery-e2e.mjs` (not a patch script) and this complete `Assets/Project/Documentation/MASTER_ROADMAP.md` in a single archive, preserving canonical folder structure. The test is based on the verified 30,218-byte upstream Phase16I source plus the observed 15-second local E2E baseline; no production server/Firestore/Unity code is changed. The eight concurrent callable requests and byte-exact first-page result comparison remain mandatory. The concurrent probe has its own 30-second diagnostic transport allowance, records each request's duration/HTTP or timeout outcome, and fails if ANY of eight requests fails, times out, or yields a different list/order. Other E2E calls retain their 15-second baseline. Over-5-second requests are explicitly flagged `PERFORMANCE OPEN`; a functional emulator PASS is not Android/production latency clearance. No retries mask failures.

**Offline verification:** The complete `public-lobby-discovery-e2e.mjs` source recovered and hash-validated against the read-only GitHub source (before changes); updated JavaScript `node --check` **PASS** and isolated concurrency-mock behavior 4/4 **PASS** before archive delivery (eight success/HTTP rejection/network rejection/nondeterministic page). ZIP must contain both complete files and no installer, `APPLY_*.mjs`, patch, diff or partial replacement. The owner must run fresh Windows `node scripts\qa.mjs --full` to establish **actual Emulator PASS**; until then FULL QA remains **UNVERIFIED / previous failure outstanding**. An emulator latency warning does not close production performance, Android physical device testing or the independent game-result verifier.

**Owner sequence:** Security/functional QA -> optional game improvements -> Google Play Android release -> optional Steam release, retaining Steam-capable code. No GitHub push, external staging deploy or production settings changed in this deliverable.


### Phase 17A.2 — Owner Windows emulator acceptance of v5.23.2 (2026-10-13 UTC)

**Owner evidence:** `Pasted text(20261013-042741).txt`, launched with `node scripts\qa.mjs --full` on the owner's Windows PC, concluded exactly `AtlasBoard QA FULL LOCAL PASS (emulators only). Deployed Steam/App Check, live TTL, Unity build and physical multiplayer remain unverified.` Prior Phase17A compiled validator 19/19 and local callable 12/12 PASS, SEC07 23/23, SEC06 28/28, SEC05 20/20, SEC04 discovery 9/9, SEC03 15/15, SEC02 19/19, inventory 11/11, promo 13/13, original Firestore Rules 17/17, matchmaking/pagination, Steam dev and App Check emulator negatives all PASSED in that run.

**Exact 16I-5 measurement from this receipt:** All 8 concurrent first-page requests returned correct identical ordering and accepted data, with request durations in milliseconds `149, 9367, 2414, 8254, 9499, 10165, 2517, 4931`, wall-clock burst `10166 ms`. **4/8 were slower than 5000ms**. No transport errors and no data inconsistency in this particular run. Functional local acceptance is real; Android device latency, deployed p95, cold-start and Firebase billing throughput are **not** accepted. Do not remove the over-5-second warning or mistake a longer QA allowance for production optimization.

### Phase 17A.3 — v5.24.0 finalization contention / forgery regression (NEXT TEST PACKAGE)

**Security objective:** Strengthen adversarial evidence around the accepted `matchHostPublishState` -> `progressionRecordCompletedMatch` chain before implementing independently trusted game replay. The current host-based match state is still NOT an independently verified game history; no client-side result consistency or arbitrary host hash can issue permanent XP, wins or achievements. An atomic same-transaction proof/state freeze design and independent server rules engine are still OPEN and must not be silently closed by finite tests.

**One changed functional file, complete replacement:** `Backend/Firebase/functions/scripts/match-result-consistency-e2e.mjs`. Preserve the original 19 compiled-validator offline cases and original Phase17A 12 actual local emulator callable checks. Append nine **new, fail-closed** 17A.3 cases using a second fully isolated local match in the same QA process:

1. Canonical emulator-only two-seat fixture, no production interaction.
2. FOUR truly concurrent host match-completion requests on the same expected revision: exactly one may commit, three must reject; no retries mask failures.
3. Assert completed network revision, event sequence, exact snapshot bytes, and status are persisted only once.
4. Reject postcompletion publication that attempts to reopen the match.
5. Reject duplicated `match_complete` after the final revision.
6. Authenticated REST client must receive 403 when trying to create `match_result_verifications/<matchId>`.
7. Two concurrent progression requests without proof remain `pending_authoritative_verification`; no account progression document and no `progression_match_events` may appear.
8. Local Admin-SDK-only forged/mismatched protected receipt still cannot mint progression; this is a negative-only test fixture, not a real verifier.
9. Subsequent host result overwrite must remain blocked even if a receipt document exists.

Report actual four-call durations and `PERFORMANCE OPEN` if any exceed 5000ms. The test uses a 30000ms **diagnostic transport deadline only for the four-way contention**, preserves all original Phase17A test deadlines, and never converts HTTP/data failures into success. The exact new marker required for acceptance is `ATLASBOARD_PHASE_17A3_FINALIZATION_RACE_PASS: 9/9 (emulator only).` Existing `ATLASBOARD_PHASE_17A_CONSISTENCY_PASS: 12/12` and all previously accepted suites remain in the unmodified existing QA sequence; no changes to QA dispatcher, production backend, Firestore Rules, Unity runtime, credentials or deployed cloud resources. If 17A.3 reveals a real race, fix its exact source and expand tests in the next FULL-FILES-ONLY ZIP before proceeding; do not weaken the assertion or mark the race closed.

**Local package-author validation:** Complete Node 22 `node --check` for new full-file E2E source and ZIP member integrity can be performed offline. Firebase Auth/Firestore/Functions runtime tests are not available in the package-author environment; **owner Windows `node scripts\qa.mjs --full` is mandatory before PASS**. Unity C# compile and Android physical multiplayer remain outstanding. No Steamworks AppID is required for this local test.

**Delivery standard remains mandatory:** ZIP contains only full project-relative files (this E2E + this complete MASTER_ROADMAP), never patch, installer, APPLY file, partial snippet or manual search/replace instructions. User extracts at repo root; do not auto-push GitHub or deploy Firebase. GitHub `main` latest read was `8e1cc9da2187d5af7f5bbbaa9dece68ae7aedb21` (Cores 01–03), so newer accepted Core04–07 + Phase17A/17A.2 are still only owner-local unless separately pushed by owner. Preserve the newer local sources on any GitHub sync; do not replace them with older `main`.

**Execution order:** Finish security/core/Unity/Android tests -> choose/implement extra features -> Google Play Release -> optionally Steam Release. Preserve Steam-ready adapters but NEVER block Android validation on an official Steamworks AppID. Crossplay is optional. Pending-release gates include genuine independent rules verification, deployed App Check Play Integrity, real two-to-four Android clients, Android AAB and store/privacy/operational validation.


### Phase 17A.3 — v5.24.0 owner Windows finalization-race acceptance (2026-10-13)

**Owner evidence:** `Pasted text(20261013-043843).txt`, fresh `node scripts\qa.mjs --full`, finished `AtlasBoard QA FULL LOCAL PASS (emulators only)`. Old Phase17A validator **19/19** unit and **12/12** emulator PASS. New Phase17A.3 **9/9** emulator PASS, including 4 simultaneous finalizations with exactly one accepted commit, immutable network snapshot/revision, post-completion reopen and replay denial, direct forged-verification client write denial, two parallel unverified reward requests minting zero XP/wins, mismatched privileged receipt denial. All prior Core01–07 functional/security suites and App Check local smoke remain PASS. **Do not confuse this with independent rules replay, deployed App Check, Unity C# compile or physical multiplayer.**

**Local Public Lobby observation:** 8/8 consistent and successfully returned but 4/8 over 5s; timings `138, 5906, 2395, 8209, 10397, 9043, 2503, 4931 ms`, burst wall `10398ms`. `PERFORMANCE OPEN` persists for staging and real phones.

**GitHub checkpoint:** Owner reports publishing the previous source package to `main`. GitHub search confirmed new commit `0a5a1b1a03f6067729015b85d8a6779706da157f`, summary `Harden Security Cores 04–07 and expand Phase 17A match integrity QA`. This GitHub checkpoint is distinct from the fresh Windows 17A.3 runtime PASS; no new push/deploy in this deliverable.

### Phase 17B — v5.25.0 Android-first Unity build QA (new package, CANDIDATE)

**User-priority clarification:** Existing `node scripts\qa.mjs --full` tests exercise the common Firebase backend, used across Android, Windows, later Steam and other supported clients; they are NOT Android-only and do NOT compile Unity. For entry into optional NEW FEATURES, two finite integration gates remain, as **workflow milestones rather than exact count of unit test cases**: **Gate A** Unity 6000.5.6f1 Editor project compile and actual Android Development APK; **Gate B** at least one real Android device UI/gameplay and 2-client multiplayer smoke. These are reasonable pre-feature baselines, NOT Android publication gates. Begin optional features only after reviewing Gates A/B; re-run backend and Unity regressions with each substantive feature. Android-first release remains later; Steam optional.

**Files included as whole files:** NEW `Assets/Project/Editor/AtlasBoardAndroidSmokeQA.cs` and NEW `Assets/Project/Documentation/PHASE17B_ANDROID_SMOKE_ACCEPTANCE.md`, plus this entire `Assets/Project/Documentation/MASTER_ROADMAP.md`. No existing project sources, Player Settings, Build Settings, Windows-only build QA, Firebase env, secret, application identifier or Steam integration overwritten. Unity QA menu: `Atlas Board > QA & Diagnostics > QA > Android > 1 - Validate Android Readiness`, then `2 - Build Development APK`. The smoke uses only `Assets/Board_Prototype.unity`, excluding the earlier `SampleScene` that appears first in original EditorBuildSettings. It requires the installed Android Build Support, Android selected, canonical scene, configured bundle/version; build output `Builds/QA/Android/AtlasBoard-Android-Development-<UTC>.apk`. Build options are Development and AllowDebugging. The script restores temporary development HTTP/APK/Gradle-export settings even if the build fails. A successful `APK_BUILD_PASS` marker represents Unity Android build only, NOT physical-device, Firebase staging, Play App Check or store readiness. The old `AtlasBoardBuildQA.cs` Windows-only gate remains unchanged.

**Candidate test criteria:** Unity Editor compiles the new C# file and complete application; Android menu preflight PASS and real APK BuildPipeline PASS without settings regression; at least one user-installed Android device then separately completes physical smoke before Gate B is accepted. **Neither Unity nor Android real-device execution was available in artifact-generation environment; not accepted until owner log.**

**Persistent pre-release security gaps:** Independent server-owned match replay verifier and protected real proof writer remain unimplemented; no verified permanent XP release. Deployed App Check Play Integrity negative tests, privacy/consent/deletion, TTL, performance, load, 2–4 device staging and compliant signed ARM64 AAB remain release blockers. 4/8 slow local Public Lobby requests remain a performance warning even after full functional local PASS. Any changes to new features must preserve the fail-closed permanent rewards guard.


---

## Phase 17B.1 — v5.25.1 Android orientation, Back, device diagnostics, staging handoff (2026-10-13 UTC)

**Evidence accepted:** Owner's Unity Android Development preflight passed. `APK_BUILD_PASS` reported; actual phone launches the app and menus/career respond to touch, but it launched portrait/cropped, did not handle Galaxy S20 FE Android Back as expected and reported `ATB103` (30 s startup delay) and `ATB110` (identity/session preparation failure). Firebase Auth/Functions/Firestore emulators running at Windows `127.0.0.1` are not automatically reachable from a physical Android device. User's 38-minute first Android Gradle/IL2CPP build was successful, not an error. The output `report.summary.totalSize=2037493599` must not be confused with actual on-disk APK size; v5.25.1 QA now records both metrics.

**v5.25.1 changed complete source files:** `Assets/Project/Editor/AtlasBoardAndroidSmokeQA.cs` (QA build applies LandscapeLeft only during build and restores original PlayerSettings); `Assets/Project/Scripts/Platform/AtlasBoardMobilePlatformQA.cs` (Android/iOS landscape lock, Android API 33+ native system Back callback routed into existing menu/room/pause flow, dev-only onscreen Back fallback); `Assets/Project/Scripts/Platform/AtlasBoardAndroidDeviceDiagnostics.cs` (Android Development-only bounded local session log in `Application.persistentDataPath/Diagnostics`, separate from desktop exe-based logs). Full acceptance instructions: `Assets/Project/Documentation/PHASE17B_ANDROID_DEVICE_QA_v5.25.1.md`.

**Status:** C# source package prepared and reviewed offline; actual Unity Editor compile, new APK build, device rotation/Back/log retrieval and backend staging acceptance all **PENDING**. No deployment, Firebase setting relaxation, Firestore Rules change, credentials or platform-specific desktop behavioral change. Android 12 or lower has existing Escape handling and a QA-only visible Back fallback; native back acceptance must be tested per OS version. Do not claim Back fully solved until a real device test succeeds.

**Production separation:** `ATB103` is a timing warning, `ATB110` an online identity/session preparation failure. A real deployed staging Firebase project (different from shared development/production `atlasboard-usa`) and correctly configured Auth/Firestore/Functions/Play Integrity is the required path to online play without manually starting the Windows Emulator. Do not treat `adb reverse` or a longer timeout as a deployed backend. Staging credentials/project ID require explicit owner setup and acceptance. Never accidentally deploy to production or enable unverified XP minting.

**Prior verification preserved:** Phase 17A.3 9/9, Phase 17A 12/12, and prior complete local Firebase Emulator QA passed in the supplied 2026-10-13 Windows run; Firestore local App Check invalid-token limitation, independent server-side game history replay verifier, 16I 8-way discovery burst with 4/8 requests over 5 s, actual Android network multiplayer, cloud staging, APK/AAB production compliance and Play Store acceptance remain open.

**Deliverable format (mandatory, restated):** Only ZIP packages with complete replacement files under correct source paths and current full `MASTER_ROADMAP.md`; no patch/diff/installer scripts. Real compiler/device PASS cannot be asserted solely from source inspection.

**Next QA acceptance:** Android v5.25.1 physical device rotation + navigation + local diagnostics, followed by secure hosted staging configuration and actual 2-device cross-network multiplayer. New features may be developed on a separate feature branch in parallel once stable baseline is preserved, but Android launch/public release is BLOCKED until required gates close.


---

## Phase 17B.2 — v5.25.2 Android safe viewport / Settings resolution / error presentation / local sharing / APK cleanup (2026-10-13)

**Owner physical evidence:** Seven 2048x921 Samsung Galaxy S20 FE landscape screenshots, Settings > Mute All Audio > Apply produces destructive resolution/layout change; main menu and other screens crop at top/edge; redundant ATB110 full-screen error and top-right toast; ATB202 browser failure; prior ATB103 slow startup. Existing backend is not accepted online on device. Owner reports ~230MB APK and growing local QA-build directory. Prior Android v5.25.1 Build Gate PASS; **this v5.25.2 build is not yet compiled or phone-validated**.

**Root cause found in GitHub:** `AtlasBoardSettingsV2Controller.ApplyAndClose` reapplies `ApplyGraphics(..., true)` even on audio-only save; desktop-only `Screen.SetResolution(graphics.ResolutionWidth, graphics.ResolutionHeight, mode)` executes outside Editor and breaks Android aspect/layout. This package implements a mobile-only display corrective guard and disables mobile-specific desktop dropdowns; a future full-controller refactor should remove the non-desktop SetResolution call at its source.

**Delivered full-file-only ZIP:** full replacements `Assets/Project/Scripts/Platform/AtlasBoardMobilePlatformQA.cs`, `Assets/Project/Scripts/Platform/AtlasBoardAndroidDeviceDiagnostics.cs`, `Assets/Project/Editor/AtlasBoardAndroidSmokeQA.cs`; new whole-file `Assets/Project/Scripts/Platform/AtlasBoardMobilePresentationGuard.cs`, `Assets/Project/Scripts/Platform/AtlasBoardMobileErrorPresentation.cs`, complete `Assets/Project/Documentation/PHASE17B_ANDROID_DEVICE_QA_v5.25.2.md`, and this whole roadmap. Android onscreen `< Back` removed. API 33+ native Back remains. Mobilized CanvasScaler Expand with vertical reserve and safeArea, mobile screen resolution repair, mobile-only resolution dropdown lock, max 1 pixel light. Duplicate mobile ATB toast suppressed only when full blocker present or same code within 75s; diagnosis/status/Retry remain. Sanitized latest on-device log share using Android ACTION_SEND sharesheet, no adb requirement for managed Unity failures. Old known QA APKs automatically removed only after new successful build, never unrelated releases. No patch, installer, or source rewriting tool in ZIP.

**Warning honesty:** Mobile `FindObjectsSortMode` obsolete compile references removed. TMP wrap obsolete in `AtlasBoardLoadingExperience.cs`, `AtlasBoardLobbyRuntimeBridge.cs` assigned-only editor flag CS0414, deprecated Dynamic Batching warning, and diagnostics symbols guidance are **still open**, not hidden or declared fixed. TMP IL2CPP large file diagnostics are not compiler errors. 230MB APK shrinking is a later optimization gate.

**Owner one-project backend decision:** Do not force new Firebase staging project. Existing `atlasboard-usa` may support hosted services directly, but LIVE production deploy remains **PENDING** until permissions, Firebase Auth SDK/Android app config, App Check Play Integrity, Firestore rules, deployed function inventory/QA-only blocks, project costs and safe validation are confirmed. An owner-authorized Firebase connection or authenticated CLI is needed to inspect/deploy. Do NOT blindly deploy emulator/test mutation functions nor open localhost emulator to Internet. No deploy/GitHub push carried out by this ZIP. Permanent XP still gated awaiting independent server-owned rules replay.

**Next acceptance order:** (1) Unity C# compile + Android Development APK v5.25.2, (2) on-device resolution/audio/touch/back/log sharing and cutout validation on 20:9 S20 FE + tablet/16:9 evidence, (3) secure hosted Firebase cloud connectivity on same project and 2-phone full match, (4) full production AAB/Play Integrity/privacy/cost/replay-verifier gates, then Google Play. Owner's preference is to begin new optional features once stable Android connectivity/gameplay is established; Steam optional. The local common-backend QA v5.24.0 PASS remains valid baseline but must be rerun for actual backend source changes.


### Phase 17B.2 v5.25.3 — Android C# compiler block correction (2026-10-13; owner Unity retest PENDING)

**Real owner evidence:** v5.25.2 failed Android Player build, `AtlasBoardMobilePresentationGuard.cs(121,40) error CS0619` because `Object.GetInstanceID()` is obsolete as an error in Unity 6000.5.6f1. Follow-on `APK_FAIL` is a consequence, not an independent verified problem. Build script restored Player Settings successfully. No fresh APK was generated.

**Delivered v5.25.3 full-file-only ZIP:** complete `AtlasBoardMobilePresentationGuard.cs` now tracks `CanvasScaler` references directly (`HashSet<CanvasScaler>.Add(scaler)`) instead of calling `GetInstanceID()`. The other four complete Unity C# source files from v5.25.2 are unchanged, preserving mobile horizontal layout/cutout guard, error-presentation deduplication, log sharing, removal of QA-only Back button, Android build settings recovery and previous-APK cleanup. Updated whole-file device QA instructions and this complete roadmap also included. No diff, installer, patch runner or manual source editing required.

**Validation:** packager checked source replacement cardinality, verified removed API does not occur, verified archive integrity and compared all four other C# source entries with the preceding package. No Unity Editor/Android SDK environment is available to run `BuildPipeline.BuildPlayer` here. Acceptance remains PENDING until new Unity `APK_BUILD_PASS` evidence.

**Warnings not falsely marked fixed:** LoadingExperience TMP word-wrap CS0618 and LobbyRuntimeBridge editor-only field CS0414 are nonfatal; test framework generates `Assets/Resources/PerformanceTestRunInfo.json`/`PerformanceTestRunSettings.json`; Android Debug Symbols warning remains guidance. Do not escalate them as CS0619. Future deliberate full-source review can remove these warnings without reverting user-local changes.

**Hosted backend status:** Firebase connection was suggested again on owner's direct request; installation/authorization must be completed by owner before direct project inspection. Single `atlasboard-usa` managed cloud is the chosen direction, but no deployment, App Check acceptance, cloud data reset, test mutation or public exposure was authorized/performed by this ZIP. Android ATB110/ATB202 remains pending cloud Auth and endpoint validation. No new-platform feature acceptance until real-device QA and authenticated online two-device match PASS.

## v5.28.0 — Phase 17E runtime integrity / account access / Android performance

**Device evidence:** the v5.27 Samsung SM-G780G Android Development build measured sustained ~16–23 FPS at 2400x1080 even after MSAA fell from 4x to 2x. Repeated leave -> scene reload -> new match also exposed legacy tablet/city presentation drift and stale legacy player-control state. Therefore v5.28 treats these as release blockers, not cosmetic polish.

### Canonical LocalOffline start contract

- PRIVATE TABLE remains zero-backend when no authenticated identity exists.
- Modern seat actions are the only user-facing seat source: Host LocalHuman, explicit LocalHuman, explicit Bot, or still-open selected seat.
- At Start, every selected still-open seat becomes a **local Bot**. No open seat may silently become Human.
- A visible **5-second** local countdown runs before gameplay.
- Only after seat finalization does the compatibility bridge mirror canonical seat state into legacy MatchSetup controls.
- Local Offline remains non-rewarded/non-ranked and creates no protected cloud history.

### Re-entry / navigation integrity

- Android/iOS persistent helpers must treat every `Board_Prototype` scene instance as new. Destroyed Unity wrappers are never retained as valid scene references.
- On scene-handle change, modern Purchase/Auction/Trade/Development/Match Result/City Info presentation components and world-input guards are verified and recreated only if missing.
- Acceptance requires: first match, leave to menu, second match, third match, settings round-trip, Career round-trip, Shop round-trip and app restart all expose the same current presentation generation. No navigation route is allowed to reveal an obsolete tablet or disable city tapping.

### Android performance contract

- Mobile runtime ceiling is separate from saved desktop/Steam graphics intent: VSync=0, target<=60, AA<=2x, low-cost hard shadows, shadow distance<=18, HDR/post-processing off, URP renderScale 0.82 when supported.
- If two 10-second windows remain below 45 FPS, mobile emergency mode uses renderScale 0.70, AA off and real-time shadows off.
- These overrides are runtime-only: do not write them back to PlayerPrefs/cloud and do not change Windows/Steam quality settings.
- Do not use `Screen.SetResolution` as an Android performance repair.
- Development-build FPS is diagnostic, not release acceptance. Final gate requires physical-device **Release** measurement as well.

### Guest + account identity contract

- Authentication is optional for Local Offline. Unsigned users receive one persistent language-neutral device-local name `Guest#####X` (five digits + one uppercase letter).
- Guest identity is probabilistically unique, not server-certified global uniqueness, and grants no protected progression.
- Career is account-backed. Unsigned Career entry opens Sign In / Create Account instead of indefinitely loading a cloud profile.
- This does not fabricate career progression: signed-in XP/wins/history still require the trusted progression backend; if no trusted backend is deployed, the account UX may work while protected Career data remains pending.
- Settings exposes the same account entry surface.
- Email/password sign-in, registration, password confirmation, loading lock, password reset email, sign-out and profile rename are supported through Firebase Auth + the existing AtlasBoard Account Service.
- Firebase Auth persistence is the session source; AtlasBoard never caches passwords to keep players signed in.
- The registration math question is convenience/UX friction only, **not** security-grade CAPTCHA.
- Passwords and password confirmation are **never** stored in RTDB, Firestore, PlayerPrefs or diagnostics. RTDB remains transient transport/catalog display only.
- Signed-in `public_profiles.displayName` is canonical across menu/lobby/local P1 presentation.
- Display-name mutation is client-throttled and Firestore Rules enforce a conservative 24-hour gate using the v1 profile `updatedAt` timestamp. Owner must deploy the updated Firestore Rules before claiming the server throttle live.

### Mobile UI acceptance additions

- Shop remains orange; Career is indigo on mobile for immediate visual separation.
- Roll position remains accepted. Trade moves farther left of P2 HUD.
- Tablet mobile scale is 1.25 and Trade controls/text have larger minimum touch/readability targets.
- City information uses a 600x720 mobile reference panel and vertical touch scroll.

### Open gates after v5.28

- Real Unity compile/APK + first/second/third match Android smoke.
- Android Development and Release FPS evidence; tune further if sustained gameplay remains below release target.
- Firebase App Check production provider integration.
- Phase17C RTDB Unity transport + two-device Private Casual Online and measured bytes/match.
- Online Rewarded server-authoritative proof/replay before XP/wins/valuable rewards are enabled.
- Google Play release and later Steam production validation.

## Phase 17E.2 — v5.30.0 Integrated Android / Account / RTDB checkpoint

**Status: IMPLEMENTED CANDIDATE — one Android acceptance build requested; runtime acceptance pending.**

This checkpoint intentionally combines the already-prepared account/guest/mobile work with the v5.29 compile correction so the owner does not need to spend ~30 minutes on multiple Android builds.

### Implemented in this candidate
- Mobile Career card accent is indigo/violet while Shop remains orange.
- Mobile Trade UI receives larger text/touch controls; gameplay Trade action is moved farther left from the lower-right HUD.
- Mobile-only camera/touch and tablet/city sizing remain isolated from Windows/Steam.
- Local Offline PRIVATE TABLE prefers the direct v5.29+ modern-seat entry point. Older project copies retain a reflection fallback.
- Canonical local seats: explicit Local Player stays Local Player, explicit Bot stays Bot, unresolved selected seats become Bots at Start.
- Local Start remains a visible 5 -> 1 countdown.
- Scene reload self-healing remains enabled so second/third matches recreate modern Purchase/Auction/Trade/Development/Result/City presentation components instead of exposing legacy UI.
- Android performance owner is consolidated under `AtlasBoardMobileGameplayUX`; the old v5.29 runtime file is retained only as a compatibility marker so fixes are not applied twice.
- Mobile base performance profile is more aggressive but non-persistent: VSync off, target <=60 FPS, renderScale target 0.75, real-time shadows off, <=2x MSAA, <=1 pixel light, HDR/post-processing/reflection probes/soft particles off. Sustained low FPS may reduce renderScale to 0.62 and disable MSAA. Desktop/Steam saved settings are not rewritten.
- Background scene/object scans are reduced in frequency to lower mobile CPU overhead.
- Persistent unsigned identity remains `Guest#####X` (five digits + one uppercase ASCII letter), stored locally and intentionally not localized.
- Career and Settings expose account access while Local Offline remains playable without sign-in.
- Account UI supports email/password login, registration, password confirmation, easy + / - math check, one-submit-at-a-time loading lock, password-reset email, sign-out and display-name change.
- Firebase Authentication remains the only password store. Password/password-confirmation are never written to RTDB, Firestore, PlayerPrefs or logs.
- Firebase Auth session persistence is used so a signed-in player is not asked for the password every launch.
- Signed-in canonical display name is reapplied to menu/lobby/local P1 presentations from one account identity.
- Firestore profile rules retain the 24-hour server-side display-name cooldown during the migration period.
- New low-frequency RTDB private user mirror is added for authenticated users. It writes general account/device metadata only after sign-in/registration/profile load; it does not run as match polling.

### Canonical account / RTDB target shape

`/users/{uid}` is PRIVATE to that authenticated UID and contains:
- `userId` — Firebase UID; immutable;
- `playerId` — deterministic Atlas player identifier (`ATB-...`); immutable;
- `email` — private mirror and must match Firebase Auth token email;
- `displayName` — canonical signed-in presentation name;
- `createdAt`, `updatedAt`, `lastDisplayNameChangeAt`;
- `/general` — schemaVersion, accountType, language, countryCode, lastSeenAt;
- `/platforms/{platform}/devices/{deviceId}` — per-device model/OS/app version/firstSeen/lastSeen metadata, allowing one account to use multiple Android/Windows/iOS/macOS/Linux devices without overwriting another device record.

Passwords never enter this tree. Email remains private and must never be copied into public room/profile projections.

### Storage end-state required by owner
- **Firebase Auth:** credentials/session/duplicate-email/password-reset.
- **RTDB:** general low-value account presentation, device/platform metadata, Private Casual live membership/presence/intents/acks/state revisions, safe display metadata/catalog mirrors.
- **Firestore + trusted backend:** valuable/security-sensitive authority: wallet/ledger, purchases/refunds, entitlements, XP/wins/rating/achievements, moderation/audit, rewarded promo redemption, independent match verification and other anti-cheat authority.
- Current Firestore account/profile/settings reads remain temporarily during migration. Do not remove them until RTDB account/device behavior and cross-device migration are tested; the end-state is to avoid duplicate general-data reads from Firestore.

### Guest identity boundary
- Guest play must work without internet.
- A completely offline guest cannot be globally server-certified unique; `Guest#####X` is a stable probabilistic device-local presentation identity.
- Once the player registers/signs in, Firebase UID becomes globally authoritative and RTDB `/users/{uid}` is created/synchronized.
- Guest/local/private-unrewarded matches mint no protected XP/wallet/competitive history.

### v5.30 acceptance gate
1. Unity Editor has **zero red compiler errors before Android Build**.
2. Build one Development APK only after step 1 passes.
3. Main Menu: Shop orange, Career violet/indigo; no HUD/card overlap.
4. Unsigned player shows stable `Guest#####X`; restart app and verify the same guest name.
5. Career unsigned -> account login modal. Settings -> account Sign In button/modal.
6. Registration checks password mismatch, incorrect math answer, duplicate-email response and one-submit loading lock; valid registration signs in successfully.
7. After valid registration/sign-in, RTDB `/users/{uid}` contains userId/playerId/private email/general/platform/device metadata; no password fields exist anywhere.
8. Close/reopen app: Firebase Auth session remains signed in and the same canonical display name appears in menu/lobby/match.
9. Password-reset request sends through Firebase Auth.
10. Rename once succeeds; immediate second rename is rejected/cooldown feedback. Server rule deployment is required before calling the 24h gate live.
11. PRIVATE TABLE player count 4: choose one extra Local Player and one explicit Bot, leave one seat unresolved. Start must fill only the unresolved seat with Bot; it must never become Human.
12. Start shows visible 5,4,3,2,1 before gameplay.
13. Mobile gameplay: Roll position accepted; Trade is left of P2 HUD and readable; tablet/trade text is touch-legible; pinch/pan/rotate and city vertical scrolling work.
14. Exit match -> Main Menu -> start second match -> start third match. Modern tablet presentations and City Info must work on every match; no legacy tablet path may appear.
15. Keep FPS counter/logging active for >=60 seconds. Record average FPS after the v5.30 mobile profile and whether emergency tier activates.
16. Desktop/Steam Editor smoke after Android acceptance: no mobile action buttons, no mobile UI repositioning and no mobile graphics override.

### Still pending after this checkpoint
- Full RTDB Private Casual two-device transport implementation/acceptance.
- Migration of remaining non-sensitive general/preferences reads from Firestore to RTDB after cross-device tests.
- Production App Check enforcement and rewarded online anti-cheat authority.
- Release AAB performance/size optimization and APK/AAB size reduction.

---

## Phase 17E.3 — v5.30.1 handoff / Unity 6 Android compile blocker
**Status: SOURCE FIX PREPARED — UNITY COMPILE AND DEVICE ACCEPTANCE PENDING.**

Observed blocker after v5.30.0 integration:
- `AtlasBoardMobileGameplayUX.cs(168,15)` used deprecated implicit `SceneHandle -> int` conversion under Unity 6000.5.6f1.
- v5.30.1 uses `SceneHandle.GetRawData()` instead; no gameplay semantics are changed.
- the additional red Console entries (`Error building Player`, `APK_FAIL result`, `APK_FAIL`) are downstream build-failure reports, not three additional source defects.
- Android Development smoke build temporarily requests `DebugSymbolLevel.SymbolTable` and restores the owner's previous symbol setting after the build so Diagnostics Data can resolve stack traces.

Known non-blocking warnings still OPEN and must be cleaned from their canonical full source files in the next chat, not hidden with global `nowarn`:
- `AtlasBoardLoadingExperience.cs`: obsolete `TMP_Text.enableWordWrapping` -> migrate to `textWrappingMode`.
- `AtlasBoardLobbyRuntimeBridge.cs`: Editor-only `useLocalEmulatorsInEditor` produces Android player `CS0414`; isolate the serialized field to Editor compilation without weakening emulator/release separation.

### Canonical account / data architecture carried forward
- Firebase project: `atlasboard-usa`; Spark/free-first. No paid Blaze activation without owner approval.
- Firestore rules and RTDB rules have already been deployed by the owner. Do not redeploy merely as part of this handoff.
- RTDB is the preferred low-cost home for general/cached/live/ephemeral data and future private-casual transport.
- valuable authoritative outcomes remain protected: XP, wallet/currency, purchase entitlements, permanent progression, verification and rewarded/ranked finalization must not become arbitrary client RTDB writes.
- requested canonical RTDB user shape: `/users/{uid}` contains stable `userId`, `playerId`, canonical email/display name/general data, with nested general and `/platforms/{platform}/devices/{deviceId}` data for multi-device/multi-platform support. Passwords are never stored in RTDB, Firestore, PlayerPrefs or logs; Firebase Auth owns credentials.
- authenticated display name is canonical across menu/lobby/match/career/store; rename target is max once per 24 hours with server-side enforcement, not client UI alone.
- unsigned players may play as persistent `Guest#####X`; guest/local-only play earns no permanent XP/win/achievement/wallet/rewarded history.

### v5.30.0 integrated features awaiting one consolidated Android acceptance build
- mobile Shop/Career visual differentiation;
- larger mobile trade/tablet interaction and Trade button moved away from lower-right HUD;
- Private Table modern `+ -> Local Player / Bot` seat flow; unresolved local-offline seats must become Bots, never surprise Local Humans;
- visible 5 -> 1 start countdown;
- modern tablet / City Info / mobile presentation must survive leave-to-menu then second/third match scene cycles;
- Android-only FPS/render profile must not alter Windows/Steam settings; previous S20 FE evidence was about 16–23 FPS and remains a P0 performance issue until retested;
- persistent guest identity and optional Firebase Auth account UX: login/register/password reset/session persistence, duplicate-email handling, submit lock/loading, simple addition/subtraction human-check UX, canonical profile naming;
- Career unsigned state should offer sign-in instead of hanging on `Loading career data...`; Settings should also expose account access;
- user/profile RTDB mirror architecture for general/platform/device metadata.

### Explicitly NOT IMPLEMENTED / still pending
- **RTDB Private Casual two-real-device multiplayer transport is NOT implemented yet.** No claim of remote RTDB multiplayer acceptance.
- migration from legacy polling (`0.25 s` host-intent / `0.5 s` per-player snapshots) to event-driven RTDB transport is pending.
- Online Rewarded production transport / server-authoritative reward verification remains pending.
- production Play Integrity/App Check enforcement proof remains pending; registration alone is not enforcement proof.
- Firebase Cloud Functions are not deployed in `atlasboard-usa`; paid Blaze is not authorized.
- full physical multi-device multiplayer, Google Play release AAB, final performance/device matrix and Steam production trust remain pending.
