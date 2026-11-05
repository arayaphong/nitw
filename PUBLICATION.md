# Publication scope

This public snapshot contains reviewed authored tools and support source only. It was assembled in a separate directory with an explicit file allowlist. The working Unity project, its generated files, local research output and Git history were not imported wholesale. The repository's pre-existing LICENSE is preserved.

## Included

Python format readers, local export preparation and diagnostics; handwritten Unity import/build adapters and verification drivers; serialization schemas; a handwritten shaped-glyph renderer; and a small handwritten native ABI adapter. Symbol names, format coordinates, event identifiers and numeric test expectations are functional references, not bundled game data.

The per-file review category, SHA-256 and publication-only transformations are in `publication-manifest.json`. User-specific absolute paths were replaced with environment/relative paths. A narrative text used as a font measurement probe was replaced with synthetic text. Two narrative-specific test assertions were removed. These changes apply to this publication snapshot; the working playable project was not modified.

## Kept local

- All extracted or original game assets: textures, sprites, logos, audio, fonts, banks, DLLs, native libraries, scene hierarchies, meshes, animation clips/controllers/curves, materials and serialized source data.
- Yarn stories/dialogue, IntroPoem, translations and shaped/glyph data containing those texts.
- Research output, screenshots, recordings, saved games, logs, validation output and decompilation/CIL dumps.
- Game-behavior runtime implementations transcribed closely from original CIL, including the current player movement, follow camera, character gaze/animation selection, title effects, startup sequence, and BusStation narration/bubble/Janitor runtime behavior.
- Recovered third-party shaders and close shader/post-processing translations. All shader files are omitted in this snapshot.
- Dependency source/binaries, virtual environments, Unity caches, generated `.meta` files, builds and machine-specific preferences.

The omitted implementations have not been cleared for public redistribution here. This conservative omission is a publication decision, not a legal finding about all reverse engineering or about every possible reimplementation. A functional complete public player would need a separate provenance/rights review or independently authored replacements for the omitted pieces.

## Updating safely

Review the exact source and its provenance before changing the manifest. Keep generated output and private research outside this snapshot. Never use a broad `git add -f` to bypass the allowlist. A file extension alone is insufficient: inspect content and run the audit on the committed tree before pushing. Repository visibility does not grant rights to the original game or dependency materials.

Tool outputs remain local even when a script is included here. Installing tools or owning a copy of the game does not, by itself, make their exported content part of this repository's license.
