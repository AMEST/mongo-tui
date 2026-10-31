import {test} from 'node:test';
import assert from 'node:assert/strict';
import {EditorState, EditorSelection} from '@codemirror/state';
import {basicSetup} from 'codemirror';
import {keymap} from '@codemirror/view';
import {executionKeys, readExecution} from './editor-execution.js';

test('textarea keeps the draft and executes only the selected fragment', () => {
  const value = 'deleteAll();\nfind();';
  assert.deepEqual(readExecution(null, {value, selectionStart: 13, selectionEnd: 20}),
    {script: value, selection: 'find();'});
});
test('empty selection executes the entire document', () => {
  assert.deepEqual(readExecution(null, {value: 'find();', selectionStart: 3, selectionEnd: 3}),
    {script: 'find();', selection: null});
});
test('whitespace selection never falls back to a destructive script', () => {
  assert.equal(readExecution(null, {value: '  deleteAll();', selectionStart: 0, selectionEnd: 2}).selection, '  ');
});
test('CodeMirror reads the normalized main range without combining selections', () => {
  const doc = 'deleteAll();\nfind();';
  const state = EditorState.create({doc, selection: EditorSelection.create([
    EditorSelection.range(0, 12), EditorSelection.range(doc.length, 13)
  ], 1), extensions: [EditorState.allowMultipleSelections.of(true)]});
  assert.deepEqual(readExecution(state), {script: doc, selection: 'find();'});
});
test('run shortcuts take precedence over the default insert-blank-line command', () => {
  let runs = 0;
  const state = EditorState.create({doc: 'find();', extensions: [basicSetup, executionKeys(() => {runs++; return true;})]});
  for (const key of ['Mod-Enter', 'F5']) {
    const binding = state.facet(keymap).flat().find(binding => binding.key === key);
    assert.equal(binding.run({state}), true);
  }
  assert.equal(runs, 2);
  assert.equal(state.doc.toString(), 'find();');
});
