"""Import the installed uGUI package's official TMP Essential Resources safely.
No package download. Existing assets are preserved. Unity GUID metadata is retained.
"""
from pathlib import Path
import tarfile

root = Path(__file__).resolve().parents[1]
packages = list((root / 'Library/PackageCache').glob('com.unity.ugui*/Package Resources/TMP Essential Resources.unitypackage'))
if len(packages) != 1:
    raise SystemExit('Expected one installed uGUI package containing TMP Essential Resources.')
written = 0
with tarfile.open(packages[0], 'r:gz') as archive:
    members = {item.name: item for item in archive.getmembers() if item.isfile()}
    for member_name, member in members.items():
        if not member_name.endswith('/pathname'): continue
        path = archive.extractfile(member).read().decode('utf-8').strip()
        if not path.startswith('Assets/TextMesh Pro/') and path != 'Assets/TextMesh Pro':
            raise ValueError(f'Unexpected asset path: {path}')
        target = (root / path).resolve()
        if not target.is_relative_to(root / 'Assets'):
            raise ValueError('Asset outside workspace Assets')
        prefix = member_name.rsplit('/', 1)[0]
        for entry, destination in [('asset', target), ('asset.meta', Path(str(target) + '.meta'))]:
            item = members.get(prefix + '/' + entry)
            if item is None: continue
            if destination.exists(): continue
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.extractfile(item).read())
            written += 1
print(f'Imported {written} missing TMP resource files from installed uGUI; no package change.')
