'use strict';
const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
let checks=0;
const has=(s,n,m)=>{assert(s.includes(n),m);checks++;};
const not=(s,n,m)=>{assert(!s.includes(n),m);checks++;};

const auction=read('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs');
const tablet=read('Assets/Project/Scripts/Core/TabletUIManager.cs');
const lobby=read('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs');

// Modern-only auction: one Tablet shell/title, no independent root Canvas.
has(auction,'auctionPanel.transform as RectTransform','modern auction is not parented to authoritative Panel_Auction');
has(auction,'tablet_header=canonical','canonical tablet header diagnostic missing');
has(auction,'header.gameObject.SetActive(false);','duplicate modern title is not disabled');
has(auction,'RebuildModernPresentation();','self-healing modern auction rebuild missing');
has(auction,'blank_shell_suppressed=true','blank-shell suppression missing');
has(auction,'graphic.enabled = false','retired auction graphics are not suppressed');
has(auction,'graphic.raycastTarget = false','retired auction can still intercept input');
not(auction,'AddComponent<Canvas>()','auction must not create a second standalone Canvas');
not(auction,'sortingOrder = 32000','auction must not override tablet sorting with a second screen');

// Tablet ownership: informational lifecycle notices never overlap decisions.
has(tablet,'pendingOnlineSeatNoticeMessage','queued seat notice state missing');
has(tablet,'reason=gameplay_panel_active','notice/gameplay collision diagnostic missing');
has(tablet,'HideOnlineSeatNoticeVisual();','notice suppression path missing');
has(tablet,'ShowOnlineSeatNoticeNow(queuedMessage, queuedSeconds);','queued notice replay missing');

// Host migration/rematch: authoritative host slot can be any slot.
has(lobby,'GetAuthoritativeHostSlotIndex()','authoritative host slot resolver missing');
has(lobby,'member.IsHost &&','host slot does not prioritize authoritative IsHost');
has(lobby,'i == hostSlotIndex','host row is still hard-coded to slot zero');
has(lobby,'IsAuthoritativeHostSlot(index)','seat mutation does not protect migrated host slot');
has(lobby,'int hostSlotIndex = GetAuthoritativeHostSlotIndex();','backend seat policies do not use migrated host slot');
not(lobby,'if (i == 0)\n            {\n                SetSeat(','host row still hard-coded to slot zero');

console.log(`ATB_PHASE19AT_SOURCE_GUARD PASS checks=${checks} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
