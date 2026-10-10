using System;
using System.Reflection;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(14600)]
[DisallowMultipleComponent]
public sealed class AtlasBoardSpecialLocalizationRefresh :
    MonoBehaviour
{
    private SpecialTileManager specialTileManager;
    private EventCardManager eventCardManager;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardSpecialLocalizationRefresh>() != null)
        {
            return;
        }

        new GameObject(
                "AtlasBoard_Phase14K_SpecialLocalizationRefresh")
            .AddComponent<
                AtlasBoardSpecialLocalizationRefresh>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        ResolveReferences();
        RefreshSpecialTilePresentation();
        RefreshEventPresentation();
    }

    private void ResolveReferences()
    {
        if (specialTileManager == null)
        {
            specialTileManager =
                UnityEngine.Object.FindAnyObjectByType<
                    SpecialTileManager>();
        }

        if (eventCardManager == null)
        {
            eventCardManager =
                UnityEngine.Object.FindAnyObjectByType<
                    EventCardManager>();
        }
    }

    private void RefreshSpecialTilePresentation()
    {
        if (specialTileManager == null ||
            !specialTileManager.IsResolvingSpecialTile)
        {
            return;
        }

        TMP_Text title =
            ReadPrivateField<TMP_Text>(
                specialTileManager,
                "specialTitleText");

        TMP_Text description =
            ReadPrivateField<TMP_Text>(
                specialTileManager,
                "specialDescriptionText");

        TMP_Text result =
            ReadPrivateField<TMP_Text>(
                specialTileManager,
                "specialResultText");

        string kind =
            specialTileManager
                .OnlinePresentationKind ??
            string.Empty;

        if (string.Equals(
                kind,
                "tax",
                StringComparison.Ordinal))
        {
            SetText(
                title,
                AtlasBoardL.T(
                    "special.tax.title"));

            SetText(
                description,
                AtlasBoardL.T(
                    "special.tax.description"));

            SetText(
                result,
                BuildMoneyResult(
                    specialTileManager
                        .OnlineValue0,
                    specialTileManager
                        .OnlineValue1 != 0,
                    specialTileManager
                        .OnlineValue2,
                    specialTileManager
                        .OnlineValue3));

            return;
        }

        if (string.Equals(
                kind,
                "bonus",
                StringComparison.Ordinal))
        {
            SetText(
                title,
                AtlasBoardL.T(
                    "special.bonus.title"));

            SetText(
                description,
                AtlasBoardL.T(
                    "special.bonus.description"));

            SetText(
                result,
                BuildMoneyResult(
                    specialTileManager
                        .OnlineValue0,
                    specialTileManager
                        .OnlineValue1 != 0,
                    specialTileManager
                        .OnlineValue2,
                    specialTileManager
                        .OnlineValue3));

            return;
        }

        if (string.Equals(
                kind,
                "vacation",
                StringComparison.Ordinal))
        {
            SetText(
                title,
                AtlasBoardL.T(
                    "special.vacation.title"));

            SetText(
                description,
                AtlasBoardL.T(
                    "special.vacation.description"));

            SetText(
                result,
                BuildMoneyResult(
                    specialTileManager
                        .OnlineValue0,
                    specialTileManager
                        .OnlineValue1 != 0,
                    specialTileManager
                        .OnlineValue2,
                    specialTileManager
                        .OnlineValue3));

            return;
        }

        if (string.Equals(
                kind,
                "rest",
                StringComparison.Ordinal))
        {
            int turns =
                Mathf.Max(
                    1,
                    specialTileManager
                        .OnlineValue0);

            SetText(
                title,
                AtlasBoardL.T(
                    "special.rest.title"));

            SetText(
                description,
                turns == 1
                    ? AtlasBoardL.T(
                        "special.rest.skip_one")
                    : AtlasBoardL.T(
                        "special.rest.skip_many",
                        turns));

            SetText(
                result,
                AtlasBoardL.T(
                    "special.result.no_money_change"));

            return;
        }

        if (string.Equals(
                kind,
                "rent_bankrupt",
                StringComparison.Ordinal))
        {
            RefreshRentBankruptcy(
                title,
                description,
                result);
        }
    }

    private void RefreshRentBankruptcy(
        TMP_Text title,
        TMP_Text description,
        TMP_Text result)
    {
        PlayerGameState debtor =
            specialTileManager
                .CurrentSpecialPlayer;

        BoardTile tile =
            ResolveTile(
                specialTileManager
                    .OnlineContextTileIndex);

        PlayerGameState owner =
            ResolvePlayer(
                specialTileManager
                    .OnlineContextPlayerSlotIndex);

        SetText(
            title,
            AtlasBoardL.T(
                "rent.bankrupt.title"));

        SetText(
            description,
            AtlasBoardL.T(
                "rent.bankrupt.description",
                AtlasBoardL.PlayerName(
                    debtor),
                tile != null
                    ? tile.DisplayName
                    : AtlasBoardL.T(
                        "tablet.property"),
                Mathf.Max(
                    0,
                    specialTileManager
                        .OnlineValue0),
                AtlasBoardL.PlayerName(
                    owner)));

        SetText(
            result,
            SpecialTileManager
                .BuildRentBankruptcyResult(
                    specialTileManager
                        .OnlineValue1,
                    Mathf.Max(
                        0,
                        specialTileManager
                            .OnlineValue0 -
                        specialTileManager
                            .OnlineValue1),
                    specialTileManager
                        .OnlineValue2,
                    specialTileManager
                        .OnlineValue3));
    }

    private void RefreshEventPresentation()
    {
        if (eventCardManager == null ||
            !eventCardManager.IsResolvingEvent ||
            eventCardManager.CurrentCard == null)
        {
            return;
        }

        TMP_Text title =
            ReadPrivateField<TMP_Text>(
                eventCardManager,
                "eventTitleText");

        TMP_Text description =
            ReadPrivateField<TMP_Text>(
                eventCardManager,
                "eventDescriptionText");

        TMP_Text result =
            ReadPrivateField<TMP_Text>(
                eventCardManager,
                "eventResultText");

        SetText(
            title,
            eventCardManager
                .CurrentCard
                .Title);

        SetText(
            description,
            eventCardManager
                .CurrentCard
                .Description);

        SetText(
            result,
            eventCardManager
                .EffectExecutionCompleted
                ? BuildEventResult(
                    eventCardManager)
                : AtlasBoardL.T(
                    "event.applying"));
    }

    private static string BuildEventResult(
        EventCardManager manager)
    {
        string kind =
            manager.OnlineResultKind ??
            string.Empty;

        if (string.Equals(
                kind,
                "money",
                StringComparison.Ordinal))
        {
            if (manager.OnlineResultValue1 !=
                0)
            {
                return AtlasBoardL.T(
                    "event.bankrupt_result",
                    Mathf.Abs(
                        manager.OnlineResultValue0),
                    manager.OnlineResultValue2);
            }

            if (manager.OnlineResultValue0 >
                0)
            {
                return
                    $"+{manager.OnlineResultValue0} ₵";
            }

            if (manager.OnlineResultValue0 <
                0)
            {
                return
                    $"{manager.OnlineResultValue0} ₵";
            }

            return AtlasBoardL.T(
                "event.money_no_change");
        }

        if (string.Equals(
                kind,
                "skip",
                StringComparison.Ordinal))
        {
            return manager.OnlineResultValue0 ==
                   1
                ? AtlasBoardL.T(
                    "event.skip_one")
                : AtlasBoardL.T(
                    "event.skip_many",
                    Mathf.Max(
                        1,
                        manager
                            .OnlineResultValue0));
        }

        if (string.Equals(
                kind,
                "move_done_tile",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.move_done_tile",
                manager.OnlineResultValue0,
                AtlasBoardL.TileName(
                    (TileType)manager
                        .OnlineResultValue1,
                    manager
                        .OnlineResultTextValue));
        }

        if (string.Equals(
                kind,
                "move_done",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.move_done",
                manager.OnlineResultValue0);
        }

        if (string.Equals(
                kind,
                "target_done_tile",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.target_done_tile",
                AtlasBoardL.TileName(
                    (TileType)manager
                        .OnlineResultValue0,
                    manager
                        .OnlineResultTextValue));
        }

        if (string.Equals(
                kind,
                "target_done",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.target_done");
        }

        if (string.Equals(
                kind,
                "target_not_found",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.target_not_found");
        }

        if (string.Equals(
                kind,
                "move_failed",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.move_failed");
        }

        if (string.Equals(
                kind,
                "effect_none",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.effect_none");
        }

        return AtlasBoardL.T(
            "event.applying");
    }

    private static string BuildMoneyResult(
        int appliedMoneyChange,
        bool causedBankruptcy,
        int transferredProperties,
        int releasedProperties)
    {
        if (causedBankruptcy)
        {
            return AtlasBoardL.R(
                "BANKRUPT\nPaid: {0} ₵\nTransferred properties: {1}\nReleased properties: {2}",
                "İFLAS\nÖdenen: {0} ₵\nDevredilen mülk: {1}\nBoşa çıkan mülk: {2}",
                "BANCARROTA\nPagado: {0} ₵\nPropiedades transferidas: {1}\nPropiedades liberadas: {2}",
                "FAILLITE\nPayé : {0} ₵\nPropriétés transférées : {1}\nPropriétés libérées : {2}",
                "INSOLVENT\nBezahlt: {0} ₵\nÜbertragene Grundstücke: {1}\nFreigegebene Grundstücke: {2}",
                "파산\n지불: {0} ₵\n이전된 부동산: {1}\n해제된 부동산: {2}",
                "БАНКРОТ\nОплачено: {0} ₵\nПередано объектов: {1}\nОсвобождено объектов: {2}",
                Mathf.Abs(
                    appliedMoneyChange),
                transferredProperties,
                releasedProperties);
        }

        if (appliedMoneyChange >
            0)
        {
            return
                $"+{appliedMoneyChange} ₵";
        }

        if (appliedMoneyChange <
            0)
        {
            return
                $"{appliedMoneyChange} ₵";
        }

        return AtlasBoardL.T(
            "special.result.no_money_change");
    }

    private static BoardTile ResolveTile(
        int tileIndex)
    {
        BoardPath board =
            UnityEngine.Object.FindAnyObjectByType<
                BoardPath>();

        if (board == null ||
            tileIndex < 0 ||
            tileIndex >=
                board.TileCount)
        {
            return null;
        }

        return board.GetTile(
            tileIndex);
    }

    private static PlayerGameState ResolvePlayer(
        int slotIndex)
    {
        TurnManager turns =
            UnityEngine.Object.FindAnyObjectByType<
                TurnManager>();

        return turns != null &&
               slotIndex >= 0
            ? turns.GetPlayerStateBySlotIndex(
                slotIndex)
            : null;
    }

    private static void SetText(
        TMP_Text text,
        string value)
    {
        if (text == null)
        {
            return;
        }

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        TMP_FontAsset current =
            text.font != null
                ? text.font
                : TMP_Settings.defaultFontAsset;

        TMP_FontAsset resolved =
            manager != null
                ? manager.ResolveFont(
                    current)
                : current;

        if (resolved != null)
        {
            text.font =
                resolved;
        }

        text.text =
            value;
    }

    private static T ReadPrivateField<T>(
        object target,
        string fieldName)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field =
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        return field != null
            ? field.GetValue(
                target) as T
            : null;
    }
}
