"""Install verified UI components without reserializing unrelated Main scene data."""
from pathlib import Path
import re
import shutil

repo = Path(__file__).resolve().parent.parent
project = repo / 'Temp/UIThemeChecks'
assert (project / 'Output/result.txt').read_text().startswith('PASS:')
scene_path = repo / 'Assets/Scenes/Main.unity'
original = scene_path.read_bytes()
source = original.decode('utf-8-sig').replace('\r\n', '\n')
authored = (project / 'Assets/Scenes/Main.unity').read_text(encoding='utf-8-sig')
pattern = re.compile(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', re.M | re.S)
before = {m[2]: (m[1], m[0]) for m in pattern.finditer(source)}
after = {m[2]: (m[1], m[0]) for m in pattern.finditer(authored)}
assert not set(before) - set(after), 'Unexpected removal of existing scene objects'
children = re.compile(r'^  m_Children:.*?(?=^  m_Father:)', re.M | re.S)
updated = {}
for ident, (kind, chunk) in before.items():
    replacement = after[ident][1]
    if chunk == replacement:
        continue
    if kind == '114':
        assert any(key in chunk for key in ('  m_Sprite:', '  m_OnClick:', '  m_Text:')), 'Unexpected component change: ' + ident
    elif kind == '224':
        assert children.sub('', chunk) == children.sub('', replacement), 'Existing UI geometry changed: ' + ident
    else:
        raise AssertionError('Unexpected non-UI change: ' + ident)
    updated[ident] = replacement
new_objects = [chunk for ident, (kind, chunk) in after.items() if ident not in before]
assert all(after[ident][0] in ('1', '224', '114', '222', '223') for ident in set(after) - set(before))
result = pattern.sub(lambda m: updated.get(m[2], m[0]), source)
root = re.search(r'^--- !u!1660057539 ', result, re.M)
insertion = root.start() if root else len(result)
result = result[:insertion] + ''.join(new_objects) + result[insertion:]
assert scene_path.read_bytes() == original, 'Scene changed during installation'
backup = project / 'Output/Main.before-ui-theme.unity'
if not backup.exists():
    backup.write_bytes(original)
newline = '\r\n' if b'\r\n' in original else '\n'
scene_path.write_bytes(result.replace('\n', newline).encode('utf-8'))
for path in ('Assets/Resources/UI/StartScreenUI.prefab', 'Assets/Prefabs/UI/ShopBookUi.prefab'):
    shutil.copy2(project / path, repo / path)
print(f'Installed {len(updated)} UI component changes and {len(new_objects)} new objects; all existing RectTransform geometry preserved.')
