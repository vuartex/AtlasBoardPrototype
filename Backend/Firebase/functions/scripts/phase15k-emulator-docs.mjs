/**
 * Privileged fixture inspection ONLY for Steam E2E tests running against
 * Firestore Emulator. Firestore rules correctly prohibit client REST reads
 * of platform_identity_links/account_platform_links/platform_link_events.
 * Never use this module in a client or a deployed callable.
 */
import {getApps, initializeApp} from "firebase-admin/app";
import {getFirestore} from "firebase-admin/firestore";

const PROJECT_ID = "atlasboard-usa";
const HOST = "127.0.0.1:8080";
const APP_NAME = "atlas-phase15k-steam-e2e-fixtures";

function emulatorDatabase() {
  const configured = process.env.FIRESTORE_EMULATOR_HOST;
  if (configured && configured !== HOST && configured !== "localhost:8080") {
    throw new Error("Phase 15K: unexpected Firestore emulator host; refusing Admin access.");
  }
  if (process.env.GCLOUD_PROJECT && process.env.GCLOUD_PROJECT !== PROJECT_ID) {
    throw new Error("Phase 15K: unexpected project; refusing Admin access.");
  }
  if (process.env.GOOGLE_CLOUD_PROJECT && process.env.GOOGLE_CLOUD_PROJECT !== PROJECT_ID) {
    throw new Error("Phase 15K: unexpected project; refusing Admin access.");
  }
  // Force local emulator routing. This module cannot talk to live Firestore.
  process.env.FIRESTORE_EMULATOR_HOST = HOST;
  const app = getApps().find((candidate) => candidate.name === APP_NAME) ??
    initializeApp({projectId: PROJECT_ID}, APP_NAME);
  return getFirestore(app);
}

function fixturePath(path) {
  if (typeof path !== "string" ||
      !/^(platform_identity_links|account_platform_links|platform_link_events)\/[^/]+$/.test(path)) {
    throw new Error("Phase 15K: invalid Steam E2E fixture path.");
  }
  return path;
}

function restValue(value) {
  if (value === null || value === undefined) return {nullValue: null};
  if (typeof value === "string") return {stringValue: value};
  if (typeof value === "boolean") return {booleanValue: value};
  if (typeof value === "number") return Number.isInteger(value) ?
    {integerValue: String(value)} : {doubleValue: value};
  if (value && typeof value.toDate === "function") {
    return {timestampValue: value.toDate().toISOString()};
  }
  if (Array.isArray(value)) return {arrayValue: {values: value.map(restValue)}};
  if (typeof value === "object") return {mapValue: {fields: restFields(value)}};
  throw new Error("Phase 15K: unexpected fixture field type.");
}

function restFields(data) {
  return Object.fromEntries(Object.entries(data ?? {}).map(
    ([key, value]) => [key, restValue(value)],
  ));
}

export async function getEmulatorDoc(path) {
  const snap = await emulatorDatabase().doc(fixturePath(path)).get();
  return {
    response: {ok: snap.exists, status: snap.exists ? 200 : 404},
    json: snap.exists ? {fields: restFields(snap.data())} : {},
    text: snap.exists ? "EMULATOR_ADMIN_DOCUMENT_FOUND" : "EMULATOR_ADMIN_DOCUMENT_MISSING",
  };
}

export async function deleteEmulatorDoc(path) {
  await emulatorDatabase().doc(fixturePath(path)).delete();
}
