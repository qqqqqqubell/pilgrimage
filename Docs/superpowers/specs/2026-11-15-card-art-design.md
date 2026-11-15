# Card artwork and hover reverse

The user explicitly specified one shared prefab, ID-based front/back/name/description/flavor binding, ordinary cards 0–21 in supplied order, faction artwork matched by name, shared question-mark faction reverse, and hover enlargement plus reverse. The user subsequently approved updating actual rules to the supplied text after reviewing Docs/CardTextDifferences.ko.md. Missing FurRally artwork uses a named placeholder by explicit instruction.

Ordinary front/reverse pairing is based on printed tarot number, not reverse filename. Imported UI textures are reduced to at most 2048 pixels, retaining ZIP originals outside the project. A serialized visual catalog stores 45 ID-based image entries; runtime CardDefinition supplies the authoritative text, shared with gameplay. Card root keeps a stable transparent hit target. The authored child visual enlarges and smoothly flips; only the reverse displays name, description, cost and flavor. Pointer exit restores front and scale. Keyboard selection uses the same readable state. Hidden opponent backs remain anonymous question marks.

Unrelated scene layouts must not be regenerated. Upgrade only the common Card prefab and CardBack prefab, preserving existing prefab GUIDs and scene references. Existing detail/right-click and double-click/Enter play behavior remains.
