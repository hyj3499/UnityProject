"""Install the Unity-validated layout, preserving all unrelated Main scene blocks."""
from pathlib import Path
import re

repo = Path(__file__).resolve().parents[1]
project = repo / 'Temp/QuickInventoryChecks'
assert (project / 'Output/result.txt').read_text().startswith('PASS:')
scene_path = repo / 'Assets/Scenes/Main.unity'
original = scene_path.read_bytes()
source = original.decode('utf-8-sig').replace('\r\n', '\n')
prefab = (project / 'Assets/QuickInventoryLayout.prefab').read_text(encoding='utf-8-sig')
pattern = re.compile(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', re.M | re.S)
before = {m[2]: (m[1], m[0]) for m in pattern.finditer(source)}
generated = {m[2]: (m[1], m[0]) for m in pattern.finditer(prefab)}
hud_id = next(k for k, (_, b) in generated.items() if 'FarmMVP.QuickInventoryHUD\n' in b)
root_id = re.search(r'm_GameObject: \{fileID: (\d+)\}', generated[hud_id][1])[1]
panel_id = next(k for k, (t, b) in generated.items() if t == '1' and '  m_Name: QuickInventoryPanel\n' in b)

def component(go, kind, contains=''):
    return next(k for k, (t, b) in generated.items()
                if t == kind and f'  m_GameObject: {{fileID: {go}}}\n' in b and contains in b)

mapping = {
    root_id: '318026118', component(root_id, '224'): '318026119',
    panel_id: '1704673185', component(panel_id, '224'): '1704673186',
    component(panel_id, '114', 'UnityEngine.UI.Image'): '1704673187',
    component(panel_id, '222'): '1704673188',
}
existing_ids = set(mapping.values())
assert all(k in before for k in existing_ids)
assert 'FarmMVP.QuickInventoryHUD\n' not in source, 'Layout already installed'
assert '  m_Children: []\n' in before['1704673186'][1], 'Panel already has authored children'
for i, k in enumerate(generated):
    if k not in mapping:
        mapping[k] = str(9750000000000 + i)
        assert mapping[k] not in before

def remap(block):
    block = re.sub(r'[ \t]+$', '', block, flags=re.M)
    block = re.sub(r'(^--- !u!\d+ &)(\d+)', lambda m: m[1] + mapping[m[2]], block, flags=re.M)
    return re.sub(r'\{fileID: (\d+)\}', lambda m: '{fileID: ' + mapping.get(m[1], m[1]) + '}', block)

authored = {mapping[k]: remap(b) for k, (_, b) in generated.items()}
updates = {}
updates['318026118'] = before['318026118'][1].replace(
    '  m_Layer:', f'  - component: {{fileID: {mapping[hud_id]}}}\n  m_Layer:', 1)
children = re.compile(r'^  m_Children:.*?(?=^  m_Father:)', re.M | re.S)
updates['1704673186'] = children.sub(lambda _: children.search(authored['1704673186'])[0], before['1704673186'][1])
updates['1704673186'] = updates['1704673186'].replace('m_SizeDelta: {x: 255, y: 32}', 'm_SizeDelta: {x: 297, y: 27}')
updates['1704673187'] = before['1704673187'][1].replace('  m_Enabled: 1', '  m_Enabled: 0').replace('  m_RaycastTarget: 1', '  m_RaycastTarget: 0')
updates['1704673187'] = re.sub(r'^  m_Sprite: .*$', '  m_Sprite: {fileID: 0}', updates['1704673187'], flags=re.M)
result = pattern.sub(lambda m: updates.get(m[2], m[0]), source)
new_blocks = ''.join(b for k, b in authored.items() if k not in existing_ids)
roots = re.search(r'^--- !u!1660057539 ', result, re.M)
at = roots.start() if roots else len(result)
result = result[:at] + new_blocks + result[at:]
after = {m[2]: m[0] for m in pattern.finditer(result)}
assert all(after[k] == b for k, (_, b) in before.items() if k not in updates)
assert len(after) == len(before) + len(generated) - len(existing_ids)
assert scene_path.read_bytes() == original, 'Scene changed during installation'
(project / 'Output/Main.before-quick-inventory.unity').write_bytes(original)
newline = '\r\n' if b'\r\n' in original else '\n'
scene_path.write_bytes(result.replace('\n', newline).encode('utf-8'))
print(f'Installed ten slots: {len(updates)} existing blocks updated; all other scene blocks preserved.')
