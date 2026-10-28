// Phase 19V source-only assertions; run with `node phase19v_pawn_preview_and_scale_contract.test.cjs`.
const fs = require('fs');
const path = require('path');
const assert = require('assert');
const root = path.resolve(__dirname, '../../..');
const art = fs.readFileSync(path.join(root, 'Assets/Project/Scripts/UI/Meta/AtlasBoardPawnShopPreviewArt.cs'), 'utf8');
const ext = fs.readFileSync(path.join(root, 'Assets/Project/Editor/AtlasBoardPhase19TPawnAssetExtension.cs'), 'utf8');
const checks = [
  ['offscreen preview isolation', art.includes('stageSlot * 64f')],
  ['preview disabled before destroy', art.includes('child.gameObject.SetActive(false)')],
  ['preview camera detached', art.includes('previewCamera.targetTexture = null')],
  ['previous RT released', art.includes('target.Release()')],
  ['stage disabled before destroy', art.includes('stage.SetActive(false)')],
  ['ghost size x1.30', ext.includes('1.716f, 0.689f, Vector3.zero')],
  ['vehicle size x1.70', (ext.match(/1.615f, 0.952f, Vector3.zero/g) || []).length === 3],
  ['existing catalog preserved', ext.includes('new List<PawnCosmeticDefinition>(catalog.Cosmetics)')],
  ['editor additive menu preserved', ext.includes('Extend Existing Catalog - Ghosts and Cars')],
];
for (const [label, ok] of checks) { assert(ok, label); console.log('PASS ' + label); }
console.log(`ATB_PHASE19V_STATIC ${checks.length}/${checks.length} PASS (no Unity runtime run)`);
