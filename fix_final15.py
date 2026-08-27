# Fix AboutPage.xaml - smart protection that only protects named entities
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

import re

# Strategy: 
# 1. First protect numeric entities (&#x...; &#...;)
# 2. Then protect named entities (& < > " &apos; &)
# 3. Replace remaining literal & with &
# 4. Restore

protected = content

# Protect numeric entities first
protected = re.sub(r'&#x[\da-fA-F]+;', '\u0000HEX\u0000', protected)
protected = re.sub(r'&#\d+;', '\u0000DEC\u0000', protected)

# Now protect NAMED entities only: & < > " &apos; &
# These are: literal & followed by (amp|lt|gt|quot|apos) and ;
# We match & + (amp|lt|gt|quot|apos) + ;
protected = re.sub(r'&(?:amp|lt|gt|quot|apos);', '\u0000AMP\u0000', protected)

# Now any remaining literal & are NOT part of valid entities
# Replace them with &
protected = protected.replace('\u0026', '\u0026')

# Restore
protected = protected.replace('\u0000HEX\u0000', '&#x')
protected = protected.replace('\u0000DEC\u0000', '&#')
protected = protected.replace('\u0000AMP\u0000', '&')

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(protected)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

protected = content
protected = re.sub(r'&#x[\da-fA-F]+;', '\u0000HEX\u0000', protected)
protected = re.sub(r'&#\d+;', '\u0000DEC\u0000', protected)
protected = re.sub(r'&(?:amp|lt|gt|quot|apos);', '\u0000AMP\u0000', protected)
protected = protected.replace('\u0026', '\u0026')
protected = protected.replace('\u0000HEX\u0000', '&#x')
protected = protected.replace('\u0000DEC\u0000', '&#')
protected = protected.replace('\u0000AMP\u0000', '&')

with open('Views/MainPage.xaml', 'w') as f:
    f.write(protected)

print('Fixed MainPage.xaml')