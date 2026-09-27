"""Compare signed package payload against the validated unsigned input."""
import hashlib
import sys
import xml.etree.ElementTree as ET
import zipfile

with zipfile.ZipFile(sys.argv[1]) as original, zipfile.ZipFile(sys.argv[2]) as signed:
    a, b = original.namelist(), signed.namelist()
    assert len(a) == len(set(a)) and len(b) == len(set(b)), 'Duplicate entries'
    assert set(b) - set(a) == {'AppxSignature.p7x'} and not set(a) - set(b)
    count = 0
    for name in a:
        if name == '[Content_Types].xml':
            before = ET.fromstring(original.read(name))
            after = ET.fromstring(signed.read(name))
            def records(root):
                return {(el.tag, tuple(sorted(el.attrib.items()))) for el in root}
            expected = ('{http://schemas.openxmlformats.org/package/2006/content-types}Override',
                        (('ContentType', 'application/vnd.ms-appx.signature'), ('PartName', '/AppxSignature.p7x')))
            assert records(after) == records(before) | {expected}, 'Unexpected content type change'
        else:
            assert hashlib.sha256(original.read(name)).digest() == hashlib.sha256(signed.read(name)).digest(), name
            count += 1
    print(f'PASS: {count} original entries unchanged; only signature and its content type added.')
