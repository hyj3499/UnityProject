"""Add only the Unity-authored Continue subtree and its references, preserving existing UI edits."""
from pathlib import Path
import re
import shutil

repo = Path(__file__).resolve().parent.parent
project = repo / 'Temp/ContinueChecks'
assert (project / 'Output/result.txt').read_text().startswith('PASS:')
pattern = re.compile(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', re.M | re.S)
source = (project / 'Assets/Resources/UI/GeneratedStartScreenUI.prefab').read_text(encoding='utf-8-sig')
source_docs = {i: (kind, body) for kind, i, body in pattern.findall(source)}
view_guid = re.search(r'guid: (\w+)', (repo / 'Assets/Scripts/UI/StartScreenView.cs.meta').read_text())[1]
source_view = next(body for kind, body in source_docs.values() if f'guid: {view_guid}' in body)
fields = ['continuePanel', 'closeContinue', 'previousSave', 'nextSave', 'startSave', 'deleteSave',
          'savedCharacterPreview', 'savePosition', 'saveName', 'saveBirthday', 'saveDate', 'saveMoney', 'savePlayTime']
refs = {name: re.search(rf'^  {name}: {{fileID: (\d+)}}', source_view, re.M)[1] for name in fields}
root_go = refs['continuePanel']
root_rt = re.search(r'component: {fileID: (\d+)}', source_docs[root_go][1])[1]
transforms = {i: body for i, (kind, body) in source_docs.items() if kind == '224'}
selected = set()
def descend(rt):
    selected.add(rt)
    go = re.search(r'm_GameObject: {fileID: (\d+)}', transforms[rt])[1]
    selected.add(go)
    selected.update(re.findall(r'component: {fileID: (\d+)}', source_docs[go][1]))
    for child, body in transforms.items():
        if f'm_Father: {{fileID: {rt}}}' in body:
            descend(child)
descend(root_rt)
assert all(i in selected for i in refs.values())
ordered = [i for i in source_docs if i in selected]
mapping = {i: str(9600000000000 + index) for index, i in enumerate(ordered)}

def install(destination):
    original = destination.read_bytes()
    text = original.decode('utf-8-sig').replace('\r\n', '\n')
    docs = {i: (kind, body) for kind, i, body in pattern.findall(text)}
    assert not set(mapping.values()) & set(docs), 'Continue subtree already present; do not overwrite editor changes'
    view_id, view = next((i, body) for i, (kind, body) in docs.items() if f'guid: {view_guid}' in body)
    assert not re.search(r'^  continuePanel: {fileID: [1-9]', view, re.M)
    main_go = re.search(r'^  mainMenu: {fileID: (\d+)}', view, re.M)[1]
    main_rt = re.search(r'component: {fileID: (\d+)}', docs[main_go][1])[1]
    design_rt = re.search(r'm_Father: {fileID: (\d+)}', docs[main_rt][1])[1]
    design = docs[design_rt][1]
    updated_design = design.replace('  m_Children:\n', '  m_Children:\n  - {fileID: ' + mapping[root_rt] + '}\n', 1)
    assert updated_design != design
    updated_view = view
    for name in fields:
        updated_view = re.sub(rf'^  {name}:.*\n', '', updated_view, flags=re.M)
    updated_view += ''.join(f'  {name}: {{fileID: {mapping[refs[name]]}}}\n' for name in fields)
    text = text.replace(f'--- !u!224 &{design_rt}\n{design}', f'--- !u!224 &{design_rt}\n{updated_design}', 1)
    text = text.replace(f'--- !u!114 &{view_id}\n{view}', f'--- !u!114 &{view_id}\n{updated_view}', 1)
    chunks = []
    for i in ordered:
        kind, body = source_docs[i]
        body = re.sub(r'{fileID: (\d+)}', lambda m: '{fileID: ' + mapping.get(m[1], m[1]) + '}', body)
        if i == root_rt:
            body = re.sub(r'm_Father: {fileID: \d+}', f'm_Father: {{fileID: {design_rt}}}', body)
        chunks.append(f'--- !u!{kind} &{mapping[i]}\n{body}')
    at = re.search(r'^--- !u!1660057539 ', text, re.M)
    pos = at.start() if at else len(text)
    text = text[:pos] + ''.join(chunks) + text[pos:]
    # Existing serialized objects must be byte-for-byte identical except the two explicit reference owners.
    after = {i: (kind, body) for kind, i, body in pattern.findall(text)}
    assert all(after[i] == value for i, value in docs.items() if i not in (view_id, design_rt))
    assert destination.read_bytes() == original, 'File changed during installation'
    backup = project / 'Output' / (destination.name + '.before-continue')
    if not backup.exists(): backup.write_bytes(original)
    newline = '\r\n' if b'\r\n' in original else '\n'
    destination.write_bytes(text.replace('\n', newline).encode('utf-8'))
    print(f'{destination.relative_to(repo)}: added {len(selected)} serialized objects; all existing object data preserved except Continue references.')

meta = 'Assets/Resources/Sprites/StartScreen/game data info pannel.png.meta'
if not (repo / meta).exists(): shutil.copy2(project / meta, repo / meta)
else: assert (repo / meta).read_bytes() == (project / meta).read_bytes(), 'Info sprite metadata changed'
install(repo / 'Assets/Resources/UI/StartScreenUI.prefab')
install(repo / 'Assets/Scenes/Main.unity')
