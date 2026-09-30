using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase12EDevelopedBankruptcyE2ETool
{
    private const int CreditorSlotIndex = 0;
    private const int DebtorSlotIndex = 1;
    private const int SurvivorSlotIndex = 2;

    private static readonly List<int>
        preparedTileIndices =
            new List<int>();

    [MenuItem(
        "Atlas Board/QA/Phase 12E/1 Prepare Developed Bankruptcy Fixture")]
    public static void PrepareFixture()
    {
        if (!TryResolveRuntime(
                out AtlasBoardTurnDiceNetworkCoordinator coordinator,
                out TurnManager turnManager,
                out BoardPath boardPath,
                out PropertyDevelopmentManager development,
                out BankruptcyManager bankruptcy))
        {
            return;
        }

        _ = bankruptcy;

        if (!coordinator.LocalIsHost)
        {
            Debug.LogError(
                "Phase 12E QA must be run from the current authoritative Host.");
            return;
        }

        PlayerGameState creditor =
            turnManager.GetPlayerStateBySlotIndex(
                CreditorSlotIndex);

        PlayerGameState debtor =
            turnManager.GetPlayerStateBySlotIndex(
                DebtorSlotIndex);

        PlayerGameState survivor =
            turnManager.GetPlayerStateBySlotIndex(
                SurvivorSlotIndex);

        if (!IsUsablePlayer(creditor) ||
            !IsUsablePlayer(debtor) ||
            !IsUsablePlayer(survivor))
        {
            Debug.LogError(
                "Phase 12E QA expects a fresh 3-player match: " +
                "Host in seat_1, Guest Human in seat_2, and a live Bot/Human in seat_3. " +
                "Start a fresh 3-player match and retry.");
            return;
        }

        List<BoardTile> debtorOwned =
            EnumerateTiles(boardPath)
                .Where(tile =>
                    tile != null &&
                    tile.IsOwned &&
                    tile.OwnerPlayerIndex ==
                        debtor.PlayerSlotIndex)
                .ToList();

        if (debtorOwned.Count > 0)
        {
            Debug.LogError(
                "Phase 12E QA fixture refuses to overwrite existing Guest-owned property. " +
                "Use a fresh match where seat_2 owns no property yet.");
            return;
        }

        List<BoardTile> candidates =
            EnumerateTiles(boardPath)
                .Where(tile =>
                    tile != null &&
                    tile.Purchasable &&
                    tile.TileType == TileType.City &&
                    !tile.IsOwned)
                .OrderBy(tile =>
                    Mathf.Max(
                        0,
                        tile.PurchasePrice))
                .ThenBy(tile =>
                    tile.TileIndex)
                .Take(3)
                .ToList();

        if (candidates.Count < 3)
        {
            Debug.LogError(
                "Phase 12E QA needs at least three unowned City properties. " +
                "Start a fresh match and retry.");
            return;
        }

        preparedTileIndices.Clear();

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            BoardTile tile =
                candidates[index];

            tile.ClearOwner();
            development.ResetDevelopment(tile);

            if (!tile.TrySetOwner(
                    debtor.PlayerSlotIndex))
            {
                Debug.LogError(
                    $"Phase 12E QA could not assign {tile.DisplayName} to seat_2.");
                preparedTileIndices.Clear();
                return;
            }

            if (debtor.OwnershipMaterial != null)
            {
                tile.ApplyOwnerMaterial(
                    debtor.OwnershipMaterial);
            }

            int requestedDevelopmentLevel =
                index + 1;

            development
                .ApplyOnlineAuthoritativeDevelopmentLevel(
                    tile,
                    requestedDevelopmentLevel,
                    debtor);

            preparedTileIndices.Add(
                tile.TileIndex);
        }

        // Make the unpaid amount exactly 1 coin. BankruptcyManager pays all
        // available cash first, then only the cheapest property necessary to
        // cover the remaining debt should transfer.
        debtor.ApplyOnlineAuthoritativeMoney(1);

        Debug.Log(
            "PHASE 12E PREPARED.\n" +
            $"Debtor: {debtor.DisplayName} [seat_2], cash=1.\n" +
            $"Creditor: {creditor.DisplayName} [seat_1].\n" +
            $"Survivor: {survivor.DisplayName} [seat_3].\n" +
            "Developed Guest properties:\n" +
            string.Join(
                "\n",
                candidates.Select(
                    (tile, index) =>
                        $"  - Tile {tile.TileIndex}: {tile.DisplayName}, " +
                        $"price={tile.PurchasePrice}, " +
                        $"development={development.GetDevelopmentLevel(tile)}")) +
            "\n\nBefore executing bankruptcy, verify the Guest client also sees " +
            "these ownership/development visuals. Then run menu step 2.",
            coordinator);
    }

    [MenuItem(
        "Atlas Board/QA/Phase 12E/2 Execute Creditor Bankruptcy")]
    public static void ExecuteBankruptcy()
    {
        if (!TryResolveRuntime(
                out AtlasBoardTurnDiceNetworkCoordinator coordinator,
                out TurnManager turnManager,
                out BoardPath boardPath,
                out PropertyDevelopmentManager development,
                out BankruptcyManager bankruptcy))
        {
            return;
        }

        if (!coordinator.LocalIsHost)
        {
            Debug.LogError(
                "Phase 12E QA must be executed by the authoritative Host.");
            return;
        }

        PlayerGameState creditor =
            turnManager.GetPlayerStateBySlotIndex(
                CreditorSlotIndex);

        PlayerGameState debtor =
            turnManager.GetPlayerStateBySlotIndex(
                DebtorSlotIndex);

        if (!IsUsablePlayer(creditor) ||
            debtor == null ||
            !debtor.IsParticipating ||
            debtor.IsBankrupt)
        {
            Debug.LogError(
                "Phase 12E QA debtor/creditor state is no longer valid.");
            return;
        }

        List<BoardTile> fixtureTiles =
            ResolveFixtureTiles(
                boardPath,
                development,
                debtor);

        if (fixtureTiles.Count < 3)
        {
            Debug.LogError(
                "Phase 12E QA fixture was not found. Run step 1 again in a fresh match.");
            return;
        }

        debtor.ApplyOnlineAuthoritativeMoney(1);

        BankruptcyManager.PaymentResolution result =
            bankruptcy.ResolveMandatoryPayment(
                debtor,
                creditor,
                2,
                "Phase 12E developed-property bankruptcy QA");

        Debug.Log(
            "PHASE 12E BANKRUPTCY EXECUTED.\n" +
            $"Amount due={result.AmountDue}, paid={result.AmountPaid}, " +
            $"unpaid={result.UnpaidAmount}.\n" +
            $"Transferred={result.TransferredPropertyCount}, " +
            $"released={result.ReleasedPropertyCount}, " +
            $"transferredValue={result.TransferredPropertyValue}.\n" +
            "Expected: paid=1, unpaid=1, transferred=1, " +
            "and every remaining fixture property released.\n" +
            "A local Host verification will run on the next Editor update. " +
            "Also inspect the Guest client after synchronization.",
            coordinator);

        EditorApplication.delayCall +=
            VerifyHostCleanup;
    }

    [MenuItem(
        "Atlas Board/QA/Phase 12E/3 Verify Host Cleanup")]
    public static void VerifyHostCleanup()
    {
        if (!TryResolveRuntime(
                out AtlasBoardTurnDiceNetworkCoordinator coordinator,
                out TurnManager turnManager,
                out BoardPath boardPath,
                out PropertyDevelopmentManager development,
                out BankruptcyManager bankruptcy))
        {
            return;
        }

        _ = bankruptcy;

        PlayerGameState creditor =
            turnManager.GetPlayerStateBySlotIndex(
                CreditorSlotIndex);

        PlayerGameState debtor =
            turnManager.GetPlayerStateBySlotIndex(
                DebtorSlotIndex);

        if (creditor == null ||
            debtor == null)
        {
            Debug.LogError(
                "Phase 12E QA could not resolve debtor/creditor for verification.");
            return;
        }

        List<BoardTile> fixtureTiles =
            ResolveFixtureTilesAfterBankruptcy(
                boardPath);

        if (fixtureTiles.Count < 3)
        {
            Debug.LogError(
                "Phase 12E QA could not resolve the three prepared fixture tiles.");
            return;
        }

        fixtureTiles =
            fixtureTiles
                .OrderBy(tile =>
                    Mathf.Max(
                        0,
                        tile.PurchasePrice))
                .ThenBy(tile =>
                    tile.TileIndex)
                .ToList();

        BoardTile expectedTransferred =
            fixtureTiles[0];

        List<string> failures =
            new List<string>();

        if (!debtor.IsBankrupt)
        {
            failures.Add(
                "Debtor is not marked bankrupt.");
        }

        PlayerPawnMover debtorPawn =
            debtor.GetComponent<
                PlayerPawnMover>();

        if (debtorPawn != null &&
            debtorPawn.IsPawnVisible)
        {
            failures.Add(
                "Debtor pawn is still visible on Host.");
        }

        for (int index = 0;
             index < fixtureTiles.Count;
             index++)
        {
            BoardTile tile =
                fixtureTiles[index];

            int level =
                development
                    .GetDevelopmentLevel(tile);

            if (level != 0)
            {
                failures.Add(
                    $"{tile.DisplayName} still has development level {level}.");
            }

            Transform markerRoot =
                tile.transform.Find(
                    "DevelopmentMarkers");

            if (markerRoot != null &&
                markerRoot.gameObject.activeInHierarchy &&
                markerRoot.childCount > 0)
            {
                failures.Add(
                    $"{tile.DisplayName} still has visible DevelopmentMarkers.");
            }

            if (index == 0)
            {
                if (!tile.IsOwned ||
                    tile.OwnerPlayerIndex !=
                        creditor.PlayerSlotIndex)
                {
                    failures.Add(
                        $"Cheapest property {tile.DisplayName} did not transfer to creditor.");
                }
            }
            else
            {
                if (tile.IsOwned)
                {
                    failures.Add(
                        $"Remaining property {tile.DisplayName} was not released.");
                }
            }
        }

        if (failures.Count == 0)
        {
            Debug.Log(
                "PHASE 12E HOST CHECK PASS.\n" +
                $"Bankrupt pawn hidden: {(debtorPawn == null ? "pawn component unavailable" : "YES")}.\n" +
                $"Transferred cheapest property: Tile {expectedTransferred.TileIndex} " +
                $"{expectedTransferred.DisplayName} -> seat_1.\n" +
                "All fixture development levels are 0 and remaining properties are unowned.\n\n" +
                "FINAL GUEST CHECK:\n" +
                "1) seat_2 pawn is gone,\n" +
                "2) no house/hotel/development markers remain on the three fixture properties,\n" +
                "3) only the cheapest property shows seat_1 ownership color,\n" +
                "4) the other two are visually unowned,\n" +
                "5) play at least two more turns and confirm none of these visuals resurrect.",
                coordinator);
        }
        else
        {
            Debug.LogError(
                "PHASE 12E HOST CHECK FAILED:\n- " +
                string.Join(
                    "\n- ",
                    failures),
                coordinator);
        }
    }

    private static bool TryResolveRuntime(
        out AtlasBoardTurnDiceNetworkCoordinator coordinator,
        out TurnManager turnManager,
        out BoardPath boardPath,
        out PropertyDevelopmentManager development,
        out BankruptcyManager bankruptcy)
    {
        coordinator =
            UnityEngine.Object
                .FindAnyObjectByType<
                    AtlasBoardTurnDiceNetworkCoordinator>();

        turnManager =
            UnityEngine.Object
                .FindAnyObjectByType<
                    TurnManager>();

        boardPath =
            UnityEngine.Object
                .FindAnyObjectByType<
                    BoardPath>();

        development =
            UnityEngine.Object
                .FindAnyObjectByType<
                    PropertyDevelopmentManager>();

        bankruptcy =
            UnityEngine.Object
                .FindAnyObjectByType<
                    BankruptcyManager>();

        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "Phase 12E QA commands must be run while the match is in Play Mode.");
            return false;
        }

        if (coordinator == null ||
            turnManager == null ||
            boardPath == null ||
            development == null ||
            bankruptcy == null)
        {
            Debug.LogError(
                "Phase 12E QA could not resolve one or more runtime managers.");
            return false;
        }

        if (!coordinator.IsPreparedOnlineMatch ||
            !turnManager.IsMatchStarted)
        {
            Debug.LogError(
                "Phase 12E QA requires an active online match.");
            return false;
        }

        return true;
    }

    private static bool IsUsablePlayer(
        PlayerGameState player)
    {
        return player != null &&
               player.IsParticipating &&
               !player.IsBankrupt;
    }

    private static IEnumerable<BoardTile>
        EnumerateTiles(
            BoardPath boardPath)
    {
        if (boardPath == null)
        {
            yield break;
        }

        for (int index = 0;
             index < boardPath.TileCount;
             index++)
        {
            BoardTile tile =
                boardPath.GetTile(index);

            if (tile != null)
            {
                yield return tile;
            }
        }
    }

    private static List<BoardTile>
        ResolveFixtureTiles(
            BoardPath boardPath,
            PropertyDevelopmentManager development,
            PlayerGameState debtor)
    {
        if (preparedTileIndices.Count >= 3)
        {
            return preparedTileIndices
                .Select(boardPath.GetTile)
                .Where(tile => tile != null)
                .ToList();
        }

        return EnumerateTiles(boardPath)
            .Where(tile =>
                tile.IsOwned &&
                tile.OwnerPlayerIndex ==
                    debtor.PlayerSlotIndex &&
                development.GetDevelopmentLevel(tile) > 0)
            .OrderBy(tile =>
                Mathf.Max(
                    0,
                    tile.PurchasePrice))
            .ThenBy(tile =>
                tile.TileIndex)
            .Take(3)
            .ToList();
    }

    private static List<BoardTile>
        ResolveFixtureTilesAfterBankruptcy(
            BoardPath boardPath)
    {
        return preparedTileIndices
            .Select(boardPath.GetTile)
            .Where(tile => tile != null)
            .ToList();
    }
}
