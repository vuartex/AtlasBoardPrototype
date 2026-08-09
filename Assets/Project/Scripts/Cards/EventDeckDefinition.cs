using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EventDeck_New",
    menuName = "Atlas Board/Cards/Event Deck")]
public class EventDeckDefinition : ScriptableObject
{
    [SerializeField]
    private string deckId = "event_deck_default";

    [SerializeField]
    private string displayName = "Event Deck";

    [SerializeField]
    private EventCardDefinition[] cards;

    public string DeckId => deckId;
    public string DisplayName => displayName;
    public IReadOnlyList<EventCardDefinition> Cards =>
        cards;

#if UNITY_EDITOR
    public void EditorConfigure(
        string newDeckId,
        string newDisplayName,
        EventCardDefinition[] newCards)
    {
        deckId = newDeckId;
        displayName = newDisplayName;
        cards = newCards;
    }
#endif
}
