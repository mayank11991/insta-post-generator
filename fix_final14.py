# Fix AboutPage.xaml - properly protect ALL valid XML entities
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

import re

# Protect ALL valid XML entity patterns:
# 1. Numeric character references: &#xNNNN; or &#NNNN;
# 2. Named entities: & < > " &apos; &
# We'll use placeholders

protected = content

# Protect numeric entities: &#x...; or &#...;
protected = re.sub(r'&#x[\da-fA-F]+;', '\u0000HEX\u0000', protected)
protected = re.sub(r'&#\d+;', '\u0000DEC\u0000', protected)

# Protect named entities (they are written as &name; in the file, i.e., literal & + name + ;)
# The file stores them as literal & character + name + ;
# But wait - in the file, are they stored as literal & or as the text "amp;"?
# Let's check: the entity & would be 5 chars: &, a, m, p, ;
# So we need to match literal & followed by valid entity name + ;

# Match & followed by valid entity name and ;
protected = re.sub(r'&', '\u0000AMP\u0000', protected)
protected = re.sub(r'<', '\u0000LT\u0000', protected)
protected = re.sub(r'>', '\u0000GT\u0000', protected)
protected = re.sub(r'\"', '\u0000QUOT\u0000', protected)
protected = re.sub(r'&apos;', '\u0000APOS\u0000', protected)

# Now ANY remaining literal & characters are NOT part of valid entities
# Replace them with &
protected = protected.replace('\u0026', '\u0026')

# Restore protected entities
protected = protected.replace('\u0000HEX\u0000', '&#x')
protected = protected.replace('\u0000DEC\u0000', '&#')
protected = protected.replace('\u0000AMP\u0000', '&')
protected = protected.replace('\u0000LT\u0000', '<')
protected = protected.replace('\u0000GT\u0000', '>')
protected = protected.replace('\u0000QUOT\u0000', '"')
protected = protected.replace('\u0000APOS\u0000', '&apos;')

with open('Views/AboutPage.xaml', 'w') as f:
    f.write(protected)

print('Fixed AboutPage.xaml')

# Fix MainPage.xaml
with open('Views/MainPage.xaml', 'r') as f:
    content = f.read()

protected = content
protected = re.sub(r'&#x[\da-fA-F]+;', '\u0000HEX\u0000', protected)
protected = re.sub(r'&#\d+;', '\u0000DEC\u0000', protected)
protected = re.sub(r'&', '\u0000AMP\u0000', protected)
protected = re.sub(r'<', '\u0000LT\u0000', protected)
protected = re.sub(r'>', '\u0000GT\u0000', protected)
protected = re.sub(r'\"', '\u0000QUOT\u0000', protected)
protected = re.sub(r'&apos;', '\u0000APOS\u0000', protected)

protected = protected.replace('\u0026', '\u0026')

protected = protected.replace('\u0000HEX\u0000', '&#x')
protected = protected.replace('\u0000DEC\u0000', '&#')
protected = protected.replace('\u0000AMP\u0000', '&')
protected = protected.replace('\u0000LT\u0000', '<')
protected = protected.replace('\u0000GT\u0000', '>')
protected = protected.replace('\u0000QUOT\u0000', '"')
protected = protected.replace('\u0000APOS\u0000', '&apos;')

with open('Views/MainPage.xaml', 'w') as f:
    f.write(protected)

print('Fixed MainPage.xaml')