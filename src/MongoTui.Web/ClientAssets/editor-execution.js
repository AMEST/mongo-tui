import {Prec} from '@codemirror/state';
import {keymap} from '@codemirror/view';

export function executionKeys(run) {
  return Prec.highest(keymap.of([{key: 'Mod-Enter', run}, {key: 'F5', run}]));
}

export function readExecution(state, textarea) {
  if (!state) {
    const script = textarea?.value ?? '';
    return {script, selection: textarea && textarea.selectionStart !== textarea.selectionEnd
      ? script.slice(textarea.selectionStart, textarea.selectionEnd) : null};
  }
  const {from, to, empty} = state.selection.main;
  return {script: state.doc.toString(), selection: empty ? null : state.sliceDoc(from, to)};
}
