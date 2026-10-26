using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Public room page compatible with the existing public browser. No Functions.
/// </summary>
public sealed class AtlasPublicLobbyPageResult
{
    public bool Success { get; private set; }
    public string ErrorLocalizationKey { get; private set; }
    public string TechnicalMessage { get; private set; }
    public IReadOnlyList<AtlasPublicLobbyCard> Rooms { get; private set; }
    public string NextCursor { get; private set; }
    public bool HasMore { get; private set; }
    public int ScannedDocuments { get; private set; }
    public int QueryCount { get; private set; }
    public bool ScanLimitReached { get; private set; }

    public static AtlasPublicLobbyPageResult Ok(
        IReadOnlyList<AtlasPublicLobbyCard> rooms, string nextCursor,
        bool hasMore, int scannedDocuments, int queryCount, bool scanLimitReached)
    {
        return new AtlasPublicLobbyPageResult
        {
            Success = true, ErrorLocalizationKey = string.Empty,
            TechnicalMessage = string.Empty, Rooms = rooms ?? Array.Empty<AtlasPublicLobbyCard>(),
            NextCursor = nextCursor ?? string.Empty, HasMore = hasMore,
            ScannedDocuments = Mathf.Max(0,scannedDocuments),
            QueryCount = Mathf.Max(0,queryCount), ScanLimitReached = scanLimitReached
        };
    }
    public static AtlasPublicLobbyPageResult Fail(string key,string message)
    {
        return new AtlasPublicLobbyPageResult
        {
            Success=false,ErrorLocalizationKey=string.IsNullOrWhiteSpace(key)?"lobby.error.unknown":key,
            TechnicalMessage=message??string.Empty,Rooms=Array.Empty<AtlasPublicLobbyCard>(),
            NextCursor=string.Empty,HasMore=false
        };
    }
}

/// <summary>
/// Spark production uses the same RTDB directory as the non-paged room list.
/// No Callable URL is requested, eliminating public browser's HTTP 404.
/// Cursor is a numeric offset in a bounded list, not a server cursor.
/// </summary>
public static class AtlasBoardPublicLobbyPagingClient
{
    public static async Task<AtlasPublicLobbyPageResult> ListPageAsync(
        AtlasBoardLobbyRuntimeBridge runtimeBridge, int limit, string cursor = "")
    {
        if(runtimeBridge == null)
            return AtlasPublicLobbyPageResult.Fail("lobby.error.service_unavailable",
                "RTDB runtime bridge missing.");
        bool ready=await runtimeBridge.EnsureOnlineIdentityAsync(allowAndroidGuestSignIn: true);
        if(!ready)
            return AtlasPublicLobbyPageResult.Fail("account.error.authentication_required",
                "Sign in to browse online rooms.");
        // The public room directory contains lightweight metadata, not
        // room snapshots or passwords. Cache ceiling avoids unbounded usage.
        AtlasPublicLobbyListResult response=await runtimeBridge.ListPublicRoomsAsync(50);
        if(!response.Success)
            return AtlasPublicLobbyPageResult.Fail(response.ErrorLocalizationKey,
                response.TechnicalMessage);
        int offset=0;
        if(!string.IsNullOrEmpty(cursor) && !int.TryParse(cursor,out offset))
            return AtlasPublicLobbyPageResult.Fail("lobby.error.unknown","Invalid page cursor.");
        offset=Mathf.Clamp(offset,0,response.Rooms.Count);
        int count=Mathf.Min(Mathf.Clamp(limit,1,25),response.Rooms.Count-offset);
        List<AtlasPublicLobbyCard> page=new List<AtlasPublicLobbyCard>(count);
        for(int i=0;i<count;i++)page.Add(response.Rooms[offset+i]);
        bool more=(offset+count)<response.Rooms.Count;
        Debug.Log("ATB_PUBLIC_DISCOVERY_PAGE transport=RTDB count="+count+
                  " scanned="+response.Rooms.Count+" next="+more);
        return AtlasPublicLobbyPageResult.Ok(page,more?(offset+count).ToString():"",
            more,response.Rooms.Count,1,false);
    }
}
