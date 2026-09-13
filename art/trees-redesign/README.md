# Tree sprite redesign

The 12 PNGs in `Assets/Resources/Sprites/Trees` now use three distinct silhouettes:

- `tree1`: cherry tree with a curved trunk and separated, asymmetric fan branches.
- `tree2`: birch with a forked ivory trunk and angular, open foliage tiers.
- `tree3`: purple flowering tree with an arched trunk and hanging flower/leaf curtains.

Each species has spring, summer, fall and leafless winter artwork. `preview.png` shows the final mature sprites enlarged 3x with no interpolation.

## Source and prompts

Created with the built-in `image_gen` tool, one image for each of the 12 species/season variants. `sources.json` records the prompts, generated-file provenance, local source PNGs and background extraction modes. The source PNGs are retained under `sources/`; the final Unity-sized PNGs are under `packed/`.

The generated sources are illustrations at a larger resolution. `tools/pack_redesigned_trees.ps1` performs background cleanup, sprite extraction, native pixel sampling, particle rotations and layout packing. It does not procedurally draw the tree artwork. In this batch only the purple spring source supplied usable alpha directly; other generated backgrounds are removed during packing.

## Unity layout

All sheets remain 336x96 RGBA, with binary transparency. The seven 48x96 columns remain seedling, growth 1, growth 2, four particles, stump, upper tree, full tree. Non-particle sprites retain the existing bottom-center pivots and 16 pixels per unit. The four particle rectangles remain 8x8 at Unity coordinates (160,8), (168,8), (160,0), (168,0).

The full tree is composed from the exact stump and upper sprites; the upper sprite covers the cut surface while the complete tree is standing. Existing texture GUIDs and sprite IDs are preserved. Three missing particle slices (`leaf0`, `leaf2`, `leaf3`) were added to `tree2_winter.png.meta`, so all 12 sheets expose all 10 sprites.

## Rebuild and validation

From the repository root, run with the bundled Windows PowerShell runtime:

```powershell
./tools/pack_redesigned_trees.ps1 -Apply
```

Without `-Apply`, it rebuilds only the packed outputs, preview and validation report. The existing `tools/build_tree_sheets.py` now copies these packed replacements when this directory is present, preventing a routine rebuild from restoring the previous artwork.

`validation.json` records dimensions, binary alpha, nonempty sprite regions, particle bounds and exact stump/upper composition checks for all 12 sheets. The final sheets and contact sheet were visually inspected. Unity Play Mode was not run.

Pre-edit PNGs and importer metadata, including existing uncommitted changes, are backed up under `art-backups/trees-before-redesign-20260912/`.
