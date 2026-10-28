using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AtlasBoardEmoteSpriteEntry { public string id; public Sprite sprite; }

// One registry asset refers to the original imported Kenney sprites. No duplicate PNGs.
public sealed class AtlasBoardEmoteSpriteCatalog : ScriptableObject
{
    public List<AtlasBoardEmoteSpriteEntry> entries = new List<AtlasBoardEmoteSpriteEntry>();
    public Sprite Find(string id)
    {
        foreach (var entry in entries)
            if (entry != null && entry.id == id) return entry.sprite;
        return null;
    }
}

public static class AtlasBoardEmotes
{
    [Serializable]
    public sealed class EmoteDefinition
    {
        public readonly string Id; public readonly int Gold;
        public readonly string[] Names;
        public EmoteDefinition(string id, int gold, params string[] names)
        { Id = id; Gold = gold; Names = names; }
        public bool IsFree => Gold == 0;
    }

    private static readonly EmoteDefinition[] Catalog =
    {
        new EmoteDefinition("emote_faceHappy", 0, "Happy", "Mutlu", "Feliz", "Heureux", "Glücklich", "행복", "Радость"),
        new EmoteDefinition("emote_faceSad", 0, "Sad", "Üzgün", "Triste", "Triste", "Traurig", "슬픔", "Грусть"),
        new EmoteDefinition("emote_faceAngry", 0, "Angry", "Kızgın", "Enfadado", "En colère", "Wütend", "화남", "Злость"),
        new EmoteDefinition("emote_alert", 150, "Alert", "Uyarı", "Alerta", "Alerte", "Warnung", "경고", "Тревога"),
        new EmoteDefinition("emote_anger", 150, "Rage", "Öfke", "Ira", "Colère", "Wut", "분노", "Ярость"),
        new EmoteDefinition("emote_bars", 150, "Jail", "Parmaklık", "Rejas", "Barreaux", "Gitter", "철창", "Решётка"),
        new EmoteDefinition("emote_cash", 190, "Cash", "Para", "Dinero", "Argent", "Geld", "현금", "Деньги"),
        new EmoteDefinition("emote_circle", 150, "Circle", "Daire", "Círculo", "Cercle", "Kreis", "원", "Круг"),
        new EmoteDefinition("emote_cloud", 150, "Cloud", "Bulut", "Nube", "Nuage", "Wolke", "구름", "Облако"),
        new EmoteDefinition("emote_cross", 150, "No", "Hayır", "No", "Non", "Nein", "아니요", "Нет"),
        new EmoteDefinition("emote_dots1", 150, "Pause", "Duraksama", "Pausa", "Pause", "Pause", "잠깐", "Пауза"),
        new EmoteDefinition("emote_dots2", 150, "Waiting", "Bekliyorum", "Esperando", "Attente", "Warten", "대기", "Ожидание"),
        new EmoteDefinition("emote_dots3", 150, "Thinking", "Düşünüyorum", "Pensando", "Réflexion", "Nachdenken", "생각 중", "Думаю"),
        new EmoteDefinition("emote_drop", 150, "Sweat", "Ter", "Sudor", "Sueur", "Schweiß", "땀", "Пот"),
        new EmoteDefinition("emote_drops", 150, "Nervous", "Gergin", "Nervioso", "Nerveux", "Nervös", "긴장", "Нервничаю"),
        new EmoteDefinition("emote_exclamation", 150, "Surprise", "Şaşkın", "Sorpresa", "Surprise", "Überraschung", "놀람", "Сюрприз"),
        new EmoteDefinition("emote_exclamations", 150, "Shocked", "Şok", "Impactado", "Choqué", "Schockiert", "충격", "Шок"),
        new EmoteDefinition("emote_heart", 180, "Love", "Sevgi", "Amor", "Amour", "Liebe", "사랑", "Любовь"),
        new EmoteDefinition("emote_heartBroken", 180, "Broken Heart", "Kırık Kalp", "Corazón roto", "Cœur brisé", "Gebrochenes Herz", "실연", "Разбитое сердце"),
        new EmoteDefinition("emote_hearts", 150, "Hearts", "Kalpler", "Corazones", "Cœurs", "Herzen", "하트", "Сердца"),
        new EmoteDefinition("emote_idea", 165, "Idea", "Fikir", "Idea", "Idée", "Idee", "아이디어", "Идея"),
        new EmoteDefinition("emote_laugh", 165, "Laugh", "Kahkaha", "Risa", "Rire", "Lachen", "웃음", "Смех"),
        new EmoteDefinition("emote_music", 175, "Music", "Müzik", "Música", "Musique", "Musik", "음악", "Музыка"),
        new EmoteDefinition("emote_question", 150, "Question", "Soru", "Pregunta", "Question", "Frage", "질문", "Вопрос"),
        new EmoteDefinition("emote_sleep", 150, "Sleep", "Uyku", "Dormir", "Dormir", "Schlafen", "잠", "Сон"),
        new EmoteDefinition("emote_sleeps", 150, "Sleepy", "Uykulu", "Somnoliento", "Somnolent", "Müde", "졸림", "Сонливость"),
        new EmoteDefinition("emote_star", 175, "Star", "Yıldız", "Estrella", "Étoile", "Stern", "별", "Звезда"),
        new EmoteDefinition("emote_stars", 180, "Stars", "Yıldızlar", "Estrellas", "Étoiles", "Sterne", "별들", "Звёзды"),
        new EmoteDefinition("emote_swirl", 150, "Dizzy", "Başım Döndü", "Mareo", "Vertige", "Schwindel", "어지러움", "Головокружение"),
    };
    private static AtlasBoardEmoteSpriteCatalog sprites;
    public static readonly string[] FreeIds = { "emote_faceHappy", "emote_faceSad", "emote_faceAngry" };
    public static IReadOnlyList<EmoteDefinition> All => Catalog;
    public static bool IsEmote(string id) => Find(id) != null;
    public static bool IsPaid(string id) { var e = Find(id); return e != null && !e.IsFree; }
    public static EmoteDefinition Find(string id)
    {
        foreach (var e in Catalog) if (e.Id == id) return e;
        return null;
    }
    public static int Price(string id) { var e = Find(id); return e != null ? e.Gold : 0; }
    public static string Name(string id, string lang)
    {
        var e = Find(id); if (e == null) return id;
        return e.Names[Mathf.Clamp(AtlasBoardLocalizationLanguages.IndexOf(lang), 0, 6)];
    }
    public static Sprite Sprite(string id)
    {
        if (sprites == null) sprites = Resources.Load<AtlasBoardEmoteSpriteCatalog>("AtlasBoard/EmoteSpriteCatalog");
        return sprites != null ? sprites.Find(id) : null;
    }
}
