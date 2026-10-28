'use strict';
const fs = require('fs');
const path = require('path');
const assert = require('assert');
const root = path.resolve(__dirname, '../../../');
const load = file => fs.readFileSync(path.join(root, file), 'utf8');
const binder = load('Assets/Project/Editor/AtlasBoardPhase19NDiceModelBinding.cs');
const controller = load('Assets/Project/Scripts/Board/DiceVisualController.cs');
const history = load('Assets/Project/Documentation/MASTER_ROADMAP.md');
let checks = 0;
function ok(test, label){ assert.ok(test,label); checks++; }
const pairs = [
  ['classicDiceModel','Dice_White.fbx'],
  ['rubyDiceModel','Dice_Red.fbx'],
  ['oceanDiceModel','Dice_Blue.fbx'],
  ['midnightDiceModel','Dice_Black.fbx'],
  ['marbleDiceModel','Dice_Marble.fbx']
];
for (const [field,file] of pairs){ok(binder.includes('"'+field+'"'),field+' bind');ok(binder.includes('"'+file+'"'),file+' asset');}
ok(binder.includes('AssetDatabase.LoadAssetAtPath<GameObject>'), 'Unity imported model ref');
ok(binder.includes('TryGetGUIDAndLocalFileIdentifier'), 'Unity not guessed model id');
ok(binder.includes('SerializedObject(dice)'), 'repair existing instance');
ok(binder.includes('property.objectReferenceValue = imported'), 'serialized GameObject assignment');
ok(binder.includes('property.objectReferenceValue != null'), 'preserve custom refs');
ok(binder.includes('ApplyModifiedPropertiesWithoutUndo'), 'serialized save');
ok(binder.includes('EditorSceneManager.MarkSceneDirty'), 'mark scene');
ok(binder.includes('!dirtyBefore'), 'protect owner unsaved scene edits');
ok(binder.includes('EditorSceneManager.SaveScene(scene)'), 'persist clean scene');
ok(binder.includes('EditorApplication.delayCall'), 'import reload hook');
ok(binder.includes('EditorSceneManager.sceneOpened'), 'scene opening hook');
ok(binder.includes('PlayModeStateChange.ExitingEditMode'), 'before play hook');
ok(binder.includes('Repair Imported Dice Model References'), 'manual menu');
ok(binder.startsWith('#if UNITY_EDITOR'), 'Editor only');
ok(!binder.includes('new DiceVisualController'), 'no duplicate runtime manager');
ok(controller.includes('GameObject prefab = classic ? null : ModelFor(id);'), 'existing skin model resolver with classic original');
ok(controller.includes('renderedModel[0, modelIndex]'), 'recovered model eligibility');
ok(controller.includes('classic || alreadyRendered'), 'avoid sticky original fallback / use original classic');
ok(controller.includes('botControlled'), 'bot default remains');
ok(controller.includes('CurrentEquippedDiceId'), 'UID equipment remains');
ok(controller.includes('Dice visual result:'), 'visual result logging remains');
ok(history.includes('v5.49.12d') && history.includes('v5.49.12e'), 'prior history preserved');
ok(history.includes('OWNER OPEN'), 'no false runtime PASS');
console.log(`ATB_PHASE19N_DICE_FBX_BIND_STATIC_QA PASS checks=${checks} realUnity=OPEN build=OPEN`);
