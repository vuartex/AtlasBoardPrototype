#!/usr/bin/env node
'use strict';
const fs=require('fs');const path=require('path');const assert=require('assert/strict');
const root=path.resolve(__dirname,'../../../');
function file(p){return fs.readFileSync(path.join(root,p),'utf8');}
const retired=file('Assets/Project/Editor/AtlasBoardAccountQaWindowsBuilder.cs');
const rematch=file('Assets/Project/Editor/AtlasBoardRematchFastForwardQA.cs');
const legacyPath=path.join(root,'Assets/Project/Editor/AtlasBoardTwoClientLobbyTestV1.cs');
const stripPath=path.join(root,'Assets/Project/Editor/AtlasBoardGuestBuildNativeFirebaseStripper.cs');
const legacy=fs.existsSync(legacyPath)?fs.readFileSync(legacyPath,'utf8'):null;
const strip=fs.existsSync(stripPath)?fs.readFileSync(stripPath,'utf8'):null;
const results=[
 ['no_duplicate_account_qa_build_menu',!retired.includes('[MenuItem(')],
 ['retired_builder_preserves_type',retired.includes('class AtlasBoardAccountQaWindowsBuilder')],
 ['no_duplicate_qa_diagnostics_root',!rematch.includes('Atlas Board/QA Diagnostics/')],
 ['rematch_qa_preserved',rematch.includes('SetCurrentMatchToFinalRound()')&&rematch.includes('FinishAfterCurrentTurnForRematchQa()')],
 ['rematch_commands_grouped',rematch.includes('Atlas Board/QA & Diagnostics/QA/Match/')],
 ...(legacy===null?[]:[
 ['canonical_guest_build_preserved',legacy.includes('Build Two-Client Guest Test v1')],
 ['canonical_live_guest_launcher_preserved',legacy.includes('Launch Live Spark Guest QA (No Steam)')],
 ['canonical_live_guest_flag_preserved',legacy.includes('-atlasLiveGuestQa')]]),
 ...(strip===null?[]:[
 ['canonical_guest_no_native_contract_preserved',strip.includes('RemoveComponentsFromScene<AtlasBoardAccountService>')]]),
];for (const [label,ok] of results){assert(ok,label);}
console.log('ATB_PHASE19BP_MENU_CONSOLIDATION PASS checks='+results.length+' canonical_guest_source='+(legacy&&strip?'CHECKED':'NOT_IN_TEST_FIXTURE')+' production=NOT_TOUCHED');
