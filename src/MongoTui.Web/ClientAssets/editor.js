import {EditorView, basicSetup} from 'codemirror';
import {EditorState, Compartment} from '@codemirror/state';
import {keymap} from '@codemirror/view';
import {javascript} from '@codemirror/lang-javascript';
import {json} from '@codemirror/lang-json';
import {autocompletion} from '@codemirror/autocomplete';
import {oneDark} from '@codemirror/theme-one-dark';
const editors = new WeakMap();
const methods = ['find', 'findOne', 'aggregate', 'countDocuments', 'getCollection', 'getSiblingDB', 'getCollectionNames', 'limit', 'skip', 'sort', 'toArray', 'insertOne', 'updateOne', 'deleteOne'];
export function create(element, value, language, label, dotnet) {
  let timer;
  const theme = new Compartment();
  const currentTheme = () => document.documentElement.dataset.theme === 'dark' ? oneDark : [];
  const complete = async context => {
    const word = context.matchBefore(/[\w$.]+/);
    if (!word || (!context.explicit && word.text.length < 2)) return null;
    const prefix = word.text.split('.').pop();
    let fields = [];
    try { fields = await dotnet.invokeMethodAsync('CompleteAsync', prefix); } catch {}
    if (context.aborted) return null;
    return {from: context.pos - prefix.length, options: [
      ...methods.filter(name => name.startsWith(prefix)).map(label => ({label, type: 'function', apply: `${label}()`})),
      ...fields.map(label => ({label, type: 'property'}))
    ]};
  };
  const view = new EditorView({parent: element, state: EditorState.create({doc: value, extensions: [
    basicSetup, language === 'json' ? json() : javascript(), theme.of(currentTheme()),
    EditorView.lineWrapping,
    EditorView.contentAttributes.of({'aria-label': label}),
    EditorView.theme({'&': {height: '100%', fontSize: '13px'}, '.cm-scroller': {fontFamily: 'var(--mono)', overflow: 'auto'}, '.cm-content': {padding: '16px 0'}, '.cm-gutters': {background: 'transparent', border: '0', color: 'var(--muted)'}, '.cm-line': {padding: '0 20px'}}),
    keymap.of([{key: 'Mod-Enter', run: () => {dotnet.invokeMethodAsync('RunAsync'); return true;}}, {key: 'F5', run: () => {dotnet.invokeMethodAsync('RunAsync'); return true;}}]),
    ...(language === 'json' ? [] : [autocompletion({override: [complete]})]),
    EditorView.updateListener.of(update => {
      if (!update.docChanged) return;
      clearTimeout(timer);
      timer = setTimeout(() => dotnet.invokeMethodAsync('ChangedAsync', update.state.doc.toString()).catch(() => {}), 400);
    })
  ]})});
  element.querySelector('textarea')?.remove();
  const observer = new MutationObserver(() => view.dispatch({effects: theme.reconfigure(currentTheme())}));
  observer.observe(document.documentElement, {attributes: true, attributeFilter: ['data-theme']});
  editors.set(element, {view, observer, cleanup: () => clearTimeout(timer)});
}
export function getValue(element) {return editors.get(element)?.view.state.doc.toString() ?? element.querySelector('textarea')?.value ?? '';}
export function destroy(element) {
  const editor = editors.get(element);
  if (!editor) return;
  editor.cleanup(); editor.observer.disconnect(); editor.view.destroy(); editors.delete(element);
}
