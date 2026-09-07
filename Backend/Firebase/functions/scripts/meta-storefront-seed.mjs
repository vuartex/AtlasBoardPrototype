import {createHash} from "node:crypto";

const PROJECT_ID = "atlasboard-usa";
const FIRESTORE = "http://127.0.0.1:8080";

const items = [
  ["pawn_explorer", "pawn", "Explorer Pawn", 400, 40, 10],
  ["dice_midnight", "dice_skin", "Midnight Dice", 300, 30, 20],
  ["board_walnut", "board_theme", "Walnut Board", 700, 60, 30],
  ["frame_founder", "profile_frame", "Founder Frame", 250, 25, 40],
  ["emote_wave", "emote", "Wave Emote", 150, 15, 50],
  ["motion_bounce", "animation_pack", "Bounce Motion", 500, 45, 60],
];

const promos = [
  {
    code: "ATLAS-WELCOME",
    rewards: [
      {type: "gold", amount: 350},
      {type: "atlas_coin", amount: 15},
    ],
  },
  {
    code: "DICE-GIFT",
    rewards: [
      {type: "inventory_item", itemId: "dice_midnight"},
    ],
  },
];

function documentUrl(path) {
  return `${FIRESTORE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

function normalizeCode(value) {
  return value.trim().toUpperCase();
}

function promoId(code) {
  return createHash("sha256")
    .update(`atlasboard:promo:${normalizeCode(code)}`, "utf8")
    .digest("hex");
}

function rewardArrayValue(rewards) {
  return {
    arrayValue: {
      values: rewards.map((reward) => ({
        mapValue: {
          fields: Object.fromEntries(
            Object.entries(reward).map(([key, value]) => {
              if (typeof value === "string") {
                return [key, {stringValue: value}];
              }
              return [key, {integerValue: String(value)}];
            }),
          ),
        },
      })),
    },
  };
}

async function listCollection(collectionId) {
  const url = `${FIRESTORE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${collectionId}?pageSize=500`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(
      `Could not list ${collectionId}: ${response.status} ${await response.text()}`,
    );
  }
  const json = await response.json();
  return Array.isArray(json.documents) ? json.documents : [];
}

async function deleteDocumentByName(name) {
  const prefix = `/v1/projects/${PROJECT_ID}/databases/(default)/documents/`;
  const index = name.indexOf(prefix);
  const path = index >= 0 ? name.substring(index + prefix.length) : "";
  if (!path) return;
  const response = await fetch(documentUrl(path), {method: "DELETE"});
  if (!response.ok && response.status !== 404) {
    throw new Error(
      `Cleanup failed for ${path}: ${response.status} ${await response.text()}`,
    );
  }
}

async function cleanupLeakedE2EFixtures() {
  let removed = 0;
  const catalog = await listCollection("item_catalog");
  for (const document of catalog) {
    const itemId = document?.fields?.itemId?.stringValue ?? "";
    if (itemId.startsWith("phase9_v2_dice_")) {
      await deleteDocumentByName(document.name ?? "");
      removed++;
    }
  }

  const promos = await listCollection("promo_codes");
  for (const document of promos) {
    const displayCode = document?.fields?.displayCode?.stringValue ?? "";
    if (displayCode.startsWith("V2-")) {
      await deleteDocumentByName(document.name ?? "");
      removed++;
    }
  }

  return removed;
}

async function patchDocument(path, fields) {
  const response = await fetch(documentUrl(path), {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({fields}),
  });

  if (!response.ok) {
    throw new Error(
      `Seed failed for ${path}: ${response.status} ${await response.text()}`,
    );
  }
}

async function writeCatalog(item) {
  const [itemId, itemType, displayName, gold, atlasCoin, sortOrder] = item;
  await patchDocument(`item_catalog/${itemId}`, {
    itemId: {stringValue: itemId},
    itemType: {stringValue: itemType},
    displayName: {stringValue: displayName},
    active: {booleanValue: true},
    sortOrder: {integerValue: String(sortOrder)},
    catalogVersion: {integerValue: "2"},
    prices: {
      mapValue: {
        fields: {
          gold: {integerValue: String(gold)},
          atlas_coin: {integerValue: String(atlasCoin)},
        },
      },
    },
  });
}

async function writePromo(promo) {
  const normalized = normalizeCode(promo.code);
  const id = promoId(normalized);
  const now = Date.now();

  await patchDocument(`promo_codes/${id}`, {
    promoId: {stringValue: id},
    codeHash: {stringValue: id},
    displayCode: {stringValue: normalized},
    active: {booleanValue: true},
    startsAtEpochMs: {integerValue: String(now - 60_000)},
    endsAtEpochMs: {integerValue: String(now + 365 * 24 * 60 * 60 * 1000)},
    globalLimit: {integerValue: "100000"},
    redemptionCount: {integerValue: "0"},
    perAccountLimit: {integerValue: "1"},
    rewards: rewardArrayValue(promo.rewards),
    schemaVersion: {integerValue: "1"},
  });
}

async function main() {
  console.log("Atlas Board Phase 9 Store v2 seed");
  console.log("Target: localhost Firestore emulator only.");

  const removed = await cleanupLeakedE2EFixtures();
  if (removed > 0) {
    console.log(`Cleanup: removed ${removed} stale Phase 9 E2E fixture(s).`);
  }

  for (const item of items) {
    await writeCatalog(item);
  }

  for (const promo of promos) {
    await writePromo(promo);
  }

  console.log(`PASS: seeded ${items.length} catalog items and ${promos.length} redeem codes.`);
  console.log("DEV CODES: ATLAS-WELCOME | DICE-GIFT");
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
