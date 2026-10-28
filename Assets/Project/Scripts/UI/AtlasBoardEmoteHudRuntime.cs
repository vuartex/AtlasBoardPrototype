using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuses existing PlayerHudPanel clicks and a single reusable popup owner.
// Never touches player movement, pawn positions, HUD layout or dice outcome.
public sealed class AtlasBoardEmoteHudRuntime : MonoBehaviour
{
    private static AtlasBoardEmoteHudRuntime instance;
    private PlayerHudPanel activeHud;
    private GameObject popup;
    private readonly Dictionary<string,long> lastSeq = new Dictionary<string,long>();
    private readonly Dictionary<string,long> localLastSent = new Dictionary<string,long>();
    private readonly Dictionary<string,int> localWindowCount = new Dictionary<string,int>();
    private readonly Dictionary<string,long> localWindowStart = new Dictionary<string,long>();
    private readonly Dictionary<string,long> localMute = new Dictionary<string,long>();
    private float nextPoll;
    private bool polling;
    private string watchedRoom = "";
    private string currentLocalUid = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureReceiver()
    {
        if (instance != null) return;
        GameObject owner = new GameObject("AtlasBoard_EmoteHudRuntime");
        instance = owner.AddComponent<AtlasBoardEmoteHudRuntime>();
    }

    public static async void OpenFor(PlayerHudPanel panel)
    {
        if (panel == null || panel.Player == null || !panel.Player.IsParticipating)
            return;

        TurnManager match = UnityEngine.Object.FindAnyObjectByType<TurnManager>();
        if (match == null || !match.IsMatchStarted)
        {
            Debug.Log("ATB_EMOTE_PICKER status=blocked reason=match_not_started");
            return;
        }

        int slotIndex = panel.Player.PlayerSlotIndex;
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();
        if (coordinator != null && coordinator.IsOnlineSessionActive &&
            !coordinator.IsLocallyControlledHumanSlot(slotIndex))
        {
            Debug.Log("ATB_EMOTE_PICKER status=blocked reason=not_local_owner slot=" + slotIndex);
            return;
        }

        string kind = panel.Player.OnlineControllerKind ?? "";
        if (kind.IndexOf("bot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (panel.GetComponent<BotPlayerController>() != null &&
             panel.GetComponent<BotPlayerController>().enabled))
        {
            Debug.Log("ATB_EMOTE_PICKER status=blocked reason=bot slot=" + slotIndex);
            return;
        }

        if (instance == null)
        {
            GameObject owner = new GameObject("AtlasBoard_EmoteHudRuntime");
            instance = owner.AddComponent<AtlasBoardEmoteHudRuntime>();
        }
        AtlasBoardEmoteHudRuntime runtime = instance;
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        if (uid != runtime.currentLocalUid)
        {
            runtime.currentLocalUid = uid;
            AtlasBoardEmoteLoadoutService.Invalidate();
        }

        // Present cached selections immediately. Previously the HUD appeared
        // unresponsive until an RTDB GET completed. That read may be delayed.
        if (!panel.gameObject.activeInHierarchy || !runtime.IsMatchLive())
            return;
        runtime.OpenPopup(panel);

        try
        {
            await AtlasBoardEmoteLoadoutService.LoadAsync();
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_EMOTE_PICKER status=load_failed error=" +
                error.GetType().Name);
            return;
        }

