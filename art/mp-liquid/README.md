# MP liquid animation

`Assets/Resources/Sprites/UI/Bars/MP Bar_animation.png` is an eight-frame pixel atlas (2 columns × 4 rows, each frame 22 × 4 pixels). Read left to right, then top to bottom. It matches the interior of the supplied MP bar. Point filtering, no mipmaps, and no compression preserve the pixels.

The animation was made with the built-in image generation tool. `generated-source.png` retains its original output. `tools/Prepare-MPAssets.ps1` crops, downsamples with nearest-neighbour sampling and packs the output, and writes Unity sprite metadata. The two user-supplied PNG files are unchanged.

The MPBar component uses the supplied frame, five fill colors and the empty background sampled from the supplied bar. The separate animation is a fading liquid highlight over the fill. During consumption, the right edge wobbles by a source pixel and the fill reaches the precise MP percentage after 0.45 seconds. Repeated consumption continues from the currently visible amount. Recovery and initial loading snap to the correct value.

Default color thresholds: above 70% green; at/below 70% yellow; at/below 50% orange; at/below 30% dark orange; at/below 20% red. These and the animation duration/intensity can be edited on the MPBar object's MPLiquidBar component.
