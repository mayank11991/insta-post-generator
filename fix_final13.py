# Fix AboutPage.xaml - use correct regex for literal ampersand character
with open('Views/AboutPage.xaml', 'r') as f:
    content = f.read()

import re

# Protect valid entities by replacing them with placeholders
# Use actual ampersand character in regex
protected = content
protected = re.sub(r'&#x[\da-fA-F]+;', '\u0000HEX\u0000', protected)
protected = re.sub(r'&#\d+;', '\u0000DEC\u0000', protected)
protected = re.sub(r'&\u003b', '\u0000AMP\u0000', protected)  # & + ;
protected = re.sub(r'<', '\u0000LT\u0000', protected)
protected = re.sub(r'>', '\u0000GT\u0000', protected)
protected = re.sub(r'\"', '\u0000QUOT\u0000', protected)
protected = re.sub(r'&apos;', '\u0000APOS\u0000', protected)

# Now replace ANY remaining literal ampersand characters with &
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
protected = re.sub(r'&\u003b', '\u0000AMP\u0000', protected)
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