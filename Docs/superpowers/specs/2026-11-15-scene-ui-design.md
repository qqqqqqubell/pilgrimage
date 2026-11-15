# Scene UI

The user chose directly editable scene objects for all UI, with the explicit exception of cards, which may use prefabs. Preserve game interactions and use white rectangular Images and dark text as asset placeholders. Static screens, rewards, lobby controls, laboratory rows and presentation slots belong to the scene. Card faces/backs and card selection entries may be instantiated from authored prefabs. Runtime binds data and actions without replacing authored layout or sprites. Retire the legacy IMGUI screen and use the same scene UI for both shipped scenes.

Alternatives: all prefabs were rejected; all scene objects were initially chosen, then cards were explicitly exempted. Existing construction code may be retained for an explicit editor migration command, excluded from player builds. No automatic scene regeneration after users edit assets.
