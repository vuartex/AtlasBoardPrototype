"use strict";
// Phase 19B: local Firebase emulator catalogue for integration QA.
// The production backend remains unchanged. Never run against live Firestore.
if (!process.env.FIRESTORE_EMULATOR_HOST ||
    process.env.ATLASBOARD_PHASE19_EMULATOR_SEED !== "YES") {
  console.error("REFUSED: FIRESTORE_EMULATOR_HOST and ATLASBOARD_PHASE19_EMULATOR_SEED=YES required");
  process.exit(2);
}
const {initializeApp} = require("firebase-admin/app");
const {getFirestore} = require("firebase-admin/firestore");
initializeApp({projectId: "atlasboard-usa"});
const db = getFirestore();
const products = [
  ["frame_halloween", "profile_frame", "Halloween Night", 350],
  ["frame_new_year", "profile_frame", "New Year Lights", 350],
  ["frame_turkiye", "profile_frame", "Türkiye Flag", 400],
  ["frame_usa", "profile_frame", "USA Flag", 400],
  ["dice_ruby_red", "dice_skin", "Ruby Red", 350],
  ["dice_ocean_blue", "dice_skin", "Ocean Blue", 350],
  ["dice_midnight_black", "dice_skin", "Midnight Black", 400],
  ["dice_graphite_gray", "dice_skin", "Graphite Gray", 400],
];
(async () => {
  let count = 0;
  for (const [itemId, itemType, displayName, price] of products) {
    await db.collection("item_catalog").doc(itemId).set({
      itemId, itemType, displayName, active: true,
      prices: {gold: price}, sortOrder: count++, catalogVersion: 19,
      source: "phase19b_emulator_catalog_seed",
    }, {merge: true});
  }
  console.log("ATB_PHASE19B_EMULATOR_CATALOG_SEED PASS items=" + count + " live=false");
  process.exit(0);
})().catch(error => {console.error(error);process.exit(1);});
