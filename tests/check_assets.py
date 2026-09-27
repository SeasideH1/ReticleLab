"""Non-browser structural smoke checks; does not claim layout/runtime validation."""
from html.parser import HTMLParser
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
class Page(HTMLParser):
    def __init__(self):
        super().__init__()
        self.ids = []
        self.assets = []
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if 'id' in attrs:
            self.ids.append(attrs['id'])
        if tag in ('script', 'img') and attrs.get('src'):
            self.assets.append(attrs['src'])
        if tag == 'link' and attrs.get('href'):
            self.assets.append(attrs['href'])

p = Page()
p.feed((root / 'index.html').read_text(encoding='utf-8'))
assert len(p.ids) == len(set(p.ids)), 'Duplicate HTML element IDs'
for asset in p.assets:
    assert (root / asset).is_file(), f'Missing resource: {asset}'
script = (root / 'app.js').read_text(encoding='utf-8')
for element in re.findall(r"\$\('([a-zA-Z0-9_-]+)'\)", script):
    if element != 'out-':
        assert element in p.ids, f'Missing fixed element: {element}'
for i in range(1, 11):
    data = (root / f'assets/motion-{i:02}.svg').read_bytes()
    assert b'<svg' in data, f'Invalid motion SVG {i}'
print(f'PASS: {len(p.ids)} unique IDs, fixed JS references, local resources, 10 motion SVGs')