        // Do not re-open a popup dismissed during the network request, or one
        // from a different player/UID after a match/account switch.
        if (runtime != null && instance == runtime &&
            runtime.popup != null && runtime.activeHud == panel &&
            runtime.currentLocalUid == AtlasBoardRtdbMetaEconomy.CurrentUid &&
            panel != null && panel.gameObject.activeInHierarchy &&
            runtime.IsMatchLive())
            runtime.OpenPopup(panel);
    }
    private bool IsMatchLive()
    {
        TurnManager manager=FindAnyObjectByType<TurnManager>();
        return manager!=null && manager.IsMatchStarted;
    }
    private void Update()
    {
        if (!IsMatchLive()) { ClosePopup(); watchedRoom=""; lastSeq.Clear(); return; }
        if (popup != null && (activeHud == null || !activeHud.gameObject.activeInHierarchy)) ClosePopup();
        AtlasBoardLobbyRuntimeBridge bridge=FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
        string room=bridge!=null && bridge.HasLobby ? bridge.CurrentLobbyId : "";
        if (room != watchedRoom) { watchedRoom=room; lastSeq.Clear(); nextPoll=Time.unscaledTime+0.2f; }
        if (!string.IsNullOrEmpty(room) && !polling && Time.unscaledTime>=nextPoll)
        {
            nextPoll=Time.unscaledTime+1.2f;
            PollAsync(room);
        }
    }
    private async void PollAsync(string room)
    {
        polling=true;
        try
        {
            var events=await AtlasBoardEmoteNetwork.ReadRoomAsync(room);
            if (room!=watchedRoom || !IsMatchLive()) return;
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AtlasBoardLobbyRuntimeBridge bridge=FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
            foreach (var pair in events)
            {
                string key=room+":"+pair.Key;
                if (lastSeq.TryGetValue(key,out long seq) && pair.Value.seq<=seq) continue;
                lastSeq[key]=pair.Value.seq;
                // The sender already drew its own bubble at submit time.
                if (pair.Key == AtlasBoardRtdbMetaEconomy.CurrentUid) continue;
                if (now-pair.Value.atMs>2500 || pair.Value.atMs>now+5000) continue;
                var member=bridge!=null && bridge.CurrentSnapshot!=null && bridge.CurrentSnapshot.Members!=null
                    ? bridge.CurrentSnapshot.Members.Find(m=>m!=null && m.Active && m.AccountId==pair.Key) : null;
                if (member==null) continue;
                foreach (var h in FindObjectsByType<PlayerHudPanel>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                    if (h.Player!=null && h.Player.PlayerSlotIndex==member.SlotIndex)
                    { ShowBubble(h,pair.Value.emoteId); break; }
            }
        }
        finally { polling=false; }
    }
    private static GameObject UI(Transform parent,string name, Vector2 size,Vector2 position,Color color)
    {
        GameObject obj = new GameObject(name,typeof(RectTransform),typeof(Image));
        obj.transform.SetParent(parent,false);
        RectTransform rect=(RectTransform)obj.transform;
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.pivot=new Vector2(.5f,.5f);
        rect.sizeDelta=size;rect.anchoredPosition=position;
        obj.GetComponent<Image>().color=color;
        return obj;
    }
    private static TMP_Text Label(Transform parent,string text,int size, Vector2 pos,Vector2 box)
    {
        GameObject obj=new GameObject("EmoteLabel",typeof(RectTransform),typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent,false);
        var rect=(RectTransform)obj.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.sizeDelta=box;rect.anchoredPosition=pos;
        TMP_Text tmp=obj.GetComponent<TMP_Text>();tmp.fontSize=size;tmp.alignment=TextAlignmentOptions.Center;
        tmp.color=Color.white;tmp.text=text;tmp.raycastTarget=false;return tmp;
    }
    private static Vector2 AnchorFor(PlayerHudPanel hud, Canvas canvas, float boxHeight, bool above)
    {
        RectTransform canvasRect=canvas.transform as RectTransform;
        RectTransform target=hud.PanelRect;
        Vector3[] corners=new Vector3[4];target.GetWorldCorners(corners);
        Vector3 bottomLeft=canvasRect.InverseTransformPoint(corners[0]);
        Vector3 topLeft=canvasRect.InverseTransformPoint(corners[1]);
        Vector2 p=new Vector2(bottomLeft.x+145f,above ? topLeft.y+boxHeight*.5f+10f : bottomLeft.y-boxHeight*.5f-10f);
        Rect bounds=canvasRect.rect;
        p.x=Mathf.Clamp(p.x,bounds.xMin+150,bounds.xMax-150);
        p.y=Mathf.Clamp(p.y,bounds.yMin+boxHeight*.5f+5,bounds.yMax-boxHeight*.5f-5);
        return p;
    }
    private void OpenPopup(PlayerHudPanel hud)
    {
        ClosePopup();
        Canvas canvas=hud.GetComponentInParent<Canvas>();
        if(canvas==null)
        {
            Debug.LogWarning("ATB_EMOTE_PICKER status=blocked reason=canvas_missing");
            return;
        }
        activeHud=hud;
        RectTransform c=canvas.transform as RectTransform;
        bool above= c.InverseTransformPoint(hud.PanelRect.position).y < 0;
        popup=UI(canvas.transform,"EmotePicker",new Vector2(312,196),AnchorFor(hud,canvas,196,above),new Color32(18,29,48,244));
        popup.transform.SetAsLastSibling();
        Label(popup.transform,"EMOTES",15,new Vector2(0,76),new Vector2(230,25));
        var active=AtlasBoardEmoteLoadoutService.Active.Take(6).ToArray();
        for(int i=0;i<active.Length;i++)
        {
            string chosen=active[i];
            var b=UI(popup.transform,"Select_"+chosen,new Vector2(88,66),
                new Vector2(-98+(i%3)*98, 26-(i/3)*73),new Color32(42,85,124,255));
            Button button=b.AddComponent<Button>();button.targetGraphic=b.GetComponent<Image>();
            AtlasBoardEmotePreviewArt.Create(b.transform,chosen,52);
            button.onClick.AddListener(()=>Send(chosen));
        }
        var close=UI(popup.transform,"Close",new Vector2(30,28),new Vector2(135,78),new Color32(160,37,75,255));
        Label(close.transform,"×",22,Vector2.zero,new Vector2(28,26));
        close.AddComponent<Button>().onClick.AddListener(ClosePopup);
        Debug.Log("ATB_EMOTE_PICKER status=open slot=" + hud.Player.PlayerSlotIndex +
            " count=" + active.Length);
    }
    private void ClosePopup()
    {
        if(popup!=null)Destroy(popup);
        popup=null;activeHud=null;
    }
    private async void Send(string id)
    {
        PlayerHudPanel hud=activeHud;
        if(hud==null || !IsMatchLive() || !AtlasBoardEmoteLoadoutService.Active.Contains(id))return;
        string uid=AtlasBoardRtdbMetaEconomy.CurrentUid;
        if(string.IsNullOrEmpty(uid))uid="local";
        long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if(localMute.TryGetValue(uid,out long mute) && now<mute){Warn("muted");return;}
        if(localLastSent.TryGetValue(uid,out long sent) && now-sent<3000){Warn("cooldown");return;}
        long start=localWindowStart.TryGetValue(uid,out long s)?s:0;
        int count=localWindowCount.TryGetValue(uid,out int n)?n:0;
        if(now-start>=180000){start=now;count=0;}
        if(count>=20){localMute[uid]=now+180000;Warn("muted");return;}
        AtlasBoardLobbyRuntimeBridge bridge=FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
        bool online=bridge!=null && bridge.HasLobby;
        if(online)
        {
            string reply=await AtlasBoardEmoteNetwork.SendAsync(bridge.CurrentLobbyId,id);
            if(reply!="ok"){Warn(reply);return;}
        }
        localLastSent[uid]=now;localWindowStart[uid]=start;localWindowCount[uid]=++count;
        if(count==20)localMute[uid]=now+180000;
        ShowBubble(hud,id);ClosePopup();
        Debug.Log("ATB_EMOTE_SENT mode="+(online?"online":"local")+" emote="+id);
    }
    private void Warn(string reason)
    {
        if(popup==null)return;
        string lang=AtlasBoardLocalizationManager.Instance!=null ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        int language = AtlasBoardLocalizationLanguages.IndexOf(lang);
        string[] cooldown = { "Wait 3 seconds.", "3 saniye bekle.", "Espera 3 segundos.",
            "Attendez 3 secondes.", "Warte 3 Sekunden.", "3초 기다려 주세요.", "Подождите 3 секунды." };
        string[] muted = { "Too many emotes. Take a 3-minute break.",
            "Çok fazla emote kullandın. 3 dakika ara ver.", "Demasiados emotes. Pausa de 3 minutos.",
            "Trop d'emotes. Pause de 3 minutes.", "Zu viele Emotes. 3 Minuten Pause.",
            "이모트를 너무 많이 보냈습니다. 3분간 쉬세요.", "Слишком много эмоций. Пауза 3 минуты." };
        string[] fail = { "Emote could not be sent.", "Emote gönderilemedi.", "No se pudo enviar el emote.",
            "Envoi impossible.", "Emote konnte nicht gesendet werden.", "이모트를 보낼 수 없습니다.",
            "Не удалось отправить эмоцию." };
        string text = reason == "cooldown" ? cooldown[language] : reason == "muted" ? muted[language] : fail[language];
        Label(popup.transform,text,14,new Vector2(0,-77),new Vector2(305,35));
    }
    private void ShowBubble(PlayerHudPanel hud,string id)
    {
        if(hud==null||!hud.gameObject.activeInHierarchy)return;
        Canvas canvas=hud.GetComponentInParent<Canvas>();if(canvas==null)return;
        RectTransform c=canvas.transform as RectTransform;
        bool above=c.InverseTransformPoint(hud.PanelRect.position).y<0;
        // The Kenney emote sprite already contains its own balloon backdrop.
        // Keep the anchored container completely transparent: an opaque Image
        // would create the unwanted white square behind the sprite.
        GameObject obj=UI(canvas.transform,"EmoteBubble_"+id,new Vector2(88,88),AnchorFor(hud,canvas,88,above),Color.clear);
        Image plate=obj.GetComponent<Image>();
        if (plate != null) plate.raycastTarget=false;
        AtlasBoardEmotePreviewArt.Create(obj.transform,id,76f);
        obj.transform.SetAsLastSibling();
        Destroy(obj,2.5f);
    }
    private void OnDestroy(){ if(instance==this)instance=null; }
}
