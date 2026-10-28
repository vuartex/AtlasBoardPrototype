"use strict";
// Read-only, static regression/owner-observation contract: NOT a Unity render test.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname,"../../..");
const read = p => fs.readFileSync(path.join(root,p),"utf8");
const dice = read("Assets/Project/Scripts/Board/DiceVisualController.cs");
const roadmap = read("Assets/Project/Documentation/MASTER_ROADMAP.md");
let checks=0;
function ok(cond,msg){ assert.ok(cond,msg); checks++; }

// Scene-authored rotations in order of Classic desired faces 1..6 are
// deliberately reused: Ruby Red and Ocean Blue share this inverse mapping.
const m = dice.match(/private static readonly int\[\] ImportedFaceToClassicRotationIndex\s*=\s*\{\s*([\d,\s]+)\s*\}/);
ok(m!==null,"The calibration must be explicitly declared on the canonical controller");
const indices=m[1].split(',').map(s=>s.trim()).filter(Boolean).map(Number);
ok(indices.length===6,"Exactly six face orientation indices");
ok(indices.every(n=>Number.isInteger(n) && n>=0 && n<6),"All orientation indices are valid");
ok(new Set(indices).size===6,"Calibration is a bijection and never duplicates a face");
ok(JSON.stringify(indices)===JSON.stringify([5,3,1,4,2,0]),"Owner-derived inverse permutation is exact");

// With the v5.49.12h legacy rotations, the imported mesh displayed:
// requested Classic 1,2,3,4,5,6 -> imported 6,3,5,2,4,1.
const oldImportedVisible=[6,3,5,2,4,1];
for(let desired=1;desired<=6;desired++)
    ok(oldImportedVisible[indices[desired-1]]===desired,`Top face ${desired} is calibrated`);

const observed=[
  ["red",[1,6],[6,1]],
  ["red",[5,2],[4,3]],
  ["red",[2,1],[3,6]],
  ["red",[4,5],[2,4]],
  ["red",[6,3],[1,5]],
  ["red",[5,1],[4,6]],
  ["red",[3,4],[5,2]],
  ["blue",[5,4],[4,2]],
  ["blue",[6,3],[1,5]],
  ["blue",[5,1],[4,6]],
  ["blue",[1,1],[6,6]],
  ["blue",[4,1],[2,6]],
  ["blue",[1,2],[6,3]]
];
for (const [skin,requested,displayed] of observed) {
    ok(requested.every((num,i)=>oldImportedVisible[num-1]===displayed[i]),
       `Owner ${skin} evidence accounted for: ${requested.join('+')} previously showed ${displayed.join('+')}`);
    ok(requested.every(num=>oldImportedVisible[indices[num-1]]===num),
       `Owner ${skin} pair should now display ${requested.join('+')}`);
}

ok(dice.includes("if (activeImportedDice)\n            index = ImportedFaceToClassicRotationIndex[index];"),
   "Remap executes only when imported die models are active");
ok(dice.includes("activeImportedDice = useImported;"),"Runtime tracks actual imported renderer usage");
ok(dice.includes("activeImportedDice = false;"),"New match resets imported calibration mode");
ok(dice.includes("GameObject prefab = classic ? null : ModelFor(id);"),"Bots and Classic keep original dice meshes");
ok(dice.includes('r.enabled = !useImported;'),"Original pipped renderers enabled for Classic");
ok(dice.includes("faceLocalEulerRotations[index]"),"Scene-authored Euler rotations retained");
ok(dice.includes('ATB_DICE_FACE_CALIBRATION mode='),"Calibration mode logged per roll");
ok(!dice.includes('Random.Range(1, 7)'),"Calibration does not add a new RNG");
ok(roadmap.includes('Phase 19R'),"Canonical roadmap extended");
ok(roadmap.includes('Phase 19Q'),"Prior roadmap entry preserved");
console.log(`ATB_PHASE19R_DICE_FACE_STATIC_QA PASS checks=${checks} unityCompile=OPEN visualTopFaceOwnerQA=OPEN blackMarble=OPEN twoClientOnline=OPEN`);
