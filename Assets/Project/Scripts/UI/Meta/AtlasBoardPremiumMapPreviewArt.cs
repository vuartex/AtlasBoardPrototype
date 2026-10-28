using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AtlasBoardPremiumMapPreviewArt
{
    public static void Create(RectTransform parent, string mapItemId)
    {
        bool germany = mapItemId == "map_germany";
        string[] names = germany ? new [] { "BERLIN", "MUNICH", "HAMBURG", "FRANKFURT" }
                                  : new [] { "MEXICO CITY", "GUADALAJARA", "MONTERREY", "PUEBLA" };
        Color baseColor = germany ? new Color32(22,45,74,255) : new Color32(32,76,52,255);
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("MapTile_"+i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
            rt.sizeDelta = new Vector2(106f,70f);
            rt.anchoredPosition = new Vector2((i%2==0?-1f:1f)*59f, (i<2?1f:-1f)*41f);
            go.GetComponent<Image>().color = baseColor;
            var t = new GameObject("City",typeof(RectTransform),typeof(TextMeshProUGUI));
            t.transform.SetParent(go.transform,false);
            var r=t.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;
            r.offsetMin=new Vector2(2f,2f);r.offsetMax=new Vector2(-2f,-2f);
            var label=t.GetComponent<TextMeshProUGUI>();label.text=names[i];label.fontSize=13f;
            label.color=Color.white;label.alignment=TextAlignmentOptions.Center;
            label.raycastTarget=false;
        }
    }
}
