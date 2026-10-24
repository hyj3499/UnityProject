"""Install the verified Unity-authored hierarchy without reserializing unrelated scene data."""
from pathlib import Path
import re
import shutil

repo = Path(__file__).resolve().parent.parent
project = repo / 'Temp/StartScreenChecks'
assert (project / 'Output/result.txt').read_text().startswith('PASS:'), 'Run Unity authoring checks first'
scene_path = repo / 'Assets/Scenes/Main.unity'
original = scene_path.read_bytes()
scene = original.decode('utf-8-sig').replace('\r\n', '\n')
prefab_path = project / 'Assets/Resources/UI/StartScreenUI.prefab'
prefab = prefab_path.read_text(encoding='utf-8-sig')
docs = re.findall(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', prefab, re.M | re.S)
root_go = next((ident, body) for kind, ident, body in docs if kind == '1' and '\n  m_Name: StartScreenUI\n' in body)
root_transform = re.search(r'component: \{fileID: (\d+)\}', root_go[1]).group(1)
scene_docs = re.findall(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', scene, re.M | re.S)
canvas_go = next((ident, body) for kind, ident, body in scene_docs if kind == '1' and '\n  m_Name: Canvas\n' in body)
canvas_transform = next(ident for kind, ident, body in scene_docs if kind == '224' and f'm_GameObject: {{fileID: {canvas_go[0]}}}' in body)
existing_ids = {ident for _, ident, _ in scene_docs}
if '\n  m_Name: StartScreenUI\n' in scene:
    old_prefab = (repo / 'Assets/Resources/UI/StartScreenUI.prefab').read_text(encoding='utf-8-sig')
    old_docs = re.findall(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)', old_prefab, re.M | re.S)
    old_go = next(body for kind, _, body in old_docs if kind == '1' and '\n  m_Name: StartScreenUI\n' in body)
    old_root = re.search(r'component: \{fileID: (\d+)\}', old_go).group(1)
    old_map = {ident: str(9400000000000 + index) for index, (_, ident, _) in enumerate(old_docs)}
    old_chunks = []
    for kind, ident, body in old_docs:
        body = re.sub(r'\{fileID: (\d+)\}', lambda m: '{fileID: '+old_map.get(m[1], m[1])+'}', body)
        if ident == old_root:
            body = body.replace('m_Father: {fileID: 0}', f'm_Father: {{fileID: {canvas_transform}}}')
        old_chunks.append(f'--- !u!{kind} &{old_map[ident]}\n{body}')
    old_block = ''.join(old_chunks)
    assert old_block in scene, 'The installed UI was edited; preserve those edits instead of regenerating'
    scene = scene.replace(old_block, '', 1)
    scene = scene.replace(f'  - {{fileID: {old_map[old_root]}}}\n', '', 1)
    existing_ids -= set(old_map.values())
mapping = {ident: str(9400000000000 + index) for index, (_, ident, _) in enumerate(docs)}
assert not set(mapping.values()) & existing_ids
chunks = []
for kind, ident, body in docs:
    body = re.sub(r'\{fileID: (\d+)\}', lambda m: '{fileID: '+mapping.get(m[1], m[1])+'}', body)
    if ident == root_transform:
        body = body.replace('m_Father: {fileID: 0}', f'm_Father: {{fileID: {canvas_transform}}}')
    chunks.append(f'--- !u!{kind} &{mapping[ident]}\n{body}')
canvas_pattern = rf'(^--- !u!224 &{canvas_transform}\n.*?  m_Children:\n)'
scene, changes = re.subn(canvas_pattern, lambda m: m[1]+f'  - {{fileID: {mapping[root_transform]}}}\n', scene, count=1, flags=re.M | re.S)
assert changes == 1
insert = ''.join(chunks)
root_header = re.search(r'^--- !u!1660057539 ', scene, re.M)
if root_header:
    scene = scene[:root_header.start()] + insert + scene[root_header.start():]
else:
    scene += insert
assert scene_path.read_bytes() == original, 'Scene changed during installation'
backup = project / 'Output/Main.before-StartScreen.unity'
if not backup.exists(): backup.write_bytes(original)
destination = repo / 'Assets/Resources/UI'
destination.mkdir(exist_ok=True)
for name in ('StartScreenUI.prefab', 'StartScreenUI.prefab.meta'):
    shutil.copy2(prefab_path.parent / name, destination / name)
if not (repo / 'Assets/Resources/UI.meta').exists():
    shutil.copy2(project / 'Assets/Resources/UI.meta', repo / 'Assets/Resources/UI.meta')
for rel in ('StartScreen.png.meta', 'CharacterPreview/base_animations.png.meta', 'CharacterPreview/eyes.png.meta', 'CharacterPreview/base_animations_headguide.png.meta', 'CharacterPreview/DefaultStanding.png', 'CharacterPreview/DefaultStanding.png.meta', 'CharacterPreview/DefaultPortrait.png', 'CharacterPreview/DefaultPortrait.png.meta'):
    shutil.copy2(project / 'Assets/Resources/Sprites/StartScreen' / rel, repo / 'Assets/Resources/Sprites/StartScreen' / rel)
newline = '\r\n' if b'\r\n' in original else '\n'
scene_path.write_bytes(scene.replace('\n', newline).encode('utf-8'))
print(f'Installed {len(docs)} serialized objects beneath Canvas/StartScreenUI; existing scene objects preserved.')
